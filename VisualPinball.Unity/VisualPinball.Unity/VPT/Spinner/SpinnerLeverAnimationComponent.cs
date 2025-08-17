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

namespace VisualPinball.Unity
{
	public class SpinnerLeverAnimationComponent : AnimationComponent<float>
	{
		// todo make packable
		[Tooltip("The axis of the rotation.")]
		public Vector3 RotationAngle = Vector3.forward;

		[Tooltip("Shifts the lever angle in relation to the input angle by the given amount of degrees.")]
		[Range(0, 360f)]
		public float Shift = 15f;

		[Tooltip("Final offset of the lever angle in degrees.")]
		[Range(-90f, 90f)]
		public float Offset;

		[Tooltip("Start angle of the movement")]
		[Range(-180f, 180f)]
		public float MinAngle = -1.56f;

		[Tooltip("End angle of the movement")]
		[Range(-180f, 180f)]
		public float MaxAngle = 13.83f;

		private Quaternion _initialRotation;

		private void Awake()
		{
			_initialRotation = transform.localRotation;
			base.Awake();
		}

		protected override void OnAnimationValueChanged(float value)
		{
			// normalize input angle
			var angleRad = math.radians((math.degrees(value) + Shift) % 360f);

			var a = math.abs(angleRad - math.PI);
			var pos = math.sin(math.smoothstep(0, math.PI, a));
			var leverAngleDeg = math.lerp(MinAngle, MaxAngle, pos);

			var axis = RotationAngle.normalized;
			var rotation = Quaternion.AngleAxis(math.degrees(math.radians(leverAngleDeg + Offset)), axis);
			transform.localRotation = _initialRotation * rotation;
		}
	}
}
