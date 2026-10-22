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
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("CollisionSound")]
	[AddComponentMenu("Pinball/Sound/Collision Sound")]
	public class CollisionSoundComponent : SoundComponent, IPackable
	{
		public BallImpactKind Source;
		private Player _player;
		private int _itemId;
		private readonly Dictionary<long, uint> _lastImpact = new();
		public override Type GetRequiredType() => typeof(ItemComponent);
		public override bool SupportsLoopingSoundAssets() => false;

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			_lastImpact.Clear();
			_player = GetComponentInParent<Player>();
			_itemId = Source == BallImpactKind.BallCollision ? 0 : UnityObjectId.Get(gameObject);
			if (_player) _player.SubscribeToImpacts(_itemId, OnImpact);
		}

		protected override void OnDisable()
		{
			if (_player) _player.UnsubscribeFromImpacts(_itemId, OnImpact);
			_player = null;
			_lastImpact.Clear();
			base.OnDisable();
		}

		public void OnImpact(BallImpact impact)
		{
			if (!isActiveAndEnabled || impact.Kind != Source || SoundAsset is not CollisionSoundAsset profile
				|| profile.Loop || !profile.Accepts(impact)) return;
			// Each object and ball (or unordered ball pair) has its own retrigger clock.
			var low = impact.OtherBallId == 0 ? impact.BallId : math.min(impact.BallId, impact.OtherBallId);
			var high = impact.OtherBallId == 0 ? 0 : math.max(impact.BallId, impact.OtherBallId);
			var key = ((long)(uint)low << 32) | (uint)high;
			if (_lastImpact.TryGetValue(key, out var previous) && unchecked(impact.TimeMsec - previous) < profile.RetriggerMsec) return;
			_lastImpact[key] = impact.TimeMsec;
			var position = _player
				? (Vector3)math.transform(_player.PlayfieldToWorldMatrix, Physics.TranslateToWorld(impact.Position.x, impact.Position.y, impact.Position.z))
				: transform.position;
			foreach (var rule in profile.Rules) {
				if (!rule.Matches(impact) || rule.Clips == null || rule.Clips.Length == 0) continue;
				var choice = UnityEngine.Random.Range(0, rule.Clips.Length);
				var index = rule.Clips[choice];
				if (profile.Clips == null || index < 0 || index >= profile.Clips.Length || !profile.Clips[index]) continue;
				var clip = profile.Clips[index];
				var gain = rule.Gain * ImpactSoundRule.Measure(rule.Volume, impact);
				if (rule.ClipGains != null && choice < rule.ClipGains.Length) gain *= rule.ClipGains[choice];
				var pitch = rule.PitchFromSpeed ? 1 + math.floor(math.length(impact.Velocity.xy)) * 20 / clip.frequency : 1;
				StartImpactSound(profile, gain, new SoundEffectPlayback {
					Clip = clip, Position = position, Pitch = pitch, BypassCooldown = true
				});
			}
		}

		public new byte[] Pack() => PackageApi.Packer.Pack(new CollisionSoundPackable { Sound = base.Pack(), Source = Source });
		public new void Unpack(byte[] bytes)
		{
			var data = PackageApi.Packer.Unpack<CollisionSoundPackable>(bytes);
			base.Unpack(data.Sound);
			Source = data.Source;
		}
	}

	public struct CollisionSoundPackable
	{
		public byte[] Sound;
		public BallImpactKind Source;
	}
}
