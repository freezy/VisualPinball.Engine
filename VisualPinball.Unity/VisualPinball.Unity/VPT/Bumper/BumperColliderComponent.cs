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
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Bumper Collider")]
	public class BumperColliderComponent : ColliderComponent<BumperData, BumperComponent>, IKinematicColliderComponent, ICollidableNonTransformableComponent
	{
		#region Data

		[Min(0f)]
		[Tooltip("Minimal impact force of the ball for the bumper to trigger.")]
		public float Threshold = 1.0f;

		[Min(0f)]
		[Tooltip("How much the ball is thrown back when colliding with the bumper.")]
		public float Force = 15f;

		[Range(-90f, 90f)]
		[Tooltip("When hit, add a random angle between 0 and this value to the trajectory.")]
		public float Scatter;

		[Tooltip("If set, a hit event is triggered.")]
		public bool HitEvent = true;

		[Tooltip("If set, transforming this object will transform the colliders as well.")]
		public bool _isKinematic;

		#endregion

		#region IKinematicColliderComponent

		public bool IsKinematic => _isKinematic;
		public int ItemId => MainComponent.gameObject.GetInstanceID();
		public float4x4 TransformationWithinPlayfield => MainComponent.TransformationWithinPlayfield;

		float4x4 ICollidableNonTransformableComponent.TranslateWithinPlayfieldMatrix(float4x4 worldToPlayfield)
			=> MainComponent.transform.localToWorldMatrix.LocalToWorldTranslateWithinPlayfield(worldToPlayfield);

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.BumperApi ?? new BumperApi(gameObject, player, physicsEngine);

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(scatterAngleDeg: Scatter);
	}
}
