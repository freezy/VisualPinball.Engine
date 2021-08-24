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
using System.ComponentModel;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Gate Collider")]
	public class GateColliderAuthoring : ItemColliderAuthoring<Gate, GateData, GateAuthoring>, IGateColliderData
	{
		#region Data

		[Range(-180f, 180f)]
		[ToolboxItem("Angle of bracket/plate when opened")]
		public float _angleMax = 90f;

		[Range(-180f, 180f)]
		[ToolboxItem("Angle of bracket/plate when closed")]
		public float _angleMin;

		[Min(0)]
		[ToolboxItem("How much damping is applied during movement")]
		public float Damping = 0.985f;

		[ToolboxItem("Elasticity on the blocking side of the gate")]
		[Min(0)]
		public float Elasticity = 0.3f;

		[Min(0)]
		[ToolboxItem("Friction on the blocking side of the gate")]
		public float Friction = 0.02f;

		[Min(0)]
		public float GravityFactor = 0.25f;

		[ToolboxItem("If set, the ball can pass through both sides of the gate.")]
		public bool _twoWay;

		#endregion

		#region IGateColliderData

		public float AngleMin { get => _angleMin; set => _angleMin = value; }

		public float AngleMax { get => _angleMax; set => _angleMax = value; }
		public bool TwoWay => _twoWay;

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(Elasticity, friction: Friction);

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity)
			=> new GateApi(gameObject, entity, parentEntity, player);
	}
}
