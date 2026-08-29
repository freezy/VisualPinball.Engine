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
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace VisualPinball.Unity
{
	internal readonly struct WireRailPathFrame
	{
		public readonly float3 Position;
		public readonly float3 Tangent;
		public readonly float3 Right;
		public readonly float3 Up;

		public WireRailPathFrame(float3 position, float3 tangent, float3 right, float3 up)
		{
			Position = position;
			Tangent = tangent;
			Right = right;
			Up = up;
		}

		public float3 TransformOffset(float2 offset)
			=> Position + Right * offset.x + Up * offset.y;
	}

	internal static class WireRailSplineGeometry
	{
		public static bool TryEvaluate(Spline spline, int segmentIndex, float curveT,
			out WireRailPathFrame frame)
		{
			frame = default;
			if (spline == null || spline.Count < 2) {
				return false;
			}
			var knotT = segmentIndex + math.saturate(curveT);
			var normalizedT = SplineUtility.GetNormalizedInterpolation(spline, knotT,
				PathIndexUnit.Knot);
			if (!spline.Evaluate(normalizedT, out var position, out var tangent, out var up)) {
				return false;
			}

			tangent = math.normalizesafe(tangent, new float3(0f, 1f, 0f));
			up -= tangent * math.dot(up, tangent);
			up = math.normalizesafe(up, new float3(0f, 0f, 1f));
			var right = math.normalizesafe(math.cross(tangent, up), new float3(1f, 0f, 0f));
			up = math.normalizesafe(math.cross(right, tangent), new float3(0f, 0f, 1f));
			frame = new WireRailPathFrame(position, tangent, right, up);
			return true;
		}
	}

	internal static class WireRailRenderMeshGenerator
	{
		public static Mesh Generate(Spline spline, IReadOnlyList<WireRailSegment> segments,
			int samplesPerSegment, int radialSegments, Mesh target)
		{
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var uvs = new List<Vector2>();
			var indices = new List<int>();
			for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
				var segment = segments[segmentIndex];
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					var wireRadius = segment.GetWireDiameter(railIndex) * 0.5f;
					AppendTube(spline, segments, segmentIndex, railIndex, wireRadius,
						samplesPerSegment, radialSegments, vertices, normals, uvs, indices);
				}
			}

			var mesh = target ? target : new Mesh();
			mesh.Clear(false);
			mesh.name = "Wire Rail Render (Generated)";
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(indices, 0, false);
			mesh.RecalculateBounds();
			return mesh;
		}

		private static void AppendTube(Spline spline, IReadOnlyList<WireRailSegment> segments,
			int segmentIndex, int railIndex, float radius, int samplesPerSegment,
			int radialSegments, ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var offset = (float2)segments[segmentIndex].GetRailOffset(railIndex);
			var firstRing = vertices.Count;
			WireRailPathFrame firstFrame = default;
			WireRailPathFrame lastFrame = default;
			for (var sampleIndex = 0; sampleIndex <= samplesPerSegment; sampleIndex++) {
				var curveT = sampleIndex / (float)samplesPerSegment;
				if (!WireRailSplineGeometry.TryEvaluate(spline, segmentIndex, curveT, out var frame)) {
					continue;
				}
				if (sampleIndex == 0) {
					firstFrame = frame;
				}
				lastFrame = frame;
				var center = frame.TransformOffset(offset);
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var angle = math.PI * 2f * radialIndex / radialSegments;
					var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
					vertices.Add((Vector3)(center + radial * radius));
					normals.Add((Vector3)radial);
					uvs.Add(new Vector2(curveT, radialIndex / (float)radialSegments));
				}
			}

			for (var sampleIndex = 0; sampleIndex < samplesPerSegment; sampleIndex++) {
				var current = firstRing + sampleIndex * radialSegments;
				var next = current + radialSegments;
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var radialNext = (radialIndex + 1) % radialSegments;
					var a = current + radialIndex;
					var b = next + radialIndex;
					var c = current + radialNext;
					var d = next + radialNext;
					indices.Add(a);
					indices.Add(b);
					indices.Add(d);
					indices.Add(a);
					indices.Add(d);
					indices.Add(c);
				}
			}

			if (!HasMatchingRail(spline, segments, segmentIndex, segmentIndex - 1, railIndex)) {
				AppendCap(firstFrame, offset, radius, radialSegments, true,
					vertices, normals, uvs, indices);
			}
			if (!HasMatchingRail(spline, segments, segmentIndex, segmentIndex + 1, railIndex)) {
				AppendCap(lastFrame, offset, radius, radialSegments, false,
					vertices, normals, uvs, indices);
			}
		}

		private static bool HasMatchingRail(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int neighborIndex,
			int railIndex)
		{
			if (spline.Closed) {
				neighborIndex = (neighborIndex + segments.Count) % segments.Count;
			} else if (neighborIndex < 0 || neighborIndex >= segments.Count) {
				return false;
			}
			var neighbor = segments[neighborIndex];
			if (neighbor.RailCount <= railIndex) {
				return false;
			}
			var offset = (float2)segments[segmentIndex].GetRailOffset(railIndex);
			var neighborOffset = (float2)neighbor.GetRailOffset(railIndex);
			var diameter = segments[segmentIndex].GetWireDiameter(railIndex);
			var neighborDiameter = neighbor.GetWireDiameter(railIndex);
			return math.distancesq(offset, neighborOffset) <= 1e-6f
				&& math.abs(diameter - neighborDiameter) <= 1e-4f;
		}

		private static void AppendCap(WireRailPathFrame frame, float2 offset, float radius,
			int radialSegments, bool start, ICollection<Vector3> vertices,
			ICollection<Vector3> normals, ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var center = frame.TransformOffset(offset);
			var normal = start ? -frame.Tangent : frame.Tangent;
			var centerIndex = vertices.Count;
			vertices.Add((Vector3)center);
			normals.Add((Vector3)normal);
			uvs.Add(new Vector2(0.5f, 0.5f));
			var ringStart = vertices.Count;
			for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
				var angle = math.PI * 2f * radialIndex / radialSegments;
				var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
				vertices.Add((Vector3)(center + radial * radius));
				normals.Add((Vector3)normal);
				uvs.Add(new Vector2(math.cos(angle) * 0.5f + 0.5f,
					math.sin(angle) * 0.5f + 0.5f));
			}
			for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
				var next = (radialIndex + 1) % radialSegments;
				indices.Add(centerIndex);
				indices.Add(ringStart + (start ? radialIndex : next));
				indices.Add(ringStart + (start ? next : radialIndex));
			}
		}
	}

	internal readonly struct WireRailProfileSpan
	{
		public readonly int StartVertex;
		public readonly int EndVertex;

		public WireRailProfileSpan(int startVertex, int endVertex)
		{
			StartVertex = startVertex;
			EndVertex = endVertex;
		}
	}

	internal sealed class WireRailChannelProfile
	{
		private const int MaximumFacetCount = 8;
		private const float FullTurn = math.PI * 2f;
		private const float TopInwardNormalAngle = math.PI * 1.5f;

		public readonly List<float2> Vertices = new();
		public readonly List<WireRailProfileSpan> Spans = new();
		public float2 RestingBallCenter { get; private set; }
		public bool IsClosed { get; private set; }

		public static bool TryCreate(IReadOnlyList<Vector2> offsets, float wireRadius,
			float ballRadius, out WireRailChannelProfile profile, out string error)
		{
			var radii = offsets == null
				? Array.Empty<float>()
				: Enumerable.Repeat(wireRadius, offsets.Count).ToArray();
			return TryCreate(offsets, radii, ballRadius, out profile, out error);
		}

		public static bool TryCreate(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius,
			out WireRailChannelProfile profile, out string error)
		{
			profile = null;
			error = null;
			if (offsets == null || offsets.Count == 0) {
				error = "A collision channel needs at least one rail.";
				return false;
			}
			if (wireRadii == null || wireRadii.Count != offsets.Count) {
				error = "Every collision rail needs a matching wire radius.";
				return false;
			}
			if (wireRadii.Any(radius => radius <= 0f) || ballRadius <= 0f) {
				error = "Wire and reference-ball radii must be positive.";
				return false;
			}

			if (!TryGetRestingBallCenter(offsets, wireRadii, ballRadius,
					out var ballCenter, out error)) {
				return false;
			}
			var collisionIndices = SelectCollisionIndices(offsets.Count);
			var supportLines = new List<FacetLine>(collisionIndices.Count);
			foreach (var railIndex in collisionIndices) {
				var offset = (float2)offsets[railIndex];
				var normal = math.normalizesafe(ballCenter - offset);
				if (math.lengthsq(normal) < 0.5f) {
					error = "A rail lies on the reference ball center and has no contact direction.";
					return false;
				}
				supportLines.Add(new FacetLine(offset + normal * wireRadii[railIndex], normal,
					NormalizeAngle(math.atan2(normal.y, normal.x))));
			}
			supportLines.Sort((first, second) => first.Angle.CompareTo(second.Angle));

			var closed = offsets.Count >= 5;
			var ordered = closed ? supportLines : OrderAroundTopOpening(supportLines);
			var lines = AddChamfers(ordered, ballCenter, ballRadius, closed);
			if (!TryBuildProfile(lines, ballCenter, ballRadius, closed,
					out profile, out error)) {
				return false;
			}
			return true;
		}

		private static List<int> SelectCollisionIndices(int railCount)
		{
			if (railCount <= MaximumFacetCount) {
				return Enumerable.Range(0, railCount).ToList();
			}
			var selected = Enumerable.Range(0, 4).ToList();
			var topCount = railCount - 4;
			var remaining = MaximumFacetCount - selected.Count;
			for (var i = 0; i < remaining; i++) {
				var topIndex = remaining == 1
					? topCount / 2
					: (int)math.round(i * (topCount - 1f) / (remaining - 1f));
				selected.Add(4 + topIndex);
			}
			return selected;
		}

		private static bool TryGetRestingBallCenter(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius,
			out float2 center, out string error)
		{
			error = null;
			if (offsets.Count == 1) {
				center = (float2)offsets[0]
					+ new float2(0f, wireRadii[0] + ballRadius);
				return true;
			}

			var first = (float2)offsets[0];
			var second = (float2)offsets[1];
			var delta = second - first;
			var separation = math.length(delta);
			if (separation <= 1e-5f) {
				center = default;
				error = "The two bottom rails have coincident centers.";
				return false;
			}
			var firstRadius = wireRadii[0] + ballRadius;
			var secondRadius = wireRadii[1] + ballRadius;
			if (separation > firstRadius + secondRadius
				|| separation < math.abs(firstRadius - secondRadius)) {
				center = default;
				error = "The reference ball cannot contact both bottom rails.";
				return false;
			}
			var distanceAlong = (firstRadius * firstRadius - secondRadius * secondRadius
				+ separation * separation) / (2f * separation);
			var midpoint = first + delta * (distanceAlong / separation);
			var perpendicular = math.normalize(new float2(-delta.y, delta.x));
			var height = math.sqrt(math.max(0f,
				firstRadius * firstRadius - distanceAlong * distanceAlong));
			var firstCandidate = midpoint + perpendicular * height;
			var secondCandidate = midpoint - perpendicular * height;
			center = firstCandidate.y >= secondCandidate.y ? firstCandidate : secondCandidate;
			return true;
		}

		private static List<FacetLine> OrderAroundTopOpening(IReadOnlyList<FacetLine> sorted)
		{
			if (sorted.Count <= 1) {
				return sorted.ToList();
			}
			var gapIndex = sorted.Count - 1;
			for (var i = 0; i < sorted.Count; i++) {
				var start = sorted[i].Angle;
				var end = sorted[(i + 1) % sorted.Count].Angle;
				if (i == sorted.Count - 1) {
					end += FullTurn;
				}
				var target = TopInwardNormalAngle;
				if (target < start) {
					target += FullTurn;
				}
				if (target >= start && target <= end) {
					gapIndex = i;
					break;
				}
			}

			var result = new List<FacetLine>(sorted.Count);
			var previousAngle = float.NegativeInfinity;
			for (var ordinal = 1; ordinal <= sorted.Count; ordinal++) {
				var source = sorted[(gapIndex + ordinal) % sorted.Count];
				var angle = source.Angle;
				while (angle <= previousAngle) {
					angle += FullTurn;
				}
				result.Add(new FacetLine(source.Point, source.Normal, angle));
				previousAngle = angle;
			}
			return result;
		}

		private static List<FacetLine> AddChamfers(IReadOnlyList<FacetLine> supports,
			float2 ballCenter, float ballRadius, bool closed)
		{
			var gapCount = closed ? supports.Count : math.max(0, supports.Count - 1);
			var chamferCount = math.min(math.max(0, MaximumFacetCount - supports.Count),
				gapCount);
			var gaps = new List<Gap>(gapCount);
			for (var i = 0; i < gapCount; i++) {
				var next = (i + 1) % supports.Count;
				var endAngle = supports[next].Angle;
				if (next == 0) {
					endAngle += FullTurn;
				}
				gaps.Add(new Gap(i, endAngle - supports[i].Angle));
			}
			var chamferGaps = gaps.OrderByDescending(gap => gap.Angle)
				.ThenBy(gap => gap.Index).Take(chamferCount)
				.Select(gap => gap.Index).ToHashSet();

			var result = new List<FacetLine>(supports.Count + chamferCount);
			for (var i = 0; i < supports.Count; i++) {
				result.Add(supports[i]);
				if (!chamferGaps.Contains(i)) {
					continue;
				}
				var next = supports[(i + 1) % supports.Count];
				var normal = math.normalizesafe(supports[i].Normal + next.Normal,
					supports[i].Normal);
				var angle = (supports[i].Angle + (next.Angle <= supports[i].Angle
					? next.Angle + FullTurn : next.Angle)) * 0.5f;
				result.Add(new FacetLine(ballCenter - normal * ballRadius, normal, angle));
			}
			return result;
		}

		private static bool TryBuildProfile(IReadOnlyList<FacetLine> lines,
			float2 ballCenter, float ballRadius, bool closed,
			out WireRailChannelProfile profile, out string error)
		{
			profile = new WireRailChannelProfile {
				RestingBallCenter = ballCenter,
				IsClosed = closed,
			};
			error = null;
			if (lines.Count == 1) {
				var tangent = Perpendicular(lines[0].Normal);
				var extension = math.max(5f, ballRadius * 0.2f);
				profile.Vertices.Add(lines[0].Point - tangent * extension);
				profile.Vertices.Add(lines[0].Point + tangent * extension);
				profile.Spans.Add(new WireRailProfileSpan(0, 1));
				return true;
			}

			if (closed) {
				for (var i = 0; i < lines.Count; i++) {
					if (!TryIntersect(lines[(i - 1 + lines.Count) % lines.Count], lines[i],
							out var vertex)) {
						error = "The generated collision profile contains parallel neighboring facets.";
						profile = null;
						return false;
					}
					profile.Vertices.Add(vertex);
				}
				for (var i = 0; i < lines.Count; i++) {
					profile.Spans.Add(new WireRailProfileSpan(i, (i + 1) % lines.Count));
				}
				return true;
			}

			var intersections = new List<float2>(lines.Count - 1);
			for (var i = 1; i < lines.Count; i++) {
				if (!TryIntersect(lines[i - 1], lines[i], out var intersection)) {
					error = "The generated collision profile contains parallel neighboring facets.";
					profile = null;
					return false;
				}
				intersections.Add(intersection);
			}
			var rimExtension = math.max(5f, ballRadius * 0.2f);
			var firstDirection = math.normalizesafe(intersections[0] - lines[0].Point,
				Perpendicular(lines[0].Normal));
			profile.Vertices.Add(lines[0].Point - firstDirection * rimExtension);
			profile.Vertices.AddRange(intersections);
			var lastDirection = math.normalizesafe(lines[^1].Point - intersections[^1],
				Perpendicular(lines[^1].Normal));
			profile.Vertices.Add(lines[^1].Point + lastDirection * rimExtension);
			for (var i = 0; i < lines.Count; i++) {
				profile.Spans.Add(new WireRailProfileSpan(i, i + 1));
			}
			return true;
		}

		private static bool TryIntersect(FacetLine first, FacetLine second,
			out float2 intersection)
		{
			var determinant = Cross(first.Normal, second.Normal);
			if (math.abs(determinant) < 1e-5f) {
				intersection = default;
				return false;
			}
			var firstDistance = math.dot(first.Normal, first.Point);
			var secondDistance = math.dot(second.Normal, second.Point);
			intersection = new float2(
				(firstDistance * second.Normal.y - first.Normal.y * secondDistance) / determinant,
				(first.Normal.x * secondDistance - firstDistance * second.Normal.x) / determinant);
			return math.all(math.isfinite(intersection));
		}

		private static float NormalizeAngle(float angle)
		{
			while (angle < 0f) {
				angle += FullTurn;
			}
			return angle;
		}

		private static float2 Perpendicular(float2 value) => new(-value.y, value.x);
		private static float Cross(float2 first, float2 second)
			=> first.x * second.y - first.y * second.x;

		private readonly struct FacetLine
		{
			public readonly float2 Point;
			public readonly float2 Normal;
			public readonly float Angle;

			public FacetLine(float2 point, float2 normal, float angle)
			{
				Point = point;
				Normal = normal;
				Angle = angle;
			}
		}

		private readonly struct Gap
		{
			public readonly int Index;
			public readonly float Angle;

			public Gap(int index, float angle)
			{
				Index = index;
				Angle = angle;
			}
		}
	}

	internal static class WireRailColliderMeshGenerator
	{
		public static bool TryGenerate(Spline spline, IReadOnlyList<WireRailSegment> segments,
			float ballDiameter, int samplesPerSegment, Mesh target,
			out Mesh mesh, out Vector3[] edgeVertices, out string error)
		{
			var vertices = new List<Vector3>();
			var indices = new List<int>();
			var edges = new List<Vector3>();
			var ballRadius = ballDiameter * 0.5f;

			for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
				var segment = segments[segmentIndex];
				var wireRadii = new float[segment.RailCount];
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					wireRadii[railIndex] = segment.GetWireDiameter(railIndex) * 0.5f;
				}
				if (!WireRailChannelProfile.TryCreate(segment.RailOffsets, wireRadii, ballRadius,
						out var profile, out error)) {
					mesh = target;
					edgeVertices = Array.Empty<Vector3>();
					return false;
				}
				if (!AppendSegment(spline, segmentIndex, profile, samplesPerSegment,
						vertices, indices, edges)) {
					mesh = target;
					edgeVertices = Array.Empty<Vector3>();
					error = $"Could not evaluate spline segment {segmentIndex + 1}.";
					return false;
				}
			}

			mesh = target ? target : new Mesh();
			mesh.Clear(false);
			mesh.name = "Wire Rail Collider (Generated)";
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetTriangles(indices, 0, false);
			mesh.RecalculateBounds();
			edgeVertices = edges.ToArray();
			error = null;
			return true;
		}

		private static bool AppendSegment(Spline spline, int segmentIndex,
			WireRailChannelProfile profile, int samplesPerSegment,
			ICollection<Vector3> vertices, ICollection<int> indices,
			ICollection<Vector3> edges)
		{
			var firstRow = vertices.Count;
			for (var sampleIndex = 0; sampleIndex <= samplesPerSegment; sampleIndex++) {
				var curveT = sampleIndex / (float)samplesPerSegment;
				if (!WireRailSplineGeometry.TryEvaluate(spline, segmentIndex, curveT,
						out var frame)) {
					return false;
				}
				foreach (var offset in profile.Vertices) {
					vertices.Add((Vector3)frame.TransformOffset(offset));
				}
			}

			var rowSize = profile.Vertices.Count;
			for (var sampleIndex = 0; sampleIndex < samplesPerSegment; sampleIndex++) {
				var currentRow = firstRow + sampleIndex * rowSize;
				var nextRow = currentRow + rowSize;
				foreach (var span in profile.Spans) {
					AppendTwoSidedQuad(currentRow + span.StartVertex,
						nextRow + span.StartVertex, currentRow + span.EndVertex,
						nextRow + span.EndVertex, indices);
				}
				for (var profileVertex = 0; profileVertex < rowSize; profileVertex++) {
					edges.Add(GetVertex(vertices, currentRow + profileVertex));
					edges.Add(GetVertex(vertices, nextRow + profileVertex));
				}
			}
			return true;
		}

		private static Vector3 GetVertex(ICollection<Vector3> vertices, int index)
		{
			if (vertices is List<Vector3> list) {
				return list[index];
			}
			return vertices.ElementAt(index);
		}

		private static void AppendTwoSidedQuad(int a, int b, int c, int d,
			ICollection<int> indices)
		{
			indices.Add(a);
			indices.Add(b);
			indices.Add(c);
			indices.Add(b);
			indices.Add(d);
			indices.Add(c);
			indices.Add(a);
			indices.Add(c);
			indices.Add(b);
			indices.Add(b);
			indices.Add(c);
			indices.Add(d);
		}
	}
}
