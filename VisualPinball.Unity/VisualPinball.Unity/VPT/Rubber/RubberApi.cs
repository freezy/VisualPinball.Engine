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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class RubberApi : ItemApi<Engine.VPT.Rubber.Rubber, Engine.VPT.Rubber.RubberData>,
		IApiInitializable, IApiHittable, IColliderGenerator
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the rubber.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		internal RubberApi(Engine.VPT.Rubber.Rubber item, Entity entity, Entity parentEntity, Player player) : base(item, entity, parentEntity, player)
		{
		}

		#region Collider Generation

		internal override bool FireHitEvents => Data.HitEvent;
		internal override float HitThreshold { get; } = 2.0f; // hard coded threshold for now

		void IColliderGenerator.CreateColliders(Table table, List<ICollider> colliders, ref int nextColliderId)
		{
			var colliderGenerator = new RubberColliderGenerator(this);
			colliderGenerator.GenerateColliders(table, colliders, ref nextColliderId);
		}

		ColliderInfo IColliderGenerator.GetNextColliderInfo(Table table, ref int nextColliderId) =>
			GetNextColliderInfo(table, ref nextColliderId);

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
