// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SpringHingeComponent))]
	[AddComponentMenu("Pinball/Mechs/Spring Hinge Collider")]
	public class SpringHingeColliderComponent : MonoBehaviour, ICollidableComponent
	{
		[Unit("mm")]
		[Tooltip("Collision-box centre in the hinge's local frame.")]
		public Vector3 LocalCentre = new(0f, -50f, 0f);

		[Tooltip("Collision-box orientation in the hinge's local frame, in degrees.")]
		public Vector3 LocalRotation;

		[Unit("mm")]
		[Tooltip("Collision-box half-extents in its local frame.")]
		public Vector3 HalfExtents = new(25f, 50f, 10f);

		[Range(0f, 1f)] public float Elasticity = 0.1f;
		[Min(0f)] public float ElasticityFalloff = 0.5f;
		[Range(0f, 1f)] public float Friction = 0.3f;
		[Tooltip("Emit a Hit event when the ball strikes the toy at a new position.")]
		public bool HitEvent = true;
		[Min(0f)] public float HitThreshold;
		public bool OverwritePhysics = true;
		public PhysicsMaterialAsset PhysicsMaterial;

		public int ItemId => GetComponent<SpringHingeComponent>().ItemId;
		public bool IsKinematic => false;
		public bool CollidersDirty { set { } }
		internal bool IsCollidable => isActiveAndEnabled && math.all((float3)HalfExtents > 0f);

		public float PhysicsElasticity { get => Elasticity; set => Elasticity = value; }
		public float PhysicsElasticityFalloff { get => ElasticityFalloff; set => ElasticityFalloff = value; }
		public float PhysicsFriction { get => Friction; set => Friction = value; }
		// Spring-hinge impacts deliberately exclude the legacy planar scatter heuristic.
		public float PhysicsScatter { get => 0f; set { } }
		public bool PhysicsOverwrite { get => OverwritePhysics; set => OverwritePhysics = value; }
		public PhysicsMaterialAsset PhysicsMaterialReference { get => PhysicsMaterial; set => PhysicsMaterial = value; }

		private void OnValidate()
		{
			HalfExtents = Vector3.Max(HalfExtents, Vector3.zero);
		}

		void ICollidableComponent.GetColliders(Player player, PhysicsEngine physicsEngine,
			ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (!IsCollidable) {
				return;
			}
			var hinge = GetComponent<SpringHingeComponent>();
			var api = hinge.SpringHingeApi ?? new SpringHingeApi(hinge, physicsEngine);
			((IApiColliderGenerator)api).CreateColliders(ref colliders, float4x4.identity, margin);
		}

		bool ICollidableComponent.IsCollidable => IsCollidable;
		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield) => float4x4.identity;
		public void OnTransformationChanged(float4x4 currTransformationMatrix) { }
	}
}
