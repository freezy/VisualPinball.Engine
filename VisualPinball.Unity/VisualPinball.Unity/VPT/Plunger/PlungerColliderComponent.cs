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

using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Plunger Collider")]
	public class PlungerColliderComponent : ColliderComponent<PlungerData, PlungerComponent>, IKinematicColliderComponent
	{
		#region Data

		[Min(0)]
		[Tooltip("How quick the plunger moves back.")]
		public float SpeedPull = 0.5f;

		[Min(0)]
		[Tooltip("How quick the plunger moves back when let go.")]
		public float SpeedFire = 80f;

		[Min(0)]
		public float Stroke = 80f;
		[Min(0)]
		public float ScatterVelocity;

		public bool IsMechPlunger;
		public bool IsAutoPlunger;

		[Min(0)]
		public float MechStrength = 85f;

		[Min(0)]
		public float MomentumXfer = 1f;

		[Range(0, 1f)]
		[Tooltip("At which position the plunger rests.")]
		public float ParkPosition = 0.5f / 3.0f;

		#endregion

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData();
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.PlungerApi ?? new PlungerApi(gameObject, player, physicsEngine);
	}
}
