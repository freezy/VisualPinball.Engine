// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace VisualPinball.Unity
{
	public enum MotionTranslationSpace
	{
		World,
		Local,
		// Append new values only; packaged enum values are serialized numerically.
	}

	[DisallowMultipleComponent]
	[MovedFrom(true, sourceNamespace: "VisualPinball.Unity", sourceClassName: "ActuatorTransformComponent")]
	// Existing packages identify this component by its original serialized name.
	[PackAs("ActuatorTransform")]
	[AddComponentMenu("Pinball/Animation/Motion Transform")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/motion-controllers.html")]
	public class MotionTransformComponent : AnimationComponent<float>, IPackable
	{
		[Tooltip("Translate this transform from its authored local position.")]
		public bool AnimatePosition = true;

		[Tooltip("Position offset at motion controller position 1, expressed in Translation Space.")]
		public Vector3 PositionOffset;

		[Tooltip("Whether Position Offset follows world axes or the follower's authored Local gizmo axes.")]
		public MotionTranslationSpace TranslationSpace = MotionTranslationSpace.World;

		[Tooltip("Rotate this transform from its authored local rotation.")]
		public bool AnimateRotation;

		[Unit("degrees")]
		[Tooltip("Local Euler rotation offset at motion controller position 1.")]
		public Vector3 RotationOffset;

		[Range(0f, 1f)]
		[Tooltip("Source position where this follower's response curve begins.")]
		public float InputMin;

		[Range(0f, 1f)]
		[Tooltip("Source position where this follower's response curve ends. Must be greater than Input Min.")]
		public float InputMax = 1f;

		[Tooltip("Maps progress through Input Min/Max to this transform's normalized travel.")]
		public AnimationCurve ResponseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		[Tooltip("Reverse progress within Input Min/Max before applying the response curve.")]
		public bool Reverse;

		private Vector3 _initialLocalPosition;
		private Quaternion _initialLocalRotation;
		private float _currentFactor;
		private bool _poseCaptured;

		public byte[] Pack() => MotionTransformPackable.Pack(this);
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => MotionTransformReferencesPackable.Pack(this, refs);
		public void Unpack(byte[] bytes) => MotionTransformPackable.Unpack(bytes, this);
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) => MotionTransformReferencesPackable.Unpack(bytes, this, refs);

		protected override void Awake()
		{
			base.Awake();
			CaptureInitialPose();
			ApplyCurrentValue();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			ApplyCurrentValue();
		}

		private void LateUpdate()
		{
			if (_poseCaptured && AnimatePosition && TranslationSpace == MotionTranslationSpace.World) {
				ApplyPosition(_currentFactor);
			}
		}

		protected override void OnAnimationValueChanged(float value) => ApplyValue(value);

		internal void CaptureInitialPose()
		{
			_initialLocalPosition = transform.localPosition;
			_initialLocalRotation = transform.localRotation;
			_poseCaptured = true;
		}

		internal void ApplyCurrentValue()
		{
			if (!_poseCaptured) {
				return;
			}
			if (Emitter is IAnimationValueProvider<float> provider) {
				ApplyValue(provider.AnimationValue);
			}
		}

		internal void ApplyValue(float value)
		{
			if (!_poseCaptured) {
				CaptureInitialPose();
			}

			var factor = EvaluateFactor(value);
			_currentFactor = factor;
			if (AnimatePosition) {
				ApplyPosition(factor);
			}
			if (AnimateRotation) {
				var endRotation = _initialLocalRotation * Quaternion.Euler(RotationOffset);
				transform.localRotation = Quaternion.SlerpUnclamped(_initialLocalRotation, endRotation, factor);
			}
		}

		public bool HasValidInputRange => math.isfinite(InputMin) && math.isfinite(InputMax) &&
			InputMin >= 0f && InputMax <= 1f && InputMin < InputMax;

		/// <summary>Shared runtime/preview mapping. Invalid input or range retains the authored pose.</summary>
		public float EvaluateFactor(float value)
		{
			if (!HasValidInputRange || !math.isfinite(value)) return 0f;
			var progress = math.saturate((value - InputMin) / (InputMax - InputMin));
			var input = Reverse ? 1f - progress : progress;
			return math.saturate(MotionState.EvaluateCurve(ResponseCurve, input));
		}

		private void ApplyPosition(float factor)
		{
			if (TranslationSpace == MotionTranslationSpace.World) {
				var parent = transform.parent;
				var baseline = parent != null ? parent.TransformPoint(_initialLocalPosition) : _initialLocalPosition;
				var desiredPosition = baseline + PositionOffset * factor;
				var desiredLocalPosition = parent != null ? parent.InverseTransformPoint(desiredPosition) : desiredPosition;
				if (math.distancesq((float3)transform.localPosition, desiredLocalPosition) > 0.000000000001f) {
					transform.localPosition = desiredLocalPosition;
				}
			} else {
				var desiredPosition = _initialLocalPosition + _initialLocalRotation * PositionOffset * factor;
				if (math.distancesq((float3)transform.localPosition, desiredPosition) > 0.000000000001f) {
					transform.localPosition = desiredPosition;
				}
			}
		}

#if UNITY_EDITOR
		protected override void OnValidate()
		{
			base.OnValidate();
			ResponseCurve = MotionControllerComponent.EnsureCurve(ResponseCurve, false);
		}
#endif
	}
}
