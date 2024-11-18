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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Playfield Collider")]
	public class PlayfieldColliderComponent : ColliderComponent<TableData, PlayfieldComponent>
	{
		#region Data

		[Tooltip("The gravity constant of this playfield")]
		public float Gravity = 0.97f;

		[Min(0f)]
		[Tooltip("Playfield bounciness, also known as coefficient of restitution. Higher is more bouncy.")]
		public float Elasticity = 0.25f;

		[Min(0f)]
		[Tooltip("How much to decrease elasticity for fast impacts.")]
		public float ElasticityFalloff;

		[Min(0)]
		[Tooltip("Playfield of the material.")]
		public float Friction = 0.075f;

		public float Scatter;

		public float DefaultScatter;

		public bool CollideWithBounds = true;

		#endregion

		[NonSerialized] public bool ShowAllColliderMeshes = false;

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(Elasticity, ElasticityFalloff, Friction, Scatter);
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.PlayfieldApi ?? new PlayfieldApi(gameObject, player, physicsEngine);

		public override float4x4 TranslateWithinPlayfieldMatrix(float4x4 worldToPlayfield) => float4x4.identity;
	}
}
