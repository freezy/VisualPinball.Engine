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
using Unity.Profiling;
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
		internal readonly Dictionary<long, WireRailPathFrame> LayoutFrames = new();
		internal readonly Dictionary<WireRailFrameCacheKey, WireRailPathFrame> RailFrames = new();
		internal Spline Spline;
		internal float SplineLength;

		internal void Prepare(Spline spline)
		{
			if (ReferenceEquals(Spline, spline)) {
				return;
			}
			Reset(spline);
		}

		internal void Reset(Spline spline)
		{
			Spline = spline;
			SplineLength = spline?.GetLength() ?? 0f;
			BoundaryTangents.Clear();
			LayoutFrames.Clear();
			RailFrames.Clear();
		}
	}

	internal readonly struct WireRailFrameCacheKey : IEquatable<WireRailFrameCacheKey>
	{
		private readonly int _segmentIndex;
		private readonly int _railIndex;
		private readonly int _curveT;
		private readonly int _tangentStep;

		public WireRailFrameCacheKey(int segmentIndex, int railIndex, float curveT,
			float tangentStep)
		{
			_segmentIndex = segmentIndex;
			_railIndex = railIndex;
			_curveT = math.asint(curveT);
			_tangentStep = math.asint(tangentStep);
		}

		public bool Equals(WireRailFrameCacheKey other)
			=> _segmentIndex == other._segmentIndex && _railIndex == other._railIndex
				&& _curveT == other._curveT && _tangentStep == other._tangentStep;

		public override bool Equals(object obj)
			=> obj is WireRailFrameCacheKey other && Equals(other);

		public override int GetHashCode()
		{
			unchecked {
				var hash = _segmentIndex;
				hash = hash * 397 ^ _railIndex;
				hash = hash * 397 ^ _curveT;
				return hash * 397 ^ _tangentStep;
			}
		}
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
		private static readonly ProfilerMarker RailFrameEvalMarker =
			new("WireRail.RailFrameEval");

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
			=> TryEvaluateLayout(spline, layouts, null, layoutIndex, layoutT, out frame);

		internal static bool TryEvaluateLayout(Spline spline,
			IReadOnlyList<WireRailSegment> layouts, WireRailPathEvaluationContext context,
			int layoutIndex, float layoutT, out WireRailPathFrame frame)
		{
			frame = default;
			if (spline == null || layouts == null || layoutIndex < 0
				|| layoutIndex >= layouts.Count || spline.Count < 2) {
				return false;
			}
			layoutT = math.saturate(layoutT);
			context?.Prepare(spline);
			var cacheKey = ((long)layoutIndex << 32) | (uint)math.asint(layoutT);
			if (context != null && context.LayoutFrames.TryGetValue(cacheKey, out frame)) {
				return true;
			}
			var splineLength = context?.SplineLength ?? spline.GetLength();
			var startDistance = math.clamp(layouts[layoutIndex].Distance, 0f, splineLength);
			var endDistance = layoutIndex + 1 < layouts.Count
				? math.clamp(layouts[layoutIndex + 1].Distance, startDistance, splineLength)
				: splineLength;
			if (!TryEvaluateDistance(spline,
					math.lerp(startDistance, endDistance, layoutT), splineLength, out frame)) {
				return false;
			}
			context?.LayoutFrames.Add(cacheKey, frame);
			return true;
		}

		public static bool TryEvaluateLayoutPosition(Spline spline,
			IReadOnlyList<WireRailSegment> layouts, int layoutIndex, float layoutT,
			out float3 position)
			=> TryEvaluateLayoutPosition(spline, layouts, null, layoutIndex, layoutT,
				out position);

		public static bool TryEvaluateLayoutPosition(Spline spline,
			IReadOnlyList<WireRailSegment> layouts, WireRailPathEvaluationContext context,
			int layoutIndex, float layoutT, out float3 position)
		{
			position = default;
			if (!TryEvaluateLayout(spline, layouts, context, layoutIndex, layoutT,
					out var frame)) {
				return false;
			}
			position = frame.Position;
			return true;
		}

		internal static bool TryEvaluateDistance(Spline spline, float distance,
			out WireRailPathFrame frame)
			=> TryEvaluateDistance(spline, distance, spline?.GetLength() ?? 0f, out frame);

		private static bool TryEvaluateDistance(Spline spline, float distance,
			float splineLength, out WireRailPathFrame frame)
		{
			frame = default;
			if (spline == null || spline.Count < 2) {
				return false;
			}
			var normalizedT = spline.ConvertIndexUnit(
				math.clamp(distance, 0f, splineLength), PathIndexUnit.Distance,
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

		public static bool TryEvaluateVBrace(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailVBraceFixture vBrace,
			out IReadOnlyList<float3> centerlinePoints)
		{
			centerlinePoints = Array.Empty<float3>();
			if (!WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(spline, segments,
					vBrace, out var profile)) {
				return false;
			}
			centerlinePoints = profile.CenterlinePoints;
			return true;
		}

		public static bool TryEvaluateDropLoop(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropLoopFixture dropLoop,
			out IReadOnlyList<float3> centerlinePoints)
		{
			centerlinePoints = Array.Empty<float3>();
			if (!WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(spline, segments,
					dropLoop, out var profile)) {
				return false;
			}
			centerlinePoints = profile.CenterlinePoints;
			return true;
		}

		public static bool TryEvaluateDrop(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropFixture drop,
			out IReadOnlyList<float3> firstRailPoints,
			out IReadOnlyList<float3> secondRailPoints)
		{
			firstRailPoints = Array.Empty<float3>();
			secondRailPoints = Array.Empty<float3>();
			if (!WireRailFixtureMeshGenerator.TryEvaluateDropProfile(spline, segments,
					drop, out var profile)) {
				return false;
			}
			firstRailPoints = profile.FirstRailPoints;
			secondRailPoints = profile.SecondRailPoints;
			return true;
		}

		internal static int GetLayoutIndexAtDistance(
			IReadOnlyList<WireRailSegment> segments, float distance, float splineLength)
		{
			if (segments == null || segments.Count == 0) {
				return -1;
			}
			var clampedDistance = math.clamp(distance, 0f, math.max(0f, splineLength));
			var segmentIndex = segments.Count - 1;
			for (var layoutIndex = 1; layoutIndex < segments.Count; layoutIndex++) {
				if (clampedDistance < segments[layoutIndex].Distance) {
					segmentIndex = layoutIndex - 1;
					break;
				}
			}
			return segmentIndex;
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
			context ??= new WireRailPathEvaluationContext();
			context.Prepare(spline);
			var cacheKey = new WireRailFrameCacheKey(segmentIndex, railIndex, curveT,
				tangentStep);
			if (context.RailFrames.TryGetValue(cacheKey, out railFrame)) {
				return true;
			}
			using (RailFrameEvalMarker.Auto()) {
				if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex, railIndex,
						curveT, out var mainFrame, out var center)) {
					return false;
				}

				var before = center;
				var after = center;
				var referenceRight = mainFrame.Right;
				var referenceUp = mainFrame.Up;
				if (curveT <= 0f) {
					var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments,
						segmentIndex);
					var connected = IsRailConnectedBoundary(segments, previousSegmentIndex,
						segmentIndex, railIndex);
					if (connected) {
						if (!TryEvaluateRailCenter(spline, segments, context, previousSegmentIndex,
								railIndex, 1f - tangentStep, out _, out before)
							|| !TryEvaluateLayout(spline, segments, context, previousSegmentIndex,
								1f, out var previousMainFrame)) {
							return false;
						}
						AverageReferenceAxes(previousMainFrame, mainFrame,
							out referenceRight, out referenceUp);
					}
					if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex,
							railIndex, tangentStep, out _, out after)) {
						return false;
					}
				} else if (curveT >= 1f) {
					if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex,
							railIndex, 1f - tangentStep, out _, out before)) {
						return false;
					}
					var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
					var connected = IsRailConnectedBoundary(segments, segmentIndex,
						nextSegmentIndex, railIndex);
					if (connected) {
						if (!TryEvaluateRailCenter(spline, segments, context, nextSegmentIndex,
								railIndex, tangentStep, out _, out after)
							|| !TryEvaluateLayout(spline, segments, context, nextSegmentIndex, 0f,
								out var nextMainFrame)) {
							return false;
						}
						AverageReferenceAxes(mainFrame, nextMainFrame,
							out referenceRight, out referenceUp);
					}
				} else {
					if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex,
							railIndex, math.max(0f, curveT - tangentStep), out _, out before)
						|| !TryEvaluateRailCenter(spline, segments, context, segmentIndex,
							railIndex, math.min(1f, curveT + tangentStep), out _, out after)) {
						return false;
					}
				}

				var tangentVector = after - before;
				var derivativeStep = math.min(tangentStep, 1f / 1024f);
				var derivativeBefore = center;
				var derivativeAfter = center;
				if (curveT <= 0f) {
					var previousSegmentIndex = GetPreviousSegmentIndex(spline, segments,
						segmentIndex);
					if (IsRailConnectedBoundary(segments, previousSegmentIndex, segmentIndex,
							railIndex)
						&& !TryEvaluateRailCenter(spline, segments, context,
							previousSegmentIndex, railIndex, 1f - derivativeStep, out _,
							out derivativeBefore)) {
						return false;
					}
					if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex,
							railIndex, derivativeStep, out _, out derivativeAfter)) {
						return false;
					}
				} else if (curveT >= 1f) {
					if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex,
							railIndex, 1f - derivativeStep, out _, out derivativeBefore)) {
						return false;
					}
					var nextSegmentIndex = GetNextSegmentIndex(spline, segments, segmentIndex);
					if (IsRailConnectedBoundary(segments, segmentIndex, nextSegmentIndex,
							railIndex)
						&& !TryEvaluateRailCenter(spline, segments, context, nextSegmentIndex,
							railIndex, derivativeStep, out _, out derivativeAfter)) {
						return false;
					}
				} else if (!TryEvaluateRailCenter(spline, segments, context, segmentIndex,
						railIndex, math.max(0f, curveT - derivativeStep), out _,
						out derivativeBefore)
					|| !TryEvaluateRailCenter(spline, segments, context, segmentIndex,
						railIndex, math.min(1f, curveT + derivativeStep), out _,
						out derivativeAfter)) {
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
				context.RailFrames.Add(cacheKey, railFrame);
				return true;
			}
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
			if (!TryEvaluateRailCenterUnsmoothed(spline, segments, context, segmentIndex, railIndex,
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
			IReadOnlyList<WireRailSegment> segments, WireRailPathEvaluationContext context,
			int segmentIndex, int railIndex,
			float curveT, out WireRailPathFrame mainFrame, out float3 center)
		{
			center = default;
			curveT = math.saturate(curveT);
			if (!TryEvaluateLayout(spline, segments, context, segmentIndex, curveT, out mainFrame)) {
				return false;
			}

			var offset = EvaluateRailOffset(spline, segments, segmentIndex, railIndex, curveT);
			if (HasConstantRailOffset(spline, segments, segmentIndex, railIndex)) {
				center = mainFrame.TransformOffset(offset);
				return true;
			}
			if (!TryEvaluateLayout(spline, segments, context, segmentIndex, 0f, out var startFrame)
				|| !TryEvaluateLayout(spline, segments, context, segmentIndex, 1f, out var endFrame)) {
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
			if (!TryEvaluateRailCenterUnsmoothed(spline, segments, context, sourceSegmentIndex,
					railIndex, 1f - ConnectionTangentSampleStep, out _, out var sourceBefore)
				|| !TryEvaluateRailCenterUnsmoothed(spline, segments, context, sourceSegmentIndex,
					railIndex, 1f, out var sourceMainFrame, out var sourceEnd)
				|| !TryEvaluateRailCenterUnsmoothed(spline, segments, context, nextSegmentIndex,
					railIndex, 0f, out var nextMainFrame, out var nextStart)
				|| !TryEvaluateRailCenterUnsmoothed(spline, segments, context, nextSegmentIndex,
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

	internal static class WireRailEndpointTrimUtility
	{
		public const int MaximumRailCount = 6;
		private const float MinimumSpan = 1e-5f;

		// A drop only contributes geometry and trimming when its two rails are a distinct, active
		// pair at the actual attachment (the endpoint moved inward by the offset, which may cross
		// a layout boundary). Every drop path uses this so caps, trims, colliders, and validation
		// stay consistent across boundaries.
		internal static bool IsDropGeneratable(IReadOnlyList<WireRailSegment> segments,
			WireRailDropFixture drop, float splineLength)
		{
			if (drop == null || segments == null || segments.Count == 0
				|| drop.FirstRailIndex == drop.SecondRailIndex
				|| drop.FirstRailIndex < 0 || drop.SecondRailIndex < 0) {
				return false;
			}
			var railTrim = math.max(0f, drop.Offset);
			var attachmentDistance = math.clamp(drop.Endpoint == WireRailEndpoint.Start
				? railTrim : splineLength - railTrim, 0f, splineLength);
			var segmentIndex = WireRailSplineGeometry.GetLayoutIndexAtDistance(segments,
				attachmentDistance, splineLength);
			if (segmentIndex < 0) {
				return false;
			}
			var segment = segments[segmentIndex];
			return drop.FirstRailIndex < segment.RailCount
				&& drop.SecondRailIndex < segment.RailCount
				&& segment.IsRailActive(drop.FirstRailIndex)
				&& segment.IsRailActive(drop.SecondRailIndex);
		}

		public static void Collect(Spline spline, IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures,
			float[] startOffsets, float[] endOffsets, bool includeDrop = true)
		{
			Array.Clear(startOffsets, 0, startOffsets.Length);
			Array.Clear(endOffsets, 0, endOffsets.Length);
			if (spline == null || spline.Closed || fixtures == null) {
				return;
			}
			var splineLength = spline.GetLength();
			foreach (var fixture in fixtures) {
				if (fixture is WireRailTrimFixture railTrim) {
					var destination = railTrim.Endpoint == WireRailEndpoint.Start
						? startOffsets : endOffsets;
					var railCount = math.min(destination.Length, railTrim.RailCount);
					for (var railIndex = 0; railIndex < railCount; railIndex++) {
						destination[railIndex] = math.max(destination[railIndex],
							railTrim.GetRailOffset(railIndex));
					}
				} else if (includeDrop && fixture is WireRailDropFixture drop) {
					// A non-generatable or conflicting drop omits the whole fixture, so it then
					// contributes nothing — neither the offset trim nor the other cutoffs.
					if (!IsDropGeneratable(segments, drop, splineLength)
						|| HasRailTrimConflict(fixtures, drop.Endpoint,
							drop.FirstRailIndex, drop.SecondRailIndex, drop)) {
						continue;
					}
					var destination = drop.Endpoint == WireRailEndpoint.Start
						? startOffsets : endOffsets;
					var railCount = math.min(destination.Length, drop.RailCount);
					// The offset shortens the two attached rails so the drop starts before the
					// endpoint; the other rails keep their own cutoffs.
					var attachedTrim = math.max(0f, drop.Offset);
					for (var railIndex = 0; railIndex < railCount; railIndex++) {
						var trim = drop.IsAttachedRail(railIndex)
							? attachedTrim
							: drop.GetRailOffset(railIndex);
						destination[railIndex] = math.max(destination[railIndex], trim);
					}
				}
			}
		}

		// When the caller passes the fixture it is evaluating as requestingFixture, that fixture
		// is skipped and another Drop's positive Offset (which shortens its own attached rails)
		// also counts as a conflict on a shared rail. Callers that cannot identify themselves
		// (e.g. the inspector) pass null and get only the rail-cutoff conflicts.
		public static bool HasRailTrimConflict(IReadOnlyList<WireRailFixture> fixtures,
			WireRailEndpoint endpoint, int firstRailIndex, int secondRailIndex,
			WireRailFixture requestingFixture = null)
		{
			if (fixtures == null || firstRailIndex < 0 || secondRailIndex < 0) {
				return false;
			}
			foreach (var fixture in fixtures) {
				if (ReferenceEquals(fixture, requestingFixture)) {
					continue;
				}
				if (fixture is WireRailTrimFixture railTrim
					&& railTrim.Endpoint == endpoint) {
					if (firstRailIndex < railTrim.RailCount
							&& railTrim.GetRailOffset(firstRailIndex) > MinimumSpan
						|| secondRailIndex < railTrim.RailCount
							&& railTrim.GetRailOffset(secondRailIndex) > MinimumSpan) {
						return true;
					}
				} else if (fixture is WireRailDropFixture drop
					&& drop.Endpoint == endpoint) {
					// An invalid drop (equal indices) is not generated, so its offset must not
					// count as a conflict that would suppress an otherwise valid fitting.
					if (requestingFixture != null && drop.Offset > MinimumSpan
						&& drop.FirstRailIndex != drop.SecondRailIndex
						&& (drop.IsAttachedRail(firstRailIndex)
							|| drop.IsAttachedRail(secondRailIndex))) {
						return true;
					}
					if (!drop.IsAttachedRail(firstRailIndex)
							&& firstRailIndex < drop.RailCount
							&& drop.GetRailOffset(firstRailIndex) > MinimumSpan
						|| !drop.IsAttachedRail(secondRailIndex)
							&& secondRailIndex < drop.RailCount
							&& drop.GetRailOffset(secondRailIndex) > MinimumSpan) {
						return true;
					}
				}
			}
			return false;
		}

		public static bool TryGetSegmentRange(IReadOnlyList<WireRailSegment> segments,
			int segmentIndex, float splineLength, float startOffset, float endOffset,
			out float startT, out float endT, out bool trimmedStart, out bool trimmedEnd)
		{
			startT = 0f;
			endT = 1f;
			trimmedStart = false;
			trimmedEnd = false;
			if (segments == null || segmentIndex < 0 || segmentIndex >= segments.Count) {
				return false;
			}
			GetSegmentDistances(segments, segmentIndex, splineLength,
				out var segmentStart, out var segmentEnd);
			var segmentLength = segmentEnd - segmentStart;
			if (segmentLength <= MinimumSpan) {
				return false;
			}
			var visibleStart = math.max(segmentStart, math.max(0f, startOffset));
			var trimmedRouteEnd = splineLength - math.max(0f, endOffset);
			var visibleEnd = math.min(segmentEnd, trimmedRouteEnd);
			if (visibleEnd - visibleStart <= MinimumSpan) {
				return false;
			}
			startT = math.saturate((visibleStart - segmentStart) / segmentLength);
			endT = math.saturate((visibleEnd - segmentStart) / segmentLength);
			trimmedStart = startOffset > MinimumSpan
				&& math.abs(visibleStart - startOffset) <= MinimumSpan;
			trimmedEnd = endOffset > MinimumSpan
				&& math.abs(visibleEnd - trimmedRouteEnd) <= MinimumSpan;
			return endT - startT > MinimumSpan;
		}

		public static void GetSegmentDistances(IReadOnlyList<WireRailSegment> segments,
			int segmentIndex, float splineLength, out float startDistance,
			out float endDistance)
		{
			startDistance = math.clamp(segments[segmentIndex].Distance, 0f, splineLength);
			endDistance = segmentIndex + 1 < segments.Count
				? math.clamp(segments[segmentIndex + 1].Distance, startDistance, splineLength)
				: splineLength;
		}
	}

	internal static class WireRailRenderMeshGenerator
	{
		private const float MaximumRingAngle = 0.08726646f;
		private const int MaximumAdaptiveDepth = 3;
		private static readonly float2[][] RadialDirections = BuildRadialDirections();
		[ThreadStatic] private static RenderBuffers _threadBuffers;

		private sealed class RenderBuffers
		{
			public readonly List<Vector3> Vertices = new();
			public readonly List<Vector3> Normals = new();
			public readonly List<Vector2> Uvs = new();
			public readonly List<int> Indices = new();
			public readonly Dictionary<int, WireRailPathFrame> PreviousFrames = new();
			public readonly List<float> SampleParameters = new();
			public readonly HashSet<int> FittedStartRails = new();
			public readonly HashSet<int> FittedEndRails = new();
			public readonly float[] StartTrimOffsets =
				new float[WireRailEndpointTrimUtility.MaximumRailCount];
			public readonly float[] EndTrimOffsets =
				new float[WireRailEndpointTrimUtility.MaximumRailCount];
			public readonly WireRailPathEvaluationContext EvaluationContext = new();

			public void Clear()
			{
				Vertices.Clear();
				Normals.Clear();
				Uvs.Clear();
				Indices.Clear();
				PreviousFrames.Clear();
				SampleParameters.Clear();
				FittedStartRails.Clear();
				FittedEndRails.Clear();
			}
		}

		public static Mesh Generate(Spline spline, IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, float wireCapBevelSize,
			int samplesPerSegment, int radialSegments, Mesh target)
		{
			radialSegments = math.clamp(radialSegments, 6, 16);
			var buffers = _threadBuffers ??= new RenderBuffers();
			buffers.Clear();
			var vertices = buffers.Vertices;
			var normals = buffers.Normals;
			var uvs = buffers.Uvs;
			var indices = buffers.Indices;
			var previousFrames = buffers.PreviousFrames;
			var evaluationContext = buffers.EvaluationContext;
			evaluationContext.Reset(spline);
			var splineLength = evaluationContext.SplineLength;
			var startEndpointSegmentIndex = WireRailSplineGeometry
				.GetLayoutIndexAtDistance(segments, 0f, splineLength);
			var endEndpointSegmentIndex = WireRailSplineGeometry
				.GetLayoutIndexAtDistance(segments, splineLength, splineLength);
			WireRailEndpointTrimUtility.Collect(spline, segments, fixtures,
				buffers.StartTrimOffsets, buffers.EndTrimOffsets);
			CollectFittedRailEnds(spline, segments, fixtures, splineLength,
				buffers.StartTrimOffsets, buffers.EndTrimOffsets,
				buffers.FittedStartRails, buffers.FittedEndRails);
			for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
				var segment = segments[segmentIndex];
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					if (!segment.IsRailActive(railIndex)) {
						previousFrames.Remove(railIndex);
						continue;
					}
					if (!WireRailEndpointTrimUtility.TryGetSegmentRange(segments, segmentIndex,
							splineLength, buffers.StartTrimOffsets[railIndex],
							buffers.EndTrimOffsets[railIndex], out var startT, out var endT,
							out var trimmedStart, out var trimmedEnd)) {
						previousFrames.Remove(railIndex);
						continue;
					}
					WireRailPathFrame? previousSegmentFrame = null;
					if (!trimmedStart && startT <= 1e-5f && segmentIndex > 0
						&& WireRailSplineGeometry.IsRailConnectedAtStart(spline, segments,
							segmentIndex, railIndex)
						&& previousFrames.TryGetValue(railIndex, out var previousFrame)) {
						previousSegmentFrame = previousFrame;
					}
					// Omit the fitted flat cap where the rail actually ends: at the endpoint
					// segment when it reaches the endpoint, or at whichever segment holds its
					// trimmed end when a Drop offset moved the attachment across a layout.
					var omitStartCapBevel = buffers.FittedStartRails.Contains(railIndex)
						&& (trimmedStart || segmentIndex == startEndpointSegmentIndex);
					var omitEndCapBevel = buffers.FittedEndRails.Contains(railIndex)
						&& (trimmedEnd || segmentIndex == endEndpointSegmentIndex);
					if (AppendTube(spline, segments, evaluationContext, segmentIndex, railIndex,
						startT, endT, samplesPerSegment, radialSegments, wireCapBevelSize,
						trimmedStart, trimmedEnd, omitStartCapBevel, omitEndCapBevel,
						previousSegmentFrame,
						buffers.SampleParameters,
						vertices, normals, uvs, indices, out var lastFrame)) {
						previousFrames[railIndex] = lastFrame;
					} else {
						previousFrames.Remove(railIndex);
					}
				}
			}
			WireRailFixtureMeshGenerator.Append(spline, segments, fixtures,
				wireCapBevelSize, radialSegments, vertices, normals, uvs, indices);
			WireRailSolderMeshGenerator.Append(spline, segments, fixtures,
				vertices, normals, uvs, indices);

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

		private static void CollectFittedRailEnds(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, float splineLength,
			IReadOnlyList<float> startTrimOffsets, IReadOnlyList<float> endTrimOffsets,
			ISet<int> fittedStartRails,
			ISet<int> fittedEndRails)
		{
			if (spline == null || spline.Closed || segments == null || segments.Count == 0
				|| fixtures == null) {
				return;
			}
			foreach (var fixture in fixtures) {
				WireRailEndpoint endpoint;
				int firstRailIndex;
				int secondRailIndex;
				// A Drop's rails join the fitting at its offset attachment, not the spline
				// endpoint, so validate and pick the fitted segment there — the same point the
				// drop profile and usability checks use — so the flat cap lands on the same rail.
				float attachmentDistance;
				if (fixture is WireRailDropLoopFixture dropLoop) {
					if (WireRailFixtureMeshGenerator.HasDropLoopAttachmentOffset(dropLoop)) {
						continue;
					}
					endpoint = dropLoop.Endpoint;
					firstRailIndex = dropLoop.FirstRailIndex;
					secondRailIndex = dropLoop.SecondRailIndex;
					attachmentDistance = endpoint == WireRailEndpoint.Start ? 0f : splineLength;
				} else if (fixture is WireRailDropFixture drop) {
					endpoint = drop.Endpoint;
					firstRailIndex = drop.FirstRailIndex;
					secondRailIndex = drop.SecondRailIndex;
					var railTrim = math.max(0f, drop.Offset);
					attachmentDistance = math.clamp(endpoint == WireRailEndpoint.Start
						? railTrim : splineLength - railTrim, 0f, splineLength);
				} else {
					continue;
				}
				if (WireRailEndpointTrimUtility.HasRailTrimConflict(fixtures,
						endpoint, firstRailIndex, secondRailIndex, fixture)) {
					continue;
				}
				var endpointSegmentIndex = WireRailSplineGeometry.GetLayoutIndexAtDistance(
					segments, attachmentDistance, splineLength);
				if (endpointSegmentIndex < 0) {
					continue;
				}
				var endpointSegment = segments[endpointSegmentIndex];
				if (firstRailIndex == secondRailIndex
					|| firstRailIndex < 0 || secondRailIndex < 0
					|| firstRailIndex >= endpointSegment.RailCount
					|| secondRailIndex >= endpointSegment.RailCount
					|| !endpointSegment.IsRailActive(firstRailIndex)
					|| !endpointSegment.IsRailActive(secondRailIndex)) {
					continue;
				}
				var fittedRails = endpoint == WireRailEndpoint.Start
					? fittedStartRails : fittedEndRails;
				// The attached rails connect to the fixture, so they get no end cap even when
				// the Drop's offset trims them back: that trim is the drop attachment, not a
				// cut end. A conflicting trim from another fixture was already rejected above.
				fittedRails.Add(firstRailIndex);
				fittedRails.Add(secondRailIndex);
			}
		}

		private static bool AppendTube(Spline spline, IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext,
			int segmentIndex, int railIndex, float startT, float endT, int samplesPerSegment,
			int radialSegments, float capBevelSize, bool trimmedStart, bool trimmedEnd,
			bool omitStartCapBevel, bool omitEndCapBevel,
			WireRailPathFrame? previousSegmentFrame,
			List<float> sampleParameters, List<Vector3> vertices, List<Vector3> normals,
			List<Vector2> uvs, List<int> indices,
			out WireRailPathFrame lastFrame)
		{
			var firstRing = vertices.Count;
			WireRailPathFrame firstFrame = default;
			lastFrame = default;
			var firstRadius = 0f;
			var lastRadius = 0f;
			BuildSampleParameters(spline, segments, evaluationContext, segmentIndex, railIndex,
				startT, endT, samplesPerSegment, sampleParameters);
			var capStart = trimmedStart || startT > 1e-5f
				|| !WireRailSplineGeometry.IsRailConnectedAtStart(spline, segments,
					segmentIndex, railIndex);
			var capEnd = trimmedEnd || endT < 1f - 1e-5f
				|| !WireRailSplineGeometry.IsRailConnectedAtEnd(spline, segments,
					segmentIndex, railIndex);
			var startCapBevelSize = omitStartCapBevel ? 0f : capBevelSize;
			var endCapBevelSize = omitEndCapBevel ? 0f : capBevelSize;
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
				var clampedBevel = sampleIndex == 0
					? math.clamp(startCapBevelSize, 0f, radius)
					: sampleIndex == sampleParameters.Count - 1
						? math.clamp(endCapBevelSize, 0f, radius)
						: 0f;
				if (clampedBevel > 1e-5f && sampleIndex == 0 && capStart) {
					tubeFrame = new WireRailPathFrame(frame.Position + frame.Tangent * clampedBevel,
						frame.Tangent, frame.Right, frame.Up);
				} else if (clampedBevel > 1e-5f
					&& sampleIndex == sampleParameters.Count - 1 && capEnd) {
					tubeFrame = new WireRailPathFrame(frame.Position - frame.Tangent * clampedBevel,
						frame.Tangent, frame.Right, frame.Up);
				}
				var radialDirections = RadialDirections[radialSegments];
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var radialDirection = radialDirections[radialIndex];
					var radial = tubeFrame.Right * radialDirection.x
						+ tubeFrame.Up * radialDirection.y;
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
				WireRailCapMeshGenerator.Append(firstFrame, firstRadius, startCapBevelSize,
					radialSegments, true,
					vertices, normals, uvs, indices);
			}
			if (capEnd) {
				WireRailCapMeshGenerator.Append(lastFrame, lastRadius, endCapBevelSize,
					radialSegments, false,
					vertices, normals, uvs, indices);
			}
			return true;
		}

		internal static List<float> BuildSampleParameters(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, int railIndex,
			int minimumSamples)
		{
			var parameters = new List<float>(minimumSamples + 1);
			BuildSampleParameters(spline, segments, new WireRailPathEvaluationContext(),
				segmentIndex, railIndex, minimumSamples, parameters);
			return parameters;
		}

		private static void BuildSampleParameters(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext,
			int segmentIndex, int railIndex, int minimumSamples, List<float> parameters)
			=> BuildSampleParameters(spline, segments, evaluationContext, segmentIndex,
				railIndex, 0f, 1f, minimumSamples, parameters);

		private static void BuildSampleParameters(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext,
			int segmentIndex, int railIndex, float startT, float endT,
			int minimumSamples, List<float> parameters)
		{
			parameters.Clear();
			parameters.Add(startT);
			for (var sampleIndex = 0; sampleIndex < minimumSamples; sampleIndex++) {
				var start = math.lerp(startT, endT,
					sampleIndex / (float)minimumSamples);
				var end = math.lerp(startT, endT,
					(sampleIndex + 1f) / minimumSamples);
				SubdivideSampleInterval(spline, segments, evaluationContext,
					segmentIndex, railIndex, start, end, 0, parameters);
			}
		}

		private static float2[][] BuildRadialDirections()
		{
			var directions = new float2[17][];
			for (var radialSegments = 0; radialSegments < directions.Length; radialSegments++) {
				directions[radialSegments] = new float2[radialSegments];
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var angle = math.PI * 2f * radialIndex / radialSegments;
					directions[radialSegments][radialIndex] = new float2(math.cos(angle),
						math.sin(angle));
				}
			}
			return directions;
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
				&& WireRailSplineGeometry.TryEvaluateLayout(spline, segments, evaluationContext,
					segmentIndex, start, out var startMainFrame)
				&& WireRailSplineGeometry.TryEvaluateLayout(spline, segments, evaluationContext,
					segmentIndex, end, out var endMainFrame)
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

	internal readonly struct WireRailVBraceProfile
	{
		public readonly WireRailPathFrame Frame;
		public readonly IReadOnlyList<float2> RailOffsets;
		public readonly IReadOnlyList<float> RailRadii;
		public readonly float2 OriginOffset;
		public readonly IReadOnlyList<float3> CenterlinePoints;

		public WireRailVBraceProfile(WireRailPathFrame frame,
			IReadOnlyList<float2> railOffsets, IReadOnlyList<float> railRadii,
			float2 originOffset, IReadOnlyList<float3> centerlinePoints)
		{
			Frame = frame;
			RailOffsets = railOffsets;
			RailRadii = railRadii;
			OriginOffset = originOffset;
			CenterlinePoints = centerlinePoints;
		}
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

	internal readonly struct WireRailDropLoopProfile
	{
		public readonly WireRailPathFrame Frame;
		public readonly IReadOnlyList<float3> FirstLeadPoints;
		public readonly IReadOnlyList<float3> TerminalPoints;
		public readonly IReadOnlyList<float3> SecondLeadPoints;
		public readonly IReadOnlyList<float3> CenterlinePoints;
		public readonly int TerminalStartSpan;
		public readonly int TerminalEndSpan;

		public WireRailDropLoopProfile(WireRailPathFrame frame,
			IReadOnlyList<float3> firstLeadPoints,
			IReadOnlyList<float3> terminalPoints,
			IReadOnlyList<float3> secondLeadPoints,
			IReadOnlyList<float3> centerlinePoints, int terminalStartSpan,
			int terminalEndSpan)
		{
			Frame = frame;
			FirstLeadPoints = firstLeadPoints;
			TerminalPoints = terminalPoints;
			SecondLeadPoints = secondLeadPoints;
			CenterlinePoints = centerlinePoints;
			TerminalStartSpan = terminalStartSpan;
			TerminalEndSpan = terminalEndSpan;
		}
	}

	internal readonly struct WireRailDropProfile
	{
		public readonly WireRailPathFrame Frame;
		public readonly IReadOnlyList<float3> FirstRailPoints;
		public readonly IReadOnlyList<float3> SecondRailPoints;
		public readonly float FirstRailRadius;
		public readonly float SecondRailRadius;
		public readonly int BendStartPointIndex;
		public readonly float3 FirstDropLinePoint;
		public readonly float3 SecondDropLinePoint;

		public WireRailDropProfile(WireRailPathFrame frame,
			IReadOnlyList<float3> firstRailPoints,
			IReadOnlyList<float3> secondRailPoints,
			float firstRailRadius, float secondRailRadius, int bendStartPointIndex,
			float3 firstDropLinePoint, float3 secondDropLinePoint)
		{
			Frame = frame;
			FirstRailPoints = firstRailPoints;
			SecondRailPoints = secondRailPoints;
			FirstRailRadius = firstRailRadius;
			SecondRailRadius = secondRailRadius;
			BendStartPointIndex = bendStartPointIndex;
			FirstDropLinePoint = firstDropLinePoint;
			SecondDropLinePoint = secondDropLinePoint;
		}
	}

	internal static class WireRailFixtureMeshGenerator
	{
		private const float FullTurn = math.PI * 2f;
		private const float LegCornerRadiusDiameterRatio = 1f;
		private const float RoundedCornerMaxAngleStep = math.PI / 12f;
		private const float LegCornerMaxSpanFraction = 0.45f;
		private const int DropLoopColliderLeadSegments = 4;
		private const int DropLoopColliderTerminalSegments = 12;
		private const float DropLoopAttachmentEpsilon = 1e-5f;

		internal static bool HasDropLoopAttachmentOffset(WireRailDropLoopFixture dropLoop)
			=> dropLoop != null && (math.abs(dropLoop.LateralOffset) > DropLoopAttachmentEpsilon
				|| math.abs(dropLoop.VerticalOffset) > DropLoopAttachmentEpsilon);

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
				} else if (fixture is WireRailVBraceFixture vBrace
					&& TryEvaluateVBraceProfile(spline, segments, vBrace,
						out var vBraceProfile)) {
					AppendVBrace(vBraceProfile, vBrace, wireCapBevelSize, radialSegments,
						vertices, normals, uvs, indices);
				} else if (fixture is WireRailCrossWireFixture crossWire
					&& TryEvaluateCrossWireProfile(spline, segments, crossWire,
						out var crossWireProfile)) {
					AppendCrossWire(crossWireProfile, crossWire, wireCapBevelSize,
						radialSegments, vertices, normals, uvs, indices);
				} else if (fixture is WireRailLegFixture leg
					&& TryEvaluateLegProfile(spline, segments, leg, out var legProfile)) {
					AppendLeg(legProfile, leg, wireCapBevelSize, radialSegments,
						vertices, normals, uvs, indices);
				} else if (fixture is WireRailDropLoopFixture dropLoop
					&& !WireRailEndpointTrimUtility.HasRailTrimConflict(fixtures,
						dropLoop.Endpoint, dropLoop.FirstRailIndex, dropLoop.SecondRailIndex,
						dropLoop)
					&& TryEvaluateDropLoopProfile(spline, segments, dropLoop,
						out var dropLoopProfile)) {
					AppendDropLoop(dropLoopProfile, dropLoop, wireCapBevelSize,
						radialSegments, vertices, normals, uvs, indices);
				} else if (fixture is WireRailDropFixture drop
					&& !WireRailEndpointTrimUtility.HasRailTrimConflict(fixtures,
						drop.Endpoint, drop.FirstRailIndex, drop.SecondRailIndex, drop)
					&& TryEvaluateDropProfile(spline, segments, drop,
						out var dropProfile)) {
					AppendDrop(dropProfile, drop, wireCapBevelSize,
						radialSegments, vertices, normals, uvs, indices);
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

		internal static bool TryEvaluateVBraceProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailVBraceFixture vBrace,
			out WireRailVBraceProfile profile)
		{
			profile = default;
			if (vBrace == null || !TryGetSplineLocation(spline, segments, vBrace.Distance,
					out var segmentIndex, out var curveT, out var frame)) {
				return false;
			}
			var segment = segments[segmentIndex];
			var railOffsets = new List<float2>(segment.RailCount);
			var railRadii = new List<float>(segment.RailCount);
			var offsetsByIndex = new float2[segment.RailCount];
			var radiiByIndex = new float[segment.RailCount];
			var activeByIndex = new bool[segment.RailCount];
			var minimum = new float2(float.PositiveInfinity);
			var maximum = new float2(float.NegativeInfinity);
			var evaluationContext = new WireRailPathEvaluationContext();
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (!segment.IsRailActive(railIndex)) {
					continue;
				}
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
				railOffsets.Add(offset);
				railRadii.Add(radius);
				offsetsByIndex[railIndex] = offset;
				radiiByIndex[railIndex] = radius;
				activeByIndex[railIndex] = true;
				minimum = math.min(minimum, offset - radius);
				maximum = math.max(maximum, offset + radius);
			}
			if (railOffsets.Count == 0) {
				return false;
			}

			var tubeRadius = vBrace.Diameter * 0.5f;
			var envelopeCenterX = (minimum.x + maximum.x) * 0.5f;
			var theoreticalTip = TryCalculateDefaultVBraceOrigin(offsetsByIndex,
				radiiByIndex, activeByIndex, tubeRadius, envelopeCenterX,
				out var fittedOrigin) && fittedOrigin.y < minimum.y - 1e-5f
				? fittedOrigin
				: new float2(envelopeCenterX,
					minimum.y - WireRailLayout.MiddleRailHeight - tubeRadius * 2f);
			// Freeze the bottom-center anchor at the default fit so editing the authored
			// bottom length or arm angle reshapes the fixture without moving it.
			var defaultHalfAngle = math.radians(WireRailVBraceFixture.DefaultAngle * 0.5f);
			var defaultBottomRise = WireRailVBraceFixture.DefaultBottomLength * 0.5f
				/ math.tan(defaultHalfAngle);
			var originOffset = theoreticalTip + new float2(0f, defaultBottomRise)
				+ new float2(vBrace.LateralOffset, vBrace.VerticalOffset);

			var halfAngle = math.radians(vBrace.Angle * 0.5f);
			var leftDirection = new float2(-math.sin(halfAngle), math.cos(halfAngle));
			var rightDirection = new float2(math.sin(halfAngle), math.cos(halfAngle));
			var halfBottomLength = vBrace.BottomLength * 0.5f;
			var leftBottom = new float2(-halfBottomLength, 0f);
			var rightBottom = new float2(halfBottomLength, 0f);
			var rawPoints = new List<float2>(4);
			if (vBrace.LeftLength > 1e-5f) {
				rawPoints.Add(leftBottom + leftDirection * vBrace.LeftLength);
			}
			rawPoints.Add(leftBottom);
			rawPoints.Add(rightBottom);
			if (vBrace.RightLength > 1e-5f) {
				rawPoints.Add(rightBottom + rightDirection * vBrace.RightLength);
			}

			var rotation = math.radians(vBrace.Rotation);
			var rotationDirection = new float2(math.cos(rotation), math.sin(rotation));
			for (var pointIndex = 0; pointIndex < rawPoints.Count; pointIndex++) {
				var point = rawPoints[pointIndex];
				rawPoints[pointIndex] = originOffset + new float2(
					point.x * rotationDirection.x - point.y * rotationDirection.y,
					point.x * rotationDirection.y + point.y * rotationDirection.x);
			}
			var roundedOffsets = BuildRoundedVBraceOffsets(rawPoints,
				vBrace.CornerRadius, tubeRadius, vBrace.RingDensity);
			if (roundedOffsets == null || roundedOffsets.Count < 2) {
				return false;
			}
			var centerlinePoints = roundedOffsets
				.Select(frame.TransformOffset).ToArray();
			profile = new WireRailVBraceProfile(frame, railOffsets, railRadii,
				originOffset, centerlinePoints);
			return true;
		}

		private static bool TryCalculateDefaultVBraceOrigin(IReadOnlyList<float2> offsets,
			IReadOnlyList<float> radii, IReadOnlyList<bool> active, float tubeRadius,
			float envelopeCenterX, out float2 origin)
		{
			origin = default;
			if (offsets.Count < 4 || radii.Count < 4 || active.Count < 4
				|| !active[0] || !active[1] || !active[2] || !active[3]) {
				return false;
			}
			var bottomLeftSide = math.sign(offsets[0].x - envelopeCenterX);
			var bottomRightSide = math.sign(offsets[1].x - envelopeCenterX);
			var upperLeftSide = math.sign(offsets[2].x - envelopeCenterX);
			var upperRightSide = math.sign(offsets[3].x - envelopeCenterX);
			if (bottomLeftSide == 0f || bottomRightSide == 0f
				|| bottomLeftSide != upperLeftSide || bottomRightSide != upperRightSide
				|| bottomLeftSide == bottomRightSide
				|| !TryBuildOuterTangent(offsets[0], offsets[2], radii[0] + tubeRadius,
					radii[2] + tubeRadius, new float2(bottomLeftSide, 0f),
					out var leftPoint, out var leftDirection)
				|| !TryBuildOuterTangent(offsets[1], offsets[3], radii[1] + tubeRadius,
					radii[3] + tubeRadius, new float2(bottomRightSide, 0f),
					out var rightPoint, out var rightDirection)) {
				return false;
			}
			var denominator = Cross(leftDirection, rightDirection);
			if (math.abs(denominator) <= 1e-5f) {
				return false;
			}
			var distance = Cross(rightPoint - leftPoint, rightDirection) / denominator;
			origin = leftPoint + leftDirection * distance;
			return math.all(math.isfinite(origin));

			static bool TryBuildOuterTangent(float2 bottom, float2 upper,
				float bottomRadius, float upperRadius, float2 outward,
				out float2 point, out float2 direction)
			{
				point = default;
				direction = default;
				var delta = upper - bottom;
				var distance = math.length(delta);
				if (distance <= 1e-5f) {
					return false;
				}
				var along = delta / distance;
				var normalAlong = (bottomRadius - upperRadius) / distance;
				if (math.abs(normalAlong) >= 1f) {
					return false;
				}
				var normalAcross = math.sqrt(1f - normalAlong * normalAlong);
				var perpendicular = new float2(-along.y, along.x);
				var firstNormal = along * normalAlong + perpendicular * normalAcross;
				var secondNormal = along * normalAlong - perpendicular * normalAcross;
				var normal = math.dot(firstNormal, outward) >= math.dot(secondNormal, outward)
					? firstNormal : secondNormal;
				point = bottom + normal * bottomRadius;
				direction = new float2(-normal.y, normal.x);
				if (math.dot(direction, delta) < 0f) {
					direction = -direction;
				}
				return true;
			}

			static float Cross(float2 left, float2 right)
				=> left.x * right.y - left.y * right.x;
		}

		private static List<float2> BuildRoundedVBraceOffsets(IReadOnlyList<float2> points,
			float desiredRadius, float minimumRadius, int ringDensity)
		{
			var rounded = new List<float2>(points.Count + ringDensity);
			AddDistinct(rounded, points[0]);
			for (var pointIndex = 1; pointIndex < points.Count - 1; pointIndex++) {
				var previous = points[pointIndex - 1];
				var corner = points[pointIndex];
				var next = points[pointIndex + 1];
				var incoming = corner - previous;
				var outgoing = next - corner;
				var incomingLength = math.length(incoming);
				var outgoingLength = math.length(outgoing);
				if (incomingLength <= 1e-5f || outgoingLength <= 1e-5f) {
					AddDistinct(rounded, corner);
					continue;
				}
				var incomingDirection = incoming / incomingLength;
				var outgoingDirection = outgoing / outgoingLength;
				var dot = math.clamp(math.dot(incomingDirection, outgoingDirection), -1f, 1f);
				var cross = incomingDirection.x * outgoingDirection.y
					- incomingDirection.y * outgoingDirection.x;
				var cornerAngle = math.acos(dot);
				if (cornerAngle <= math.radians(0.5f) || math.abs(cross) <= 1e-6f) {
					AddDistinct(rounded, corner);
					continue;
				}
				var tangentScale = math.tan(cornerAngle * 0.5f);
				var tangentDistance = math.min(math.max(0.05f, desiredRadius)
					* tangentScale, math.min(incomingLength, outgoingLength)
					* WireRailVBraceFixture.MaximumCornerSpanFraction);
				var radius = tangentDistance / tangentScale;
				if (radius + 1e-5f < minimumRadius) {
					return null;
				}
				var start = corner - incomingDirection * tangentDistance;
				var end = corner + outgoingDirection * tangentDistance;
				var turnSign = math.sign(cross);
				var center = start + new float2(-incomingDirection.y,
					incomingDirection.x) * radius * turnSign;
				var startRadius = start - center;
				var segmentCount = math.max(2, (int)math.ceil(math.max(
					ringDensity * cornerAngle / FullTurn,
					cornerAngle / RoundedCornerMaxAngleStep)));
				AddDistinct(rounded, start);
				for (var segmentIndex = 1; segmentIndex < segmentCount; segmentIndex++) {
					var angle = cornerAngle * turnSign * segmentIndex / segmentCount;
					var direction = new float2(math.cos(angle), math.sin(angle));
					AddDistinct(rounded, center + new float2(
						startRadius.x * direction.x - startRadius.y * direction.y,
						startRadius.x * direction.y + startRadius.y * direction.x));
				}
				AddDistinct(rounded, end);
			}
			AddDistinct(rounded, points[^1]);
			return rounded;

			static void AddDistinct(List<float2> target, float2 point)
			{
				if (target.Count == 0 || math.distancesq(target[^1], point) > 1e-10f) {
					target.Add(point);
				}
			}
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
				leg.FootConnectionLength, WireRailLegFixture.FootBendSegments,
				leg.FootClockwise);
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

		internal static bool TryEvaluateDropLoopProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropLoopFixture dropLoop,
			out WireRailDropLoopProfile profile)
		{
			if (dropLoop == null) {
				profile = default;
				return false;
			}
			var leadSegments = math.max(2,
				(int)math.ceil(dropLoop.RingDensity * 0.25f));
			var terminalSegments = math.max(2,
				(int)math.ceil(dropLoop.RingDensity * 0.5f));
			return TryEvaluateDropLoopProfile(spline, segments, dropLoop, leadSegments,
				terminalSegments, out profile);
		}

		internal static bool TryEvaluateDropLoopColliderProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropLoopFixture dropLoop,
			out WireRailDropLoopProfile profile)
			=> TryEvaluateDropLoopProfile(spline, segments, dropLoop,
				DropLoopColliderLeadSegments, DropLoopColliderTerminalSegments, out profile);

		private static bool TryEvaluateDropLoopProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropLoopFixture dropLoop,
			int leadSegments, int terminalSegments, out WireRailDropLoopProfile profile)
		{
			profile = default;
			if (dropLoop == null || spline == null || spline.Closed
				|| !TryGetSplineLocation(spline, segments,
					dropLoop.Endpoint == WireRailEndpoint.Start ? 0f : spline.GetLength(),
					out var segmentIndex, out var curveT, out var frame)) {
				return false;
			}
			var segment = segments[segmentIndex];
			if (dropLoop.FirstRailIndex == dropLoop.SecondRailIndex
				|| dropLoop.FirstRailIndex < 0 || dropLoop.SecondRailIndex < 0
				|| dropLoop.FirstRailIndex >= segment.RailCount
				|| dropLoop.SecondRailIndex >= segment.RailCount
				|| !segment.IsRailActive(dropLoop.FirstRailIndex)
				|| !segment.IsRailActive(dropLoop.SecondRailIndex)) {
				return false;
			}

			var evaluationContext = new WireRailPathEvaluationContext();
			if (!WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments,
					evaluationContext, segmentIndex, dropLoop.FirstRailIndex, curveT,
					out var firstRail)
				|| !WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments,
					evaluationContext, segmentIndex, dropLoop.SecondRailIndex, curveT,
					out var secondRail)) {
				return false;
			}

			var offset = frame.Right * dropLoop.LateralOffset
				+ frame.Up * dropLoop.VerticalOffset;
			var firstAttachment = firstRail + offset;
			var secondAttachment = secondRail + offset;
			var outward = dropLoop.Endpoint == WireRailEndpoint.Start
				? -frame.Tangent : frame.Tangent;
			var pairAxis = math.normalizesafe(secondAttachment - firstAttachment,
				frame.Right);
			pairAxis = math.normalizesafe(math.mul(quaternion.AxisAngle(frame.Tangent,
				math.radians(dropLoop.Rotation)), pairAxis), frame.Right);
			var midpoint = (firstAttachment + secondAttachment) * 0.5f;
			var loopCenter = midpoint + outward * dropLoop.LeadLength;
			var loopRadius = dropLoop.LoopDiameter * 0.5f;
			var firstArc = loopCenter - pairAxis * loopRadius;
			var secondArc = loopCenter + pairAxis * loopRadius;
			if (math.distancesq(firstAttachment, firstArc)
				> math.distancesq(firstAttachment, secondArc)) {
				pairAxis = -pairAxis;
				(firstArc, secondArc) = (secondArc, firstArc);
			}

			var firstLead = BuildLead(firstAttachment, firstArc, outward,
				dropLoop.TangentLength, leadSegments);
			var secondLeadFromRail = BuildLead(secondAttachment, secondArc, outward,
				dropLoop.TangentLength, leadSegments);
			var secondLead = secondLeadFromRail.Reverse().ToArray();
			var terminal = new float3[terminalSegments + 1];
			for (var pointIndex = 0; pointIndex <= terminalSegments; pointIndex++) {
				var angle = math.PI * pointIndex / terminalSegments;
				terminal[pointIndex] = loopCenter - pairAxis * math.cos(angle) * loopRadius
					+ outward * math.sin(angle) * loopRadius;
			}
			var centerline = new List<float3>(firstLead.Length + terminal.Length
				+ secondLead.Length - 2);
			foreach (var point in firstLead) {
				AddDistinct(centerline, point);
			}
			var terminalStartSpan = centerline.Count - 1;
			for (var pointIndex = 1; pointIndex < terminal.Length; pointIndex++) {
				AddDistinct(centerline, terminal[pointIndex]);
			}
			var terminalEndSpan = centerline.Count - 1;
			for (var pointIndex = 1; pointIndex < secondLead.Length; pointIndex++) {
				AddDistinct(centerline, secondLead[pointIndex]);
			}
			profile = new WireRailDropLoopProfile(frame, firstLead, terminal,
				secondLead, centerline, terminalStartSpan, terminalEndSpan);
			return true;

			static void AddDistinct(List<float3> points, float3 point)
			{
				if (points.Count == 0 || math.distancesq(points[^1], point) > 1e-10f) {
					points.Add(point);
				}
			}

			static float3[] BuildLead(float3 attachment, float3 arcPoint,
				float3 outwardDirection, float tangentLength, int segmentCount)
			{
				var maximumTangentLength = math.distance(attachment, arcPoint) * 0.49f;
				var handleLength = math.min(math.max(0f, tangentLength), maximumTangentLength);
				var control1 = attachment + outwardDirection * handleLength;
				var control2 = arcPoint - outwardDirection * handleLength;
				var points = new float3[segmentCount + 1];
				for (var pointIndex = 0; pointIndex <= segmentCount; pointIndex++) {
					var t = pointIndex / (float)segmentCount;
					var inverse = 1f - t;
					points[pointIndex] = inverse * inverse * inverse * attachment
						+ 3f * inverse * inverse * t * control1
						+ 3f * inverse * t * t * control2
						+ t * t * t * arcPoint;
				}
				return points;
			}
		}

		internal static bool TryEvaluateDropProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropFixture drop,
			out WireRailDropProfile profile)
		{
			profile = default;
			if (drop == null || spline == null || spline.Closed) {
				return false;
			}
			// The offset shortens the rails: the drop attaches this far back from the endpoint.
			var splineLength = spline.GetLength();
			var railTrim = math.max(0f, drop.Offset);
			var attachmentDistance = drop.Endpoint == WireRailEndpoint.Start
				? railTrim : splineLength - railTrim;
			if (!TryGetSplineLocation(spline, segments, attachmentDistance,
					out var segmentIndex, out var curveT, out var frame)) {
				return false;
			}
			var segment = segments[segmentIndex];
			if (drop.FirstRailIndex == drop.SecondRailIndex
				|| drop.FirstRailIndex < 0 || drop.SecondRailIndex < 0
				|| drop.FirstRailIndex >= segment.RailCount
				|| drop.SecondRailIndex >= segment.RailCount
				|| !segment.IsRailActive(drop.FirstRailIndex)
				|| !segment.IsRailActive(drop.SecondRailIndex)) {
				return false;
			}

			var evaluationContext = new WireRailPathEvaluationContext();
			if (!WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments,
					evaluationContext, segmentIndex, drop.FirstRailIndex, curveT,
					out var firstAttachment)
				|| !WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments,
					evaluationContext, segmentIndex, drop.SecondRailIndex, curveT,
					out var secondAttachment)) {
				return false;
			}

			var outward = drop.Endpoint == WireRailEndpoint.Start
				? -frame.Tangent : frame.Tangent;
			outward = math.normalizesafe(math.mul(
				quaternion.AxisAngle(frame.Up, math.radians(drop.ZAngle)), outward),
				outward);
			var down = -frame.Up;
			var bendRadius = math.min(math.max(0.05f, drop.Diameter),
				drop.DropLength);
			var firstRailPoints = BuildPath(firstAttachment);
			var secondRailPoints = BuildPath(secondAttachment);
			if (firstRailPoints == null || secondRailPoints == null
				|| firstRailPoints.Count != secondRailPoints.Count) {
				return false;
			}
			var firstRailRadius = WireRailSplineGeometry.EvaluateWireDiameter(spline,
				segments, segmentIndex, drop.FirstRailIndex, curveT) * 0.5f;
			var secondRailRadius = WireRailSplineGeometry.EvaluateWireDiameter(spline,
				segments, segmentIndex, drop.SecondRailIndex, curveT) * 0.5f;
			const int bendStartPointIndex = 0;
			var dropLineDistance = bendRadius;
			profile = new WireRailDropProfile(frame, firstRailPoints, secondRailPoints,
				firstRailRadius, secondRailRadius, bendStartPointIndex,
				firstAttachment + outward * dropLineDistance,
				secondAttachment + outward * dropLineDistance);
			return true;

			List<float3> BuildPath(float3 attachment)
			{
				var bendStart = attachment;
				var bendCenter = bendStart + down * bendRadius;
				var bendNormal = math.normalizesafe(math.cross(outward, down));
				if (math.lengthsq(bendNormal) <= 1e-8f) {
					return null;
				}
				var bendSegments = math.max(2,
					(int)math.ceil((math.PI * 0.5f) / RoundedCornerMaxAngleStep));
				var points = new List<float3>(bendSegments + 3);
				AddDistinct(points, attachment);
				AddDistinct(points, bendStart);
				var startRadius = -down * bendRadius;
				for (var bendIndex = 1; bendIndex <= bendSegments; bendIndex++) {
					var angle = math.PI * 0.5f * bendIndex / bendSegments;
					AddDistinct(points, bendCenter + math.mul(
						quaternion.AxisAngle(bendNormal, angle), startRadius));
				}
				var bendEnd = bendCenter + outward * bendRadius;
				AddDistinct(points, bendEnd
					+ down * math.max(0f, drop.DropLength - bendRadius));
				return points;

				static void AddDistinct(ICollection<float3> target, float3 point)
				{
					if (target is List<float3> list && list.Count > 0
						&& math.distancesq(list[^1], point) <= 1e-10f) {
						return;
					}
					target.Add(point);
				}
			}
		}

		// Samples the two attached rails just before a drop's attachment so a preview can show
		// the rails leading into the drop. Points are ordered from furthest-back toward the
		// attachment (exclusive). Preview-only: this is not part of the generated mesh.
		internal static bool TryEvaluateDropIncomingLeads(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropFixture drop, float desiredLead,
			out List<float3> firstLead, out List<float3> secondLead)
		{
			firstLead = null;
			secondLead = null;
			if (drop == null || spline == null || spline.Closed || desiredLead <= 1e-3f) {
				return false;
			}
			var splineLength = spline.GetLength();
			var railTrim = math.max(0f, drop.Offset);
			var attachmentDistance = drop.Endpoint == WireRailEndpoint.Start
				? railTrim : splineLength - railTrim;
			// Incoming rail runs toward the spline interior (away from the drop's endpoint).
			var inwardSign = drop.Endpoint == WireRailEndpoint.Start ? 1f : -1f;
			var available = drop.Endpoint == WireRailEndpoint.Start
				? splineLength - attachmentDistance : attachmentDistance;
			var lead = math.clamp(desiredLead, 0f, math.max(0f, available));
			if (lead <= 1e-3f) {
				return false;
			}
			var sampleCount = math.clamp((int)math.ceil(lead / 5f), 4, 24);
			var context = new WireRailPathEvaluationContext();
			firstLead = new List<float3>(sampleCount);
			secondLead = new List<float3>(sampleCount);
			for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++) {
				var t = lead * (1f - (float)sampleIndex / sampleCount);
				var distance = math.clamp(attachmentDistance + inwardSign * t, 0f, splineLength);
				if (!TryGetSplineLocation(spline, segments, distance,
						out var segmentIndex, out var curveT, out _)) {
					continue;
				}
				var segment = segments[segmentIndex];
				if (drop.FirstRailIndex >= 0 && drop.FirstRailIndex < segment.RailCount
					&& segment.IsRailActive(drop.FirstRailIndex)
					&& WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments, context,
						segmentIndex, drop.FirstRailIndex, curveT, out var firstPos)) {
					firstLead.Add(firstPos);
				}
				if (drop.SecondRailIndex >= 0 && drop.SecondRailIndex < segment.RailCount
					&& segment.IsRailActive(drop.SecondRailIndex)
					&& WireRailSplineGeometry.TryEvaluateRailPosition(spline, segments, context,
						segmentIndex, drop.SecondRailIndex, curveT, out var secondPos)) {
					secondLead.Add(secondPos);
				}
			}
			return firstLead.Count > 0 || secondLead.Count > 0;
		}

		private static List<float3> BuildRoundedLegPath(IReadOnlyList<float3> points,
			int lastCornerIndex, float desiredRadius)
		{
			var rounded = new List<float3>(points.Count + math.max(0, lastCornerIndex) * 6);
			AddDistinct(rounded, points[0]);
			var finalRoundedCorner = math.min(lastCornerIndex, points.Count - 2);
			for (var pointIndex = 1; pointIndex < points.Count - 1; pointIndex++) {
				// A reversal at the rail attachment would run the leg back through the
				// attachment wire. Downstream joints are authorable foot geometry: retain
				// a sharp joint when no finite tangent fillet can represent the pose.
				if (pointIndex == 1 && IsAttachmentFoldback(points[pointIndex - 1],
						points[pointIndex], points[pointIndex + 1])) {
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

			static bool IsAttachmentFoldback(float3 previous, float3 corner, float3 next)
			{
				var incoming = math.normalizesafe(corner - previous);
				var outgoing = math.normalizesafe(next - corner);
				return math.lengthsq(incoming) > 1e-8f
					&& math.lengthsq(outgoing) > 1e-8f
					&& math.dot(incoming, outgoing) <= -math.cos(math.radians(5f));
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
					(int)math.ceil(cornerAngle / RoundedCornerMaxAngleStep));
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
			float connectionArmLength, int bendSegments, bool clockwise)
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
			if (clockwise) {
				for (var pointIndex = 0; pointIndex < points.Count; pointIndex++) {
					var point = points[pointIndex];
					point.x = -point.x;
					points[pointIndex] = point;
				}
			}
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
			segmentIndex = WireRailSplineGeometry.GetLayoutIndexAtDistance(segments,
				clampedDistance, length);
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

		private static void AppendVBrace(WireRailVBraceProfile profile,
			WireRailVBraceFixture vBrace, float capBevelSize, int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			AppendPolylineTube(profile.CenterlinePoints, profile.Frame,
				vBrace.Diameter, capBevelSize, radialSegments,
				vertices, normals, uvs, indices);
		}

		private static void AppendLeg(WireRailLegProfile profile,
			WireRailLegFixture leg, float capBevelSize, int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			AppendPolylineTube(profile.CombinedPath, profile.AttachmentProfile.Frame,
				leg.Diameter, capBevelSize, radialSegments,
				vertices, normals, uvs, indices, true);
		}

		private static void AppendDropLoop(WireRailDropLoopProfile profile,
			WireRailDropLoopFixture dropLoop, float capBevelSize, int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			var closeEnds = HasDropLoopAttachmentOffset(dropLoop);
			AppendPolylineTube(profile.CenterlinePoints, profile.Frame,
				dropLoop.Diameter, capBevelSize, radialSegments,
				vertices, normals, uvs, indices, closeEnds, closeEnds);
		}

		private static void AppendDrop(WireRailDropProfile profile,
			WireRailDropFixture drop, float capBevelSize, int radialSegments,
			ICollection<Vector3> vertices, ICollection<Vector3> normals,
			ICollection<Vector2> uvs, ICollection<int> indices)
		{
			AppendPolylineTube(profile.FirstRailPoints, profile.Frame,
				drop.Diameter, capBevelSize, radialSegments,
				vertices, normals, uvs, indices, false, true);
			AppendPolylineTube(profile.SecondRailPoints, profile.Frame,
				drop.Diameter, capBevelSize, radialSegments,
				vertices, normals, uvs, indices, false, true);
		}

		internal static void AppendPolylineTube(IReadOnlyList<float3> sourcePoints,
			WireRailPathFrame referenceFrame, float diameter, float capBevelSize,
			int radialSegments, ICollection<Vector3> vertices,
			ICollection<Vector3> normals, ICollection<Vector2> uvs,
			ICollection<int> indices, bool allowPathReversals = false)
			=> AppendPolylineTube(sourcePoints, referenceFrame, diameter, capBevelSize,
				radialSegments, vertices, normals, uvs, indices, true, true,
				allowPathReversals: allowPathReversals);

		internal static void AppendPolylineTube(IReadOnlyList<float3> sourcePoints,
			WireRailPathFrame referenceFrame, float diameter, float capBevelSize,
			int radialSegments, ICollection<Vector3> vertices,
			ICollection<Vector3> normals, ICollection<Vector2> uvs,
			ICollection<int> indices, bool capStart, bool capEnd,
			ICollection<int> secondaryIndices = null, int secondaryStartSpan = 0,
			int secondaryEndSpan = 0, bool allowPathReversals = false)
			=> AppendPolylineSweep(sourcePoints, referenceFrame, diameter, capBevelSize,
				radialSegments, 0f, vertices, normals, uvs, indices, capStart, capEnd,
				secondaryIndices, secondaryStartSpan, secondaryEndSpan, allowPathReversals);

		internal static void AppendPolylineBox(IReadOnlyList<float3> sourcePoints,
			WireRailPathFrame referenceFrame, float width,
			ICollection<Vector3> vertices, ICollection<int> indices,
			ICollection<int> secondaryIndices = null, int secondaryStartSpan = 0,
			int secondaryEndSpan = 0, bool closeEnds = false)
		{
			const int sideCount = 4;
			const float cornerRotation = math.PI * 0.25f;
			var firstRing = vertices.Count;
			AppendPolylineSweep(sourcePoints, referenceFrame, width * math.sqrt(2f), 0f,
				sideCount, cornerRotation, vertices, null, null, indices, false, false,
				secondaryIndices, secondaryStartSpan, secondaryEndSpan, false);
			if (!closeEnds || vertices.Count - firstRing < sideCount * 2) {
				return;
			}
			var lastRing = vertices.Count - sideCount;
			indices.Add(firstRing);
			indices.Add(firstRing + 1);
			indices.Add(firstRing + 2);
			indices.Add(firstRing);
			indices.Add(firstRing + 2);
			indices.Add(firstRing + 3);
			indices.Add(lastRing);
			indices.Add(lastRing + 2);
			indices.Add(lastRing + 1);
			indices.Add(lastRing);
			indices.Add(lastRing + 3);
			indices.Add(lastRing + 2);
		}

		private static void AppendPolylineSweep(IReadOnlyList<float3> sourcePoints,
			WireRailPathFrame referenceFrame, float diameter, float capBevelSize,
			int radialSegments, float radialRotation, ICollection<Vector3> vertices,
			ICollection<Vector3> normals, ICollection<Vector2> uvs,
			ICollection<int> indices, bool capStart, bool capEnd,
			ICollection<int> secondaryIndices, int secondaryStartSpan,
			int secondaryEndSpan, bool allowPathReversals)
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
			if (!allowPathReversals) {
				for (var pointIndex = 1; pointIndex < points.Count - 1; pointIndex++) {
					if (!IsPathReversal(points[pointIndex - 1], points[pointIndex],
						points[pointIndex + 1])) {
						continue;
					}
					return;
				}
			}

			var tubeRadius = math.max(0.05f, diameter * 0.5f);
			var firstSpan = math.distance(points[0], points[1]);
			var lastSpan = math.distance(points[^2], points[^1]);
			var startBevel = capStart
				? math.min(math.clamp(capBevelSize, 0f, tubeRadius), firstSpan * 0.5f)
				: 0f;
			var endBevel = capEnd
				? math.min(math.clamp(capBevelSize, 0f, tubeRadius), lastSpan * 0.5f)
				: 0f;
			var frames = new WireRailPathFrame[points.Count];
			for (var pointIndex = 0; pointIndex < points.Count; pointIndex++) {
				var tangent = pointIndex == 0
					? math.normalizesafe(points[1] - points[0], -referenceFrame.Up)
					: pointIndex == points.Count - 1
						? math.normalizesafe(points[^1] - points[^2], -referenceFrame.Up)
						: EvaluateInteriorTangent(points, pointIndex, -referenceFrame.Up,
							allowPathReversals);
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
					var angle = radialRotation + FullTurn * radialIndex / radialSegments;
					var radial = frame.Right * math.cos(angle) + frame.Up * math.sin(angle);
					vertices.Add((Vector3)(position + radial * tubeRadius));
					normals?.Add((Vector3)radial);
					uvs?.Add(new Vector2(pointIndex / (float)(points.Count - 1),
						radialIndex / (float)radialSegments));
				}
			}

			for (var pointIndex = 0; pointIndex < points.Count - 1; pointIndex++) {
				var spanIndices = secondaryIndices != null
					&& pointIndex >= secondaryStartSpan && pointIndex < secondaryEndSpan
					? secondaryIndices : indices;
				var current = firstRing + pointIndex * radialSegments;
				var next = current + radialSegments;
				for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
					var radialNext = (radialIndex + 1) % radialSegments;
					var a = current + radialIndex;
					var b = next + radialIndex;
					var c = current + radialNext;
					var d = next + radialNext;
					spanIndices.Add(a);
					spanIndices.Add(b);
					spanIndices.Add(d);
					spanIndices.Add(a);
					spanIndices.Add(d);
					spanIndices.Add(c);
				}
			}

			if (capStart) {
				WireRailCapMeshGenerator.Append(frames[0], tubeRadius, startBevel,
					radialSegments, true, vertices, normals, uvs, indices);
			}
			if (capEnd) {
				WireRailCapMeshGenerator.Append(frames[^1], tubeRadius, endBevel,
					radialSegments, false, vertices, normals, uvs, indices);
			}

			static float3 Project(float3 direction, float3 tangent)
				=> direction - tangent * math.dot(direction, tangent);

			static float3 EvaluateInteriorTangent(IReadOnlyList<float3> path,
				int pointIndex, float3 fallback, bool allowPathReversals)
			{
				var incoming = math.normalizesafe(path[pointIndex] - path[pointIndex - 1],
					fallback);
				var outgoing = math.normalizesafe(path[pointIndex + 1] - path[pointIndex],
					incoming);
				if (allowPathReversals
					&& math.dot(incoming, outgoing) <= -math.cos(math.radians(0.5f))) {
					return incoming;
				}
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

	internal readonly struct WireRailTopOpening
	{
		public readonly int FirstRailIndex;
		public readonly int SecondRailIndex;

		public bool IsValid => FirstRailIndex >= 0 && SecondRailIndex >= 0
			&& FirstRailIndex != SecondRailIndex;

		public WireRailTopOpening(int firstRailIndex, int secondRailIndex)
		{
			FirstRailIndex = firstRailIndex;
			SecondRailIndex = secondRailIndex;
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
		public WireRailTopOpening TopOpening { get; private set; }

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
			=> TryCreateCore(offsets, wireRadii, ballRadius, ballRadius * 2f, null, null, false,
				out profile, out _, out error);

		internal static bool TryCreate(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius, Vector2 ballCenterHint,
			out WireRailChannelProfile profile, out string error)
			=> TryCreateCore(offsets, wireRadii, ballRadius, ballRadius * 2f,
				(float2)ballCenterHint,
				null, false, out profile, out _, out error);

		internal static bool TryCreate(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius, Vector2 ballCenterHint,
			WireRailTopOpening? forcedTopOpening, bool forceClosed,
			out WireRailChannelProfile profile, out bool forcedOpeningUnavailable,
			out string error)
			=> TryCreate(offsets, wireRadii, ballRadius, ballRadius * 2f, ballCenterHint,
				forcedTopOpening, forceClosed, out profile, out forcedOpeningUnavailable,
				out error);

		internal static bool TryCreate(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius, float topOpeningDiameter,
			Vector2 ballCenterHint,
			WireRailTopOpening? forcedTopOpening, bool forceClosed,
			out WireRailChannelProfile profile, out bool forcedOpeningUnavailable,
			out string error)
			=> TryCreateCore(offsets, wireRadii, ballRadius, topOpeningDiameter,
				(float2)ballCenterHint,
				forcedTopOpening, forceClosed, out profile,
				out forcedOpeningUnavailable, out error);

		private static bool TryCreateCore(IReadOnlyList<Vector2> offsets,
			IReadOnlyList<float> wireRadii, float ballRadius, float topOpeningDiameter,
			float2? ballCenterHint,
			WireRailTopOpening? forcedTopOpening, bool forceClosed,
			out WireRailChannelProfile profile, out bool forcedOpeningUnavailable,
			out string error)
		{
			profile = null;
			forcedOpeningUnavailable = false;
			error = null;
			if (offsets == null || offsets.Count == 0) {
				error = "A collision channel needs at least one rail.";
				return false;
			}
			if (wireRadii == null || wireRadii.Count != offsets.Count) {
				error = "Every collision rail needs a matching wire radius.";
				return false;
			}
			if (wireRadii.Any(radius => radius <= 0f) || ballRadius <= 0f
				|| topOpeningDiameter <= 0f) {
				error = "Wire and reference-ball radii must be positive.";
				return false;
			}

			if (!TryGetRestingBallCenter(offsets, wireRadii, ballRadius, ballCenterHint,
					out var ballCenter, out error)) {
				return false;
			}
			var allSupportLines = new List<FacetLine>(offsets.Count);
			for (var railIndex = 0; railIndex < offsets.Count; railIndex++) {
				var offset = (float2)offsets[railIndex];
				var normal = math.normalizesafe(ballCenter - offset);
				if (math.lengthsq(normal) < 0.5f) {
					error = "A rail lies on the reference ball center and has no contact direction.";
					return false;
				}
				allSupportLines.Add(new FacetLine(offset + normal * wireRadii[railIndex], normal,
					NormalizeAngle(math.atan2(normal.y, normal.x)), offset,
					wireRadii[railIndex], railIndex));
			}
			allSupportLines.Sort((first, second) => first.Angle.CompareTo(second.Angle));

			var topOpeningInwardNormal = NormalizeAngle(-math.PI * 0.5f);
			var topOpeningGapIndex = -1;
			var hasPassableTopOpening = offsets.Count >= 5
				&& TryGetPassableTopOpening(allSupportLines, topOpeningInwardNormal,
					topOpeningDiameter, out topOpeningGapIndex);
			var topOpening = hasPassableTopOpening
				? GetOpening(allSupportLines, topOpeningGapIndex)
				: default;
			if (forcedTopOpening.HasValue && !forceClosed) {
				if (!forcedTopOpening.Value.IsValid
					|| FindOpeningGapIndex(allSupportLines, forcedTopOpening.Value) < 0) {
					forcedOpeningUnavailable = true;
					error = null;
					return false;
				}
				topOpening = forcedTopOpening.Value;
			}
			var openAtTop = !forceClosed && offsets.Count >= 5
				&& (forcedTopOpening.HasValue || hasPassableTopOpening);
			var closed = offsets.Count >= 5 && !openAtTop;
			var collisionIndices = SelectCollisionIndices(offsets.Count,
				openAtTop ? topOpening : null);
			var supportLines = new List<FacetLine>(collisionIndices.Count);
			var supportCenter = float2.zero;
			foreach (var line in allSupportLines) {
				if (!collisionIndices.Contains(line.RailIndex)) {
					continue;
				}
				supportLines.Add(line);
				supportCenter += line.RailCenter;
			}
			supportCenter /= supportLines.Count;
			var openingDirection = math.normalizesafe(ballCenter - supportCenter,
				new float2(0f, 1f));
			var ordered = closed
				? supportLines
				: openAtTop
					? OrderAroundOpening(supportLines,
						FindOpeningGapIndex(supportLines, topOpening))
					: OrderAroundOpening(supportLines, NormalizeAngle(math.atan2(
						-openingDirection.y, -openingDirection.x)));
			var lines = openAtTop
				? ordered
				: AddChamfers(ordered, ballCenter, ballRadius, closed);
			if (!TryBuildProfile(lines, ballCenter, ballRadius, closed, openAtTop,
					out profile, out error)) {
				return false;
			}
			profile.TopOpening = topOpening;
			return true;
		}

		private static List<int> SelectCollisionIndices(int railCount,
			WireRailTopOpening? topOpening)
		{
			if (railCount <= MaximumFacetCount) {
				return Enumerable.Range(0, railCount).ToList();
			}
			var selected = Enumerable.Range(0, 4).ToList();
			if (topOpening.HasValue) {
				AddTopOpeningRail(topOpening.Value.FirstRailIndex);
				AddTopOpeningRail(topOpening.Value.SecondRailIndex);
			}
			var availableTopRails = Enumerable.Range(4, railCount - 4)
				.Where(index => !selected.Contains(index)).ToList();
			var remaining = MaximumFacetCount - selected.Count;
			for (var i = 0; i < remaining; i++) {
				var availableIndex = remaining == 1
					? availableTopRails.Count / 2
					: (int)math.round(i * (availableTopRails.Count - 1f)
						/ (remaining - 1f));
				selected.Add(availableTopRails[availableIndex]);
			}
			selected.Sort();
			return selected;

			void AddTopOpeningRail(int railIndex)
			{
				if (railIndex >= 4 && !selected.Contains(railIndex)) {
					selected.Add(railIndex);
				}
			}
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
			=> OrderAroundOpening(sorted, FindGapIndex(sorted, openingInwardNormal));

		private static WireRailTopOpening GetOpening(IReadOnlyList<FacetLine> sorted,
			int gapIndex)
			=> new(sorted[gapIndex].RailIndex,
				sorted[(gapIndex + 1) % sorted.Count].RailIndex);

		private static int FindOpeningGapIndex(IReadOnlyList<FacetLine> sorted,
			WireRailTopOpening opening)
		{
			for (var gapIndex = 0; gapIndex < sorted.Count; gapIndex++) {
				if (sorted[gapIndex].RailIndex == opening.FirstRailIndex
					&& sorted[(gapIndex + 1) % sorted.Count].RailIndex
						== opening.SecondRailIndex) {
					return gapIndex;
				}
			}
			return -1;
		}

		private static List<FacetLine> OrderAroundOpening(IReadOnlyList<FacetLine> sorted,
			int gapIndex)
		{
			if (sorted.Count <= 1) {
				return sorted.ToList();
			}

			var result = new List<FacetLine>(sorted.Count);
			var previousAngle = float.NegativeInfinity;
			for (var ordinal = 1; ordinal <= sorted.Count; ordinal++) {
				var source = sorted[(gapIndex + ordinal) % sorted.Count];
				var angle = source.Angle;
				while (angle <= previousAngle) {
					angle += FullTurn;
				}
				result.Add(new FacetLine(source.Point, source.Normal, angle,
					source.RailCenter, source.RailRadius, source.RailIndex));
				previousAngle = angle;
			}
			return result;
		}

		private static bool TryGetPassableTopOpening(IReadOnlyList<FacetLine> sorted,
			float topOpeningInwardNormal, float ballDiameter, out int gapIndex)
		{
			gapIndex = 0;
			if (sorted.Count < 2) {
				return true;
			}
			var containingGapIndex = FindGapIndex(sorted, topOpeningInwardNormal);
			var containingNextIndex = (containingGapIndex + 1) % sorted.Count;
			if (!CoversTopDirection(sorted[containingGapIndex])
				&& !CoversTopDirection(sorted[containingNextIndex])
				&& GetClearGap(sorted[containingGapIndex], sorted[containingNextIndex])
					> ballDiameter + 1e-5f) {
				gapIndex = containingGapIndex;
				return true;
			}
			var selectedFirstRailIndex = int.MaxValue;
			var selectedSecondRailIndex = int.MaxValue;
			var found = false;
			for (var candidateGapIndex = 0; candidateGapIndex < sorted.Count;
				candidateGapIndex++) {
				var nextIndex = (candidateGapIndex + 1) % sorted.Count;
				if (!CoversTopDirection(sorted[candidateGapIndex])
					&& !CoversTopDirection(sorted[nextIndex])) {
					continue;
				}
				var clearance = GetClearGap(sorted[candidateGapIndex], sorted[nextIndex]);
				if (clearance <= ballDiameter + 1e-5f) {
					continue;
				}
				var firstRailIndex = math.min(sorted[candidateGapIndex].RailIndex,
					sorted[nextIndex].RailIndex);
				var secondRailIndex = math.max(sorted[candidateGapIndex].RailIndex,
					sorted[nextIndex].RailIndex);
				if (found && (firstRailIndex > selectedFirstRailIndex
					|| firstRailIndex == selectedFirstRailIndex
					&& secondRailIndex >= selectedSecondRailIndex)) {
					continue;
				}
				gapIndex = candidateGapIndex;
				selectedFirstRailIndex = firstRailIndex;
				selectedSecondRailIndex = secondRailIndex;
				found = true;
			}
			return found;

			bool CoversTopDirection(FacetLine line)
			{
				var angularRadius = math.asin(math.saturate(line.RailRadius
					/ (line.RailRadius + ballDiameter * 0.5f)));
				return AngularDistance(line.Angle, topOpeningInwardNormal)
					<= angularRadius;
			}
		}

		private static float GetClearGap(FacetLine first, FacetLine second)
			=> math.distance(first.RailCenter, second.RailCenter)
				- first.RailRadius - second.RailRadius;

		private static float AngularDistance(float first, float second)
			=> math.abs(math.atan2(math.sin(first - second), math.cos(first - second)));

		private static int FindGapIndex(IReadOnlyList<FacetLine> sorted,
			float targetAngle)
		{
			var gapIndex = sorted.Count - 1;
			for (var i = 0; i < sorted.Count; i++) {
				var start = sorted[i].Angle;
				var end = sorted[(i + 1) % sorted.Count].Angle;
				if (i == sorted.Count - 1) {
					end += FullTurn;
				}
				var target = targetAngle;
				if (target < start) {
					target += FullTurn;
				}
				if (target >= start && target <= end) {
					gapIndex = i;
					break;
				}
			}
			return gapIndex;
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
			float2 ballCenter, float ballRadius, bool closed, bool connectOpenRails,
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

			if (connectOpenRails) {
				foreach (var line in lines) {
					profile.Vertices.Add(line.Point);
				}
				for (var i = 0; i < lines.Count - 1; i++) {
					profile.Spans.Add(new WireRailProfileSpan(i, i + 1));
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
			public readonly float2 RailCenter;
			public readonly float RailRadius;
			public readonly int RailIndex;

			public FacetLine(float2 point, float2 normal, float angle,
				float2 railCenter = default, float railRadius = 0f,
				int railIndex = -1)
			{
				Point = point;
				Normal = normal;
				Angle = angle;
				RailCenter = railCenter;
				RailRadius = railRadius;
				RailIndex = railIndex;
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

	internal readonly struct WireRailColliderWidening
	{
		private const float MinimumLength = 0.01f;

		public readonly bool WidenStart;
		public readonly float StartSize;
		public readonly float StartLength;
		public readonly bool WidenExit;
		public readonly float ExitSize;
		public readonly float ExitLength;
		public bool HasStartTaper => WidenStart && StartSize > 1f + 1e-5f;
		public bool HasExitTaper => WidenExit && ExitSize > 1f + 1e-5f;

		public WireRailColliderWidening(bool widenStart, float startSize,
			float startLength, bool widenExit, float exitSize, float exitLength)
		{
			WidenStart = widenStart;
			StartSize = math.max(1f, startSize);
			StartLength = math.max(MinimumLength, startLength);
			WidenExit = widenExit;
			ExitSize = math.max(1f, exitSize);
			ExitLength = math.max(MinimumLength, exitLength);
		}

		public float EvaluateRadius(float radius, float distance, float routeLength)
		{
			var scale = 1f;
			if (HasStartTaper) {
				var startBlend = math.saturate(distance / StartLength);
				scale = math.max(scale, math.lerp(StartSize, 1f, startBlend));
			}
			if (HasExitTaper) {
				var distanceFromExit = math.max(0f, routeLength - distance);
				var exitBlend = math.saturate(distanceFromExit / ExitLength);
				// Overlapping endpoint tapers describe alternative clearances; multiplying them
				// would create a larger, unintended bulge in the middle of a short route.
				scale = math.max(scale, math.lerp(ExitSize, 1f, exitBlend));
			}
			return radius * scale;
		}
	}

	internal static class WireRailColliderMeshGenerator
	{
		private const int MaximumAdaptiveDepth = 10;
		[ThreadStatic] private static ColliderBuffers _threadBuffers;

		private sealed class ColliderBuffers
		{
			public readonly List<Vector3> Vertices = new();
			public readonly List<int> Indices = new();
			public readonly List<int> TerminalIndices = new();
			public readonly List<Vector3> Edges = new();
			public readonly List<int> ActiveRailIndices = new(6);
			public readonly List<ColliderSample> Samples = new();
			public readonly List<float> BoundaryParameters = new(16);
			public readonly float[] StartTrimOffsets =
				new float[WireRailEndpointTrimUtility.MaximumRailCount];
			public readonly float[] EndTrimOffsets =
				new float[WireRailEndpointTrimUtility.MaximumRailCount];
			public readonly WireRailPathEvaluationContext EvaluationContext = new();

			public void Clear()
			{
				Vertices.Clear();
				Indices.Clear();
				TerminalIndices.Clear();
				Edges.Clear();
				ActiveRailIndices.Clear();
				Samples.Clear();
				BoundaryParameters.Clear();
			}
		}

		public static bool TryGenerate(Spline spline, IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, float ballDiameter,
			WireRailColliderWidening widening, int samplesPerSegment, Mesh target,
			out Mesh mesh, out Vector3[] edgeVertices, out int topologyRetryCount,
			out string error)
		{
			topologyRetryCount = 0;
			var buffers = _threadBuffers ??= new ColliderBuffers();
			buffers.Clear();
			var vertices = buffers.Vertices;
			var indices = buffers.Indices;
			var terminalIndices = buffers.TerminalIndices;
			var edges = buffers.Edges;
			var ballRadius = ballDiameter * 0.5f;
			var evaluationContext = buffers.EvaluationContext;
			evaluationContext.Reset(spline);
			if (spline == null || spline.Closed) {
				widening = default;
			}
			// The Drop's per-rail cutoffs never touch the collider (only its render mesh), but
			// its offset shortens the colliders of the two rails it connects to: they end that
			// far short of the endpoint, where the vertical drop faces then take over. The
			// outer rails keep their full colliders.
			WireRailEndpointTrimUtility.Collect(spline, segments, fixtures,
				buffers.StartTrimOffsets, buffers.EndTrimOffsets, includeDrop: false);
			// Drops exist only on open splines; skip their collider contributions otherwise.
			if (spline != null && !spline.Closed) {
				ShortenDropAttachedRailColliders(segments, fixtures,
					evaluationContext.SplineLength, buffers.StartTrimOffsets,
					buffers.EndTrimOffsets);
			}

			for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
				if (!HasActiveRails(segments[segmentIndex])) {
					continue;
				}
				if (!AppendTrimmedSegment(spline, segments, evaluationContext, segmentIndex,
						ballRadius, widening, samplesPerSegment, buffers.StartTrimOffsets,
						buffers.EndTrimOffsets, buffers.ActiveRailIndices, buffers.Samples,
						buffers.BoundaryParameters, vertices, indices, edges,
						out var segmentTopologyRetryCount, out error)) {
					mesh = target;
					edgeVertices = Array.Empty<Vector3>();
					return false;
				}
				topologyRetryCount += segmentTopologyRetryCount;
			}
			AppendDropLoopColliders(spline, segments, fixtures, buffers);
			AppendDropColliders(spline, segments, fixtures, ballRadius, widening, buffers);

			mesh = target ? target : new Mesh();
			mesh.Clear(false);
			mesh.name = "Wire Rail Collider (Generated)";
			mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.subMeshCount = terminalIndices.Count > 0 ? 2 : 1;
			mesh.SetTriangles(indices, 0, false);
			if (terminalIndices.Count > 0) {
				mesh.SetTriangles(terminalIndices, 1, false);
			}
			mesh.RecalculateBounds();
			edgeVertices = edges.ToArray();
			error = null;
			return true;
		}

		private static void AppendDropLoopColliders(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, ColliderBuffers buffers)
		{
			if (fixtures == null) {
				return;
			}
			foreach (var fixture in fixtures) {
				if (fixture is not WireRailDropLoopFixture dropLoop
					|| WireRailEndpointTrimUtility.HasRailTrimConflict(fixtures,
						dropLoop.Endpoint, dropLoop.FirstRailIndex, dropLoop.SecondRailIndex,
						dropLoop)
					|| !WireRailFixtureMeshGenerator.TryEvaluateDropLoopColliderProfile(spline,
						segments, dropLoop, out var profile)) {
					continue;
				}
				WireRailFixtureMeshGenerator.AppendPolylineBox(profile.CenterlinePoints,
					profile.Frame, dropLoop.Diameter,
					buffers.Vertices, buffers.Indices,
					buffers.TerminalIndices, profile.TerminalStartSpan,
					profile.TerminalEndSpan,
					WireRailFixtureMeshGenerator.HasDropLoopAttachmentOffset(dropLoop));
			}
		}

		// Shortens the collider only for the two rails the Drop connects to: a positive offset
		// trims those rails at the drop's attachment so they end that far short of the spline
		// end, matching the render. The outer rails keep their full colliders.
		private static void ShortenDropAttachedRailColliders(
			IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, float splineLength,
			float[] startOffsets, float[] endOffsets)
		{
			if (fixtures == null) {
				return;
			}
			foreach (var fixture in fixtures) {
				if (fixture is not WireRailDropFixture drop
					|| !WireRailEndpointTrimUtility.IsDropGeneratable(segments, drop,
						splineLength)
					|| WireRailEndpointTrimUtility.HasRailTrimConflict(fixtures,
						drop.Endpoint, drop.FirstRailIndex, drop.SecondRailIndex, drop)) {
					continue;
				}
				var trim = math.max(0f, drop.Offset);
				if (trim <= 1e-5f) {
					continue;
				}
				var destination = drop.Endpoint == WireRailEndpoint.Start
					? startOffsets : endOffsets;
				Shorten(drop.FirstRailIndex);
				Shorten(drop.SecondRailIndex);

				void Shorten(int railIndex)
				{
					if (railIndex >= 0 && railIndex < destination.Length) {
						destination[railIndex] = math.max(destination[railIndex], trim);
					}
				}
			}
		}

		// After the channel is scaled to the drop point, the two floor faces the ball rests on
		// are extended straight down there for the drop length, so the ball rolls to the
		// shortened end and then falls once it clears the vertical section.
		private static void AppendDropColliders(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			IReadOnlyList<WireRailFixture> fixtures, float ballRadius,
			WireRailColliderWidening widening,
			ColliderBuffers buffers)
		{
			// Drops exist only on open splines.
			if (fixtures == null || spline == null || spline.Closed) {
				return;
			}
			var splineLength = buffers.EvaluationContext.SplineLength;
			foreach (var fixture in fixtures) {
				if (fixture is not WireRailDropFixture drop
					|| !WireRailEndpointTrimUtility.IsDropGeneratable(segments, drop,
						splineLength)
					|| WireRailEndpointTrimUtility.HasRailTrimConflict(fixtures,
						drop.Endpoint, drop.FirstRailIndex, drop.SecondRailIndex, drop)
					|| !TryGetDropEndpointProfile(spline, segments, drop, ballRadius, widening,
						buffers, out var frame, out var profile)) {
					continue;
				}
				// The channel floor can have several up-facing spans; the two the ball rests
				// on are the pair meeting at the lowest point of the cross-section. Only those
				// two get extended down, matching the V the ball rides into the drop.
				var lowestVertex = -1;
				var lowestHeight = float.PositiveInfinity;
				foreach (var span in profile.Spans) {
					if (!IsDropFloorSpan(profile, span)) {
						continue;
					}
					UpdateLowest(span.StartVertex);
					UpdateLowest(span.EndVertex);
				}
				if (lowestVertex < 0) {
					continue;
				}

				var down = -frame.Up;
				var vertices = buffers.Vertices;
				var indices = buffers.Indices;
				foreach (var span in profile.Spans) {
					if (!IsDropFloorSpan(profile, span)
						|| (span.StartVertex != lowestVertex
							&& span.EndVertex != lowestVertex)) {
						continue;
					}
					var top0 = frame.TransformOffset(profile.Vertices[span.StartVertex]);
					var top1 = frame.TransformOffset(profile.Vertices[span.EndVertex]);
					var baseIndex = vertices.Count;
					vertices.Add((Vector3)top0);
					vertices.Add((Vector3)(top0 + down * drop.DropLength));
					vertices.Add((Vector3)top1);
					vertices.Add((Vector3)(top1 + down * drop.DropLength));
					AppendTwoSidedQuad(baseIndex, baseIndex + 1, baseIndex + 2,
						baseIndex + 3, indices);
				}

				void UpdateLowest(int vertexIndex)
				{
					if (profile.Vertices[vertexIndex].y < lowestHeight) {
						lowestHeight = profile.Vertices[vertexIndex].y;
						lowestVertex = vertexIndex;
					}
				}
			}
		}

		private static bool TryGetDropEndpointProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, WireRailDropFixture drop,
			float ballRadius, WireRailColliderWidening widening, ColliderBuffers buffers,
			out WireRailPathFrame frame,
			out WireRailChannelProfile profile)
		{
			frame = default;
			profile = null;
			var splineLength = buffers.EvaluationContext.SplineLength;
			// The drop point moves back into the rails by a positive offset (the channel is
			// scaled to end there), so the faces sit on the shortened channel's floor.
			var railTrim = math.max(0f, drop.Offset);
			var dropPointDistance = math.clamp(drop.Endpoint == WireRailEndpoint.Start
				? railTrim : splineLength - railTrim, 0f, splineLength);
			var topOpeningDiameter = ballRadius * 2f;
			ballRadius = widening.EvaluateRadius(ballRadius, dropPointDistance, splineLength);
			var segmentIndex = WireRailSplineGeometry.GetLayoutIndexAtDistance(segments,
				dropPointDistance, splineLength);
			if (segmentIndex < 0) {
				return false;
			}
			var segment = segments[segmentIndex];
			var segmentStart = segment.Distance;
			var segmentEnd = segmentIndex + 1 < segments.Count
				? segments[segmentIndex + 1].Distance : splineLength;
			var curveT = segmentEnd > segmentStart
				? math.saturate((dropPointDistance - segmentStart) / (segmentEnd - segmentStart))
				: 0f;
			if (!WireRailSplineGeometry.TryEvaluateLayout(spline, segments, segmentIndex,
					curveT, out frame)) {
				return false;
			}
			// Build the profile from the rails actually present at the drop point: a rail is
			// gone here if its own endpoint trim (including this drop's offset on the attached
			// rails, or another Rail Trim) removed it before this distance. Otherwise the faces
			// would use a different cross-section than the rendered drop and normal channel.
			buffers.ActiveRailIndices.Clear();
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (segment.IsRailActive(railIndex)
					&& dropPointDistance >= buffers.StartTrimOffsets[railIndex] - 1e-4f
					&& dropPointDistance
						<= splineLength - buffers.EndTrimOffsets[railIndex] + 1e-4f) {
					buffers.ActiveRailIndices.Add(railIndex);
				}
			}
			if (buffers.ActiveRailIndices.Count == 0
				|| !TryCreateProfile(spline, segments, segmentIndex, curveT,
					buffers.ActiveRailIndices, ballRadius, topOpeningDiameter, null, false,
					out profile, out _, out _)
				|| profile.Spans.Count == 0) {
				profile = null;
				return false;
			}
			return true;
		}

		// A floor span is one whose surface faces up toward the resting ball; those are the
		// faces the ball actually sits on, so they are the ones the drop extends downward.
		private static bool IsDropFloorSpan(WireRailChannelProfile profile,
			WireRailProfileSpan span)
		{
			var start = profile.Vertices[span.StartVertex];
			var end = profile.Vertices[span.EndVertex];
			var edge = end - start;
			if (math.lengthsq(edge) <= 1e-8f) {
				return false;
			}
			var inwardNormal = math.normalizesafe(new float2(-edge.y, edge.x));
			var midpoint = (start + end) * 0.5f;
			if (math.dot(inwardNormal, profile.RestingBallCenter - midpoint) < 0f) {
				inwardNormal = -inwardNormal;
			}
			return inwardNormal.y > 1e-4f;
		}

		private static bool TryCreateProfile(Spline spline,
			IReadOnlyList<WireRailSegment> segments, int segmentIndex, float curveT,
			IReadOnlyList<int> activeRailIndices, float ballRadius, float topOpeningDiameter,
			WireRailTopOpening? forcedTopOpening, bool forceClosed,
			out WireRailChannelProfile profile, out bool forcedOpeningUnavailable,
			out string error)
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
				topOpeningDiameter,
				ballCenterHint, forcedTopOpening, forceClosed,
				out profile, out forcedOpeningUnavailable, out error);
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

		private static bool AppendTrimmedSegment(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext, int segmentIndex, float ballRadius,
			WireRailColliderWidening widening, int curvatureDetail,
			IReadOnlyList<float> startTrimOffsets,
			IReadOnlyList<float> endTrimOffsets, List<int> activeRailIndices,
			List<ColliderSample> samples, List<float> boundaryParameters,
			List<Vector3> vertices, List<int> indices, List<Vector3> edges,
			out int topologyRetryCount, out string error)
		{
			topologyRetryCount = 0;
			error = null;
			var splineLength = evaluationContext.SplineLength;
			WireRailEndpointTrimUtility.GetSegmentDistances(segments, segmentIndex,
				splineLength, out var segmentStart, out var segmentEnd);
			var segmentLength = segmentEnd - segmentStart;
			if (segmentLength <= 1e-5f) {
				return true;
			}
			var segment = segments[segmentIndex];
			boundaryParameters.Clear();
			boundaryParameters.Add(0f);
			boundaryParameters.Add(1f);
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (!segment.IsRailActive(railIndex)) {
					continue;
				}
				AddBoundary(startTrimOffsets[railIndex]);
				AddBoundary(splineLength - endTrimOffsets[railIndex]);
			}
			if (widening.HasStartTaper) {
				AddBoundary(widening.StartLength);
			}
			if (widening.HasExitTaper) {
				AddBoundary(splineLength - widening.ExitLength);
			}
			boundaryParameters.Sort();
			for (var boundaryIndex = boundaryParameters.Count - 1; boundaryIndex > 0;
				boundaryIndex--) {
				if (boundaryParameters[boundaryIndex]
					- boundaryParameters[boundaryIndex - 1] <= 1e-5f) {
					boundaryParameters.RemoveAt(boundaryIndex);
				}
			}

			for (var intervalIndex = 0; intervalIndex < boundaryParameters.Count - 1;
				intervalIndex++) {
				var startT = boundaryParameters[intervalIndex];
				var endT = boundaryParameters[intervalIndex + 1];
				if (endT - startT <= 1e-5f) {
					continue;
				}
				var midpointDistance = math.lerp(segmentStart, segmentEnd,
					(startT + endT) * 0.5f);
				activeRailIndices.Clear();
				var authoredActiveRailCount = 0;
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					if (!segment.IsRailActive(railIndex)) {
						continue;
					}
					authoredActiveRailCount++;
					if (midpointDistance + 1e-5f >= startTrimOffsets[railIndex]
						&& midpointDistance - 1e-5f
							<= splineLength - endTrimOffsets[railIndex]) {
						activeRailIndices.Add(railIndex);
					}
				}
				if (activeRailIndices.Count == 0) {
					continue;
				}
				var trimmedRailSet = activeRailIndices.Count < authoredActiveRailCount;
				var midpointT = (startT + endT) * 0.5f;
				var midpointRadius = widening.EvaluateRadius(ballRadius, midpointDistance,
					splineLength);
				if (trimmedRailSet && !TryCreateProfile(spline, segments, segmentIndex,
						midpointT, activeRailIndices, midpointRadius, ballRadius * 2f, null, false,
						out _, out _, out _)) {
					continue;
				}
				if (!AppendSegmentRange(spline, segments, evaluationContext, segmentIndex,
						startT, endT, ballRadius, widening, curvatureDetail, activeRailIndices,
						samples, vertices, indices, edges, out var topologyRetried, out error)) {
					return false;
				}
				if (topologyRetried) {
					topologyRetryCount++;
				}
			}
			return true;

			void AddBoundary(float distance)
			{
				if (distance <= segmentStart + 1e-5f || distance >= segmentEnd - 1e-5f) {
					return;
				}
				boundaryParameters.Add(math.saturate(
					(distance - segmentStart) / segmentLength));
			}
		}

		private static bool AppendSegmentRange(Spline spline,
			IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext, int segmentIndex,
			float startT, float endT, float ballRadius, WireRailColliderWidening widening,
			int curvatureDetail, List<int> activeRailIndices, List<ColliderSample> samples,
			List<Vector3> vertices, List<int> indices, List<Vector3> edges,
			out bool topologyRetried, out string error)
		{
			if (!TryBuildAdaptiveSamples(spline, segments, evaluationContext, segmentIndex,
					startT, endT, activeRailIndices, ballRadius, widening, curvatureDetail, samples,
					out topologyRetried, out error)) {
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
			IReadOnlyList<WireRailSegment> segments,
			WireRailPathEvaluationContext evaluationContext, int segmentIndex,
			float startT, float endT, IReadOnlyList<int> activeRailIndices,
			float ballRadius, WireRailColliderWidening widening, int curvatureDetail,
			List<ColliderSample> samples, out bool topologyRetried, out string error)
		{
			topologyRetried = false;
			samples.Clear();
			var evaluationError = default(string);
			WireRailTopOpening? forcedTopOpening = null;
			WireRailTopOpening? detectedTopOpening = null;
			var forceClosed = false;
			var openingPairMismatch = false;
			var forcedOpeningUnavailable = false;
			WireRailEndpointTrimUtility.GetSegmentDistances(segments, segmentIndex,
				evaluationContext.SplineLength, out var segmentStart, out var segmentEnd);
			if (!TryEvaluateSample(startT, out var start)
				|| !TryEvaluateSample(math.lerp(startT, endT, 0.25f),
					out var probeFirstQuarter)
				|| !TryEvaluateSample(math.lerp(startT, endT, 0.5f),
					out var probeMiddle)
				|| !TryEvaluateSample(math.lerp(startT, endT, 0.75f),
					out var probeThirdQuarter)
				|| !TryEvaluateSample(endT, out var end)) {
				error = evaluationError;
				return false;
			}
			RememberTopOpening(start.Profile);
			RememberTopOpening(probeFirstQuarter.Profile);
			RememberTopOpening(probeMiddle.Profile);
			RememberTopOpening(probeThirdQuarter.Profile);
			RememberTopOpening(end.Profile);
			forcedTopOpening = detectedTopOpening;
			if (forcedTopOpening.HasValue
				&& (!TryPrepareSample(ref start)
					|| !TryPrepareSample(ref probeFirstQuarter)
					|| !TryPrepareSample(ref probeMiddle)
					|| !TryPrepareSample(ref probeThirdQuarter)
					|| !TryPrepareSample(ref end))) {
				if (forcedOpeningUnavailable) {
					topologyRetried = true;
					return TryBuildClosed(out error);
				}
				error = evaluationError;
				return false;
			}
			if (openingPairMismatch) {
				error = OpeningMigrationError();
				return false;
			}
			if (!HasMatchingTopology(start.Profile, end.Profile)) {
				error = TopologyError();
				return false;
			}
			samples.Add(start);
			var topologyMismatch = false;
			if (TryAppendInterval(start, end, probeFirstQuarter, probeMiddle,
					probeThirdQuarter, 0, out error)) {
				return true;
			}
			if (forcedOpeningUnavailable) {
				topologyRetried = true;
				return TryBuildClosed(out error);
			}
			if (forcedTopOpening.HasValue || forceClosed || !topologyMismatch
				|| !detectedTopOpening.HasValue) {
				return false;
			}

			// An authored transition curve can create a narrow opening between the fixed
			// preflight probes. Once adaptive sampling discovers it, rebuild this span with
			// a consistently open profile instead of dropping the complete collider.
			forcedTopOpening = detectedTopOpening;
			topologyRetried = true;
			topologyMismatch = false;
			samples.Clear();
			forcedOpeningUnavailable = false;
			if (!TryPrepareSample(ref start)
				|| !TryPrepareSample(ref probeFirstQuarter)
				|| !TryPrepareSample(ref probeMiddle)
				|| !TryPrepareSample(ref probeThirdQuarter)
				|| !TryPrepareSample(ref end)) {
				if (forcedOpeningUnavailable) {
					return TryBuildClosed(out error);
				}
				error = evaluationError;
				return false;
			}
			samples.Add(start);
			var retried = TryAppendInterval(start, end, probeFirstQuarter, probeMiddle,
				probeThirdQuarter, 0, out error);
			return !retried && forcedOpeningUnavailable
				? TryBuildClosed(out error)
				: retried;

			bool TryAppendInterval(ColliderSample intervalStart, ColliderSample intervalEnd,
				ColliderSample? knownFirstQuarter, ColliderSample? knownMiddle,
				ColliderSample? knownThirdQuarter, int depth, out string intervalError)
			{
				var interval = intervalEnd.CurveT - intervalStart.CurveT;
				var firstQuarter = knownFirstQuarter.GetValueOrDefault();
				if (!knownFirstQuarter.HasValue
					&& !TryEvaluateSample(intervalStart.CurveT + interval * 0.25f,
						out firstQuarter)) {
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
				var thirdQuarter = knownThirdQuarter.GetValueOrDefault();
				if (!knownThirdQuarter.HasValue
					&& !TryEvaluateSample(intervalStart.CurveT + interval * 0.75f,
						out thirdQuarter)) {
					intervalError = evaluationError;
					return false;
				}
				if (!HasMatchingTopology(start.Profile, firstQuarter.Profile)
					|| !HasMatchingTopology(start.Profile, middle.Profile)
					|| !HasMatchingTopology(start.Profile, thirdQuarter.Profile)) {
					RememberTopOpening(firstQuarter.Profile);
					RememberTopOpening(middle.Profile);
					RememberTopOpening(thirdQuarter.Profile);
					if (openingPairMismatch) {
						intervalError = OpeningMigrationError();
						return false;
					}
					topologyMismatch = true;
					intervalError = TopologyError();
					return false;
				}
				if (depth < MaximumAdaptiveDepth
					&& ShouldSubdivide(intervalStart, firstQuarter, middle, thirdQuarter,
						intervalEnd, curvatureDetail)) {
					if (!TryAppendInterval(intervalStart, middle, null, firstQuarter, null,
							depth + 1, out intervalError)) {
						return false;
					}
					return TryAppendInterval(middle, intervalEnd, null, thirdQuarter, null,
						depth + 1, out intervalError);
				}
				samples.Add(intervalEnd);
				intervalError = null;
				return true;
			}

			bool TryEvaluateSample(float curveT, out ColliderSample sample)
			{
				sample = default;
				if (!WireRailSplineGeometry.TryEvaluateLayout(spline, segments, evaluationContext,
						segmentIndex, curveT, out var frame)) {
					evaluationError = $"Could not evaluate spline segment {segmentIndex + 1}.";
					return false;
				}
				if (!TryCreateProfile(spline, segments, segmentIndex, curveT,
						activeRailIndices, widening.EvaluateRadius(ballRadius,
							math.lerp(segmentStart, segmentEnd, curveT),
							evaluationContext.SplineLength), ballRadius * 2f,
						forcedTopOpening, forceClosed,
						out var profile, out var openingUnavailable,
						out evaluationError)) {
					forcedOpeningUnavailable |= openingUnavailable;
					return false;
				}
				sample = new ColliderSample(curveT, frame, profile);
				return true;
			}

			bool TryBuildClosed(out string closedError)
			{
				forcedTopOpening = null;
				detectedTopOpening = null;
				forceClosed = true;
				openingPairMismatch = false;
				forcedOpeningUnavailable = false;
				topologyMismatch = false;
				samples.Clear();
				if (!TryEvaluateSample(startT, out start)
					|| !TryEvaluateSample(math.lerp(startT, endT, 0.25f),
						out probeFirstQuarter)
					|| !TryEvaluateSample(math.lerp(startT, endT, 0.5f),
						out probeMiddle)
					|| !TryEvaluateSample(math.lerp(startT, endT, 0.75f),
						out probeThirdQuarter)
					|| !TryEvaluateSample(endT, out end)) {
					closedError = evaluationError;
					return false;
				}
				if (!HasMatchingTopology(start.Profile, end.Profile)) {
					closedError = TopologyError();
					return false;
				}
				samples.Add(start);
				return TryAppendInterval(start, end, probeFirstQuarter, probeMiddle,
					probeThirdQuarter, 0, out closedError);
			}

			bool TryPrepareSample(ref ColliderSample sample)
			{
				if (!forceClosed && !sample.Profile.IsClosed
					&& SameOpening(sample.Profile.TopOpening,
						forcedTopOpening.GetValueOrDefault())) {
					return true;
				}
				return TryEvaluateSample(sample.CurveT, out sample);
			}

			void RememberTopOpening(WireRailChannelProfile profile)
			{
				if (profile.IsClosed || !profile.TopOpening.IsValid) {
					return;
				}
				if (!detectedTopOpening.HasValue) {
					detectedTopOpening = profile.TopOpening;
				} else if (!SameOpening(detectedTopOpening.Value, profile.TopOpening)) {
					openingPairMismatch = true;
				}
			}

			bool SameOpening(WireRailTopOpening first, WireRailTopOpening second)
				=> first.FirstRailIndex == second.FirstRailIndex
					&& first.SecondRailIndex == second.SecondRailIndex;

			string OpeningMigrationError()
				=> $"The top opening moves between different rail pairs while blending "
					+ $"segment {segmentIndex + 1}. Add a wire layout where the opening changes.";

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

		private static Vector3 GetVertex(List<Vector3> vertices, int index)
			=> vertices[index];

		private static void AppendTwoSidedQuad(int a, int b, int c, int d,
			List<int> indices)
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
