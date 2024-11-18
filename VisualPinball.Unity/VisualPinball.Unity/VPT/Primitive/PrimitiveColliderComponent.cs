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
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Primitive Collider")]
	public class PrimitiveColliderComponent : ColliderComponent<PrimitiveData, PrimitiveComponent>, IKinematicColliderComponent
	{
		#region Data

		[Tooltip("If set, a hit event is triggered.")]
		public bool HitEvent = true;

		[Range(0, 100f)]
		[Tooltip("Minimal impact needed in order to trigger a hit event.")]
		public float Threshold = 2f;

		[Min(0f)]
		[Tooltip("Bounciness, also known as coefficient of restitution. Higher is more bouncy.")]
		public float Elasticity = 0.3f;

		[Min(0f)]
		[Tooltip("How much to decrease elasticity for fast impacts.")]
		public float ElasticityFalloff = 0.5f;

		[Min(0)]
		[Tooltip("Friction of the material.")]
		public float Friction = 0.3f;

		[Range(-90f, 90f)]
		[Tooltip("When hit, add a random angle between 0 and this value to the trajectory.")]
		public float Scatter;

		[Range(0, 1f)]
		[Tooltip("Reduces triangles of the collider mesh for better performance. Be sure to verify it's what you want using the debug collider view.")]
		public float CollisionReductionFactor = 0;

		[Tooltip("Ignore the assigned physics material above and use the value below.")]
		public bool OverwritePhysics = true;

		[Tooltip("If set, transforming this object will transform the colliders as well.")]
		public bool _isKinematic;

		#endregion

		#region IKinematicColliderComponent

		public bool IsKinematic => _isKinematic;
		public int ItemId => MainComponent.gameObject.GetInstanceID();
		public float4x4 TransformationWithinPlayfield => MainComponent.TransformationWithinPlayfield;

		#endregion

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(Elasticity, ElasticityFalloff, Friction, Scatter, OverwritePhysics);
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=>MainComponent.PrimitiveApi ?? new PrimitiveApi(gameObject, player, physicsEngine);

		public override float4x4 TranslateWithinPlayfieldMatrix(float4x4 worldToPlayfield)
			=> MainComponent.transform.localToWorldMatrix.LocalToWorldTranslateWithinPlayfield(worldToPlayfield);
	}
}
