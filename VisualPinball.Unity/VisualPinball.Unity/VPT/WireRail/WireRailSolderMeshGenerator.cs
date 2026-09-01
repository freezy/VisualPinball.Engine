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
using UnityEngine;
using UnityEngine.Splines;

namespace VisualPinball.Unity
{
	internal readonly struct WireRailTouch
	{
		public readonly float3 FirstPoint;
		public readonly float3 SecondPoint;
		public readonly float3 FirstTangent;
		public readonly float3 SecondTangent;
		public readonly float FirstRadius;
		public readonly float SecondRadius;
		public readonly float SurfaceDistance;

		public WireRailTouch(float3 firstPoint, float3 secondPoint,
			float3 firstTangent, float3 secondTangent, float firstRadius,
			float secondRadius, float surfaceDistance)
		{
			FirstPoint = firstPoint;
			SecondPoint = secondPoint;
			FirstTangent = firstTangent;
			SecondTangent = secondTangent;
			FirstRadius = firstRadius;
			SecondRadius = secondRadius;
			SurfaceDistance = surfaceDistance;
		}

		public float3 Position => (FirstPoint + SecondPoint) * 0.5f;
	}

	internal static class WireRailWireTouchDetector
	{
		private const float DistanceEpsilon = 1e-4f;

		internal static bool TryFindTouch(float3 firstStart, float3 firstEnd,
			float firstRadius, float3 secondStart, float3 secondEnd,
			float secondRadius, float threshold, out WireRailTouch touch)
		{
			ClosestPointsBetweenSegments(firstStart, firstEnd, secondStart, secondEnd,
				out var firstPoint, out var secondPoint);
			var surfaceDistance = math.distance(firstPoint, secondPoint)
				- math.max(0f, firstRadius) - math.max(0f, secondRadius);
			if (surfaceDistance > math.max(0f, threshold) + DistanceEpsilon) {
				touch = default;
				return false;
			}
			touch = new WireRailTouch(firstPoint, secondPoint,
				math.normalizesafe(firstEnd - firstStart, new float3(0f, 1f, 0f)),
				math.normalizesafe(secondEnd - secondStart, new float3(1f, 0f, 0f)),
				math.max(0f, firstRadius), math.max(0f, secondRadius), surfaceDistance);
			return true;
		}

		internal static bool TryFindTouch(float3 firstStart, float3 firstEnd,
			float firstRadius, IReadOnlyList<float3> secondPoints, float secondRadius,
			float threshold, out WireRailTouch touch)
		{
			touch = default;
			if (secondPoints == null || secondPoints.Count < 2) {
				return false;
			}
			var found = false;
			for (var pointIndex = 0; pointIndex < secondPoints.Count - 1; pointIndex++) {
				if (!TryFindTouch(firstStart, firstEnd, firstRadius,
						secondPoints[pointIndex], secondPoints[pointIndex + 1],
						secondRadius, threshold, out var candidate)
					|| found && candidate.SurfaceDistance >= touch.SurfaceDistance) {
					continue;
				}
				touch = candidate;
				found = true;
			}
			return found;
		}

		private static void ClosestPointsBetweenSegments(float3 firstStart,
			float3 firstEnd, float3 secondStart, float3 secondEnd,
			out float3 firstPoint, out float3 secondPoint)
		{
			var firstDirection = firstEnd - firstStart;
			var secondDirection = secondEnd - secondStart;
			var delta = firstStart - secondStart;
			var firstLengthSquared = math.lengthsq(firstDirection);
			var secondLengthSquared = math.lengthsq(secondDirection);
			var secondProjection = math.dot(secondDirection, delta);
			float firstT;
			float secondT;
			if (firstLengthSquared <= 1e-10f && secondLengthSquared <= 1e-10f) {
				firstPoint = firstStart;
				secondPoint = secondStart;
				return;
			}
			if (firstLengthSquared <= 1e-10f) {
				firstT = 0f;
				secondT = math.saturate(secondProjection / secondLengthSquared);
			} else {
				var firstProjection = math.dot(firstDirection, delta);
				if (secondLengthSquared <= 1e-10f) {
					secondT = 0f;
					firstT = math.saturate(-firstProjection / firstLengthSquared);
				} else {
					var directionsDot = math.dot(firstDirection, secondDirection);
					var denominator = firstLengthSquared * secondLengthSquared
						- directionsDot * directionsDot;
					firstT = denominator > 1e-10f
						? math.saturate((directionsDot * secondProjection
							- firstProjection * secondLengthSquared) / denominator)
						: 0f;
					secondT = (directionsDot * firstT + secondProjection)
						/ secondLengthSquared;
					if (secondT < 0f) {
						secondT = 0f;
						firstT = math.saturate(-firstProjection / firstLengthSquared);
					} else if (secondT > 1f) {
						secondT = 1f;
						firstT = math.saturate((directionsDot - firstProjection)
							/ firstLengthSquared);
					}
				}
			}
			firstPoint = firstStart + firstDirection * firstT;
			secondPoint = secondStart + secondDirection * secondT;
		}
	}

