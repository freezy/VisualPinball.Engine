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

namespace VisualPinball.Unity
{
	public enum ActuatorTranslationSpace
	{
		World,
		Local,
		// Append new values only; packaged enum values are serialized numerically.
	}

	[DisallowMultipleComponent]
	[PackAs("ActuatorTransform")]
	[AddComponentMenu("Pinball/Animation/Actuator Transform")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/actuators.html")]
	public class ActuatorTransformComponent : AnimationComponent<float>, IPackable
	{
		[Tooltip("Translate this transform from its authored local position.")]
		public bool AnimatePosition = true;

		[Tooltip("Position offset at actuator position 1, expressed in Translation Space.")]
		public Vector3 PositionOffset;

		[Tooltip("Whether Position Offset follows world axes or the follower's authored Local gizmo axes.")]
		public ActuatorTranslationSpace TranslationSpace = ActuatorTranslationSpace.World;

		[Tooltip("Rotate this transform from its authored local rotation.")]
		public bool AnimateRotation;

		[Unit("degrees")]
		[Tooltip("Local Euler rotation offset at actuator position 1.")]
		public Vector3 RotationOffset;

		[Tooltip("Maps the actuator's normalized position to this transform's normalized travel.")]
		public AnimationCurve ResponseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		[Tooltip("Reverse the actuator value before applying the response curve.")]
		public bool Reverse;

		private Vector3 _initialLocalPosition;
		private Quaternion _initialLocalRotation;
		private float _currentFactor;
		private bool _poseCaptured;

		public byte[] Pack() => ActuatorTransformPackable.Pack(this);
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => ActuatorTransformReferencesPackable.Pack(this, refs);
		public void Unpack(byte[] bytes) => ActuatorTransformPackable.Unpack(bytes, this);
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) => ActuatorTransformReferencesPackable.Unpack(bytes, this, refs);

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
			if (_poseCaptured && AnimatePosition && TranslationSpace == ActuatorTranslationSpace.World) {
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

			var input = Reverse ? 1f - math.saturate(value) : math.saturate(value);
			var factor = math.saturate(ActuatorMotionState.EvaluateCurve(ResponseCurve, input));
			_currentFactor = factor;
			if (AnimatePosition) {
				ApplyPosition(factor);
			}
			if (AnimateRotation) {
				var endRotation = _initialLocalRotation * Quaternion.Euler(RotationOffset);
				transform.localRotation = Quaternion.SlerpUnclamped(_initialLocalRotation, endRotation, factor);
			}
		}

		private void ApplyPosition(float factor)
		{
			if (TranslationSpace == ActuatorTranslationSpace.World) {
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
			ResponseCurve = ActuatorComponent.EnsureCurve(ResponseCurve, false);
		}
#endif
	}
}
