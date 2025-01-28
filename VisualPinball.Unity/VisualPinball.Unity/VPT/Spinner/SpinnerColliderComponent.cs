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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Spinner Collider")]
	public class SpinnerColliderComponent : ColliderComponent<SpinnerData, SpinnerComponent>
	{
		#region Data

		[Min(0f)]
		[Tooltip("Bounciness (coefficient of restitution) of the spinner bracket.")]
		public float Elasticity = 0.3f;

		[Tooltip("Collider z-position relative to the spinner.")]
		public float ZPosition;

		#endregion

		#region Physics Material

		protected override float PhysicsElasticity => Elasticity;
		protected override float PhysicsElasticityFalloff => 1;
		protected override float PhysicsFriction => 0;
		protected override float PhysicsScatter => 0;
		protected override bool PhysicsOverwrite => true;

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.SpinnerApi ?? new SpinnerApi(gameObject, player, physicsEngine);

	}
}