	internal static class WireRailSolderMeshGenerator
	{
		private const float FullTurn = math.PI * 2f;
		private const int BlobRadialSegments = 6;
		private const int BlobRingCount = 3;
		private const int BlobVertexCount = BlobRadialSegments * BlobRingCount + 2;
		internal const int TrianglesPerBlob = BlobRadialSegments * BlobRingCount * 2;

		[ThreadStatic] private static SolderBuffers _threadBuffers;

		private sealed class SolderBuffers
		{
			public readonly List<FixtureWirePath> FixturePaths = new(2);
			public readonly List<WireRailTouch> Touches = new(8);
			public readonly float[] StartTrimOffsets =
				new float[WireRailEndpointTrimUtility.MaximumRailCount];
			public readonly float[] EndTrimOffsets =
				new float[WireRailEndpointTrimUtility.MaximumRailCount];
			public readonly float3[] BlobPositions = new float3[BlobVertexCount];
			public readonly Vector2[] BlobUvs = new Vector2[BlobVertexCount];
		}

		private readonly struct FixtureWirePath
		{
			public readonly IReadOnlyList<float3> Points;
			public readonly float Radius;

			public FixtureWirePath(IReadOnlyList<float3> points, float radius)
			{
				Points = points;
				Radius = radius;
			}
		}

		internal static void Append(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			if (spline == null || segments == null || segments.Count == 0
				|| fixtures == null) {
				return;
			}
			var buffers = _threadBuffers ??= new SolderBuffers();
			WireRailEndpointTrimUtility.Collect(spline, segments, fixtures,
				buffers.StartTrimOffsets, buffers.EndTrimOffsets);
			foreach (var fixture in fixtures) {
				buffers.Touches.Clear();
				CollectTouches(spline, segments, fixtures, fixture,
					buffers.StartTrimOffsets, buffers.EndTrimOffsets,
					buffers.FixturePaths, buffers.Touches);
				foreach (var touch in buffers.Touches) {
					AppendTouch(touch, CalculateSeed(touch), vertices, normals, uvs, indices);
				}
			}
		}

		internal static void CollectTouches(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, WireRailFixture fixture,
			ICollection<WireRailTouch> touches)
		{
			if (touches == null) {
				throw new ArgumentNullException(nameof(touches));
			}
			var buffers = _threadBuffers ??= new SolderBuffers();
			WireRailEndpointTrimUtility.Collect(spline, segments, fixtures,
				buffers.StartTrimOffsets, buffers.EndTrimOffsets);
			CollectTouches(spline, segments, fixtures, fixture,
				buffers.StartTrimOffsets, buffers.EndTrimOffsets,
				buffers.FixturePaths, touches);
		}

