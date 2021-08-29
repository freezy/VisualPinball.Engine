// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class RampApi : ItemCollidableApi<RampAuthoring, RampColliderAuthoring, Engine.VPT.Ramp.RampData>,
		IApiInitializable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		internal RampApi(GameObject go, Entity entity, Entity parentEntity, Player player) : base(go, entity, parentEntity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		#endregion

		#region Collider Generation

		protected override bool FireHitEvents => ColliderComponent.HitEvent;
		protected override float HitThreshold => ColliderComponent.Threshold;

		protected override void CreateColliders(List<ICollider> colliders)
		{
			var colliderGenerator = new RampColliderGenerator(this, MainComponent, ColliderComponent);
			colliderGenerator.GenerateColliders(MainComponent.PlayfieldHeight, colliders);
		}

		#endregion

	}
}
