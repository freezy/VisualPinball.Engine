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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Surface Collider")]
	public class SurfaceColliderAuthoring : ItemColliderAuthoring<Surface, SurfaceData, SurfaceAuthoring>
	{
		#region Data

		public bool HitEvent;

		public float Threshold = 2.0f;

		public PhysicsMaterial SlingShotMaterial;

		public float SlingshotForce = 80f;

		public float SlingshotThreshold;

		public bool SlingshotAnimation = true;

		public bool OverwritePhysics = true;

		public float Elasticity;

		public float ElasticityFalloff;

		public float Friction;

		public float Scatter;

		#endregion

		public static readonly Type[] ValidParentTypes = {
			typeof(RubberAuthoring),
			typeof(PrimitiveAuthoring)
		};

		public override IEnumerable<Type> ValidParents => ValidParentTypes;
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity)
			=> new SurfaceApi(Item, entity, parentEntity, PhysicsMaterial, player);
	}
}
