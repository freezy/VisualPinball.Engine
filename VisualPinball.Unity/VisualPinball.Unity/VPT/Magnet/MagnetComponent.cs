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
using VisualPinball.Unity.Collections;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[PackAs("Magnet")]
	[AddComponentMenu("Pinball/Mechs/Magnet")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/magnets.html")]
	public class MagnetComponent : MonoBehaviour, ICoilDeviceComponent, ISwitchDeviceComponent, IPackable, IKinematicTransformComponent
	{
		public const string MagnetCoilItem = "magnet_coil";
		public const string BallHeldSwitchItem = "ball_held";
		public const float MillimetersToWorld = 0.001f;
		public const float DefaultPlanarDamping = 0.985f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[Min(0f)]
		[Unit("mm")]
		[Tooltip("Planar radius in which the magnet influences balls.")]
		public float Radius = 50f;

		[Tooltip("Magnet strength. In VPX-compatible mode this uses cvpmMagnet strength values.")]
		public float Strength = 10f;

		[Tooltip("Playfield magnets act through a vertical cylinder; spatial magnets grab and carry balls in 3-D.")]
		public MagnetType MagnetType = VisualPinball.Unity.MagnetType.Playfield;

		[Tooltip("How the authored strength value is interpreted.")]
		public MagnetForceProfile ForceProfile = MagnetForceProfile.VpxCompatible;

		[Tooltip("Whether the magnet can hold a ball at its center.")]
		public bool GrabBall;

		[Min(0f)]
		[Unit("mm")]
		[Tooltip("Radius around the center where grab mode captures the ball.")]
		public float GrabRadius = 10.8f;

		[Min(0f)]
		[Unit("mm")]
		[Tooltip("Vertical range above the magnet surface where balls are affected.")]
		public float HeightRange = 50f;

		[Tooltip("Whether the magnet starts enabled before coil or script control changes it.")]
		public bool IsEnabledOnStart;

		[Tooltip("If set, transforming this object during gameplay moves the magnetic field with it.")]
		public bool IsKinematic;

		[Tooltip("Draw play-mode force vectors for balls inside the radius.")]
		public bool DrawDebugForces;

		public byte[] Pack() => MagnetPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => System.Array.Empty<byte>();

		public void Unpack(byte[] bytes) => MagnetPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(MagnetCoilItem) {
				Description = "Magnet"
			}
		};

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(BallHeldSwitchItem) {
				Description = "Ball Held"
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IApiCoil ICoilDeviceComponent.CoilDevice(string deviceId) => ((IApiCoilDevice)MagnetApi).Coil(deviceId);
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		public MagnetApi MagnetApi { get; private set; }

		private PhysicsEngine _physicsEngine;

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for magnet {name}.");
				return;
			}

			_physicsEngine = GetComponentInParent<PhysicsEngine>();
			MagnetApi = new MagnetApi(gameObject, player, _physicsEngine);

			player.Register(MagnetApi, this);
			if (_physicsEngine) {
				_physicsEngine.Register(this);
			} else {
				Logger.Error($"Cannot find physics engine for magnet {name}.");
			}
		}

		private void OnValidate()
		{
			Radius = math.max(0f, Radius);
			GrabRadius = math.max(0f, GrabRadius);
			HeightRange = math.max(0f, HeightRange);
			SyncPhysicsState();
		}

		internal MagnetState CreateState()
		{
			var pos = GetPlayfieldPositionVpx(transform);
			return new MagnetState {
				Position = pos.xy,
				Height = pos.z,
				Radius = MillimetersToVpx(Radius),
				Strength = Strength,
				GrabRadius = GrabBall ? MillimetersToVpx(GrabRadius) : 0f,
				PlanarDamping = math.clamp(DefaultPlanarDamping, 0f, 1f),
				IsEnabled = IsEnabledOnStart,
				IsKinematic = IsKinematic,
				Profile = MagnetType == VisualPinball.Unity.MagnetType.Spatial ? MagnetForceProfile.Physical : ForceProfile,
				HeightRange = MillimetersToVpx(HeightRange),
				MagnetType = MagnetType,
				GrabbedBalls = default,
				ReleasedBalls = default
			};
		}

		/// <summary>
		/// Pushes inspector edits to the live physics state during play mode.
		/// Builds a fresh state from the authored fields and preserves the
		/// runtime-owned ones (coil state and grab bookkeeping).
		/// </summary>
		private void SyncPhysicsState()
		{
			if (!Application.isPlaying || !_physicsEngine) {
				return;
			}

			var itemId = ItemId;
			var synced = CreateState();
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.MagnetStates.ContainsKey(itemId)) {
					return;
				}
				ref var magnet = ref state.MagnetStates.GetValueByRef(itemId);
				synced.IsEnabled = magnet.IsEnabled;
				synced.GrabbedBalls = magnet.GrabbedBalls;
				synced.ReleasedBalls = magnet.ReleasedBalls;
				magnet = synced;
			});
		}

		public int ItemId => UnityObjectId.Get(gameObject);

		bool IKinematicTransformComponent.IsKinematic => IsKinematic;

		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> Physics.GetLocalToPlayfieldMatrixInVpx(transform.localToWorldMatrix, worldToPlayfield);

		public void OnTransformationChanged(float4x4 currTransformationMatrix)
		{
		}

		internal static float MillimetersToVpx(float value) => Physics.ScaleToVpx(value * MillimetersToWorld);

		/// <summary>
		/// Playfield position in VPX space, valid for any nesting depth. The local
		/// position is only equivalent when every ancestor up to the playfield sits
		/// at identity, which re-parenting in the editor silently breaks.
		/// </summary>
		internal static float3 GetPlayfieldPositionVpx(Transform transform)
		{
			var playfield = transform.GetComponentInParent<PlayfieldComponent>();
			return playfield
				? (float3)transform.position.TranslateToVpx(playfield.transform)
				: (float3)transform.localPosition.TranslateToVpx();
		}

		private void OnDrawGizmosSelected()
		{
			var radiusWorld = Radius * MillimetersToWorld;
			var grabRadiusWorld = GrabBall ? GrabRadius * MillimetersToWorld : 0f;
			var heightRangeWorld = HeightRange * MillimetersToWorld;

			Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.9f);
			if (MagnetType == VisualPinball.Unity.MagnetType.Spatial) {
				Gizmos.DrawWireSphere(transform.position, radiusWorld);
			} else {
				DrawLocalDisc(radiusWorld);
			}

			if (GrabBall && grabRadiusWorld > 0f) {
				Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
				if (MagnetType == VisualPinball.Unity.MagnetType.Spatial) {
					Gizmos.DrawWireSphere(transform.position, grabRadiusWorld);
				} else {
					DrawLocalDisc(grabRadiusWorld);
				}
			}

			if (MagnetType == VisualPinball.Unity.MagnetType.Playfield && heightRangeWorld > 0f) {
				Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.35f);
				DrawLocalCylinder(radiusWorld, heightRangeWorld);
			}

			if (!Application.isPlaying || !DrawDebugForces) {
				return;
			}
			var player = GetComponentInParent<Player>();
			if (!player) {
				return;
			}
			foreach (var ball in player.GetComponentsInChildren<BallComponent>()) {
				var offset = ball.transform.position - transform.position;
				if (MagnetType == VisualPinball.Unity.MagnetType.Spatial) {
					if (offset.sqrMagnitude <= radiusWorld * radiusWorld) {
						Gizmos.DrawLine(ball.transform.position, ball.transform.position - offset.normalized * math.min(radiusWorld * 0.25f, offset.magnitude));
					}
				} else {
					var planarOffset = Vector3.ProjectOnPlane(offset, transform.up);
					if (planarOffset.sqrMagnitude <= radiusWorld * radiusWorld) {
						Gizmos.DrawLine(ball.transform.position, ball.transform.position - planarOffset.normalized * math.min(radiusWorld * 0.25f, planarOffset.magnitude));
					}
				}
			}
		}

		private void DrawLocalDisc(float radius, float localHeight = 0f)
		{
			if (radius <= 0f) {
				return;
			}

			const int segments = 64;
			var previous = transform.TransformPoint(new Vector3(radius, localHeight, 0f));
			for (var i = 1; i <= segments; i++) {
				var angle = (math.TAU * i) / segments;
				var next = transform.TransformPoint(new Vector3(math.cos(angle) * radius, localHeight, math.sin(angle) * radius));
				Gizmos.DrawLine(previous, next);
				previous = next;
			}
		}

		private void DrawLocalCylinder(float radius, float height)
		{
			if (radius <= 0f || height <= 0f) {
				return;
			}

			DrawLocalDisc(radius);
			DrawLocalDisc(radius, height);

			const int segments = 8;
			for (var i = 0; i < segments; i++) {
				var angle = (math.TAU * i) / segments;
				var localBase = new Vector3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);
				var localTop = new Vector3(localBase.x, height, localBase.z);
				Gizmos.DrawLine(transform.TransformPoint(localBase), transform.TransformPoint(localTop));
			}
		}
	}
}
