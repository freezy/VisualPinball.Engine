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
	[PackAs("SpringHingeCollider")]
	[AddComponentMenu("Pinball/Mechs/Spring Hinge Collider")]
	public class SpringHingeColliderComponent : MonoBehaviour, ICollidableComponent, IPackable
	{
		private SpringHingeComponent _hinge;
		private Quaternion _initialLocalRotation;
		private bool _poseCaptured;

		[Unit("VPX")]
		[Tooltip("Collision-box centre in VPX units along the hinge's local axes.")]
		public Vector3 LocalCentre = new(0f, -50f, 0f);

		[Tooltip("Collision-box orientation in the hinge's local frame, in degrees.")]
		public Vector3 LocalRotation;

		[Unit("VPX")]
		[Tooltip("Collision-box half-extents in its local frame, in VPX units.")]
		public Vector3 HalfExtents = new(25f, 50f, 10f);

		[SerializeField]
		[Tooltip("Show the analytic collision box in the Scene view.")]
		public bool ShowColliderMesh;

		[Range(0f, 1f)] public float Elasticity = 0.1f;
		[Min(0f)] public float ElasticityFalloff = 0.5f;
		[Range(0f, 1f)] public float Friction = 0.3f;
		[Tooltip("Emit a Hit event when the ball strikes the toy at a new position.")]
		public bool HitEvent = true;
		[Min(0f)] public float HitThreshold;
		public bool OverwritePhysics = true;
		public PhysicsMaterialAsset PhysicsMaterial;

		public byte[] Pack() => SpringHingeColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> SpringHingeColliderReferencesPackable.PackReferences(this, files);

		public void Unpack(byte[] bytes) => SpringHingeColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> SpringHingeColliderReferencesPackable.Unpack(data, this, files);

		private SpringHingeComponent Hinge
			=> _hinge ? _hinge : _hinge = GetComponent<SpringHingeComponent>();

		public int ItemId => Hinge.ItemId;
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

		private void Awake()
		{
			_hinge = GetComponent<SpringHingeComponent>();
			CaptureInitialPose();
		}

		private void OnEnable()
		{
			if (Hinge) {
				Hinge.OnAnimationValueChanged += ApplyAngle;
			}
		}

		private void OnDisable()
		{
			if (Hinge) {
				Hinge.OnAnimationValueChanged -= ApplyAngle;
			}
		}

		private void OnValidate()
		{
			HalfExtents = Vector3.Max(HalfExtents, Vector3.zero);
		}

		internal void CaptureInitialPose()
		{
			_initialLocalRotation = transform.localRotation;
			_poseCaptured = true;
		}

		internal void ApplyAngle(float angle)
		{
			if (!_poseCaptured) {
				CaptureInitialPose();
			}
			var axis = math.normalizesafe((float3)Hinge.HingeAxis, new float3(1f, 0f, 0f));
			transform.localRotation = _initialLocalRotation
			                          * Quaternion.AngleAxis(math.degrees(angle), axis);
		}

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			if (!ShowColliderMesh || !enabled) {
				return;
			}
			var hinge = Hinge;
			if (!hinge) {
				return;
			}
			var angle = Application.isPlaying ? hinge.PublishedAngle : 0f;
			var axis = math.normalizesafe((float3)hinge.HingeAxis, new float3(1f, 0f, 0f));
			var matrix = hinge.ReferenceLocalToWorldMatrix
			             * Matrix4x4.Rotate(Quaternion.AngleAxis(math.degrees(angle), axis))
			             * Matrix4x4.TRS(LocalCentre * Physics.ScaleInv,
				             Quaternion.Euler(LocalRotation), Vector3.one);
			var previousMatrix = Gizmos.matrix;
			var previousColor = Gizmos.color;
			Gizmos.matrix = matrix;
			Gizmos.color = ColliderColor.TransformedColliderSelected;
			Gizmos.DrawCube(Vector3.zero, HalfExtents * (2f * Physics.ScaleInv));
			Gizmos.color = new Color32(0, 255, 75, 230);
			Gizmos.DrawWireCube(Vector3.zero, HalfExtents * (2f * Physics.ScaleInv));
			Gizmos.matrix = previousMatrix;
			Gizmos.color = previousColor;
		}
#endif

		void ICollidableComponent.GetColliders(Player player, PhysicsEngine physicsEngine,
			ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (!IsCollidable) {
				return;
			}
			var hinge = Hinge;
			var api = hinge.SpringHingeApi ?? new SpringHingeApi(hinge, physicsEngine);
			((IApiColliderGenerator)api).CreateColliders(ref colliders, float4x4.identity, margin);
		}

		bool ICollidableComponent.IsCollidable => IsCollidable;
		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield) => float4x4.identity;
		public void OnTransformationChanged(float4x4 currTransformationMatrix) { }
	}
}
