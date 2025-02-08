﻿// Visual Pinball Engine
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

using System;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public static class TransformExtensions
	{
		private const string NodeSeparator = ".";

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

		public static void SetZPosition(this Transform transform, float pos)
		{
			var position = transform.position;
			position.y = Physics.ScaleToWorld(pos); // we're in z here
			transform.position = position;
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

		public static string GetPath(this Transform transform, Transform root = null, string path = "")
		{
			var name = $"{transform.GetSiblingIndex()}";
			if (transform == root || transform.parent == null) {
				var suffix = string.IsNullOrEmpty(path) ? "" : NodeSeparator;
				return $"0{suffix}{path}";
			}
			return $"{transform.parent.GetPath(root, path)}{NodeSeparator}{name}";
		}

		public static Transform FindByPath(this Transform transform, string path)
		{
			if (path == "0") {
				return transform;
			}
			var indexOfSeparator = path.IndexOf(NodeSeparator[0]);
			var firstIndex = path[..indexOfSeparator];
			if (int.TryParse(firstIndex, out var index)) {
				return transform.GetChild(index).FindByPath(path[(indexOfSeparator + 1)..]);
			}
			throw new InvalidOperationException($"Cannot parse index {firstIndex}.");
		}
	}
}
