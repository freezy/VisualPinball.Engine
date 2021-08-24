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
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class PrimitiveApi : ItemCollidableApi<PrimitiveAuthoring, PrimitiveColliderAuthoring, Primitive, PrimitiveData>,
		IApiInitializable, IApiHittable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball glides on the primitive.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		internal PrimitiveApi(GameObject go, Entity entity, Entity parentEntity, Player player) : base(go, entity, parentEntity, player)
		{
		}

		#region Collider Generation

		protected override bool FireHitEvents => ColliderComponent.HitEvent;
		protected override float HitThreshold => ColliderComponent.Threshold;

		protected override void CreateColliders(Table table, List<ICollider> colliders)
		{
			var mf = MainComponent.GetComponent<MeshFilter>();
			if (mf && mf.sharedMesh) {
				var colliderGenerator = new PrimitiveColliderGenerator(this, MainComponent.CreateData());
				colliderGenerator.GenerateColliders(table, mf.sharedMesh.ToVpMesh(), ColliderComponent.CollisionReductionFactor, colliders);
			}
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

		#endregion
	}
}
