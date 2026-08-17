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
	[DisallowMultipleComponent]
	[PackAs("ActuatorTransform")]
	[AddComponentMenu("Pinball/Animation/Actuator Transform")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/actuators.html")]
	public class ActuatorTransformComponent : AnimationComponent<float>, IPackable
	{
		[Tooltip("Translate this transform from its authored local position.")]
		public bool AnimatePosition = true;

		[Tooltip("Local position offset at actuator position 1.")]
		public Vector3 PositionOffset;

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
			if (AnimatePosition) {
				transform.localPosition = _initialLocalPosition + PositionOffset * factor;
			}
			if (AnimateRotation) {
				var endRotation = _initialLocalRotation * Quaternion.Euler(RotationOffset);
				transform.localRotation = Quaternion.SlerpUnclamped(_initialLocalRotation, endRotation, factor);
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
