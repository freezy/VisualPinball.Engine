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

namespace VisualPinball.Unity
{
	internal readonly struct DropTargetDeflectionData
	{
		internal readonly float3 VelocityJacobian;
		internal readonly float InverseGeneralizedMass;
		internal readonly float3 Axis;
		internal readonly float3 Pivot;

		internal DropTargetDeflectionData(in float3 velocityJacobian,
			float inverseGeneralizedMass, in float3 axis, in float3 pivot)
		{
			VelocityJacobian = velocityJacobian;
			InverseGeneralizedMass = inverseGeneralizedMass;
			Axis = axis;
			Pivot = pivot;
		}
	}

	internal static class DropTargetDeflectionPhysics
	{
		internal static DropTargetDeflectionData AtPoint(in DropTargetStaticState staticState,
			in DropTargetMechanicalState mechanical, in float3 point)
		{
			var config = staticState.Mechanical;
			var rearDirection = -math.normalizesafe(staticState.FaceNormal);
			if (config.DeflectionKind != DropTargetDeflectionKind.HingedBlade) {
				var invMass = config.EffectiveFaceMass > 0f ? 1f / config.EffectiveFaceMass : 0f;
				return new DropTargetDeflectionData(rearDirection, invMass, float3.zero,
					float3.zero);
			}

			var baseTransform = mechanical.PoseInitialized
				? mechanical.BaseTransform
				: float4x4.identity;
			var localAxis = new float3(config.DeflectionAxis.x, config.DeflectionAxis.y,
				config.DeflectionAxis.z);
			var localPivot = new float3(config.DeflectionPivot.x, config.DeflectionPivot.y,
				config.DeflectionPivot.z);
			var localReference = new float3(config.ReferenceContactPoint.x,
				config.ReferenceContactPoint.y, config.ReferenceContactPoint.z);
			var axis = math.normalizesafe(baseTransform.MultiplyVector(localAxis));
			var pivot = math.transform(baseTransform, localPivot);
			var referencePoint = math.transform(baseTransform, localReference);
			var referenceJacobian = math.cross(axis, referencePoint - pivot);
			if (math.dot(referenceJacobian, rearDirection) < 0f) {
				axis = -axis;
				referenceJacobian = -referenceJacobian;
			}
			var referenceLeverSq = math.lengthsq(referenceJacobian);
			var inertia = config.EffectiveFaceMass * referenceLeverSq;
			var invInertia = inertia > 1e-6f ? 1f / inertia : 0f;
			var velocityJacobian = math.cross(axis, point - pivot);
			return new DropTargetDeflectionData(in velocityJacobian, invInertia, in axis, in pivot);
		}

		internal static float3 SurfaceVelocityAtPoint(in DropTargetStaticState staticState,
			in DropTargetMechanicalState mechanical, in float3 point)
		{
			var deflection = AtPoint(in staticState, in mechanical, in point);
			return deflection.VelocityJacobian * mechanical.QDot
				+ new float3(0f, 0f, -1f) * mechanical.DDot;
		}
	}
}
