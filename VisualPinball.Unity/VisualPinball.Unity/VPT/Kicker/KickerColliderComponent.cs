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
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Kicker Collider")]
	public class KickerColliderComponent : ColliderComponent<KickerData, KickerComponent>
	{
		#region Data

		[Range(-90f, 90f)]
		[Tooltip("How many degrees of randomness is added to the ball trajectory when ejecting.")]
		public float Scatter;

		[Range(0f, 1f)]
		[Tooltip("How fast the ball gets caught by the trigger.")]
		public float HitAccuracy = 0.7f;

		[Tooltip("The height of the collider of the kicker.")]
		public float HitHeight = 40.0f;

		[Tooltip("Whether the ball continues moving through the kicker. If not set, the ball is frozen and kept in the kicker.")]
		public bool FallThrough;

		[Tooltip("Use a better collision model. Currently disabled.")]
		public bool LegacyMode = true;

		[Range(-180f, 180f)]
		[Tooltip("Angle of the ball when kicked out.")]
		public float EjectAngle = 90f;

		[Range(0f, 100f)]
		[Tooltip("Speed the kicker hits the ball when ejecting.")]
		public float EjectSpeed = 3f;

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;
		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(scatterAngleDeg: Scatter);
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity)
			=> new KickerApi(gameObject, entity, parentEntity, player);
	}
}
