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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	public class SpinnerLeverAnimationComponent : AnimationComponent<SpinnerData, SpinnerComponent>, IRotatableAnimationComponent
	{
		[Tooltip("Shifts the lever angle by the given amount of degrees.")]
		[Range(-90f, 90f)]
		public float Shift = 15f;

		[Tooltip("Start angle of the movement")]
		[Range(-90f, 90f)]
		public float MinAngle = -1.56f;

		[Tooltip("End angle of the movement")]
		[Range(-90f, 90f)]
		public float MaxAngle = 13.83f;

		public void OnRotationUpdated(float angleRad)
		{
			angleRad = math.radians((math.degrees(angleRad) + Shift) % 360f);
			var a = math.abs(angleRad - math.PI);
			var pos = math.sin(math.smoothstep(0, math.PI, a));
			var leverAngleDeg = math.lerp(MinAngle, MaxAngle, pos);
			transform.localRotation = quaternion.RotateX(math.radians(leverAngleDeg));
		}
	}
}
