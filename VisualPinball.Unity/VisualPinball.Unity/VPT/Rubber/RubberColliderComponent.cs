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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Rubber Collider")]
	public class RubberColliderComponent : ItemColliderComponent<RubberData, RubberComponent>
	{
		#region Data

		public bool HitEvent;

		[Tooltip("Z-axis translation for the collider mesh.")]
		public float HitHeight = 25f;

		public bool OverwritePhysics;

		public float Elasticity;

		public float ElasticityFalloff;

		public float Friction;

		public float Scatter;

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;
		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(Elasticity, ElasticityFalloff, Friction, Scatter, OverwritePhysics);
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity)
			=> new RubberApi(gameObject, entity, parentEntity, player);
	}
}
