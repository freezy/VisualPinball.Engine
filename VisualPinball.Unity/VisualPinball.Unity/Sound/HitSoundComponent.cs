// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Add this component to an object on the playfield to play a sound whenever it gets hit by the ball.
	/// </summary>
	[AddComponentMenu("Pinball/Sound/Hit Sound")]
	public class HitSoundComponent : EventSoundComponent<IApiHittable, HitEventArgs>
	{
		public override bool SupportsLoopingSoundAssets() => false;

		public override Type GetRequiredType() => typeof(ItemComponent);

		protected override bool TryFindEventSource(out IApiHittable hittable)
		{
			hittable = null;
			var player = GetComponentInParent<Player>();
			if (player == null)
				return false;

			foreach (var component in GetComponents<ItemComponent>())
			{
				hittable = player.TableApi.Hittable(component);
				if (hittable != null)
					return true;
			}
			return false;
		}

		protected override void Subscribe(IApiHittable eventSource) => eventSource.Hit += OnEvent;

		protected override void Unsubscribe(IApiHittable eventSource) => eventSource.Hit -= OnEvent;
	}
}