		private static void CollectTouches(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, WireRailFixture fixture,
			IReadOnlyList<float> startTrimOffsets,
			IReadOnlyList<float> endTrimOffsets,
			List<FixtureWirePath> fixturePaths, ICollection<WireRailTouch> touches)
		{
			fixturePaths.Clear();
			if (spline == null || segments == null || segments.Count == 0
				|| fixture == null || fixture is WireRailTrimFixture
				|| fixture is WireRailDropLoopFixture
				|| fixture is WireRailDropFixture
				|| !TryBuildFixturePaths(spline, segments, fixture, fixturePaths)
				|| fixturePaths.Count == 0) {
				return;
			}

			var splineLength = spline.GetLength();
			var distance = math.clamp(fixture.Distance, 0f, math.max(0f, splineLength));
			var segmentIndex = WireRailSplineGeometry.GetLayoutIndexAtDistance(segments,
				distance, splineLength);
			if (segmentIndex < 0) {
				return;
			}
			var segmentStart = segments[segmentIndex].Distance;
			var segmentEnd = segmentIndex + 1 < segments.Count
				? segments[segmentIndex + 1].Distance : splineLength;
			var curveT = segmentEnd > segmentStart
				? math.saturate((distance - segmentStart) / (segmentEnd - segmentStart))
				: 0f;
			var maximumFixtureRadius = 0f;
			foreach (var fixturePath in fixturePaths) {
				maximumFixtureRadius = math.max(maximumFixtureRadius, fixturePath.Radius);
			}
			var atStart = !spline.Closed && distance <= 1e-4f;
			var atEnd = !spline.Closed && distance >= splineLength - 1e-4f;
			var evaluationContext = new WireRailPathEvaluationContext();
			var segment = segments[segmentIndex];
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				// Skip a rail whose endpoint trim removed it at the fixture: the rail is absent
				// anywhere before its trimmed start or after its trimmed end, not only exactly
				// at the spline endpoints, so a fixture inside that gap must not solder to it.
				if (!segment.IsRailActive(railIndex)
					|| distance < startTrimOffsets[railIndex] - 1e-4f
					|| distance > splineLength - endTrimOffsets[railIndex] + 1e-4f
					|| !WireRailSplineGeometry.TryEvaluateRailFrame(spline, segments,
						evaluationContext, segmentIndex, railIndex, curveT, 1f / 128f,
						out var railFrame)) {
					continue;
				}
				var railRadius = WireRailSplineGeometry.EvaluateWireDiameter(spline,
					segments, segmentIndex, railIndex, curveT) * 0.5f;
				var railSpan = math.max(WireRailLayout.ReferenceWireDiameter,
					railRadius + maximumFixtureRadius + fixture.SolderThreshold) * 2f;
				var railStart = atStart
					? railFrame.Position
					: railFrame.Position - railFrame.Tangent * railSpan;
				var railEnd = atEnd
					? railFrame.Position
					: railFrame.Position + railFrame.Tangent * railSpan;
				foreach (var fixturePath in fixturePaths) {
					if (WireRailWireTouchDetector.TryFindTouch(railStart, railEnd,
							railRadius, fixturePath.Points, fixturePath.Radius,
							fixture.SolderThreshold, out var touch)) {
						touches.Add(touch);
					}
				}
			}
		}

		private static bool TryBuildFixturePaths(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailFixture fixture,
			ICollection<FixtureWirePath> paths)
		{
			switch (fixture) {
				case WireRailBraceFixture brace when WireRailFixtureMeshGenerator
					.TryEvaluateBraceProfile(spline, segments, brace, out var braceProfile): {
					var points = BuildBracePath(braceProfile, brace);
					if (points.Count >= 2) {
						paths.Add(new FixtureWirePath(points, brace.Diameter * 0.5f));
					}
					break;
				}
				case WireRailVBraceFixture vBrace when WireRailFixtureMeshGenerator
					.TryEvaluateVBraceProfile(spline, segments, vBrace, out var vBraceProfile):
					paths.Add(new FixtureWirePath(vBraceProfile.CenterlinePoints,
						vBrace.Diameter * 0.5f));
					break;
				case WireRailCrossWireFixture crossWire when WireRailFixtureMeshGenerator
					.TryEvaluateCrossWireProfile(spline, segments, crossWire,
						out var crossWireProfile):
					paths.Add(new FixtureWirePath(new[] {
						crossWireProfile.Start,
						crossWireProfile.End,
					}, crossWire.Diameter * 0.5f));
					break;
				case WireRailLegFixture leg when WireRailFixtureMeshGenerator
					.TryEvaluateLegProfile(spline, segments, leg, out var legProfile):
					paths.Add(new FixtureWirePath(new[] {
						legProfile.AttachmentProfile.Start,
						legProfile.AttachmentProfile.End,
					}, leg.Diameter * 0.5f));
					break;
				default:
					return false;
			}
			return paths.Count > 0;
		}

		private static List<float3> BuildBracePath(WireRailBraceProfile profile,
			WireRailBraceFixture brace)
		{
			var points = new List<float3>();
			if (!brace.TryGetVisibleArc(out var startAngle, out var sweepAngle,
					out var closed)) {
				return points;
			}
			var segmentCount = math.max(2,
				(int)math.ceil(brace.RingDensity * sweepAngle / FullTurn));
			var angles = BuildBraceAngles(brace, startAngle, sweepAngle, closed,
				segmentCount);
			foreach (var angle in angles) {
				points.Add(profile.Frame.TransformOffset(profile.CenterOffset
					+ brace.EvaluateCenterlineOffset(angle, profile.Radius)));
			}
			if (closed && points.Count > 1) {
				points.Add(points[0]);
			}
			return points;
		}

