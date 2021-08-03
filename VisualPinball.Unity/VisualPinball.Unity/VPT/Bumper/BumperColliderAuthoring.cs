﻿// Visual Pinball Engine
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
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Bumper Collider")]
	public class BumperColliderAuthoring : ItemColliderAuthoring<Bumper, BumperData, BumperAuthoring>
	{
		public static readonly Type[] ValidParentTypes = new Type[0];

		#region Data

		[Min(0f)]
		[Tooltip("Minimal impact force of the ball for the bumper to trigger.")]
		public float Threshold = 1.0f;

		[Min(0f)]
		[Tooltip("How much the ball is thrown back.")]
		public float Force = 15f;

		[Range(-90f, 90f)]
		[Tooltip("A random angle added to the collision trajectory.")]
		public float Scatter;

		[Tooltip("If set, a hit event is triggered.")]
		public bool HitEvent = true;

		#endregion

		public override IEnumerable<Type> ValidParents => ValidParentTypes;
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity)
			=> new BumperApi(Item, entity, parentEntity, player);
	}
}