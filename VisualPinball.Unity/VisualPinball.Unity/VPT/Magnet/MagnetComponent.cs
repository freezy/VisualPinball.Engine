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
	[PackAs("Magnet")]
	[AddComponentMenu("Pinball/Mechs/Magnet")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/magnets.html")]
	public class MagnetComponent : MonoBehaviour, ICoilDeviceComponent, ISwitchDeviceComponent, IPackable
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

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for magnet {name}.");
				return;
			}

			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			MagnetApi = new MagnetApi(gameObject, player, physicsEngine);

			player.Register(MagnetApi, this);
			if (physicsEngine) {
				physicsEngine.Register(this);
			} else {
				Logger.Error($"Cannot find physics engine for magnet {name}.");
			}
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
				Profile = ForceProfile,
				HeightRange = MillimetersToVpx(HeightRange),
				GrabbedBalls = default,
				ReleasedBalls = default
			};
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
			DrawLocalDisc(radiusWorld);

			if (GrabBall && grabRadiusWorld > 0f) {
				Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
				DrawLocalDisc(grabRadiusWorld);
			}

			if (heightRangeWorld > 0f) {
				Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.35f);
				Gizmos.DrawLine(transform.position, transform.position + transform.up * heightRangeWorld);
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
				var planarOffset = Vector3.ProjectOnPlane(offset, transform.up);
				if (planarOffset.sqrMagnitude <= radiusWorld * radiusWorld) {
					Gizmos.DrawLine(ball.transform.position, ball.transform.position - planarOffset.normalized * math.min(radiusWorld * 0.25f, planarOffset.magnitude));
				}
			}
		}

		private void DrawLocalDisc(float radius)
		{
			if (radius <= 0f) {
				return;
			}

			const int segments = 64;
			var previous = transform.TransformPoint(new Vector3(radius, 0f, 0f));
			for (var i = 1; i <= segments; i++) {
				var angle = (math.TAU * i) / segments;
				var next = transform.TransformPoint(new Vector3(math.cos(angle) * radius, 0f, math.sin(angle) * radius));
				Gizmos.DrawLine(previous, next);
				previous = next;
			}
		}
	}
}