		private static List<float> BuildBraceAngles(WireRailBraceFixture brace,
			float startAngle, float sweepAngle, bool closed, int segmentCount)
		{
			var angles = new List<float>(segmentCount + 3);
			var pointCount = closed ? segmentCount : segmentCount + 1;
			for (var pointIndex = 0; pointIndex < pointCount; pointIndex++) {
				angles.Add(startAngle + sweepAngle * pointIndex / segmentCount);
			}
			if (brace.TryGetStraightSection(out var straightStart, out var straightSweep)) {
				AddBoundary(straightStart);
				AddBoundary(straightStart + straightSweep);
			}
			angles.Sort();
			for (var angleIndex = angles.Count - 1; angleIndex > 0; angleIndex--) {
				if (math.abs(angles[angleIndex] - angles[angleIndex - 1]) < 1e-5f) {
					angles.RemoveAt(angleIndex);
				}
			}
			return angles;

			void AddBoundary(float boundary)
			{
				for (var turn = -2; turn <= 2; turn++) {
					var unwrapped = boundary + turn * FullTurn;
					var atEnd = math.abs(unwrapped - (startAngle + sweepAngle)) < 1e-5f;
					if (unwrapped >= startAngle - 1e-5f
						&& unwrapped <= startAngle + sweepAngle + 1e-5f
						&& (!closed || !atEnd)) {
						angles.Add(unwrapped);
					}
				}
			}
		}

