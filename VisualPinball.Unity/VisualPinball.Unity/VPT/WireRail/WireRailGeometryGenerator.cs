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

		public float3 TransformOffsetVector(float2 offset)
			=> Right * offset.x + Up * offset.y;
	}

	public sealed class WireRailPathEvaluationContext
	{
		internal readonly Dictionary<long, WireRailBoundaryTangent> BoundaryTangents = new();
	}

	internal readonly struct WireRailBoundaryTangent
	{
		public readonly float3 Incoming;
		public readonly float3 Outgoing;
		public readonly float3 Shared;

		public WireRailBoundaryTangent(float3 incoming, float3 outgoing, float3 shared)
		{
			Incoming = incoming;
			Outgoing = outgoing;
			Shared = shared;
		}
	}

	public static class WireRailSplineGeometry
	{
		private const float ConnectionTangentBlend = 0.5f;
		private const float ConnectionTangentSampleStep = 1f / 1024f;

		internal static bool TryEvaluate(Spline spline, int segmentIndex, float curveT,
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

		public static float2 EvaluateRailOffset(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			float curveT)
		{
			var current = (float2)segments[segmentIndex].GetRailOffset(railIndex);
			var offset = current;
			var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments, segmentIndex);
			if (IsContinuousBoundary(segments, previousSegmentIndex, segmentIndex, railIndex)) {
				var previous = (float2)segments[previousSegmentIndex].GetRailOffset(railIndex);
				var connection = segments[previousSegmentIndex].ConnectionToNext;
				var junction = math.lerp(previous, current,
					connection.GetWireWeight(railIndex));
				offset += (junction - current)
					* (1f - connection.EvaluateWireTransition(railIndex, curveT));
			}
			var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
			if (IsContinuousBoundary(segments, segmentIndex, nextSegmentIndex, railIndex)) {
				var next = (float2)segments[nextSegmentIndex].GetRailOffset(railIndex);
				var connection = segments[segmentIndex].ConnectionToNext;
				var junction = math.lerp(current, next, connection.GetWireWeight(railIndex));
				offset += (junction - current)
					* connection.EvaluateWireTransition(railIndex, curveT);
			}
			return offset;
		}

		public static float EvaluateWireDiameter(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			float curveT)
		{
			var current = segments[segmentIndex].GetWireDiameter(railIndex);
			var diameter = current;
			var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments, segmentIndex);
			if (IsContinuousBoundary(segments, previousSegmentIndex, segmentIndex, railIndex)) {
				var connection = segments[previousSegmentIndex].ConnectionToNext;
				var junction = math.lerp(
					segments[previousSegmentIndex].GetWireDiameter(railIndex), current,
					connection.GetWireWeight(railIndex));
				diameter += (junction - current)
					* (1f - connection.EvaluateWireTransition(railIndex, curveT));
			}
			var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
			if (IsContinuousBoundary(segments, segmentIndex, nextSegmentIndex, railIndex)) {
				var connection = segments[segmentIndex].ConnectionToNext;
				var junction = math.lerp(current,
					segments[nextSegmentIndex].GetWireDiameter(railIndex),
					connection.GetWireWeight(railIndex));
				diameter += (junction - current)
					* connection.EvaluateWireTransition(railIndex, curveT);
			}
			return diameter;
		}

		public static bool IsContinuousAtStart(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex)
			=> IsContinuousBoundary(segments,
				GetPreviousSegmentIndex(spline, segments, segmentIndex), segmentIndex, railIndex);

		public static bool IsContinuousAtEnd(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex)
			=> IsContinuousBoundary(segments, segmentIndex,
				GetNextSegmentIndex(spline, segments, segmentIndex), railIndex);

		internal static bool IsRailConnectedAtStart(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex)
			=> IsRailConnectedBoundary(segments,
				GetPreviousSegmentIndex(spline, segments, segmentIndex), segmentIndex,
				railIndex);

		internal static bool IsRailConnectedAtEnd(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex)
			=> IsRailConnectedBoundary(segments, segmentIndex,
				GetNextSegmentIndex(spline, segments, segmentIndex), railIndex);

		internal static bool TryEvaluateRailFrame(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			float curveT, float tangentStep, out WireRailPathFrame railFrame)
			=> TryEvaluateRailFrame(spline, segments, new WireRailPathEvaluationContext(),
				segmentIndex, railIndex, curveT, tangentStep, out railFrame);

		internal static bool TryEvaluateRailFrame(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailPathEvaluationContext context,
			int segmentIndex, int railIndex, float curveT, float tangentStep,
			out WireRailPathFrame railFrame)
		{
			railFrame = default;
			curveT = math.saturate(curveT);
			tangentStep = math.clamp(tangentStep, 1e-5f, 0.5f);
			if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex, curveT,
					out var mainFrame, out var center)) {
				return false;
			}

			var before = center;
			var after = center;
			var referenceRight = mainFrame.Right;
			var referenceUp = mainFrame.Up;
			if (curveT <= 0f) {
				var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments, segmentIndex);
				var connected = IsRailConnectedBoundary(segments, previousSegmentIndex,
					segmentIndex, railIndex);
				if (connected) {
					if (!TryEvaluateRailCenter(spline, segments, context, previousSegmentIndex,
							railIndex, 1f - tangentStep, out _, out before)
						|| !TryEvaluate(spline, previousSegmentIndex, 1f,
							out var previousMainFrame)) {
						return false;
					}
					AverageReferenceAxes(previousMainFrame, mainFrame,
						out referenceRight, out referenceUp);
				}
				if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
						tangentStep, out _, out after)) {
					return false;
				}
			} else if (curveT >= 1f) {
				if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
						1f - tangentStep, out _, out before)) {
					return false;
				}
				var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
				var connected = IsRailConnectedBoundary(segments, segmentIndex,
					nextSegmentIndex, railIndex);
				if (connected) {
					if (!TryEvaluateRailCenter(spline, segments, context, nextSegmentIndex, railIndex,
							tangentStep, out _, out after)
						|| !TryEvaluate(spline, nextSegmentIndex, 0f, out var nextMainFrame)) {
						return false;
					}
					AverageReferenceAxes(mainFrame, nextMainFrame,
						out referenceRight, out referenceUp);
				}
			} else {
				if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
						math.max(0f, curveT - tangentStep), out _, out before)
					|| !TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
						math.min(1f, curveT + tangentStep), out _, out after)) {
					return false;
				}
			}

			var tangentVector = after - before;
			var derivativeStep = math.min(tangentStep, 1f / 1024f);
			var derivativeBefore = center;
			var derivativeAfter = center;
			if (curveT <= 0f) {
				var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments, segmentIndex);
				if (IsRailConnectedBoundary(segments, previousSegmentIndex, segmentIndex,
						railIndex)
					&& !TryEvaluateRailCenter(spline, segments, context, previousSegmentIndex, railIndex,
						1f - derivativeStep, out _, out derivativeBefore)) {
					return false;
				}
				if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
						derivativeStep, out _, out derivativeAfter)) {
					return false;
				}
			} else if (curveT >= 1f) {
				if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
						1f - derivativeStep, out _, out derivativeBefore)) {
					return false;
				}
				var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
				if (IsRailConnectedBoundary(segments, segmentIndex, nextSegmentIndex, railIndex)
					&& !TryEvaluateRailCenter(spline, segments, context, nextSegmentIndex, railIndex,
						derivativeStep, out _, out derivativeAfter)) {
					return false;
				}
			} else if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
					math.max(0f, curveT - derivativeStep), out _, out derivativeBefore)
				|| !TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
					math.min(1f, curveT + derivativeStep), out _, out derivativeAfter)) {
				return false;
			}
			var derivative = derivativeAfter - derivativeBefore;
			if (math.lengthsq(derivative) > 1e-12f
				&& math.dot(math.normalize(derivative), mainFrame.Tangent) > 0f) {
				tangentVector = derivative;
			}
			var tangent = math.normalizesafe(tangentVector, mainFrame.Tangent);
			var right = referenceRight - tangent * math.dot(referenceRight, tangent);
			if (math.lengthsq(right) <= 1e-8f) {
				right = math.cross(tangent, referenceUp);
			}
			right = math.normalizesafe(right, mainFrame.Right);
			var up = math.normalizesafe(math.cross(right, tangent), mainFrame.Up);
			railFrame = new WireRailPathFrame(center, tangent, right, up);
			return true;
		}

		public static bool TryEvaluateRailPosition(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			float curveT, out float3 position)
			=> TryEvaluateRailPosition(spline, segments, new WireRailPathEvaluationContext(),
				segmentIndex, railIndex, curveT, out position);

		public static bool TryEvaluateRailPosition(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailPathEvaluationContext context,
			int segmentIndex, int railIndex, float curveT, out float3 position)
		{
			context ??= new WireRailPathEvaluationContext();
			return TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
				curveT, out _, out position);
		}

		private static bool TryEvaluateRailCenter(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailPathEvaluationContext context,
			int segmentIndex, int railIndex, float curveT,
			out WireRailPathFrame mainFrame, out float3 center)
		{
			curveT = math.saturate(curveT);
			if (!TryEvaluateRailCenterUnsmoothed(spline, segments, segmentIndex, railIndex,
					curveT, out mainFrame, out center)) {
				return false;
			}

			var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments, segmentIndex);
			// A compact Hermite correction rotates both sides toward the same tangent without
			// moving the knot or changing the unaffected half of either segment.
			if (curveT > 0f && curveT < ConnectionTangentBlend
				&& IsContinuousBoundary(segments, previousSegmentIndex, segmentIndex, railIndex)) {
				if (!TryEvaluateSharedBoundaryTangent(spline, segments, context,
						previousSegmentIndex,
						segmentIndex, railIndex, out _, out var outgoing, out var sharedTangent)) {
					return false;
				}
				var u = curveT / ConnectionTangentBlend;
				var influence = ConnectionTangentBlend * u * (1f - u) * (1f - u);
				center += influence * (sharedTangent * math.length(outgoing) - outgoing);
			}

			var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
			if (curveT > 1f - ConnectionTangentBlend && curveT < 1f
				&& IsContinuousBoundary(segments, segmentIndex, nextSegmentIndex, railIndex)) {
				if (!TryEvaluateSharedBoundaryTangent(spline, segments, context, segmentIndex,
						nextSegmentIndex, railIndex, out var incoming, out _, out var sharedTangent)) {
					return false;
				}
				var u = (curveT - (1f - ConnectionTangentBlend)) / ConnectionTangentBlend;
				var influence = ConnectionTangentBlend * u * u * (u - 1f);
				center += influence * (sharedTangent * math.length(incoming) - incoming);
			}
			return true;
		}

		private static bool TryEvaluateRailCenterUnsmoothed(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			float curveT, out WireRailPathFrame mainFrame, out float3 center)
		{
			center = default;
			curveT = math.saturate(curveT);
			if (!TryEvaluate(spline, segmentIndex, curveT, out mainFrame)
				|| !TryEvaluate(spline, segmentIndex, 0f, out var startFrame)
				|| !TryEvaluate(spline, segmentIndex, 1f, out var endFrame)) {
				return false;
			}

			var offset = EvaluateRailOffset(spline, segments, segmentIndex, railIndex, curveT);
			var startOffset = EvaluateRailOffset(spline, segments, segmentIndex, railIndex, 0f);
			var endOffset = EvaluateRailOffset(spline, segments, segmentIndex, railIndex, 1f);
			var offsetDelta = endOffset - startOffset;
			var offsetDeltaLengthSquared = math.lengthsq(offsetDelta);
			// Rotating a large offset with every spline frame can fold the rail back when the
			// bend radius becomes smaller than that offset. Interpolate the endpoint
			// displacements instead, while preserving any authored motion off that line.
			var transitionT = offsetDeltaLengthSquared > 1e-6f
				? math.saturate(math.dot(offset - startOffset, offsetDelta)
					/ offsetDeltaLengthSquared)
				: curveT;
			var interpolatedOffset = math.lerp(startOffset, endOffset, transitionT);
			var residualOffset = offset - interpolatedOffset;
			var startDisplacement = startFrame.TransformOffsetVector(startOffset);
			var endDisplacement = endFrame.TransformOffsetVector(endOffset);
			var displacement = math.lerp(startDisplacement, endDisplacement, transitionT)
				+ mainFrame.TransformOffsetVector(residualOffset);
			center = mainFrame.Position + displacement;
			return true;
		}

		private static bool TryEvaluateSharedBoundaryTangent(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailPathEvaluationContext context,
			int sourceSegmentIndex,
			int nextSegmentIndex, int railIndex, out float3 incoming, out float3 outgoing,
			out float3 sharedTangent)
		{
			var cacheKey = ((long)sourceSegmentIndex << 32) | (uint)railIndex;
			if (context.BoundaryTangents.TryGetValue(cacheKey, out var cached)) {
				incoming = cached.Incoming;
				outgoing = cached.Outgoing;
				sharedTangent = cached.Shared;
				return true;
			}
			incoming = default;
			outgoing = default;
			sharedTangent = default;
			if (!TryEvaluateRailCenterUnsmoothed(spline, segments, sourceSegmentIndex,
					railIndex, 1f - ConnectionTangentSampleStep, out _, out var sourceBefore)
				|| !TryEvaluateRailCenterUnsmoothed(spline, segments, sourceSegmentIndex,
					railIndex, 1f, out var sourceMainFrame, out var sourceEnd)
				|| !TryEvaluateRailCenterUnsmoothed(spline, segments, nextSegmentIndex,
					railIndex, 0f, out var nextMainFrame, out var nextStart)
				|| !TryEvaluateRailCenterUnsmoothed(spline, segments, nextSegmentIndex,
					railIndex, ConnectionTangentSampleStep, out _, out var nextAfter)) {
				return false;
			}

			incoming = (sourceEnd - sourceBefore) / ConnectionTangentSampleStep;
			outgoing = (nextAfter - nextStart) / ConnectionTangentSampleStep;
			var referenceTangent = math.normalizesafe(
				sourceMainFrame.Tangent + nextMainFrame.Tangent, sourceMainFrame.Tangent);
			var incomingDirection = math.normalizesafe(incoming, referenceTangent);
			var outgoingDirection = math.normalizesafe(outgoing, referenceTangent);
			if (math.dot(incomingDirection, referenceTangent) <= 0f) {
				incomingDirection = referenceTangent;
			}
			if (math.dot(outgoingDirection, referenceTangent) <= 0f) {
				outgoingDirection = referenceTangent;
			}
			sharedTangent = math.normalizesafe(incomingDirection + outgoingDirection,
				referenceTangent);
			context.BoundaryTangents.Add(cacheKey,
				new WireRailBoundaryTangent(incoming, outgoing, sharedTangent));
			return true;
		}

		private static void AverageReferenceAxes(WireRailPathFrame first,
			WireRailPathFrame second, out float3 right, out float3 up)
		{
			right = math.normalizesafe(first.Right + second.Right, second.Right);
			up = math.normalizesafe(first.Up + second.Up, second.Up);
		}

		private static bool IsContinuousBoundary(IReadOnlyList<WireRailSegment> segments,
			int sourceSegmentIndex, int nextSegmentIndex, int railIndex)
			=> sourceSegmentIndex >= 0 && nextSegmentIndex >= 0
				&& sourceSegmentIndex < segments.Count && nextSegmentIndex < segments.Count
				&& segments[sourceSegmentIndex].RailCount > railIndex
				&& segments[nextSegmentIndex].RailCount > railIndex
				&& segments[sourceSegmentIndex].ConnectionToNext.IsWireContinuous(railIndex);

		private static bool IsRailConnectedBoundary(
			IReadOnlyList<WireRailSegment> segments, int sourceSegmentIndex,
			int nextSegmentIndex, int railIndex)
		{
			if (sourceSegmentIndex < 0 || nextSegmentIndex < 0
				|| sourceSegmentIndex >= segments.Count || nextSegmentIndex >= segments.Count
				|| segments[sourceSegmentIndex].RailCount <= railIndex
				|| segments[nextSegmentIndex].RailCount <= railIndex) {
				return false;
			}
			if (IsContinuousBoundary(segments, sourceSegmentIndex, nextSegmentIndex,
					railIndex)) {
				return true;
			}
			var sourceOffset = (float2)segments[sourceSegmentIndex].GetRailOffset(railIndex);
			var nextOffset = (float2)segments[nextSegmentIndex].GetRailOffset(railIndex);
			var sourceDiameter = segments[sourceSegmentIndex].GetWireDiameter(railIndex);
			var nextDiameter = segments[nextSegmentIndex].GetWireDiameter(railIndex);
			return math.distancesq(sourceOffset, nextOffset) <= 1e-6f
				&& math.abs(sourceDiameter - nextDiameter) <= 1e-4f;
		}

		private static int GetPreviousSegmentIndex(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex)
		{
			if (segmentIndex > 0) {
				return segmentIndex - 1;
			}
			return spline != null && spline.Closed && segments.Count > 1
				? segments.Count - 1 : -1;
		}

		private static int GetNextSegmentIndex(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex)
		{
			if (segmentIndex + 1 < segments.Count) {
				return segmentIndex + 1;
			}
			return spline != null && spline.Closed && segments.Count > 1 ? 0 : -1;
		}
	}

	internal static class WireRailRenderMeshGenerator
	{
		private const float MaximumRingAngle = 0.08726646f;
		private const int MaximumAdaptiveDepth = 3;

		public static Mesh Generate(Spline spline, IReadOnlyList<WireRailSegment> segments,
			int samplesPerSegment, int radialSegments, Mesh target)
		{
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var uvs = new List<Vector2>();
			var indices = new List<int>();
			var previousFrames = new Dictionary<int, WireRailPathFrame>();
			var evaluationContext = new WireRailPathEvaluationContext();
			for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
				var segment = segments[segmentIndex];
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					WireRailPathFrame? previousSegmentFrame = null;
					if (segmentIndex > 0
						&& WireRailSplineGeometry.IsRailConnectedAtStart(spline, segments,
							segmentIndex, railIndex)
						&& previousFrames.TryGetValue(railIndex, out var previousFrame)) {
						previousSegmentFrame = previousFrame;
					}
					if (AppendTube(spline, segments, evaluationContext, segmentIndex, railIndex,
						samplesPerSegment, radialSegments,
						previousSegmentFrame,
						vertices, normals, uvs, indices, out var lastFrame)) {
						previousFrames[railIndex] = lastFrame;
					} else {
						previousFrames.Remove(railIndex);
					}
				}
			}

			var mesh = target ? target : new Mesh();
			mesh.Clear(false);
			mesh.name = "Wire Rail Render (Generated)";
			mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(indices, 0, false);
			mesh.RecalculateBounds();
			return mesh;
		}

		private static bool AppendTube(Spline spline, IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext,
			int segmentIndex, int railIndex, int samplesPerSegment,
			int radialSegments, WireRailPathFrame? previousSegmentFrame,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices,
			out WireRailPathFrame lastFrame)
		{
			var firstRing = vertices.Count;
			WireRailPathFrame firstFrame = default;
			lastFrame = default;
			var firstRadius = 0f;
			var lastRadius = 0f;
			var sampleParameters = BuildSampleParameters(spline, segments, evaluationContext,
				segmentIndex,
				railIndex, samplesPerSegment);
			for (var sampleIndex = 0; sampleIndex < sampleParameters.Count; sampleIndex++) {
				var curveT = sampleParameters[sampleIndex];
				var tangentStep = sampleIndex == 0
					? sampleParameters[1] - curveT
					: sampleIndex == sampleParameters.Count - 1
						? curveT - sampleParameters[sampleIndex - 1]
						: math.min(curveT - sampleParameters[sampleIndex - 1],
							sampleParameters[sampleIndex + 1] - curveT);
				if (!WireRailSplineGeometry.TryEvaluateRailFrame(spline, segments,
						evaluationContext, segmentIndex, railIndex, curveT, tangentStep,
						out var frame)) {
					return false;
				}
				if (sampleIndex == 0 && previousSegmentFrame.HasValue) {
					var previous = previousSegmentFrame.Value;
					frame = new WireRailPathFrame(frame.Position, frame.Tangent,
						previous.Right, previous.Up);
				} else if (sampleIndex > 0) {
					var right = Transport(lastFrame.Right, lastFrame.Tangent, frame.Tangent);
					var up = math.normalizesafe(math.cross(right, frame.Tangent), lastFrame.Up);
					right = math.normalizesafe(math.cross(frame.Tangent, up), right);
					frame = new WireRailPathFrame(frame.Position, frame.Tangent, right, up);
				}
				var radius = WireRailSplineGeometry.EvaluateWireDiameter(spline, segments,
					segmentIndex, railIndex, curveT) * 0.5f;
				if (sampleIndex == 0) {
					firstFrame = frame;
					firstRadius = radius;
				}
				lastFrame = frame;
				lastRadius = radius;
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var angle = math.PI * 2f * radialIndex / radialSegments;
					var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
					vertices.Add((Vector3)(frame.Position + radial * radius));
					normals.Add((Vector3)radial);
					uvs.Add(new Vector2(curveT, radialIndex / (float)radialSegments));
				}
			}

			for (var sampleIndex = 0; sampleIndex < sampleParameters.Count - 1; sampleIndex++) {
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

			if (!WireRailSplineGeometry.IsRailConnectedAtStart(spline, segments,
					segmentIndex, railIndex)) {
				AppendCap(firstFrame, firstRadius, radialSegments, true,
					vertices, normals, uvs, indices);
			}
			if (!WireRailSplineGeometry.IsRailConnectedAtEnd(spline, segments,
					segmentIndex, railIndex)) {
				AppendCap(lastFrame, lastRadius, radialSegments, false,
					vertices, normals, uvs, indices);
			}
			return true;
		}

		internal static List<float> BuildSampleParameters(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			int minimumSamples)
			=> BuildSampleParameters(spline, segments, new WireRailPathEvaluationContext(),
				segmentIndex, railIndex, minimumSamples);

		private static List<float> BuildSampleParameters(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext,
			int segmentIndex, int railIndex, int minimumSamples)
		{
			var parameters = new List<float>(minimumSamples + 1) { 0f };
			for (var sampleIndex = 0; sampleIndex < minimumSamples; sampleIndex++) {
				var start = sampleIndex / (float)minimumSamples;
				var end = (sampleIndex + 1f) / minimumSamples;
				SubdivideSampleInterval(spline, segments, evaluationContext,
					segmentIndex, railIndex, start, end, 0, parameters);
			}
			return parameters;
		}

		private static void SubdivideSampleInterval(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext,
			int segmentIndex, int railIndex,
			float start, float end, int depth, ICollection<float> parameters)
		{
			var step = end - start;
			var evaluationStep = math.min(step, 1f / 1024f);
			if (depth < MaximumAdaptiveDepth
				&& WireRailSplineGeometry.TryEvaluateRailFrame(spline, segments,
					evaluationContext, segmentIndex, railIndex, start, evaluationStep,
					out var startFrame)
				&& WireRailSplineGeometry.TryEvaluateRailFrame(spline, segments,
					evaluationContext, segmentIndex, railIndex, end, evaluationStep,
					out var endFrame)
				&& WireRailSplineGeometry.TryEvaluate(spline, segmentIndex, start,
					out var startMainFrame)
				&& WireRailSplineGeometry.TryEvaluate(spline, segmentIndex, end,
					out var endMainFrame)
				&& math.dot(startFrame.Tangent, startMainFrame.Tangent) > 0f
				&& math.dot(endFrame.Tangent, endMainFrame.Tangent) > 0f
				&& math.acos(math.clamp(math.dot(startFrame.Tangent, endFrame.Tangent),
					-1f, 1f)) > MaximumRingAngle) {
				var middle = (start + end) * 0.5f;
				SubdivideSampleInterval(spline, segments, evaluationContext,
					segmentIndex, railIndex, start, middle, depth + 1, parameters);
				SubdivideSampleInterval(spline, segments, evaluationContext,
					segmentIndex, railIndex, middle, end, depth + 1, parameters);
				return;
			}
			parameters.Add(end);
		}

		private static float3 Transport(float3 direction, float3 fromTangent,
			float3 toTangent)
		{
			var axis = math.cross(fromTangent, toTangent);
			var sinAngle = math.length(axis);
			var cosAngle = math.clamp(math.dot(fromTangent, toTangent), -1f, 1f);
			float3 transported;
			if (sinAngle > 1e-6f) {
				axis /= sinAngle;
				var rotation = quaternion.AxisAngle(axis, math.atan2(sinAngle, cosAngle));
				transported = math.mul(rotation, direction);
			} else {
				transported = direction;
			}
			var projected = transported - toTangent * math.dot(transported, toTangent);
			return math.normalizesafe(projected, direction);
		}

		private static void AppendCap(WireRailPathFrame frame, float radius,
			int radialSegments, bool start, ICollection<Vector3> vertices,
			ICollection<Vector3> normals, ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var center = frame.Position;
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
				if (!AppendSegment(spline, segments, segmentIndex, ballRadius,
						samplesPerSegment, vertices, indices, edges, out error)) {
					mesh = target;
					edgeVertices = Array.Empty<Vector3>();
					return false;
				}
			}

			mesh = target ? target : new Mesh();
			mesh.Clear(false);
			mesh.name = "Wire Rail Collider (Generated)";
			mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetTriangles(indices, 0, false);
			mesh.RecalculateBounds();
			edgeVertices = edges.ToArray();
			error = null;
			return true;
		}

		private static bool TryCreateProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, float curveT,
			float ballRadius, out WireRailChannelProfile profile, out string error)
		{
			var segment = segments[segmentIndex];
			var offsets = new Vector2[segment.RailCount];
			var wireRadii = new float[segment.RailCount];
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				offsets[railIndex] = WireRailSplineGeometry.EvaluateRailOffset(spline,
					segments, segmentIndex, railIndex, curveT);
				wireRadii[railIndex] = WireRailSplineGeometry.EvaluateWireDiameter(spline,
					segments, segmentIndex, railIndex, curveT) * 0.5f;
			}
			return WireRailChannelProfile.TryCreate(offsets, wireRadii, ballRadius,
				out profile, out error);
		}

		private static bool HasMatchingTopology(WireRailChannelProfile first,
			WireRailChannelProfile second)
		{
			if (first.IsClosed != second.IsClosed
				|| first.Vertices.Count != second.Vertices.Count
				|| first.Spans.Count != second.Spans.Count) {
				return false;
			}
			for (var spanIndex = 0; spanIndex < first.Spans.Count; spanIndex++) {
				var firstSpan = first.Spans[spanIndex];
				var secondSpan = second.Spans[spanIndex];
				if (firstSpan.StartVertex != secondSpan.StartVertex
					|| firstSpan.EndVertex != secondSpan.EndVertex) {
					return false;
				}
			}
			return true;
		}

		private static bool AppendSegment(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, float ballRadius,
			int samplesPerSegment, ICollection<Vector3> vertices,
			ICollection<int> indices, ICollection<Vector3> edges, out string error)
		{
			var firstRow = vertices.Count;
			WireRailChannelProfile referenceProfile = null;
			for (var sampleIndex = 0; sampleIndex <= samplesPerSegment; sampleIndex++) {
				var curveT = sampleIndex / (float)samplesPerSegment;
				if (!WireRailSplineGeometry.TryEvaluate(spline, segmentIndex, curveT,
						out var frame)) {
					error = $"Could not evaluate spline segment {segmentIndex + 1}.";
					return false;
				}
				if (!TryCreateProfile(spline, segments, segmentIndex, curveT, ballRadius,
						out var profile, out error)) {
					return false;
				}
				if (referenceProfile == null) {
					referenceProfile = profile;
				} else if (!HasMatchingTopology(referenceProfile, profile)) {
					error = $"The collider profile changes topology while blending segment "
						+ $"{segmentIndex + 1}. Adjust the segment connection or rail layout.";
					return false;
				}
				foreach (var offset in profile.Vertices) {
					vertices.Add((Vector3)frame.TransformOffset(offset));
				}
			}

			var rowSize = referenceProfile.Vertices.Count;
			for (var sampleIndex = 0; sampleIndex < samplesPerSegment; sampleIndex++) {
				var currentRow = firstRow + sampleIndex * rowSize;
				var nextRow = currentRow + rowSize;
				foreach (var span in referenceProfile.Spans) {
					AppendTwoSidedQuad(currentRow + span.StartVertex,
						nextRow + span.StartVertex, currentRow + span.EndVertex,
						nextRow + span.EndVertex, indices);
				}
				for (var profileVertex = 0; profileVertex < rowSize; profileVertex++) {
					edges.Add(GetVertex(vertices, currentRow + profileVertex));
					edges.Add(GetVertex(vertices, nextRow + profileVertex));
				}
			}
			error = null;
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
