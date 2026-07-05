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
	public class TurntableComponent : MonoBehaviour, ICoilDeviceComponent, IPackable
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
		[Tooltip("Acceleration toward MaxSpeed in speed units per second.")]
		public float SpinUp = 600f;

		[Min(0f)]
		[Tooltip("Deceleration toward zero in speed units per second.")]
		public float SpinDown = 600f;

		[Tooltip("Whether the turntable motor starts enabled before coil or script control changes it.")]
		public bool MotorOnStart;

		[Tooltip("Initial spin direction.")]
		public bool SpinClockwise = true;

		[Tooltip("Optional visual disc to rotate with the simulated speed.")]
		public Transform RotationTarget;

		[Min(0f)]
		[Tooltip("Degrees per second the visual disc rotates per speed unit. At the VPX-typical speed of 90, the default of 4 spins the disc at 60 RPM.")]
		public float VisualSpeedFactor = 4f;

		public TurntableApi TurntableApi { get; private set; }

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

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => System.Array.Empty<byte>();

		public void Unpack(byte[] bytes) => TurntablePackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for turntable {name}.");
				return;
			}

			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			TurntableApi = new TurntableApi(gameObject, player, physicsEngine);

			player.Register(TurntableApi, this);
			if (physicsEngine) {
				physicsEngine.Register(this);
			} else {
				Logger.Error($"Cannot find physics engine for turntable {name}.");
			}
		}

		private void Update()
		{
			if (!RotationTarget || TurntableApi == null) {
				return;
			}
			RotationTarget.localRotation = Quaternion.AngleAxis(TurntableApi.RotationAngle, Vector3.up);
		}

		internal TurntableState CreateState()
		{
			var pos = (float3)transform.localPosition.TranslateToVpx();
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
				RotationAngle = 0f,
				VisualSpeedFactor = VisualSpeedFactor
			};
			TurntablePhysics.RefreshTargetSpeed(ref state);
			return state;
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
