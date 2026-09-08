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
	[PackAs("SpringHingeAnimation")]
	[AddComponentMenu("Pinball/Animation/Spring Hinge Transform")]
	public class SpringHingeAnimationComponent : AnimationComponent<float>, IPackable
	{
		[Tooltip("Rotation axis in this moving transform's local frame.")]
		public Vector3 RotationAxis = Vector3.right;

		private Quaternion _initialLocalRotation;
		private bool _poseCaptured;

		public byte[] Pack() => SpringHingeAnimationPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> SpringHingeAnimationReferencesPackable.Pack(this, refs);

		public void Unpack(byte[] bytes) => SpringHingeAnimationPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files)
			=> SpringHingeAnimationReferencesPackable.Unpack(bytes, this, refs);

		protected override void Awake()
		{
			base.Awake();
			CaptureInitialPose();
		}

		protected override void OnAnimationValueChanged(float angle) => ApplyAngle(angle);

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
			var axis = math.normalizesafe((float3)RotationAxis, new float3(1f, 0f, 0f));
			transform.localRotation = _initialLocalRotation
				* Quaternion.AngleAxis(math.degrees(angle), axis);
		}

#if UNITY_EDITOR
		protected override void OnValidate()
		{
			base.OnValidate();
			if (math.lengthsq((float3)RotationAxis) < 1e-8f) {
				RotationAxis = Vector3.right;
			}
		}
#endif
	}
}
