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

		internal static bool TryEvaluateLayout(Spline spline,
			IReadOnlyList<WireRailSegment> layouts, int layoutIndex, float layoutT,
			out WireRailPathFrame frame)
		{
			frame = default;
			if (spline == null || layouts == null || layoutIndex < 0
				|| layoutIndex >= layouts.Count || spline.Count < 2) {
				return false;
			}
			var splineLength = spline.GetLength();
			var startDistance = math.clamp(layouts[layoutIndex].Distance, 0f, splineLength);
			var endDistance = layoutIndex + 1 < layouts.Count
				? math.clamp(layouts[layoutIndex + 1].Distance, startDistance, splineLength)
				: splineLength;
			return TryEvaluateDistance(spline,
				math.lerp(startDistance, endDistance, math.saturate(layoutT)), out frame);
		}

		public static bool TryEvaluateLayoutPosition(Spline spline,
			IReadOnlyList<WireRailSegment> layouts, int layoutIndex, float layoutT,
			out float3 position)
		{
			position = default;
			if (!TryEvaluateLayout(spline, layouts, layoutIndex, layoutT, out var frame)) {
				return false;
			}
			position = frame.Position;
			return true;
		}

		internal static bool TryEvaluateDistance(Spline spline, float distance,
			out WireRailPathFrame frame)
		{
			frame = default;
			if (spline == null || spline.Count < 2) {
				return false;
			}
			var normalizedT = spline.ConvertIndexUnit(
				math.clamp(distance, 0f, spline.GetLength()), PathIndexUnit.Distance,
				PathIndexUnit.Normalized);
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

		public static bool TryEvaluateBrace(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailBraceFixture brace,
			out float3 center, out float3 tangent, out float3 right, out float3 up,
			out float radius)
		{
			center = default;
			tangent = default;
			right = default;
			up = default;
			radius = 0f;
			if (!WireRailFixtureMeshGenerator.TryEvaluateBraceProfile(spline, segments,
					brace, out var profile)) {
				return false;
			}
			center = profile.Center;
			tangent = profile.Frame.Tangent;
			right = profile.Frame.Right;
			up = profile.Frame.Up;
			radius = profile.Radius;
			return true;
		}

		public static bool TryEvaluateCrossWire(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailCrossWireFixture crossWire,
			out float3 start, out float3 end)
		{
			start = default;
			end = default;
			if (!WireRailFixtureMeshGenerator.TryEvaluateCrossWireProfile(spline,
					segments, crossWire, out var profile)) {
				return false;
			}
			start = profile.Start;
			end = profile.End;
			return true;
		}

		public static bool TryEvaluateLeg(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailLegFixture leg,
			out IReadOnlyList<float3> centerlinePoints)
		{
			centerlinePoints = Array.Empty<float3>();
			if (!WireRailFixtureMeshGenerator.TryEvaluateLegProfile(spline, segments,
					leg, out var profile)) {
				return false;
			}
			centerlinePoints = profile.CombinedPath;
			return true;
		}

		public static float2 EvaluateRailOffset(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			float curveT)
		{
			var current = (float2)segments[segmentIndex].GetRailOffset(railIndex);
			var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
			if (!IsContinuousBoundary(segments, segmentIndex, nextSegmentIndex, railIndex)) {
				return current;
			}
			var next = (float2)segments[nextSegmentIndex].GetRailOffset(railIndex);
			var transition = segments[segmentIndex].ConnectionToNext
				.EvaluateWireTransition(railIndex, curveT);
			return math.lerp(current, next, transition);
		}

		public static float EvaluateWireDiameter(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			float curveT)
		{
			var current = segments[segmentIndex].GetWireDiameter(railIndex);
			var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
			if (!IsContinuousBoundary(segments, segmentIndex, nextSegmentIndex, railIndex)) {
				return current;
			}
			var next = segments[nextSegmentIndex].GetWireDiameter(railIndex);
			var transition = segments[segmentIndex].ConnectionToNext
				.EvaluateWireTransition(railIndex, curveT);
			return math.lerp(current, next, transition);
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
					|| !TryEvaluateLayout(spline, segments, previousSegmentIndex, 1f,
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
						|| !TryEvaluateLayout(spline, segments, nextSegmentIndex, 0f,
							out var nextMainFrame)) {
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
				&& RequiresRailTangentSmoothing(spline, segments, previousSegmentIndex,
					segmentIndex, railIndex)) {
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
				&& RequiresRailTangentSmoothing(spline, segments, segmentIndex,
					nextSegmentIndex, railIndex)) {
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
			if (!TryEvaluateLayout(spline, segments, segmentIndex, curveT, out mainFrame)) {
				return false;
			}

			var offset = EvaluateRailOffset(spline, segments, segmentIndex, railIndex, curveT);
			if (HasConstantRailOffset(spline, segments, segmentIndex, railIndex)) {
				center = mainFrame.TransformOffset(offset);
				return true;
			}
			if (!TryEvaluateLayout(spline, segments, segmentIndex, 0f, out var startFrame)
				|| !TryEvaluateLayout(spline, segments, segmentIndex, 1f, out var endFrame)) {
				return false;
			}
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
				&& segments[sourceSegmentIndex].IsRailActive(railIndex)
				&& segments[nextSegmentIndex].IsRailActive(railIndex)
				&& segments[sourceSegmentIndex].ConnectionToNext.IsWireContinuous(railIndex);

		private static bool RequiresRailTangentSmoothing(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int sourceSegmentIndex,
			int nextSegmentIndex, int railIndex)
			=> IsContinuousBoundary(segments, sourceSegmentIndex, nextSegmentIndex, railIndex)
				&& !(HasConstantRailOffset(spline, segments, sourceSegmentIndex, railIndex)
					&& HasConstantRailOffset(spline, segments, nextSegmentIndex, railIndex)
					&& RailOffsetsMatch(segments[sourceSegmentIndex],
						segments[nextSegmentIndex], railIndex));

		private static bool HasConstantRailOffset(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex)
		{
			var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments, segmentIndex);
			if (IsContinuousBoundary(segments, previousSegmentIndex, segmentIndex, railIndex)
				&& !RailOffsetsMatch(segments[previousSegmentIndex], segments[segmentIndex],
					railIndex)) {
				return false;
			}
			var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
			return !IsContinuousBoundary(segments, segmentIndex, nextSegmentIndex, railIndex)
				|| RailOffsetsMatch(segments[segmentIndex], segments[nextSegmentIndex], railIndex);
		}

		private static bool RailOffsetsMatch(WireRailSegment first,
			WireRailSegment second, int railIndex)
		{
			var delta = (float2)first.GetRailOffset(railIndex)
				- (float2)second.GetRailOffset(railIndex);
			return math.lengthsq(delta) <= 1e-8f;
		}

		private static bool IsRailConnectedBoundary(
			IReadOnlyList<WireRailSegment> segments, int sourceSegmentIndex,
			int nextSegmentIndex, int railIndex)
		{
			if (sourceSegmentIndex < 0 || nextSegmentIndex < 0
				|| sourceSegmentIndex >= segments.Count || nextSegmentIndex >= segments.Count
				|| segments[sourceSegmentIndex].RailCount <= railIndex
				|| segments[nextSegmentIndex].RailCount <= railIndex
				|| !segments[sourceSegmentIndex].IsRailActive(railIndex)
				|| !segments[nextSegmentIndex].IsRailActive(railIndex)) {
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
			IReadOnlyList<WireRailFixture> fixtures, float wireCapBevelSize,
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
					if (!segment.IsRailActive(railIndex)) {
						previousFrames.Remove(railIndex);
						continue;
					}
					WireRailPathFrame? previousSegmentFrame = null;
					if (segmentIndex > 0
						&& WireRailSplineGeometry.IsRailConnectedAtStart(spline, segments,
							segmentIndex, railIndex)
						&& previousFrames.TryGetValue(railIndex, out var previousFrame)) {
						previousSegmentFrame = previousFrame;
					}
					if (AppendTube(spline, segments, evaluationContext, segmentIndex, railIndex,
						samplesPerSegment, radialSegments, wireCapBevelSize,
						previousSegmentFrame,
						vertices, normals, uvs, indices, out var lastFrame)) {
						previousFrames[railIndex] = lastFrame;
					} else {
						previousFrames.Remove(railIndex);
					}
				}
			}
			WireRailFixtureMeshGenerator.Append(spline, segments, fixtures,
				wireCapBevelSize, radialSegments, vertices, normals, uvs, indices);

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
			int radialSegments, float capBevelSize, WireRailPathFrame? previousSegmentFrame,
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
			var capStart = !WireRailSplineGeometry.IsRailConnectedAtStart(spline, segments,
				segmentIndex, railIndex);
			var capEnd = !WireRailSplineGeometry.IsRailConnectedAtEnd(spline, segments,
				segmentIndex, railIndex);
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
				var tubeFrame = frame;
				var clampedBevel = math.clamp(capBevelSize, 0f, radius);
				if (clampedBevel > 1e-5f && sampleIndex == 0 && capStart) {
					tubeFrame = new WireRailPathFrame(frame.Position + frame.Tangent * clampedBevel,
						frame.Tangent, frame.Right, frame.Up);
				} else if (clampedBevel > 1e-5f
					&& sampleIndex == sampleParameters.Count - 1 && capEnd) {
					tubeFrame = new WireRailPathFrame(frame.Position - frame.Tangent * clampedBevel,
						frame.Tangent, frame.Right, frame.Up);
				}
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var angle = math.PI * 2f * radialIndex / radialSegments;
					var radial = tubeFrame.Right * math.cos(angle) + tubeFrame.Up * math.sin(angle);
					vertices.Add((Vector3)(tubeFrame.Position + radial * radius));
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

			if (capStart) {
				WireRailCapMeshGenerator.Append(firstFrame, firstRadius, capBevelSize,
					radialSegments, true,
					vertices, normals, uvs, indices);
			}
			if (capEnd) {
				WireRailCapMeshGenerator.Append(lastFrame, lastRadius, capBevelSize,
					radialSegments, false,
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
				&& WireRailSplineGeometry.TryEvaluateLayout(spline, segments, segmentIndex, start,
					out var startMainFrame)
				&& WireRailSplineGeometry.TryEvaluateLayout(spline, segments, segmentIndex, end,
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

	}

	internal readonly struct WireRailBraceProfile
	{
		public readonly WireRailPathFrame Frame;
		public readonly float2 CenterOffset;
		public readonly float BaseRadius;
		public readonly float Radius;

		public WireRailBraceProfile(WireRailPathFrame frame, float2 centerOffset,
			float baseRadius, float radius)
		{
			Frame = frame;
			CenterOffset = centerOffset;
			BaseRadius = baseRadius;
			Radius = radius;
		}

		public float3 Center => Frame.TransformOffset(CenterOffset);

		public float3 GetCenterlinePosition(float angle)
			=> Frame.TransformOffset(CenterOffset + new float2(math.cos(angle),
				math.sin(angle)) * Radius);
	}

	internal readonly struct WireRailCrossWireProfile
	{
		public readonly WireRailPathFrame Frame;
		public readonly float2 StartRailOffset;
		public readonly float2 EndRailOffset;
		public readonly float StartRailRadius;
		public readonly float EndRailRadius;
		public readonly float2 RotationOriginOffset;
		public readonly float2 StartOffset;
		public readonly float2 EndOffset;

		public WireRailCrossWireProfile(WireRailPathFrame frame, float2 startRailOffset,
			float2 endRailOffset, float startRailRadius, float endRailRadius,
			float2 rotationOriginOffset, float2 startOffset, float2 endOffset)
		{
			Frame = frame;
			StartRailOffset = startRailOffset;
			EndRailOffset = endRailOffset;
			StartRailRadius = startRailRadius;
			EndRailRadius = endRailRadius;
			RotationOriginOffset = rotationOriginOffset;
			StartOffset = startOffset;
			EndOffset = endOffset;
		}

		public float3 Start => Frame.TransformOffset(StartOffset);
		public float3 End => Frame.TransformOffset(EndOffset);
	}

	internal readonly struct WireRailLegProfile
	{
		public readonly WireRailCrossWireProfile AttachmentProfile;
		public readonly IReadOnlyList<float3> LegPoints;
		public readonly IReadOnlyList<float3> FootPoints;
		public readonly IReadOnlyList<float3> CombinedPath;

		public WireRailLegProfile(WireRailCrossWireProfile attachmentProfile,
			IReadOnlyList<float3> legPoints, IReadOnlyList<float3> footPoints,
			IReadOnlyList<float3> combinedPath)
		{
			AttachmentProfile = attachmentProfile;
			LegPoints = legPoints;
			FootPoints = footPoints;
			CombinedPath = combinedPath;
		}
	}

	internal static class WireRailFixtureMeshGenerator
	{
		private const float FullTurn = math.PI * 2f;
		private const float LegCornerRadiusDiameterRatio = 1f;
		private const float LegCornerMaxAngleStep = math.PI / 12f;
		private const float LegCornerMaxSpanFraction = 0.45f;

		public static void Append(Spline spline, IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, float wireCapBevelSize,
			int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			if (fixtures == null) {
				return;
			}
			foreach (var fixture in fixtures) {
				if (fixture is WireRailBraceFixture brace
					&& TryEvaluateBraceProfile(spline, segments, brace, out var profile)) {
					AppendBrace(profile, brace, wireCapBevelSize, radialSegments, vertices,
						normals, uvs, indices);
				} else if (fixture is WireRailCrossWireFixture crossWire
					&& TryEvaluateCrossWireProfile(spline, segments, crossWire,
						out var crossWireProfile)) {
					AppendCrossWire(crossWireProfile, crossWire, wireCapBevelSize,
						radialSegments, vertices, normals, uvs, indices);
				} else if (fixture is WireRailLegFixture leg
					&& TryEvaluateLegProfile(spline, segments, leg, out var legProfile)) {
					AppendLeg(legProfile, leg, wireCapBevelSize, radialSegments,
						vertices, normals, uvs, indices);
				}
			}
		}

		internal static bool TryEvaluateBraceProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailBraceFixture brace,
			out WireRailBraceProfile profile)
		{
			profile = default;
			if (brace == null || !TryGetSplineLocation(spline, segments, brace.Distance,
					out var segmentIndex, out var curveT, out var frame)) {
				return false;
			}
			var segment = segments[segmentIndex];
			var activeRailIndices = Enumerable.Range(0, segment.RailCount)
				.Where(segment.IsRailActive).ToArray();
			if (activeRailIndices.Length == 0) {
				return false;
			}

			var railOffsets = new float2[activeRailIndices.Length];
			var railRadii = new float[activeRailIndices.Length];
			var minimum = new float2(float.PositiveInfinity);
			var maximum = new float2(float.NegativeInfinity);
			var evaluationContext = new WireRailPathEvaluationContext();
			for (var activeRailIndex = 0; activeRailIndex < activeRailIndices.Length;
				activeRailIndex++) {
				var railIndex = activeRailIndices[activeRailIndex];
				if (!WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments,
						evaluationContext, segmentIndex, railIndex, curveT,
						out var railPosition)) {
					return false;
				}
				var relative = railPosition - frame.Position;
				var offset = new float2(math.dot(relative, frame.Right),
					math.dot(relative, frame.Up));
				var radius = WireRailSplineGeometry.EvaluateWireDiameter(spline, segments,
					segmentIndex, railIndex, curveT) * 0.5f;
				railOffsets[activeRailIndex] = offset;
				railRadii[activeRailIndex] = radius;
				minimum = math.min(minimum, offset - radius);
				maximum = math.max(maximum, offset + radius);
			}

			var automaticCenterOffset = (minimum + maximum) * 0.5f;
			var envelopeRadius = 0f;
			for (var railIndex = 0; railIndex < railOffsets.Length; railIndex++) {
				envelopeRadius = math.max(envelopeRadius,
					math.distance(railOffsets[railIndex], automaticCenterOffset)
						+ railRadii[railIndex]);
			}
			var centerOffset = automaticCenterOffset
				+ new float2(brace.LateralOffset, brace.VerticalOffset);
			var tubeRadius = brace.Diameter * 0.5f;
			var baseRadius = envelopeRadius + tubeRadius;
			profile = new WireRailBraceProfile(frame, centerOffset, baseRadius,
				math.max(tubeRadius, baseRadius * brace.Scale));
			return true;
		}

		internal static bool TryEvaluateCrossWireProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailCrossWireFixture crossWire,
			out WireRailCrossWireProfile profile)
		{
			profile = default;
			return crossWire != null && TryEvaluateCrossWireProfile(spline, segments,
				crossWire.Distance, crossWire.StartRailIndex, crossWire.EndRailIndex,
				crossWire.Angle, crossWire.LateralOffset, crossWire.VerticalOffset,
				crossWire.LengthAdjustment, out profile);
		}

		private static bool TryEvaluateCrossWireProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, float distance, int startRailIndex,
			int endRailIndex, float angleDegrees, float lateralOffset,
			float verticalOffset, float lengthAdjustment,
			out WireRailCrossWireProfile profile)
		{
			profile = default;
			if (!TryGetSplineLocation(spline, segments, distance,
					out var segmentIndex, out var curveT, out var frame)) {
				return false;
			}
			var segment = segments[segmentIndex];
			if (startRailIndex == endRailIndex || startRailIndex >= segment.RailCount
				|| endRailIndex >= segment.RailCount
				|| !segment.IsRailActive(startRailIndex)
				|| !segment.IsRailActive(endRailIndex)) {
				return false;
			}

			var evaluationContext = new WireRailPathEvaluationContext();
			var startRailOffset = default(float2);
			var endRailOffset = default(float2);
			var startRailRadius = 0f;
			var endRailRadius = 0f;
			var envelopeMinimum = new float2(float.PositiveInfinity);
			var envelopeMaximum = new float2(float.NegativeInfinity);
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (!segment.IsRailActive(railIndex)) {
					continue;
				}
				if (!TryEvaluateRailOffset(railIndex, out var railOffset,
						out var railRadius)) {
					return false;
				}
				if (railIndex == startRailIndex) {
					startRailOffset = railOffset;
					startRailRadius = railRadius;
				}
				if (railIndex == endRailIndex) {
					endRailOffset = railOffset;
					endRailRadius = railRadius;
				}
				envelopeMinimum = math.min(envelopeMinimum, railOffset - railRadius);
				envelopeMaximum = math.max(envelopeMaximum, railOffset + railRadius);
			}
			var railDirection = math.normalizesafe(endRailOffset - startRailOffset,
				new float2(1f, 0f));
			var attachmentStart = startRailOffset + railDirection * startRailRadius;
			var attachmentEnd = endRailOffset - railDirection * endRailRadius;
			var rotationOriginOffset = (envelopeMinimum + envelopeMaximum) * 0.5f;
			var angle = math.radians(angleDegrees);
			var direction = new float2(math.cos(angle), math.sin(angle));
			var bottomCenter = (attachmentStart + attachmentEnd) * 0.5f;
			var relativeBottomCenter = bottomCenter - rotationOriginOffset;
			var rotatedBottomCenter = rotationOriginOffset + new float2(
				relativeBottomCenter.x * direction.x
					- relativeBottomCenter.y * direction.y,
				relativeBottomCenter.x * direction.y
					+ relativeBottomCenter.y * direction.x);
			var center = rotatedBottomCenter
				+ new float2(lateralOffset, verticalOffset);
			var length = math.max(0.1f, math.distance(attachmentStart, attachmentEnd)
				+ lengthAdjustment);
			var startOffset = center - direction * length * 0.5f;
			var endOffset = center + direction * length * 0.5f;
			profile = new WireRailCrossWireProfile(frame, startRailOffset, endRailOffset,
				startRailRadius, endRailRadius, rotationOriginOffset, startOffset, endOffset);
			return true;

			bool TryEvaluateRailOffset(int railIndex, out float2 railOffset,
				out float railRadius)
			{
				railOffset = default;
				railRadius = 0f;
				if (!WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments,
						evaluationContext, segmentIndex, railIndex, curveT,
						out var railPosition)) {
					return false;
				}
				var relative = railPosition - frame.Position;
				railOffset = new float2(math.dot(relative, frame.Right),
					math.dot(relative, frame.Up));
				railRadius = WireRailSplineGeometry.EvaluateWireDiameter(spline, segments,
					segmentIndex, railIndex, curveT) * 0.5f;
				return true;
			}
		}

		internal static bool TryEvaluateLegProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailLegFixture leg,
			out WireRailLegProfile profile)
		{
			profile = default;
			if (leg == null || !TryEvaluateCrossWireProfile(spline, segments,
					leg.Distance, 0, 1, 0f, leg.LateralOffset, leg.VerticalOffset,
					leg.LengthAdjustment,
					out var attachmentProfile)) {
				return false;
			}

			var frame = attachmentProfile.Frame;
			var legStart = leg.LegSide == WireRailLegSide.Left
				? attachmentProfile.Start : attachmentProfile.End;
			var authoredDirection = (float3)leg.StartDirection;
			var startDirection = math.normalizesafe(
				frame.Right * authoredDirection.x
				+ frame.Tangent * authoredDirection.y
				+ frame.Up * authoredDirection.z, -frame.Up);
			var elbow = legStart + startDirection * leg.StartLength;

			var footRotation = quaternion.EulerXYZ(math.radians((float3)leg.FootRotation));
			var footPosition = (float3)leg.FootPosition;
			var localFootPoints = BuildUHookPoints(leg.FootWidth, leg.FootLength,
				leg.FootConnectionLength, WireRailLegFixture.FootBendSegments);
			var footPoints = new float3[localFootPoints.Count];
			for (var pointIndex = 0; pointIndex < localFootPoints.Count; pointIndex++) {
				var local = footPosition + math.mul(footRotation, localFootPoints[pointIndex]);
				footPoints[pointIndex] = legStart + frame.Right * local.x
					+ frame.Tangent * local.y + frame.Up * local.z;
			}

			var legPoints = new List<float3> { legStart };
			AddDistinct(legPoints, elbow);
			AddDistinct(legPoints, footPoints[0]);
			var oppositeAttachmentEnd = leg.LegSide == WireRailLegSide.Left
				? attachmentProfile.End : attachmentProfile.Start;
			var combinedPath = new List<float3>(legPoints.Count + footPoints.Length + 1) {
				oppositeAttachmentEnd,
			};
			foreach (var legPoint in legPoints) {
				AddDistinct(combinedPath, legPoint);
			}
			var lastLegCornerIndex = combinedPath.Count - 1;
			for (var pointIndex = 1; pointIndex < footPoints.Length; pointIndex++) {
				AddDistinct(combinedPath, footPoints[pointIndex]);
			}
			if (combinedPath.Count < 2) {
				return false;
			}
			combinedPath = BuildRoundedLegPath(combinedPath, lastLegCornerIndex,
				leg.Diameter * LegCornerRadiusDiameterRatio);
			if (combinedPath == null) {
				return false;
			}
			profile = new WireRailLegProfile(attachmentProfile, legPoints,
				footPoints, combinedPath);
			return true;

			static void AddDistinct(ICollection<float3> points, float3 point)
			{
				if (points is List<float3> list && list.Count > 0
					&& math.distancesq(list[^1], point) <= 1e-10f) {
					return;
				}
				points.Add(point);
			}
		}

		private static List<float3> BuildRoundedLegPath(IReadOnlyList<float3> points,
			int lastCornerIndex, float desiredRadius)
		{
			var rounded = new List<float3>(points.Count + math.max(0, lastCornerIndex) * 6);
			AddDistinct(rounded, points[0]);
			var finalRoundedCorner = math.min(lastCornerIndex, points.Count - 2);
			for (var pointIndex = 1; pointIndex < points.Count - 1; pointIndex++) {
				if (IsPathReversal(points[pointIndex - 1], points[pointIndex],
						points[pointIndex + 1])
					|| (pointIndex <= finalRoundedCorner && IsCornerTooTight(
						points[pointIndex - 1], points[pointIndex], points[pointIndex + 1],
						desiredRadius * 0.5f))) {
					return null;
				}
				if (pointIndex > finalRoundedCorner || !TryAppendRoundedCorner(
						points[pointIndex - 1], points[pointIndex], points[pointIndex + 1],
						desiredRadius, rounded)) {
					AddDistinct(rounded, points[pointIndex]);
				}
			}
			AddDistinct(rounded, points[^1]);
			return rounded;

			static bool IsCornerTooTight(float3 previous, float3 corner, float3 next,
				float minimumRadius)
			{
				var incoming = corner - previous;
				var outgoing = next - corner;
				var incomingLength = math.length(incoming);
				var outgoingLength = math.length(outgoing);
				if (incomingLength <= 1e-5f || outgoingLength <= 1e-5f) {
					return false;
				}
				var cornerAngle = math.acos(math.clamp(math.dot(incoming / incomingLength,
					outgoing / outgoingLength), -1f, 1f));
				if (cornerAngle <= math.radians(0.5f)) {
					return false;
				}
				var tangentScale = math.tan(cornerAngle * 0.5f);
				var maximumRadius = math.min(incomingLength, outgoingLength)
					* LegCornerMaxSpanFraction / tangentScale;
				return maximumRadius + 1e-5f < minimumRadius;
			}

			static bool TryAppendRoundedCorner(float3 previous, float3 corner, float3 next,
				float desiredRadius, List<float3> target)
			{
				var incoming = corner - previous;
				var outgoing = next - corner;
				var incomingLength = math.length(incoming);
				var outgoingLength = math.length(outgoing);
				if (incomingLength <= 1e-5f || outgoingLength <= 1e-5f) {
					return false;
				}
				var incomingDirection = incoming / incomingLength;
				var outgoingDirection = outgoing / outgoingLength;
				var cornerAngle = math.acos(math.clamp(
					math.dot(incomingDirection, outgoingDirection), -1f, 1f));
				if (cornerAngle <= math.radians(0.5f)
					|| cornerAngle >= math.PI - math.radians(0.5f)) {
					return false;
				}
				var tangentScale = math.tan(cornerAngle * 0.5f);
				var tangentDistance = math.min(
					math.max(0.05f, desiredRadius) * tangentScale,
					math.min(incomingLength, outgoingLength) * LegCornerMaxSpanFraction);
				if (tangentDistance <= 1e-5f || tangentScale <= 1e-5f) {
					return false;
				}
				var radius = tangentDistance / tangentScale;
				var bendNormal = math.normalizesafe(
					math.cross(incomingDirection, outgoingDirection));
				if (math.lengthsq(bendNormal) <= 1e-8f) {
					return false;
				}
				var start = corner - incomingDirection * tangentDistance;
				var end = corner + outgoingDirection * tangentDistance;
				var center = start + math.cross(bendNormal, incomingDirection) * radius;
				var startRadius = start - center;
				var segmentCount = math.max(2,
					(int)math.ceil(cornerAngle / LegCornerMaxAngleStep));
				AddDistinct(target, start);
				for (var segmentIndex = 1; segmentIndex < segmentCount; segmentIndex++) {
					var angle = cornerAngle * segmentIndex / segmentCount;
					AddDistinct(target, center + math.mul(
						quaternion.AxisAngle(bendNormal, angle), startRadius));
				}
				AddDistinct(target, end);
				return true;
			}

			static void AddDistinct(ICollection<float3> target, float3 point)
			{
				if (target is List<float3> list && list.Count > 0
					&& math.distancesq(list[^1], point) <= 1e-10f) {
					return;
				}
				target.Add(point);
			}
		}

		private static List<float3> BuildUHookPoints(float width, float armLength,
			float connectionArmLength, int bendSegments)
		{
			var radius = math.max(0.05f, width * 0.5f);
			armLength = math.max(0f, armLength);
			connectionArmLength = math.max(0f, connectionArmLength);
			bendSegments = math.max(2, bendSegments);
			var arcCenterY = -armLength * 0.5f + radius * 0.5f;
			var openEndY = armLength * 0.5f + radius * 0.5f;
			var points = new List<float3>(bendSegments + 3) {
				new(-radius, arcCenterY + connectionArmLength, 0f),
				new(-radius, arcCenterY, 0f),
			};
			for (var segmentIndex = 1; segmentIndex <= bendSegments; segmentIndex++) {
				var angle = math.PI + math.PI * segmentIndex / bendSegments;
				points.Add(new float3(math.cos(angle) * radius,
					arcCenterY + math.sin(angle) * radius, 0f));
			}
			points.Add(new float3(radius, openEndY, 0f));
			return points;
		}

		private static bool TryGetSplineLocation(Spline spline,
			IReadOnlyList<WireRailSegment> segments, float distance,
			out int segmentIndex, out float curveT, out WireRailPathFrame frame)
		{
			segmentIndex = 0;
			curveT = 0f;
			frame = default;
			if (spline == null || segments == null || segments.Count == 0
				|| spline.Count < 2) {
				return false;
			}

			var length = spline.GetLength();
			var clampedDistance = math.clamp(distance, 0f, math.max(0f, length));
			segmentIndex = segments.Count - 1;
			for (var layoutIndex = 1; layoutIndex < segments.Count; layoutIndex++) {
				if (clampedDistance < segments[layoutIndex].Distance) {
					segmentIndex = layoutIndex - 1;
					break;
				}
			}
			var startDistance = segments[segmentIndex].Distance;
			var endDistance = segmentIndex + 1 < segments.Count
				? segments[segmentIndex + 1].Distance
				: length;
			curveT = endDistance > startDistance
				? math.saturate((clampedDistance - startDistance) / (endDistance - startDistance))
				: 0f;
			return WireRailSplineGeometry.TryEvaluateDistance(spline, clampedDistance,
				out frame);
		}

		private static void AppendBrace(WireRailBraceProfile profile,
			WireRailBraceFixture brace, float capBevelSize, int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			if (!brace.TryGetVisibleArc(out var startAngle, out var sweepAngle,
					out var closed)) {
				return;
			}
			var longitudinalSegments = math.max(2,
				(int)math.ceil(brace.RingDensity * sweepAngle / FullTurn));
			var angles = BuildBraceAngles(brace, startAngle, sweepAngle, closed,
				longitudinalSegments);
			var ringCount = angles.Count;
			var firstRing = vertices.Count;
			var firstFrame = default(WireRailPathFrame);
			var lastFrame = default(WireRailPathFrame);
			var tubeRadius = brace.Diameter * 0.5f;
			var clampedBevel = math.clamp(capBevelSize, 0f, tubeRadius);
			for (var ringIndex = 0; ringIndex < ringCount; ringIndex++) {
				var angle = angles[ringIndex];
				var centerlineOffset = brace.EvaluateCenterlineOffset(angle, profile.Radius);
				var tangentOffset = brace.EvaluateCenterlineTangent(angle);
				var tangent = math.normalizesafe(profile.Frame.Right * tangentOffset.x
					+ profile.Frame.Up * tangentOffset.y, profile.Frame.Up);
				var outwardOffset = math.normalizesafe(
					new float2(tangentOffset.y, -tangentOffset.x), new float2(1f, 0f));
				if (math.dot(outwardOffset, centerlineOffset) < 0f) {
					outwardOffset = -outwardOffset;
				}
				var outward = math.normalizesafe(profile.Frame.Right * outwardOffset.x
					+ profile.Frame.Up * outwardOffset.y, profile.Frame.Right);
				var up = math.normalizesafe(math.cross(outward, tangent),
					-profile.Frame.Tangent);
				var capFrame = new WireRailPathFrame(
					profile.Frame.TransformOffset(profile.CenterOffset + centerlineOffset),
					tangent, outward, up);
				if (ringIndex == 0) {
					firstFrame = capFrame;
				}
				lastFrame = capFrame;
				var tubeFrame = capFrame;
				if (!closed && clampedBevel > 1e-5f && ringIndex == 0) {
					tubeFrame = new WireRailPathFrame(
						capFrame.Position + capFrame.Tangent * clampedBevel,
						capFrame.Tangent, capFrame.Right, capFrame.Up);
				} else if (!closed && clampedBevel > 1e-5f
					&& ringIndex == ringCount - 1) {
					tubeFrame = new WireRailPathFrame(
						capFrame.Position - capFrame.Tangent * clampedBevel,
						capFrame.Tangent, capFrame.Right, capFrame.Up);
				}
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var radialAngle = FullTurn * radialIndex / radialSegments;
					var radial = tubeFrame.Right * math.cos(radialAngle)
						+ tubeFrame.Up * math.sin(radialAngle);
					vertices.Add((Vector3)(tubeFrame.Position + radial * tubeRadius));
					normals.Add((Vector3)radial);
					uvs.Add(new Vector2((angle - startAngle) / sweepAngle,
						radialIndex / (float)radialSegments));
				}
			}

			var ringPairCount = closed ? ringCount : ringCount - 1;
			for (var ringIndex = 0; ringIndex < ringPairCount; ringIndex++) {
				var current = firstRing + ringIndex * radialSegments;
				var next = firstRing + ((ringIndex + 1) % ringCount) * radialSegments;
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

			if (!closed) {
				WireRailCapMeshGenerator.Append(firstFrame, tubeRadius, capBevelSize,
					radialSegments, true, vertices, normals, uvs, indices);
				WireRailCapMeshGenerator.Append(lastFrame, tubeRadius, capBevelSize,
					radialSegments, false, vertices, normals, uvs, indices);
			}
		}

		private static void AppendCrossWire(WireRailCrossWireProfile profile,
			WireRailCrossWireFixture crossWire, float capBevelSize, int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var start = profile.Start;
			var end = profile.End;
			var length = math.distance(start, end);
			if (length <= 1e-5f) {
				return;
			}
			var tangent = (end - start) / length;
			var right = math.normalizesafe(profile.Frame.Tangent,
				new float3(0f, 1f, 0f));
			var up = math.normalizesafe(math.cross(right, tangent), profile.Frame.Up);
			var startFrame = new WireRailPathFrame(start, tangent, right, up);
			var endFrame = new WireRailPathFrame(end, tangent, right, up);
			var tubeRadius = crossWire.Diameter * 0.5f;
			var bevel = math.min(math.clamp(capBevelSize, 0f, tubeRadius), length * 0.5f);
			var bodyStart = start + tangent * bevel;
			var bodyEnd = end - tangent * bevel;
			if (math.distancesq(bodyStart, bodyEnd) > 1e-10f) {
				var firstRing = vertices.Count;
				for (var ringIndex = 0; ringIndex < 2; ringIndex++) {
					var position = ringIndex == 0 ? bodyStart : bodyEnd;
					for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
						var radialAngle = FullTurn * radialIndex / radialSegments;
						var radial = right * math.cos(radialAngle)
							+ up * math.sin(radialAngle);
						vertices.Add((Vector3)(position + radial * tubeRadius));
						normals.Add((Vector3)radial);
						uvs.Add(new Vector2(ringIndex,
							radialIndex / (float)radialSegments));
					}
				}
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var radialNext = (radialIndex + 1) % radialSegments;
					var a = firstRing + radialIndex;
					var b = firstRing + radialSegments + radialIndex;
					var c = firstRing + radialNext;
					var d = firstRing + radialSegments + radialNext;
					indices.Add(a);
					indices.Add(b);
					indices.Add(d);
					indices.Add(a);
					indices.Add(d);
					indices.Add(c);
				}
			}
			WireRailCapMeshGenerator.Append(startFrame, tubeRadius, bevel,
				radialSegments, true, vertices, normals, uvs, indices);
			WireRailCapMeshGenerator.Append(endFrame, tubeRadius, bevel,
				radialSegments, false, vertices, normals, uvs, indices);
		}

		private static void AppendLeg(WireRailLegProfile profile,
			WireRailLegFixture leg, float capBevelSize, int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			AppendPolylineTube(profile.CombinedPath, profile.AttachmentProfile.Frame,
				leg.Diameter, capBevelSize, radialSegments,
				vertices, normals, uvs, indices);
		}

		internal static void AppendPolylineTube(IReadOnlyList<float3> sourcePoints,
			WireRailPathFrame referenceFrame, float diameter, float capBevelSize,
			int radialSegments, ICollection<Vector3> vertices,
			ICollection<Vector3> normals, ICollection<Vector2> uvs,
			ICollection<int> indices)
		{
			if (sourcePoints == null || sourcePoints.Count < 2) {
				return;
			}
			var points = new List<float3>(sourcePoints.Count);
			for (var pointIndex = 0; pointIndex < sourcePoints.Count; pointIndex++) {
				if (points.Count == 0
					|| math.distancesq(points[^1], sourcePoints[pointIndex]) > 1e-10f) {
					points.Add(sourcePoints[pointIndex]);
				}
			}
			if (points.Count < 2) {
				return;
			}
			for (var pointIndex = 1; pointIndex < points.Count - 1; pointIndex++) {
				if (IsPathReversal(points[pointIndex - 1], points[pointIndex],
						points[pointIndex + 1])) {
					return;
				}
			}

			var tubeRadius = math.max(0.05f, diameter * 0.5f);
			var firstSpan = math.distance(points[0], points[1]);
			var lastSpan = math.distance(points[^2], points[^1]);
			var startBevel = math.min(math.clamp(capBevelSize, 0f, tubeRadius),
				firstSpan * 0.5f);
			var endBevel = math.min(math.clamp(capBevelSize, 0f, tubeRadius),
				lastSpan * 0.5f);
			var frames = new WireRailPathFrame[points.Count];
			for (var pointIndex = 0; pointIndex < points.Count; pointIndex++) {
				var tangent = pointIndex == 0
					? math.normalizesafe(points[1] - points[0], -referenceFrame.Up)
					: pointIndex == points.Count - 1
						? math.normalizesafe(points[^1] - points[^2], -referenceFrame.Up)
						: EvaluateInteriorTangent(points, pointIndex, -referenceFrame.Up);
				float3 right;
				if (pointIndex == 0) {
					right = Project(referenceFrame.Tangent, tangent);
					if (math.lengthsq(right) <= 1e-8f) {
						right = Project(referenceFrame.Right, tangent);
					}
					right = math.normalizesafe(right, referenceFrame.Right);
				} else {
					right = Transport(frames[pointIndex - 1].Right,
						frames[pointIndex - 1].Tangent, tangent);
				}
				var up = math.normalizesafe(math.cross(right, tangent), referenceFrame.Up);
				right = math.normalizesafe(math.cross(tangent, up), right);
				frames[pointIndex] = new WireRailPathFrame(points[pointIndex], tangent,
					right, up);
			}

			var firstRing = vertices.Count;
			for (var pointIndex = 0; pointIndex < points.Count; pointIndex++) {
				var frame = frames[pointIndex];
				var position = frame.Position;
				if (pointIndex == 0) {
					position += frame.Tangent * startBevel;
				} else if (pointIndex == points.Count - 1) {
					position -= frame.Tangent * endBevel;
				}
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var angle = FullTurn * radialIndex / radialSegments;
					var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
					vertices.Add((Vector3)(position + radial * tubeRadius));
					normals.Add((Vector3)radial);
					uvs.Add(new Vector2(pointIndex / (float)(points.Count - 1),
						radialIndex / (float)radialSegments));
				}
			}

			for (var pointIndex = 0; pointIndex < points.Count - 1; pointIndex++) {
				var current = firstRing + pointIndex * radialSegments;
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

			WireRailCapMeshGenerator.Append(frames[0], tubeRadius, startBevel,
				radialSegments, true, vertices, normals, uvs, indices);
			WireRailCapMeshGenerator.Append(frames[^1], tubeRadius, endBevel,
				radialSegments, false, vertices, normals, uvs, indices);

			static float3 Project(float3 direction, float3 tangent)
				=> direction - tangent * math.dot(direction, tangent);

			static float3 EvaluateInteriorTangent(IReadOnlyList<float3> path,
				int pointIndex, float3 fallback)
			{
				var incoming = math.normalizesafe(path[pointIndex] - path[pointIndex - 1],
					fallback);
				var outgoing = math.normalizesafe(path[pointIndex + 1] - path[pointIndex],
					incoming);
				return math.normalizesafe(incoming + outgoing, incoming);
			}

			static float3 Transport(float3 direction, float3 fromTangent,
				float3 toTangent)
			{
				var axis = math.cross(fromTangent, toTangent);
				var sinAngle = math.length(axis);
				var cosAngle = math.clamp(math.dot(fromTangent, toTangent), -1f, 1f);
				var transported = direction;
				if (sinAngle > 1e-6f) {
					axis /= sinAngle;
					transported = math.mul(quaternion.AxisAngle(axis,
						math.atan2(sinAngle, cosAngle)), direction);
				}
				return math.normalizesafe(Project(transported, toTangent), direction);
			}
		}

		private static List<float> BuildBraceAngles(WireRailBraceFixture brace,
			float startAngle, float sweepAngle, bool closed, int longitudinalSegments)
		{
			var angles = new List<float>(longitudinalSegments + 3);
			var baseRingCount = closed ? longitudinalSegments : longitudinalSegments + 1;
			for (var ringIndex = 0; ringIndex < baseRingCount; ringIndex++) {
				angles.Add(startAngle + sweepAngle * ringIndex / longitudinalSegments);
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

		private static bool IsPathReversal(float3 previous, float3 corner, float3 next)
		{
			var incoming = math.normalizesafe(corner - previous);
			var outgoing = math.normalizesafe(next - corner);
			return math.lengthsq(incoming) > 1e-8f && math.lengthsq(outgoing) > 1e-8f
				&& math.dot(incoming, outgoing) <= -math.cos(math.radians(0.5f));
		}
	}

	internal static class WireRailCapMeshGenerator
	{
		private const float FullTurn = math.PI * 2f;

		public static void Append(WireRailPathFrame frame, float radius,
			float bevelSize, int radialSegments, bool start, ICollection<Vector3> vertices,
			ICollection<Vector3> normals, ICollection<Vector2> uvs,
			ICollection<int> indices)
		{
			var normal = start ? -frame.Tangent : frame.Tangent;
			bevelSize = math.clamp(bevelSize, 0f, radius);
			if (bevelSize > 1e-5f) {
				AppendCapBevel(frame, normal, radius, bevelSize, radialSegments, start,
					vertices, normals, uvs, indices);
				radius -= bevelSize;
			}
			if (radius <= 1e-5f) {
				return;
			}
			AppendFlatCap(frame, normal, radius, radialSegments, start,
				vertices, normals, uvs, indices);
		}

		private static void AppendCapBevel(WireRailPathFrame frame, float3 normal,
			float radius, float bevelSize, int radialSegments, bool start,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var bodyCenter = frame.Position - normal * bevelSize;
			var capRadius = radius - bevelSize;
			var outerRingStart = vertices.Count;
			for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
				var angle = FullTurn * radialIndex / radialSegments;
				var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
				var bevelNormal = math.normalizesafe(radial + normal, radial);
				vertices.Add((Vector3)(bodyCenter + radial * radius));
				normals.Add((Vector3)bevelNormal);
				uvs.Add(new Vector2(0f, radialIndex / (float)radialSegments));
			}
			if (capRadius <= 1e-5f) {
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var next = (radialIndex + 1) % radialSegments;
					var middleAngle = FullTurn * (radialIndex + 0.5f) / radialSegments;
					var middleRadial = frame.Right * math.cos(middleAngle)
						+ frame.Up * math.sin(middleAngle);
					var tipIndex = vertices.Count;
					vertices.Add((Vector3)frame.Position);
					normals.Add((Vector3)math.normalizesafe(middleRadial + normal, normal));
					uvs.Add(new Vector2(1f, (radialIndex + 0.5f) / radialSegments));
					indices.Add(outerRingStart + radialIndex);
					indices.Add(start ? outerRingStart + next : tipIndex);
					indices.Add(start ? tipIndex : outerRingStart + next);
				}
				return;
			}
			var innerRingStart = vertices.Count;
			for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
				var angle = FullTurn * radialIndex / radialSegments;
				var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
				var bevelNormal = math.normalizesafe(radial + normal, radial);
				vertices.Add((Vector3)(frame.Position + radial * capRadius));
				normals.Add((Vector3)bevelNormal);
				uvs.Add(new Vector2(1f, radialIndex / (float)radialSegments));
			}
			for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
				var next = (radialIndex + 1) % radialSegments;
				var a = outerRingStart + radialIndex;
				var b = innerRingStart + radialIndex;
				var c = outerRingStart + next;
				var d = innerRingStart + next;
				if (start) {
					indices.Add(a);
					indices.Add(d);
					indices.Add(b);
					indices.Add(a);
					indices.Add(c);
					indices.Add(d);
				} else {
					indices.Add(a);
					indices.Add(b);
					indices.Add(d);
					indices.Add(a);
					indices.Add(d);
					indices.Add(c);
				}
			}
		}

		private static void AppendFlatCap(WireRailPathFrame frame, float3 normal,
			float radius, int radialSegments, bool start,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var centerIndex = vertices.Count;
			vertices.Add((Vector3)frame.Position);
			normals.Add((Vector3)normal);
			uvs.Add(new Vector2(0.5f, 0.5f));
			var ringStart = vertices.Count;
			for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
				var angle = FullTurn * radialIndex / radialSegments;
				var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
				vertices.Add((Vector3)(frame.Position + radial * radius));
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
			=> TryCreateCore(offsets, wireRadii, ballRadius, null, out profile, out error);

		internal static bool TryCreate(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius, Vector2 ballCenterHint,
			out WireRailChannelProfile profile, out string error)
			=> TryCreateCore(offsets, wireRadii, ballRadius, (float2)ballCenterHint,
				out profile, out error);

		private static bool TryCreateCore(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius, float2? ballCenterHint,
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

			if (!TryGetRestingBallCenter(offsets, wireRadii, ballRadius, ballCenterHint,
					out var ballCenter, out error)) {
				return false;
			}
			var collisionIndices = SelectCollisionIndices(offsets.Count);
			var supportLines = new List<FacetLine>(collisionIndices.Count);
			var supportCenter = float2.zero;
			foreach (var railIndex in collisionIndices) {
				var offset = (float2)offsets[railIndex];
				supportCenter += offset;
				var normal = math.normalizesafe(ballCenter - offset);
				if (math.lengthsq(normal) < 0.5f) {
					error = "A rail lies on the reference ball center and has no contact direction.";
					return false;
				}
				supportLines.Add(new FacetLine(offset + normal * wireRadii[railIndex], normal,
					NormalizeAngle(math.atan2(normal.y, normal.x))));
			}
			supportLines.Sort((first, second) => first.Angle.CompareTo(second.Angle));
			supportCenter /= collisionIndices.Count;

			var closed = offsets.Count >= 5;
			var openingDirection = math.normalizesafe(ballCenter - supportCenter,
				new float2(0f, 1f));
			var openingInwardNormal = NormalizeAngle(math.atan2(-openingDirection.y,
				-openingDirection.x));
			var ordered = closed
				? supportLines
				: OrderAroundOpening(supportLines, openingInwardNormal);
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
			IReadOnlyList<float> wireRadii, float ballRadius, float2? ballCenterHint,
			out float2 center, out string error)
		{
			error = null;
			if (offsets.Count == 1) {
				var offset = (float2)offsets[0];
				var direction = new float2(0f,
					ballCenterHint.HasValue && ballCenterHint.Value.y < offset.y ? -1f : 1f);
				center = offset + direction * (wireRadii[0] + ballRadius);
				return true;
			}

			var first = (float2)offsets[0];
			var second = (float2)offsets[1];
			var delta = second - first;
			var separation = math.length(delta);
			if (separation <= 1e-5f) {
				center = default;
				error = "The two support rails have coincident centers.";
				return false;
			}
			var firstRadius = wireRadii[0] + ballRadius;
			var secondRadius = wireRadii[1] + ballRadius;
			if (separation > firstRadius + secondRadius
				|| separation < math.abs(firstRadius - secondRadius)) {
				center = default;
				error = "The reference ball cannot contact both support rails.";
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
			if (ballCenterHint.HasValue) {
				var firstDistance = math.distancesq(firstCandidate, ballCenterHint.Value);
				var secondDistance = math.distancesq(secondCandidate, ballCenterHint.Value);
				center = math.abs(firstDistance - secondDistance) <= 1e-4f
					? (firstCandidate.y >= secondCandidate.y
						? firstCandidate : secondCandidate)
					: (firstDistance < secondDistance ? firstCandidate : secondCandidate);
			} else {
				center = firstCandidate.y >= secondCandidate.y
					? firstCandidate : secondCandidate;
			}
			return true;
		}

		private static List<FacetLine> OrderAroundOpening(IReadOnlyList<FacetLine> sorted,
			float openingInwardNormal)
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
				var target = openingInwardNormal;
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
		private const int MaximumAdaptiveDepth = 10;

		public static bool TryGenerate(Spline spline, IReadOnlyList<WireRailSegment> segments,
			float ballDiameter, int samplesPerSegment, Mesh target,
			out Mesh mesh, out Vector3[] edgeVertices, out string error)
		{
			var vertices = new List<Vector3>();
			var indices = new List<int>();
			var edges = new List<Vector3>();
			var ballRadius = ballDiameter * 0.5f;

			for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
				if (!HasActiveRails(segments[segmentIndex])) {
					continue;
				}
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
			IReadOnlyList<int> activeRailIndices, float ballRadius,
			out WireRailChannelProfile profile, out string error)
		{
			var offsets = new Vector2[activeRailIndices.Count];
			var wireRadii = new float[activeRailIndices.Count];
			var allOffsets = new Vector2[segments[segmentIndex].RailCount];
			var allWireRadii = new float[segments[segmentIndex].RailCount];
			var envelopeMinimum = new float2(float.PositiveInfinity);
			var envelopeMaximum = new float2(float.NegativeInfinity);
			for (var railIndex = 0; railIndex < segments[segmentIndex].RailCount;
				railIndex++) {
				var offset = WireRailSplineGeometry.EvaluateRailOffset(spline, segments,
					segmentIndex, railIndex, curveT);
				var radius = WireRailSplineGeometry.EvaluateWireDiameter(spline, segments,
					segmentIndex, railIndex, curveT) * 0.5f;
				allOffsets[railIndex] = (Vector2)offset;
				allWireRadii[railIndex] = radius;
				envelopeMinimum = math.min(envelopeMinimum, offset - radius);
				envelopeMaximum = math.max(envelopeMaximum, offset + radius);
			}
			for (var activeRailIndex = 0; activeRailIndex < activeRailIndices.Count;
				activeRailIndex++) {
				var railIndex = activeRailIndices[activeRailIndex];
				offsets[activeRailIndex] = allOffsets[railIndex];
				wireRadii[activeRailIndex] = allWireRadii[railIndex];
			}
			var ballCenterHint = (Vector2)((envelopeMinimum + envelopeMaximum) * 0.5f);
			return WireRailChannelProfile.TryCreate(offsets, wireRadii, ballRadius,
				ballCenterHint,
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
			int curvatureDetail, ICollection<Vector3> vertices,
			ICollection<int> indices, ICollection<Vector3> edges, out string error)
		{
			var segment = segments[segmentIndex];
			var activeRailIndices = new List<int>(segment.RailCount);
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (segment.IsRailActive(railIndex)) {
					activeRailIndices.Add(railIndex);
				}
			}
			if (!TryBuildAdaptiveSamples(spline, segments, segmentIndex, activeRailIndices,
					ballRadius, curvatureDetail, out var samples, out error)) {
				return false;
			}
			var firstRow = vertices.Count;
			foreach (var sample in samples) {
				foreach (var offset in sample.Profile.Vertices) {
					vertices.Add((Vector3)sample.Frame.TransformOffset(offset));
				}
			}

			var referenceProfile = samples[0].Profile;
			var rowSize = referenceProfile.Vertices.Count;
			for (var sampleIndex = 0; sampleIndex < samples.Count - 1; sampleIndex++) {
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

		private static bool TryBuildAdaptiveSamples(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex,
			IReadOnlyList<int> activeRailIndices, float ballRadius, int curvatureDetail,
			out List<ColliderSample> samples, out string error)
		{
			var builtSamples = new List<ColliderSample>();
			samples = builtSamples;
			var evaluationError = default(string);
			if (!TryEvaluateSample(0f, out var start)
				|| !TryEvaluateSample(1f, out var end)) {
				error = evaluationError;
				return false;
			}
			if (!HasMatchingTopology(start.Profile, end.Profile)) {
				error = TopologyError();
				return false;
			}
			builtSamples.Add(start);
			return TryAppendInterval(start, end, null, 0, out error);

			bool TryAppendInterval(ColliderSample intervalStart, ColliderSample intervalEnd,
				ColliderSample? knownMiddle, int depth, out string intervalError)
			{
				var interval = intervalEnd.CurveT - intervalStart.CurveT;
				if (!TryEvaluateSample(intervalStart.CurveT + interval * 0.25f,
						out var firstQuarter)
					|| !TryEvaluateSample(intervalStart.CurveT + interval * 0.75f,
						out var thirdQuarter)) {
					intervalError = evaluationError;
					return false;
				}
				var middle = knownMiddle.GetValueOrDefault();
				if (!knownMiddle.HasValue
					&& !TryEvaluateSample(intervalStart.CurveT + interval * 0.5f,
						out middle)) {
					intervalError = evaluationError;
					return false;
				}
				if (!HasMatchingTopology(start.Profile, firstQuarter.Profile)
					|| !HasMatchingTopology(start.Profile, middle.Profile)
					|| !HasMatchingTopology(start.Profile, thirdQuarter.Profile)) {
					intervalError = TopologyError();
					return false;
				}
				if (depth < MaximumAdaptiveDepth
					&& ShouldSubdivide(intervalStart, firstQuarter, middle, thirdQuarter,
						intervalEnd, curvatureDetail)) {
					if (!TryAppendInterval(intervalStart, middle, firstQuarter, depth + 1,
							out intervalError)) {
						return false;
					}
					return TryAppendInterval(middle, intervalEnd, thirdQuarter, depth + 1,
						out intervalError);
				}
				builtSamples.Add(intervalEnd);
				intervalError = null;
				return true;
			}

			bool TryEvaluateSample(float curveT, out ColliderSample sample)
			{
				sample = default;
				if (!WireRailSplineGeometry.TryEvaluateLayout(spline, segments, segmentIndex,
						curveT, out var frame)) {
					evaluationError = $"Could not evaluate spline segment {segmentIndex + 1}.";
					return false;
				}
				if (!TryCreateProfile(spline, segments, segmentIndex, curveT,
						activeRailIndices, ballRadius, out var profile,
						out evaluationError)) {
					return false;
				}
				sample = new ColliderSample(curveT, frame, profile);
				return true;
			}

			string TopologyError()
				=> $"The collider profile changes topology while blending segment "
					+ $"{segmentIndex + 1}. Adjust the segment connection or rail layout.";
		}

		private static bool ShouldSubdivide(ColliderSample start,
			ColliderSample firstQuarter, ColliderSample middle, ColliderSample thirdQuarter,
			ColliderSample end, int curvatureDetail)
		{
			curvatureDetail = math.clamp(curvatureDetail, 2, 32);
			var maximumAngle = math.radians(45f / curvatureDetail);
			if (Angle(start.Frame.Tangent, firstQuarter.Frame.Tangent) > maximumAngle
				|| Angle(firstQuarter.Frame.Tangent, middle.Frame.Tangent) > maximumAngle
				|| Angle(middle.Frame.Tangent, thirdQuarter.Frame.Tangent) > maximumAngle
				|| Angle(thirdQuarter.Frame.Tangent, end.Frame.Tangent) > maximumAngle) {
				return true;
			}

			var maximumDeviation = 4f / curvatureDetail;
			var maximumDeviationSquared = maximumDeviation * maximumDeviation;
			return SampleDeviates(firstQuarter, 0.25f)
				|| SampleDeviates(middle, 0.5f)
				|| SampleDeviates(thirdQuarter, 0.75f);

			bool SampleDeviates(ColliderSample sample, float interpolation)
			{
				for (var vertexIndex = 0; vertexIndex < sample.Profile.Vertices.Count;
					vertexIndex++) {
					var startVertex = start.Frame.TransformOffset(
						start.Profile.Vertices[vertexIndex]);
					var sampleVertex = sample.Frame.TransformOffset(
						sample.Profile.Vertices[vertexIndex]);
					var endVertex = end.Frame.TransformOffset(end.Profile.Vertices[vertexIndex]);
					if (math.distancesq(sampleVertex,
							math.lerp(startVertex, endVertex, interpolation))
						> maximumDeviationSquared) {
						return true;
					}
				}
				return false;
			}
		}

		private static float Angle(float3 first, float3 second)
			=> math.acos(math.clamp(math.dot(first, second), -1f, 1f));

		private readonly struct ColliderSample
		{
			public readonly float CurveT;
			public readonly WireRailPathFrame Frame;
			public readonly WireRailChannelProfile Profile;

			public ColliderSample(float curveT, WireRailPathFrame frame,
				WireRailChannelProfile profile)
			{
				CurveT = curveT;
				Frame = frame;
				Profile = profile;
			}
		}

		private static bool HasActiveRails(WireRailSegment segment)
		{
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (segment.IsRailActive(railIndex)) {
					return true;
				}
			}
			return false;
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
