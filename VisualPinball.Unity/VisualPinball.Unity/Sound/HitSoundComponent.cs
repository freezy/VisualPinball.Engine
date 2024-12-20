// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using NLog;
using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sound/Hit Sound")]
	public class HitSoundComponent : SoundComponent
	{
		private IApiHittable _hittable;

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			if (TryFindHittable(out _hittable))
				_hittable.Hit += OnHittableHit;
			else
				Logger.Warn("Could not find main component with Api of type IApiHittable. Sound will not be triggered.");
		}

		private bool TryFindHittable(out IApiHittable hittable)
		{
			hittable = null;
			var player = GetComponentInParent<Player>();
			if (player == null)
				return false;
			foreach (var component in GetComponents<ItemComponent>()) {
				hittable = player.TableApi.Hittable(component);
				if (hittable != null)
					return true;
			}
			return false;
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			if (_hittable != null) {
				_hittable.Hit -= OnHittableHit;
				_hittable = null;
			}
		}
		
		private async void OnHittableHit(object sender, HitEventArgs e) => await Play();
		public override bool SupportsLoopingSoundAssets() => false;
		public override Type GetRequiredType() => typeof(ItemComponent);
	}
}
