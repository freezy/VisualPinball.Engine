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
		public const float DefaultPlanarDamping = 0.985f;
		public const float DefaultCoilRiseTimeMs = 20f;
		public const float DefaultCoilFallTimeMs = 20f;
		public const float DefaultInfluenceRadius = 92.6355f;
		public const float DefaultPoleRadius = 18.5271f;
		public const float DefaultGrabRadius = 20.009268f;
		public const float DefaultCylinderRadius = 25f;
		public const float DefaultCylinderHeight = 50f;
		public const float DefaultCylindricalDamping = 1f;
		public const float DefaultHeightRange = 92.6355f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		[System.NonSerialized] private Transform _gizmoPlayfieldTransform;

		[Min(0f)]
		[Unit("VPX")]
		[Tooltip("Distance over which the magnet influences balls, in VPX units. A Cylindrical magnet is strongest at contact, half-strength halfway through this distance, and zero at its boundary.")]
		public float Radius = DefaultInfluenceRadius;

		[Tooltip("Magnet strength. For a Cylindrical magnet this controls the full-current pull at contact. In VPX-compatible mode this uses cvpmMagnet strength values.")]
		public float Strength = 10f;

		[Tooltip("Playfield magnets act through the playfield plane, Spatial magnets attract to a point in 3-D, and Cylindrical magnets attract to the surface of an upright cylinder.")]
		public MagnetType MagnetType = VisualPinball.Unity.MagnetType.Playfield;

		[Tooltip("How the authored strength value is interpreted.")]
		public MagnetForceProfile ForceProfile = MagnetForceProfile.VpxCompatible;

		[Min(0f)]
		[Unit("ms")]
		[Tooltip("Electrical rise time constant for Physical, Spatial, and Cylindrical magnets. The current reaches about 63% after one time constant.")]
		public float CoilRiseTime = DefaultCoilRiseTimeMs;

		[Min(0f)]
		[Unit("ms")]
		[Tooltip("Electrical decay time constant for Physical, Spatial, and Cylindrical magnets. Set this to match the driver flyback circuit.")]
		public float CoilFallTime = DefaultCoilFallTimeMs;

		[Min(0f)]
		[Unit("VPX")]
		[Tooltip("Effective pole radius used to shape Physical Playfield and Spatial fields, in VPX units. Cylindrical magnets use Influence Distance for their complete force curve.")]
		public float PoleRadius = DefaultPoleRadius;

		[Tooltip("Whether the magnet can hold a ball at its center. Cylindrical magnets acquire automatically at contact and hold against their surface.")]
		public bool GrabBall;

		[Min(0f)]
		[Unit("VPX")]
		[Tooltip("Distance around a Playfield or Spatial magnet's center where grab mode captures the ball, in VPX units. Cylindrical magnets grab automatically at contact.")]
		public float GrabRadius = DefaultGrabRadius;

		[Min(0f)]
		[Unit("VPX")]
		[Tooltip("Radius of the magnetic cylinder surface, in VPX units. Influence Distance is measured outward from this surface.")]
		public float CylinderRadius = DefaultCylinderRadius;

		[Min(0f)]
		[Unit("VPX")]
		[Tooltip("Height of the magnetic cylinder above the component origin, in VPX units. Zero creates an infinite vertical cylinder.")]
		public float CylinderHeight = DefaultCylinderHeight;

		[Min(0f)]
		[Tooltip("How quickly a Cylindrical magnet removes held-ball spin and surface-normal motion. Zero applies no magnetic damping, one preserves the default behavior, and higher values settle faster.")]
		public float CylindricalDamping = DefaultCylindricalDamping;

		[Min(0f)]
		[Unit("VPX")]
		[Tooltip("Vertical range above the magnet surface where balls are affected, in VPX units. Zero means unlimited.")]
		public float HeightRange = DefaultHeightRange;

		[Tooltip("Whether the magnet starts enabled before coil or script control changes it.")]
		public bool IsEnabledOnStart;

		[Tooltip("If set, transforming this object during gameplay moves the magnetic field with it.")]
		public bool IsKinematic;

		[Tooltip("Draw play-mode force vectors and a green/red runtime coil-status gizmo.")]
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
			CoilRiseTime = math.max(0f, CoilRiseTime);
			CoilFallTime = math.max(0f, CoilFallTime);
			PoleRadius = math.max(0f, PoleRadius);
			CylinderRadius = math.max(0f, CylinderRadius);
			CylinderHeight = math.max(0f, CylinderHeight);
			CylindricalDamping = math.max(0f, CylindricalDamping);
			SyncPhysicsState();
		}

		internal MagnetState CreateState()
		{
			var pos = GetPlayfieldPositionVpx(transform);
			var commandedPower = IsEnabledOnStart ? 1f : 0f;
			var usesPhysicalResponse = MagnetType != MagnetType.Playfield || ForceProfile == MagnetForceProfile.Physical;
			return new MagnetState {
				Position = pos.xy,
				Height = pos.z,
				Radius = Radius,
				Strength = Strength,
				CommandedPower = commandedPower,
				EffectiveCurrent = usesPhysicalResponse ? 0f : commandedPower,
				EffectiveStrength = usesPhysicalResponse ? 0f : Strength * commandedPower,
				RiseTime = CoilRiseTime / MagnetPhysics.VpxMagnetUpdateMs,
				FallTime = CoilFallTime / MagnetPhysics.VpxMagnetUpdateMs,
				PoleRadius = PoleRadius,
				GrabRadius = GrabBall
					? MagnetType == MagnetType.Cylindrical ? MagnetPhysics.CylindricalContactTolerance : GrabRadius
					: 0f,
				CylinderRadius = CylinderRadius,
				CylinderHeight = CylinderHeight,
				CylindricalDamping = CylindricalDamping,
				PlanarDamping = DefaultPlanarDamping,
				IsEnabled = IsEnabledOnStart,
				IsKinematic = IsKinematic,
				// three-dimensional magnets dispatch on MagnetType and never read Profile
				Profile = ForceProfile,
				HeightRange = HeightRange,
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
				synced.CommandedPower = magnet.CommandedPower;
				synced.EffectiveCurrent = magnet.EffectiveCurrent;
				synced.EffectiveStrength = magnet.EffectiveStrength;
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

		private void OnDrawGizmos()
		{
			if (!Application.isPlaying || !DrawDebugForces) {
				return;
			}

			CacheGizmoPlayfieldTransform();
			var center = GetPlayfieldPositionVpx(transform);
			var isOn = MagnetApi != null ? MagnetApi.IsEnabled : IsEnabledOnStart;
			Gizmos.color = isOn
				? new Color(0.1f, 1f, 0.2f, 0.95f)
				: new Color(1f, 0.15f, 0.1f, 0.8f);
			if (MagnetType == VisualPinball.Unity.MagnetType.Cylindrical) {
				DrawVpxCylinder(center, CylinderRadius, CylinderHeight);
			}

			const float markerOffset = 8f;
			const float markerRadius = 4f;
			var markerHeight = MagnetType == VisualPinball.Unity.MagnetType.Cylindrical && CylinderHeight > 0f
				? CylinderHeight + markerOffset
				: markerOffset;
			DrawVpxSphere(center + new float3(0f, 0f, markerHeight), markerRadius);
		}

		private void OnDrawGizmosSelected()
		{
			CacheGizmoPlayfieldTransform();
			var center = GetPlayfieldPositionVpx(transform);

			Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.9f);
			switch (MagnetType) {
				case VisualPinball.Unity.MagnetType.Spatial:
					DrawVpxSphere(center, Radius);
					break;
				case VisualPinball.Unity.MagnetType.Cylindrical:
					DrawVpxCylinder(center, CylinderRadius, CylinderHeight);
					Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.35f);
					DrawVpxCylinder(center, CylinderRadius + Radius, CylinderHeight);
					if (CylinderHeight > 0f) {
						DrawVpxDisc(center, CylinderRadius, -Radius);
						DrawVpxDisc(center, CylinderRadius, CylinderHeight + Radius);
					}
					break;
				default:
					DrawVpxDisc(center, Radius);
					break;
			}

			if (GrabBall && GrabRadius > 0f && MagnetType != VisualPinball.Unity.MagnetType.Cylindrical) {
				Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
				if (MagnetType == VisualPinball.Unity.MagnetType.Spatial) {
					DrawVpxSphere(center, GrabRadius);
				} else {
					DrawVpxDisc(center, GrabRadius);
				}
			}

			if (MagnetType != VisualPinball.Unity.MagnetType.Cylindrical &&
			    (MagnetType == VisualPinball.Unity.MagnetType.Spatial || ForceProfile == MagnetForceProfile.Physical) &&
			    PoleRadius > 0f) {
				Gizmos.color = new Color(0.75f, 0.2f, 1f, 0.55f);
				if (MagnetType == VisualPinball.Unity.MagnetType.Spatial) {
					DrawVpxSphere(center, PoleRadius);
				} else {
					DrawVpxDisc(center, PoleRadius);
				}
			}

			if (MagnetType == VisualPinball.Unity.MagnetType.Playfield && HeightRange > 0f) {
				Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.35f);
				DrawVpxCylinder(center, Radius, HeightRange);
			}

			if (!Application.isPlaying || !DrawDebugForces) {
				return;
			}
			var player = GetComponentInParent<Player>();
			if (!player) {
				return;
			}
			var magnetState = CreateState();
			foreach (var ball in player.GetComponentsInChildren<BallComponent>()) {
				var ballPosition = WorldToVpx(ball.transform.position);
				switch (MagnetType) {
					case VisualPinball.Unity.MagnetType.Spatial:
					{
						var offset = ballPosition - center;
						if (math.lengthsq(offset) <= Radius * Radius) {
							Gizmos.DrawLine(ball.transform.position, VpxToWorld(center));
						}
						break;
					}
					case VisualPinball.Unity.MagnetType.Cylindrical:
					{
						var ballState = new BallState { Position = ballPosition, Radius = ball.Radius };
						var surface = MagnetPhysics.CylinderSurface(in ballState, in magnetState);
						if (surface.AirGap <= Radius) {
							Gizmos.DrawLine(ball.transform.position, VpxToWorld(ballPosition - surface.Offset));
						}
						break;
					}
					default:
					{
						var planarOffset = ballPosition.xy - center.xy;
						if (math.lengthsq(planarOffset) <= Radius * Radius) {
							Gizmos.DrawLine(ball.transform.position, VpxToWorld(new float3(center.x, center.y, ballPosition.z)));
						}
						break;
					}
				}
			}
		}

		private void CacheGizmoPlayfieldTransform()
		{
			var playfield = GetComponentInParent<PlayfieldComponent>();
			_gizmoPlayfieldTransform = playfield ? playfield.transform : null;
		}

		private float3 WorldToVpx(Vector3 worldPoint)
		{
			return _gizmoPlayfieldTransform
				? (float3)worldPoint.TranslateToVpx(_gizmoPlayfieldTransform)
				: (float3)worldPoint.TranslateToVpx();
		}

		private Vector3 VpxToWorld(float3 vpxPoint)
		{
			var point = new Vector3(vpxPoint.x, vpxPoint.y, vpxPoint.z);
			return _gizmoPlayfieldTransform ? point.TranslateToWorld(_gizmoPlayfieldTransform) : point.TranslateToWorld();
		}

		private void DrawVpxCircle(float3 center, float radius, float3 firstAxis, float3 secondAxis)
		{
			if (radius <= 0f) {
				return;
			}
			const int segments = 64;
			var previous = VpxToWorld(center + firstAxis * radius);
			for (var i = 1; i <= segments; i++) {
				var angle = (math.TAU * i) / segments;
				var next = VpxToWorld(center + (firstAxis * math.cos(angle) + secondAxis * math.sin(angle)) * radius);
				Gizmos.DrawLine(previous, next);
				previous = next;
			}
		}

		private void DrawVpxDisc(float3 center, float radius, float heightOffset = 0f)
			=> DrawVpxCircle(center + new float3(0f, 0f, heightOffset), radius, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f));

		private void DrawVpxSphere(float3 center, float radius)
		{
			DrawVpxCircle(center, radius, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f));
			DrawVpxCircle(center, radius, new float3(1f, 0f, 0f), new float3(0f, 0f, 1f));
			DrawVpxCircle(center, radius, new float3(0f, 1f, 0f), new float3(0f, 0f, 1f));
		}

		private void DrawVpxCylinder(float3 center, float radius, float height)
		{
			if (radius <= 0f) {
				return;
			}

			DrawVpxDisc(center, radius);
			if (height <= 0f) {
				return;
			}
			DrawVpxDisc(center, radius, height);

			const int segments = 8;
			for (var i = 0; i < segments; i++) {
				var angle = (math.TAU * i) / segments;
				var vpxBase = center + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0f);
				var vpxTop = vpxBase + new float3(0f, 0f, height);
				Gizmos.DrawLine(VpxToWorld(vpxBase), VpxToWorld(vpxTop));
			}
		}
	}
}
