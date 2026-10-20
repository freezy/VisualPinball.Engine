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

using System;
using UnityEngine;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	[PackAs("FlipperSound")]
	[AddComponentMenu("Pinball/Sound/Flipper Sound")]
	public class FlipperSoundComponent : SoundComponent, IPackable
	{
		public SoundEffectAsset Attack, Up, Reflip, Down;
		public float ReflipDegrees = 20f;
		public float ReleaseDegrees = 5f;
		public float UpLevel = 1f;
		public float DownLevel = 0.45f;
		public Vector2 AttackGain = new(0.010f, 0.635f);
		public Vector2 ReflipGain = new(0.8f, 1f);
		private float _hitGain;
		private FlipperApi _api;
		private FlipperComponent _flipper;
		public override Type GetRequiredType() => typeof(FlipperComponent);
		public override bool SupportsLoopingSoundAssets() => false;
		protected override MultiPlayMode EffectiveMultiPlayMode => MultiPlayMode.PlayInParallel;
		protected override void OnEnableAfterAfterAwake() {
			base.OnEnableAfterAfterAwake();
			_hitGain = UpLevel;
			_flipper = GetComponent<FlipperComponent>();
			_api = GetComponentInParent<Player>()?.TableApi.Flipper(_flipper);
			if (_api == null) {
				Logger.Warn("Flipper sound {Name} needs a registered flipper.", name);
				return;
			}
			_api.Actuated += OnActuated;
			_api.Collide += OnCollide;
		}
		protected override void OnDisable() {
			if (_api != null) {
				_api.Actuated -= OnActuated;
				_api.Collide -= OnCollide;
				_api = null;
			}
			_hitGain = UpLevel;
			base.OnDisable();
		}
		private void OnCollide(object sender, CollideEventArgs e) => _hitGain = math.clamp(e.FlipperHit / 10f, 0f, 1f) * UpLevel;
		public static bool IsReflip(float angle, float start, float end, float threshold) => (end - angle) * math.sign(end - start) < threshold;
		public static bool HasReleaseTravel(float angle, float start, float end, float threshold) => (angle - start) * math.sign(end - start) > threshold;
		private void OnActuated(object sender, FlipperActuationEventArgs e) {
			var angle = math.degrees(e.Angle);
			if (e.Energized) {
				if (IsReflip(angle, _api.RestAngle, _api.EndAngle, ReflipDegrees))
					Play(Reflip, UnityEngine.Random.Range(ReflipGain.x, ReflipGain.y) * UpLevel);
				else {
					Play(Attack, UnityEngine.Random.Range(AttackGain.x, AttackGain.y));
					Play(Up, _hitGain);
				}
			}
			else {
				if (HasReleaseTravel(angle, _api.RestAngle, _api.EndAngle, ReleaseDegrees))
					Play(Down, DownLevel);
				_hitGain = UpLevel;
			}
		}
		private void Play(SoundEffectAsset asset, float gain) {
			if (asset)
				StartSound(asset, gain);
		}
		public new byte[] Pack() => PackageApi.Packer.Pack(new FlipperSoundPackable
		{
			Sound = base.Pack(),
			ReflipDegrees = ReflipDegrees,
			ReleaseDegrees = ReleaseDegrees,
			UpLevel = UpLevel,
			DownLevel = DownLevel,
			AttackGain = AttackGain,
			ReflipGain = ReflipGain
		});
		public new void Unpack(byte[] bytes) {
			var p = PackageApi.Packer.Unpack<FlipperSoundPackable>(bytes);
			base.Unpack(p.Sound);
			ReflipDegrees = p.ReflipDegrees;
			ReleaseDegrees = p.ReleaseDegrees;
			UpLevel = p.UpLevel;
			DownLevel = p.DownLevel;
			AttackGain = p.AttackGain;
			ReflipGain = p.ReflipGain;
		}
		public new byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => PackageApi.Packer.Pack(new FlipperSoundReferencesPackable
		{
			Attack = Attack ? files.AddAsset(Attack) : 0,
			Up = Up ? files.AddAsset(Up) : 0,
			Reflip = Reflip ? files.AddAsset(Reflip) : 0,
			Down = Down ? files.AddAsset(Down) : 0
		});
		public new void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) {
			var p = PackageApi.Packer.Unpack<FlipperSoundReferencesPackable>(bytes);
			Attack = p.Attack == 0 ? null : files.GetAsset<SoundEffectAsset>(p.Attack);
			Up = p.Up == 0 ? null : files.GetAsset<SoundEffectAsset>(p.Up);
			Reflip = p.Reflip == 0 ? null : files.GetAsset<SoundEffectAsset>(p.Reflip);
			Down = p.Down == 0 ? null : files.GetAsset<SoundEffectAsset>(p.Down);
		}
	}
	public struct FlipperSoundPackable
	{
		public byte[] Sound;
		public float ReflipDegrees, ReleaseDegrees, UpLevel, DownLevel;
		public Vector2 AttackGain, ReflipGain;
	}
	public struct FlipperSoundReferencesPackable
	{
		public int Attack, Up, Reflip, Down;
	}
}