		internal static void AppendTouch(WireRailTouch touch, uint seed,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var center = touch.Position;
			var firstAxis = math.normalizesafe(touch.FirstTangent,
				new float3(0f, 1f, 0f));
			var secondAxis = touch.SecondTangent
				- firstAxis * math.dot(touch.SecondTangent, firstAxis);
			if (math.lengthsq(secondAxis) <= 1e-8f) {
				var separation = touch.SecondPoint - touch.FirstPoint;
				secondAxis = separation - firstAxis * math.dot(separation, firstAxis);
			}
			if (math.lengthsq(secondAxis) <= 1e-8f) {
				var fallback = math.abs(firstAxis.z) < 0.9f
					? new float3(0f, 0f, 1f) : new float3(1f, 0f, 0f);
				secondAxis = fallback - firstAxis * math.dot(fallback, firstAxis);
			}
			secondAxis = math.normalize(secondAxis);
			var normalAxis = math.normalizesafe(math.cross(firstAxis, secondAxis),
				new float3(1f, 0f, 0f));
			var separationAxis = touch.SecondPoint - touch.FirstPoint;
			if (math.dot(normalAxis, separationAxis) < 0f) {
				normalAxis = -normalAxis;
			}

			var state = seed == 0 ? 0x9e3779b9u : seed;
			var firstExtent = (touch.FirstRadius * 1.25f
				+ touch.SecondRadius * 0.35f) * RandomRange(ref state, 0.92f, 1.08f);
			var secondExtent = (touch.SecondRadius * 1.25f
				+ touch.FirstRadius * 0.35f) * RandomRange(ref state, 0.92f, 1.08f);
			var centerDistance = math.distance(touch.FirstPoint, touch.SecondPoint);
			var normalExtent = math.max(math.max(touch.FirstRadius, touch.SecondRadius) * 0.75f,
				centerDistance * 0.5f + math.min(touch.FirstRadius, touch.SecondRadius) * 0.25f)
				* RandomRange(ref state, 0.92f, 1.08f);

			var buffers = _threadBuffers ??= new SolderBuffers();
			var positions = buffers.BlobPositions;
			var blobUvs = buffers.BlobUvs;
			positions[0] = center - normalAxis * normalExtent
				* RandomRange(ref state, 0.9f, 1.02f);
			blobUvs[0] = new Vector2(0.5f, 0f);
			for (var ringIndex = 0; ringIndex < BlobRingCount; ringIndex++) {
				var latitude = math.lerp(-math.PI * 0.25f, math.PI * 0.25f,
					ringIndex / (float)(BlobRingCount - 1));
				var latitudeRadius = math.cos(latitude);
				var latitudeHeight = math.sin(latitude);
				var angleOffset = RandomRange(ref state, -0.14f, 0.14f);
				for (var radialIndex = 0; radialIndex < BlobRadialSegments; radialIndex++) {
					var angle = FullTurn * radialIndex / BlobRadialSegments + angleOffset
						+ RandomRange(ref state, -0.08f, 0.08f);
					var radialVariation = RandomRange(ref state, 0.84f, 1.16f);
					var normalVariation = RandomRange(ref state, 0.9f, 1.1f);
					var radial = firstAxis * (math.cos(angle) * firstExtent)
						+ secondAxis * (math.sin(angle) * secondExtent);
					var vertexIndex = 1 + ringIndex * BlobRadialSegments + radialIndex;
					positions[vertexIndex] = center + radial * latitudeRadius
						* radialVariation + normalAxis * normalExtent * latitudeHeight
						* normalVariation;
					blobUvs[vertexIndex] = new Vector2(
						radialIndex / (float)BlobRadialSegments,
						(ringIndex + 1f) / (BlobRingCount + 1f));
				}
			}
			var topPole = BlobVertexCount - 1;
			positions[topPole] = center + normalAxis * normalExtent
				* RandomRange(ref state, 0.9f, 1.02f);
			blobUvs[topPole] = new Vector2(0.5f, 1f);

			for (var radialIndex = 0; radialIndex < BlobRadialSegments; radialIndex++) {
				var next = (radialIndex + 1) % BlobRadialSegments;
				AppendTriangle(0, 1 + next, 1 + radialIndex);
				for (var ringIndex = 0; ringIndex < BlobRingCount - 1; ringIndex++) {
					var firstRing = 1 + ringIndex * BlobRadialSegments;
					var secondRing = firstRing + BlobRadialSegments;
					AppendTriangle(firstRing + radialIndex, firstRing + next,
						secondRing + radialIndex);
					AppendTriangle(firstRing + next, secondRing + next,
						secondRing + radialIndex);
				}
				var lastRing = 1 + (BlobRingCount - 1) * BlobRadialSegments;
				AppendTriangle(topPole, lastRing + radialIndex, lastRing + next);
			}

			void AppendTriangle(int firstIndex, int secondIndex, int thirdIndex)
			{
				var first = positions[firstIndex];
				var second = positions[secondIndex];
				var third = positions[thirdIndex];
				var faceNormal = math.cross(second - first, third - first);
				if (math.dot(faceNormal, (first + second + third) / 3f - center) < 0f) {
					(second, third) = (third, second);
					(secondIndex, thirdIndex) = (thirdIndex, secondIndex);
				}
				var firstVertex = vertices.Count;
				vertices.Add((Vector3)first);
				vertices.Add((Vector3)second);
				vertices.Add((Vector3)third);
				normals.Add((Vector3)EvaluateSmoothNormal(first));
				normals.Add((Vector3)EvaluateSmoothNormal(second));
				normals.Add((Vector3)EvaluateSmoothNormal(third));
				uvs.Add(blobUvs[firstIndex]);
				uvs.Add(blobUvs[secondIndex]);
				uvs.Add(blobUvs[thirdIndex]);
				indices.Add(firstVertex);
				indices.Add(firstVertex + 1);
				indices.Add(firstVertex + 2);
			}

			float3 EvaluateSmoothNormal(float3 position)
			{
				var offset = position - center;
				var firstExtentSquared = math.max(firstExtent * firstExtent, 1e-8f);
				var secondExtentSquared = math.max(secondExtent * secondExtent, 1e-8f);
				var normalExtentSquared = math.max(normalExtent * normalExtent, 1e-8f);
				var ellipsoidNormal = firstAxis * (math.dot(offset, firstAxis) / firstExtentSquared)
					+ secondAxis * (math.dot(offset, secondAxis) / secondExtentSquared)
					+ normalAxis * (math.dot(offset, normalAxis) / normalExtentSquared);
				return math.normalizesafe(ellipsoidNormal,
					math.normalizesafe(offset, normalAxis));
			}
		}

		private static uint CalculateSeed(WireRailTouch touch)
		{
			var seed = 0x811c9dc5u;
			seed = Mix(seed, math.asuint(touch.Position.x));
			seed = Mix(seed, math.asuint(touch.Position.y));
			seed = Mix(seed, math.asuint(touch.Position.z));
			seed = Mix(seed, math.asuint(touch.FirstRadius));
			seed = Mix(seed, math.asuint(touch.SecondRadius));
			return seed;
		}

		private static uint Mix(uint seed, uint value)
		{
			seed ^= value + 0x9e3779b9u + (seed << 6) + (seed >> 2);
			seed ^= seed >> 16;
			seed *= 0x7feb352du;
			seed ^= seed >> 15;
			return seed;
		}

		private static float RandomRange(ref uint state, float minimum, float maximum)
		{
			state ^= state << 13;
			state ^= state >> 17;
			state ^= state << 5;
			var unit = (state & 0x00ffffffu) / 16777215f;
			return math.lerp(minimum, maximum, unit);
		}
	}
}
