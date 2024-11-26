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

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public static class TransformExtensions
	{
		public static void SetFromMatrix(this Transform tf, Matrix4x4 trs)
		{
			tf.localScale = new Vector3(
				trs.GetColumn(0).magnitude,
				trs.GetColumn(1).magnitude,
				trs.GetColumn(2).magnitude
			);
			tf.localPosition = trs.GetColumn(3);
			tf.localRotation = Quaternion.LookRotation(
				trs.GetColumn(2),
				trs.GetColumn(1)
			);
		}

		public static void SetLocalYRotation(this Transform transform, float angleRad)
		{
			var localToWorldMatrix = transform.localToWorldMatrix;
			var localRotationY = transform.localRotation.eulerAngles.y;
			var inverseYRotation = math.inverse(float4x4.RotateY(math.radians(localRotationY)));

			var localToWorldPhysicsMatrix = math.mul(localToWorldMatrix, inverseYRotation);
			var rotatedMatrix = math.mul(localToWorldPhysicsMatrix, float4x4.RotateY(angleRad));

			var newUp = localToWorldMatrix.MultiplyVector(Vector3.up);
			var newForward = rotatedMatrix.c2.xyz; // Extract forward direction from the matrix

			transform.rotation = Quaternion.LookRotation(newForward, newUp);
		}

		public static void SetLocalXRotation(this Transform transform, float angleRad)
		{
			var localToWorldMatrix = transform.localToWorldMatrix;

			// Get the current local X rotation and calculate its inverse
			var localRotationX = transform.localRotation.eulerAngles.x;
			var inverseXRotation = math.inverse(float4x4.RotateX(math.radians(localRotationX)));

			// Remove the current X rotation
			var localToWorldPhysicsMatrix = math.mul(localToWorldMatrix, inverseXRotation);

			// Apply the new X rotation
			var rotatedMatrix = math.mul(localToWorldPhysicsMatrix, float4x4.RotateX(angleRad));

			// Extract the updated forward and up directions from the rotated matrix
			var newForward = rotatedMatrix.c2.xyz; // Correct forward vector after rotation
			var newUp = rotatedMatrix.c1.xyz;      // Correct up vector after rotation

			// Set the object's rotation using the new forward and up directions
			transform.rotation = Quaternion.LookRotation(newForward, newUp);
		}
	}
}
