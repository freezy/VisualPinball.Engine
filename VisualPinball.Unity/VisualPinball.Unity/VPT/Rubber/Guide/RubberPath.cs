// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public enum RubberPathSource
	{
		Spline,
		Guides,
	}

	public enum RubberPathElementType
	{
		FreeSpan,
		SupportedArc,
	}

	[Serializable]
	public struct RubberPathElement
	{
		public RubberPathElementType Type;
		public float2 Start;
		public float2 End;
		public float2 Control1;
		public float2 Control2;
		public float2 Center;
		public float Radius;
		public float StartAngleRad;
		public float SweepAngleRad;
		public int StartBindingIndex;
		public int EndBindingIndex;
		public float StartDistance;
		public float Length;
	}

	public static class RubberPathMath
	{
		private const int MaximumSubdivisionDepth = 12;
		private const float LengthTolerance = 1e-4f;

		public static float2 PointAt(RubberPathElement element, float t)
		{
			t = math.saturate(t);
			if (element.Type == RubberPathElementType.SupportedArc) {
				var angle = element.StartAngleRad + element.SweepAngleRad * t;
				return element.Center
					+ new float2(math.cos(angle), math.sin(angle)) * element.Radius;
			}

			var oneMinusT = 1f - t;
			return oneMinusT * oneMinusT * oneMinusT * element.Start
				+ 3f * oneMinusT * oneMinusT * t * element.Control1
				+ 3f * oneMinusT * t * t * element.Control2
				+ t * t * t * element.End;
		}

		/// <summary>
		/// Which side of a closed path its inside lies on, seen along the path: +1 when
		/// it is to the left of the tangent t, at (-t.y, t.x), -1 when it is to the right.
		/// From the signed area of the path, sampled along every element.
		/// </summary>
		public static int InsideSign(IReadOnlyList<RubberPathElement> path)
		{
			const int samplesPerElement = 8;
			var area = 0f;
			for (var i = 0; i < path.Count; i++) {
				var previous = PointAt(path[i], 0f);
				for (var j = 1; j <= samplesPerElement; j++) {
					var point = PointAt(path[i], j / (float)samplesPerElement);
					area += previous.x * point.y - point.x * previous.y;
					previous = point;
				}
			}
			return area >= 0f ? 1 : -1;
		}

		/// <summary>
		/// The unit normal of an element at <paramref name="t"/> that points into the
		/// path's inside, for the side <see cref="InsideSign"/> returned.
		/// </summary>
		public static float2 InwardAt(RubberPathElement element, float t, int insideSign)
		{
			var tangent = TangentAt(element, t);
			return insideSign >= 0
				? new float2(-tangent.y, tangent.x)
				: new float2(tangent.y, -tangent.x);
		}

		public static float2 TangentAt(RubberPathElement element, float t)
		{
			t = math.saturate(t);
			if (element.Type == RubberPathElementType.SupportedArc) {
				var angle = element.StartAngleRad + element.SweepAngleRad * t;
				var direction = new float2(-math.sin(angle), math.cos(angle));
				return element.SweepAngleRad < 0f ? -direction : direction;
			}

			var oneMinusT = 1f - t;
			var derivative = 3f * oneMinusT * oneMinusT
				* (element.Control1 - element.Start)
				+ 6f * oneMinusT * t * (element.Control2 - element.Control1)
				+ 3f * t * t * (element.End - element.Control2);
			return math.normalizesafe(derivative,
				math.normalizesafe(element.End - element.Start, new float2(1f, 0f)));
		}

		public static bool IsStraightFreeSpan(RubberPathElement element,
			float epsilon = 1e-4f)
		{
			if (element.Type != RubberPathElementType.FreeSpan) {
				return false;
			}
			var delta = element.End - element.Start;
			return math.distance(element.Control1, element.Start + delta / 3f) <= epsilon
				&& math.distance(element.Control2, element.Start + delta * (2f / 3f))
				<= epsilon;
		}

		public static int FreeSpanSubdivisionCount(RubberPathElement element,
			float tolerance)
		{
			if (element.Type != RubberPathElementType.FreeSpan
				|| !float.IsFinite(tolerance) || tolerance <= 0f) {
				return 1;
			}
			var depth = RequiredSubdivisionDepth(element.Start, element.Control1,
				element.Control2, element.End, tolerance, 0);
			return 1 << depth;
		}

		public static float FreeSpanLength(RubberPathElement element)
		{
			var subdivisions = FreeSpanSubdivisionCount(element, LengthTolerance);
			var length = 0f;
			var previous = element.Start;
			for (var i = 1; i <= subdivisions; i++) {
				var point = PointAt(element, i / (float)subdivisions);
				length += math.distance(previous, point);
				previous = point;
			}
			return length;
		}

		public static float DistanceToElement(float2 point, RubberPathElement element)
		{
			if (element.Type == RubberPathElementType.SupportedArc) {
				return math.abs(math.distance(point, element.Center) - element.Radius);
			}

			var subdivisions = FreeSpanSubdivisionCount(element, 1e-3f);
			var minimum = float.PositiveInfinity;
			var previous = element.Start;
			for (var i = 1; i <= subdivisions; i++) {
				var next = PointAt(element, i / (float)subdivisions);
				minimum = math.min(minimum, DistanceToSegment(point, previous, next));
				previous = next;
			}
			return minimum;
		}

		private static int RequiredSubdivisionDepth(float2 start, float2 control1,
			float2 control2, float2 end, float tolerance, int depth)
		{
			if (depth >= MaximumSubdivisionDepth
				|| math.max(DistanceToLine(control1, start, end),
					DistanceToLine(control2, start, end)) <= tolerance) {
				return depth;
			}

			var startControl = (start + control1) * 0.5f;
			var middleControl = (control1 + control2) * 0.5f;
			var endControl = (control2 + end) * 0.5f;
			var leftMiddle = (startControl + middleControl) * 0.5f;
			var rightMiddle = (middleControl + endControl) * 0.5f;
			var midpoint = (leftMiddle + rightMiddle) * 0.5f;
			return math.max(
				RequiredSubdivisionDepth(start, startControl, leftMiddle, midpoint,
					tolerance, depth + 1),
				RequiredSubdivisionDepth(midpoint, rightMiddle, endControl, end,
					tolerance, depth + 1));
		}

		private static float DistanceToLine(float2 point, float2 start, float2 end)
		{
			var delta = end - start;
			var length = math.length(delta);
			if (length <= 1e-6f) {
				return math.distance(point, start);
			}
			return math.abs(delta.x * (point.y - start.y)
				- delta.y * (point.x - start.x)) / length;
		}

		public static float DistanceToSegment(float2 point, float2 start, float2 end)
		{
			var delta = end - start;
			var lengthSquared = math.lengthsq(delta);
			if (lengthSquared <= 1e-12f) {
				return math.distance(point, start);
			}
			var t = math.saturate(math.dot(point - start, delta) / lengthSquared);
			return math.distance(point, start + delta * t);
		}
	}
}
