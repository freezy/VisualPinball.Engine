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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	public class SurfaceApi : CollidableApi<SurfaceComponent, SurfaceColliderComponent, SurfaceData>,
		IApi, IApiHittable, IApiSlingshot
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the surface.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when a slingshot segment was hit.
		/// </summary>
		public event EventHandler Slingshot;

		internal SurfaceApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		#region Collider Generation

		protected override bool FireHitEvents => true;
		protected override float HitThreshold => ColliderComponent.Threshold;
		protected override void CreateColliders(ref ColliderReference colliders, float margin)
		{
			if (MainComponent.DragPoints.Length == 0) {
				return;
			}
			var colliderGenerator = new SurfaceColliderGenerator(this, MainComponent, ColliderComponent);
			colliderGenerator.GenerateColliders(MainComponent.PlayfieldHeight, ref colliders, margin);
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

		public void OnSlingshot(int ballId)
		{
			Slingshot?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
