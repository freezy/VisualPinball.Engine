// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	internal static class SpringHingeColliderGenerator
	{
		private const float MillimetersToWorld = 0.001f;
		private const float OrthogonalityTolerance = 1e-4f;

		internal static SpringHingeCollider Create(SpringHingeComponent hinge,
			SpringHingeColliderComponent collider, ColliderInfo info, float margin)
		{
			var referenceMatrix = hinge.ReferenceLocalToWorldMatrix;
			var pivot = hinge.ToPlayfieldVpx(referenceMatrix.MultiplyPoint3x4(Vector3.zero));
			var centre = hinge.ToPlayfieldVpx(referenceMatrix.MultiplyPoint3x4(
				collider.LocalCentre * MillimetersToWorld));
			var localRotation = Quaternion.Euler(collider.LocalRotation);
			var halfAxisX = hinge.ToPlayfieldVector(localRotation
				* (Vector3.right * (collider.HalfExtents.x * MillimetersToWorld)));
			var halfAxisY = hinge.ToPlayfieldVector(localRotation
				* (Vector3.up * (collider.HalfExtents.y * MillimetersToWorld)));
			var halfAxisZ = hinge.ToPlayfieldVector(localRotation
				* (Vector3.forward * (collider.HalfExtents.z * MillimetersToWorld)));
			var lengthX = math.length(halfAxisX);
			var lengthY = math.length(halfAxisY);
			var lengthZ = math.length(halfAxisZ);
			if (lengthX <= math.EPSILON || lengthY <= math.EPSILON || lengthZ <= math.EPSILON) {
				throw new InvalidOperationException(
					$"Spring hinge '{hinge.name}' collider has a degenerate transform or extent.");
			}
			var axisX = math.normalizesafe(halfAxisX, hinge.ToPlayfieldDirection(localRotation * Vector3.right));
			var axisY = math.normalizesafe(halfAxisY, hinge.ToPlayfieldDirection(localRotation * Vector3.up));
			var axisZ = math.normalizesafe(halfAxisZ, hinge.ToPlayfieldDirection(localRotation * Vector3.forward));
			if (math.abs(math.dot(axisX, axisY)) > OrthogonalityTolerance
			    || math.abs(math.dot(axisX, axisZ)) > OrthogonalityTolerance
			    || math.abs(math.dot(axisY, axisZ)) > OrthogonalityTolerance) {
				throw new InvalidOperationException(
					$"Spring hinge '{hinge.name}' collider transform is sheared. Use an orthogonal transform for the analytic box proxy.");
			}
			var halfExtents = new float3(lengthX, lengthY, lengthZ)
				+ math.max(0f, margin);
			return new SpringHingeCollider(hinge.ItemId, in pivot, centre - pivot, in halfExtents,
				in axisX, in axisY, in axisZ, info);
		}
	}
}
