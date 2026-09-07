// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEngine;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SpringHingeComponent))]
	[AddComponentMenu("Pinball/Mechs/Spring Hinge Collider")]
	public class SpringHingeColliderComponent : MonoBehaviour
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
		[Range(-90f, 90f)] public float Scatter;
		public bool OverwritePhysics = true;
		public PhysicsMaterialAsset PhysicsMaterial;

		private void OnValidate()
		{
			HalfExtents = Vector3.Max(HalfExtents, Vector3.zero);
		}
	}
}
