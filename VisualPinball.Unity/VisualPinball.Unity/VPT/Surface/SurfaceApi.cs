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
	public class SurfaceApi : ItemCollidableApi<SurfaceComponent, SurfaceColliderComponent, Engine.VPT.Surface.SurfaceData>,
		IApiInitializable, IApiHittable, IApiSlingshot
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

		internal SurfaceApi(GameObject go, Entity entity, Entity parentEntity, Player player) : base(go, entity, parentEntity, player)
		{
		}

		#region Collider Generation

		protected override bool FireHitEvents => true;
		protected override float HitThreshold => ColliderComponent.Threshold;
		protected override void CreateColliders(List<ICollider> colliders)
		{
			if (MainComponent.DragPoints.Length == 0) {
				return;
			}
			var colliderGenerator = new SurfaceColliderGenerator(this, MainComponent, ColliderComponent);
			colliderGenerator.GenerateColliders(MainComponent.PlayfieldHeight, colliders);
		}

		#endregion

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(Entity ballEntity, bool _)
		{
			Hit?.Invoke(this, new HitEventArgs(ballEntity));
		}

		public void OnSlingshot(Entity ballEntity)
		{
			Slingshot?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
