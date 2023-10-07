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

using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	public class RubberApi : CollidableApi<RubberComponent, RubberColliderComponent, RubberData>,
		IApi, IApiHittable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the rubber.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		internal RubberApi(GameObject go, Player player) : base(go, player)
		{
		}

		#region Collider Generation

		protected override bool FireHitEvents => ColliderComponent.HitEvent;
		protected override float HitThreshold => 2.0f; // hard coded threshold for now

		protected override void CreateColliders(ref ColliderReference colliders, float margin)
		{
			var colliderGenerator = new RubberColliderGenerator(this, new RubberMeshGenerator(MainComponent));
			colliderGenerator.GenerateColliders(MainComponent.PlayfieldHeight, ColliderComponent.HitHeight, MainComponent.PlayfieldDetailLevel, ref colliders, margin);
		}

		#endregion

		#region Events

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		void IApiHittable.OnHit(int ballId, bool _)
		{
			Hit?.Invoke(this, new HitEventArgs(ballId));
		}

		#endregion
	}
}
