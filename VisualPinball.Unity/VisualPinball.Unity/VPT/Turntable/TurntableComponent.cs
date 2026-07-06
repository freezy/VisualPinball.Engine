// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[PackAs("Turntable")]
	[AddComponentMenu("Pinball/Mechs/Turntable")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/magnets.html")]
	public class TurntableComponent : MonoBehaviour, ICoilDeviceComponent, IPackable, IKinematicTransformComponent
	{
		public const string MotorCoilItem = "motor_coil";
		public const string DirectionCoilItem = "direction_coil";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[Min(0f)]
		[Unit("mm")]
		[Tooltip("Planar radius in which the turntable influences balls.")]
		public float Radius = 60f;

		[Min(0f)]
		[Unit("mm")]
		[Tooltip("Vertical range above the disc surface where balls are affected.")]
		public float HeightRange = 50f;

		[Tooltip("VPX-compatible maximum turntable speed.")]
		public float MaxSpeed = 100f;

		[Min(0f)]
		[Tooltip("Acceleration toward MaxSpeed in speed units per second. cvpmTurntable defaults to 10.")]
		public float SpinUp = 10f;

		[Min(0f)]
		[Tooltip("Deceleration toward zero in speed units per second. cvpmTurntable defaults to 4.")]
		public float SpinDown = 4f;

		[Tooltip("Whether the turntable motor starts enabled before coil or script control changes it.")]
		public bool MotorOnStart;

		[Tooltip("Initial spin direction.")]
		public bool SpinClockwise = true;

		[Tooltip("If set, transforming this object during gameplay moves the turntable force field with it.")]
		public bool IsKinematic;

		[Tooltip("Optional visual disc to rotate with the simulated speed.")]
		public Transform RotationTarget;

		[Min(0f)]
		[Tooltip("Degrees per second the visual disc rotates per speed unit. At the VPX-typical speed of 90, the default of 4 spins the disc at 60 RPM.")]
		public float VisualSpeedFactor = 4f;

		public TurntableApi TurntableApi { get; private set; }

		private PhysicsEngine _physicsEngine;
		private Transform _rotationTarget;
		private Quaternion _rotationTargetInitialRotation;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(MotorCoilItem) {
				Description = "Motor"
			},
			new GamelogicEngineCoil(DirectionCoilItem) {
				Description = "Direction"
			}
		};

		IApiCoil ICoilDeviceComponent.CoilDevice(string deviceId) => ((IApiCoilDevice)TurntableApi).Coil(deviceId);
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		public byte[] Pack() => TurntablePackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => TurntableReferencesPackable.Pack(this, refs);

		public void Unpack(byte[] bytes) => TurntablePackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> TurntableReferencesPackable.Unpack(data, this, refs);

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for turntable {name}.");
				return;
			}

			_physicsEngine = GetComponentInParent<PhysicsEngine>();
			TurntableApi = new TurntableApi(gameObject, player, _physicsEngine);

			player.Register(TurntableApi, this);
			if (_physicsEngine) {
				_physicsEngine.Register(this);
			} else {
				Logger.Error($"Cannot find physics engine for turntable {name}.");
			}
		}

		private void OnValidate()
		{
			Radius = math.max(0f, Radius);
			HeightRange = math.max(0f, HeightRange);
			SpinUp = math.max(0f, SpinUp);
			SpinDown = math.max(0f, SpinDown);
			VisualSpeedFactor = math.max(0f, VisualSpeedFactor);
			SyncPhysicsState();
		}

		/// <summary>
		/// Pushes inspector edits to the live physics state during play mode.
		/// Builds a fresh state from the authored fields and preserves the
		/// runtime-owned ones (motor, direction, current speed and angle).
		/// </summary>
		private void SyncPhysicsState()
		{
			if (!Application.isPlaying || !_physicsEngine) {
				return;
			}

			var itemId = ItemId;
			var synced = CreateState();
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.TurntableStates.ContainsKey(itemId)) {
					return;
				}
				ref var turntable = ref state.TurntableStates.GetValueByRef(itemId);
				synced.Speed = turntable.Speed;
				synced.MotorOn = turntable.MotorOn;
				synced.SpinClockwise = turntable.SpinClockwise;
				synced.RotationAngle = turntable.RotationAngle;
				TurntablePhysics.RefreshTargetSpeed(ref synced);
				turntable = synced;
			});
		}

		private void Update()
		{
			if (!RotationTarget || TurntableApi == null) {
				return;
			}
			if (_rotationTarget != RotationTarget) {
				_rotationTarget = RotationTarget;
				_rotationTargetInitialRotation = RotationTarget.localRotation;
			}
			RotationTarget.localRotation = _rotationTargetInitialRotation * Quaternion.AngleAxis(TurntableApi.RotationAngle, Vector3.up);
		}

		internal TurntableState CreateState()
		{
			var pos = MagnetComponent.GetPlayfieldPositionVpx(transform);
			var state = new TurntableState {
				Position = pos.xy,
				Height = pos.z,
				Radius = MagnetComponent.MillimetersToVpx(Radius),
				HeightRange = MagnetComponent.MillimetersToVpx(HeightRange),
				Speed = 0f,
				MaxSpeed = MaxSpeed,
				SpinUp = SpinUp,
				SpinDown = SpinDown,
				MotorOn = MotorOnStart,
				SpinClockwise = SpinClockwise,
				IsKinematic = IsKinematic,
				RotationAngle = 0f,
				VisualSpeedFactor = VisualSpeedFactor
			};
			TurntablePhysics.RefreshTargetSpeed(ref state);
			return state;
		}

		public int ItemId => UnityObjectId.Get(gameObject);

		bool IKinematicTransformComponent.IsKinematic => IsKinematic;

		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> Physics.GetLocalToPlayfieldMatrixInVpx(transform.localToWorldMatrix, worldToPlayfield);

		public void OnTransformationChanged(float4x4 currTransformationMatrix)
		{
		}

		private void OnDrawGizmosSelected()
		{
			var radiusWorld = Radius * MagnetComponent.MillimetersToWorld;
			if (radiusWorld <= 0f) {
				return;
			}

			Gizmos.color = new Color(0.95f, 0.75f, 0.1f, 0.9f);
			const int segments = 64;
			var previous = transform.TransformPoint(new Vector3(radiusWorld, 0f, 0f));
			for (var i = 1; i <= segments; i++) {
				var angle = (math.TAU * i) / segments;
				var next = transform.TransformPoint(new Vector3(math.cos(angle) * radiusWorld, 0f, math.sin(angle) * radiusWorld));
				Gizmos.DrawLine(previous, next);
				previous = next;
			}
		}
	}
}
