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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Splines;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	public static class DragPointSplineConverter
	{
		private const float MinKnotDistance = 1e-4f;

		public static Spline ToSpline(IReadOnlyList<DragPointData> dragPoints, bool loop)
		{
			if (dragPoints == null) {
				throw new ArgumentNullException(nameof(dragPoints));
			}

			var positions = new float3[dragPoints.Count];
			var smooth = new bool[dragPoints.Count];
			for (var i = 0; i < dragPoints.Count; i++) {
				positions[i] = ToFloat3(dragPoints[i].Center);
				smooth[i] = dragPoints[i].IsSmooth;
			}

			var (tangentIn, tangentOut) = CalculateTangents(positions, smooth, loop);
			var spline = new Spline(dragPoints.Count, loop);
			for (var i = 0; i < dragPoints.Count; i++) {
				spline.Add(new BezierKnot(positions[i], tangentIn[i], tangentOut[i],
					quaternion.identity), TangentMode.Broken);
			}
			return spline;
		}

		public static List<DragPointMetadata> ToMetadata(IReadOnlyList<DragPointData> dragPoints)
		{
			if (dragPoints == null) {
				throw new ArgumentNullException(nameof(dragPoints));
			}

			var metadata = new List<DragPointMetadata>(dragPoints.Count);
			for (var i = 0; i < dragPoints.Count; i++) {
				metadata.Add(new DragPointMetadata(dragPoints[i]));
			}
			return metadata;
		}

		public static DragPointData[] ToDragPoints(Spline spline,
			IReadOnlyList<DragPointMetadata> metadata)
		{
			ValidateMetadata(spline, metadata);

			var dragPoints = new DragPointData[spline.Count];
			for (var i = 0; i < spline.Count; i++) {
				var position = spline[i].Position;
				var dragPoint = new DragPointData(new Vertex3D(position.x, position.y, position.z));
				metadata[i].CopyTo(dragPoint);
				dragPoints[i] = dragPoint;
			}
			return dragPoints;
		}

		public static void RecalculateTangents(Spline spline,
			IReadOnlyList<DragPointMetadata> metadata)
		{
			ValidateMetadata(spline, metadata);

			var positions = new float3[spline.Count];
			var smooth = new bool[spline.Count];
			for (var i = 0; i < spline.Count; i++) {
				positions[i] = spline[i].Position;
				smooth[i] = metadata[i].IsSmooth;
			}

			var (tangentIn, tangentOut) = CalculateTangents(positions, smooth, spline.Closed);
			for (var i = 0; i < spline.Count; i++) {
				var knot = spline[i];
				knot.TangentIn = tangentIn[i];
				knot.TangentOut = tangentOut[i];
				knot.Rotation = quaternion.identity;
				spline.SetTangentModeNoNotify(i, TangentMode.Broken);
				spline.SetKnot(i, knot);
			}
		}

		private static (float3[] tangentIn, float3[] tangentOut) CalculateTangents(
			IReadOnlyList<float3> positions, IReadOnlyList<bool> smooth, bool loop)
		{
			var tangentIn = new float3[positions.Count];
			var tangentOut = new float3[positions.Count];
			var segmentCount = loop ? positions.Count : math.max(0, positions.Count - 1);
			for (var i = 0; i < segmentCount; i++) {
				var nextIndex = i < positions.Count - 1 ? i + 1 : 0;
				var p1 = positions[i];
				var p2 = positions[nextIndex];
				if (math.all(p1 == p2)) {
					continue;
				}

				var previousIndex = smooth[i] ? i - 1 : i;
				if (previousIndex < 0) {
					previousIndex = loop ? positions.Count - 1 : 0;
				}

				var followingIndex = smooth[nextIndex] ? i + 2 : i + 1;
				if (followingIndex >= positions.Count) {
					followingIndex = loop ? followingIndex - positions.Count : positions.Count - 1;
				}

				var (startTangent, endTangent) = CalculateSegmentTangents(
					positions[previousIndex], p1, p2, positions[followingIndex]);
				tangentOut[i] = startTangent / 3f;
				tangentIn[nextIndex] = -endTangent / 3f;
			}
			return (tangentIn, tangentOut);
		}

		private static (float3 start, float3 end) CalculateSegmentTangents(float3 p0,
			float3 p1, float3 p2, float3 p3)
		{
			var dt0 = math.sqrt(math.distance(p0, p1));
			var dt1 = math.sqrt(math.distance(p1, p2));
			var dt2 = math.sqrt(math.distance(p2, p3));
			if (dt1 < MinKnotDistance) {
				dt1 = 1f;
			}
			if (dt0 < MinKnotDistance) {
				dt0 = dt1;
			}
			if (dt2 < MinKnotDistance) {
				dt2 = dt1;
			}

			var start = (p1 - p0) / dt0 - (p2 - p0) / (dt0 + dt1)
				+ (p2 - p1) / dt1;
			var end = (p2 - p1) / dt1 - (p3 - p1) / (dt1 + dt2)
				+ (p3 - p2) / dt2;
			return (start * dt1, end * dt1);
		}

		private static void ValidateMetadata(Spline spline,
			IReadOnlyList<DragPointMetadata> metadata)
		{
			if (spline == null) {
				throw new ArgumentNullException(nameof(spline));
			}
			if (metadata == null) {
				throw new ArgumentNullException(nameof(metadata));
			}
			if (spline.Count != metadata.Count) {
				throw new ArgumentException(
					$"Spline has {spline.Count} knots but metadata has {metadata.Count} entries.",
					nameof(metadata));
			}
		}

		private static float3 ToFloat3(Vertex3D vertex)
		{
			return new float3(vertex.X, vertex.Y, vertex.Z);
		}
	}
}
