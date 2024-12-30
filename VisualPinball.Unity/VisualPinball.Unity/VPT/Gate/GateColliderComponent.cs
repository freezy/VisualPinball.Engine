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

// ReSharper disable InconsistentNaming

using System.ComponentModel;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Gate Collider")]
	public class GateColliderComponent : ColliderComponent<GateData, GateComponent>, IGateColliderData
	{
		#region Data

		[Range(-180f, 180f)]
		[ToolboxItem("Angle of bracket/plate when opened")]
		public float _angleMax = 90f;

		[Range(-180f, 180f)]
		[ToolboxItem("Angle of bracket/plate when closed")]
		public float _angleMin;

		[Range(-75, -50f)]
		[ToolboxItem("Bottom Z position of the gate collider, relative to the axis.")]
		public float ZLow = -50f;

		[Range(-50, 50)]
		[ToolboxItem("Distance in gate direction of the collider.")]
		public float Distance = 0f;

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

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(Elasticity, friction: Friction);

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.GateApi ?? new GateApi(gameObject, player, physicsEngine);
	}
}
