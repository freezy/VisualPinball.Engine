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
	public class RampApi : ItemApi<Engine.VPT.Ramp.Ramp, Engine.VPT.Ramp.RampData>, IApiInitializable, IApiColliderGenerator
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		internal RampApi(Engine.VPT.Ramp.Ramp item, Entity entity, Entity parentEntity, Player player) : base(item, entity, parentEntity, player)
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

		protected override bool FireHitEvents => Data.HitEvent;
		protected override float HitThreshold => Data.Threshold;

		void IApiColliderGenerator.CreateColliders(Table table, List<ICollider> colliders)
		{
			var colliderGenerator = new RampColliderGenerator(this);
			colliderGenerator.GenerateColliders(table, colliders);
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo(Table table) => GetColliderInfo(table);

		#endregion

	}
}
