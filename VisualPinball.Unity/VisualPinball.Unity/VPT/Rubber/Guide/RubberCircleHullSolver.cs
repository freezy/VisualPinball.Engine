// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public readonly struct RubberGuideCircle
	{
		public readonly float2 Center;
		public readonly float Radius;
		public readonly int BindingIndex;

		public RubberGuideCircle(float2 center, float radius, int bindingIndex)
		{
			Center = center;
			Radius = radius;
			BindingIndex = bindingIndex;
		}
	}

	public sealed class RubberAutofitResult
	{
		public bool IsValid { get; internal set; }
		public RubberPathElement[] Elements { get; internal set; } = Array.Empty<RubberPathElement>();
		public int[] SupportingBindingIndices { get; internal set; } = Array.Empty<int>();
		public int[] EnclosedBindingIndices { get; internal set; } = Array.Empty<int>();
		public string Error { get; internal set; }
		public float Length => Elements.Sum(element => element.Length);
	}

	/// <summary>
	/// Computes the counter-clockwise boundary of the convex hull of expanded circular guides.
	/// The exact result alternates between free spans and outward circular support arcs.
	/// </summary>
	public static class RubberCircleHullSolver
	{
		private const float TwoPi = 2f * math.PI;
		private const float RoundnessEpsilon = 1e-6f;

		private readonly struct Circle
		{
			public readonly float2 Center;
			public readonly float Radius;
			public readonly int BindingIndex;
			public readonly int InputIndex;

			public Circle(RubberGuideCircle source, float cordRadius, int inputIndex)
			{
				Center = source.Center;
				Radius = source.Radius + cordRadius;
				BindingIndex = source.BindingIndex;
				InputIndex = inputIndex;
			}
		}

		private readonly struct Tangent
		{
			public readonly int StartCircle;
			public readonly int EndCircle;
			public readonly float2 Start;
			public readonly float2 End;
			public readonly float2 InwardNormal;

			public Tangent(int startCircle, int endCircle, float2 start, float2 end,
				float2 inwardNormal)
			{
				StartCircle = startCircle;
				EndCircle = endCircle;
				Start = start;
				End = end;
				InwardNormal = inwardNormal;
			}

			public float Length => math.distance(Start, End);
		}

		private readonly struct ContactArc
		{
			public readonly int CircleIndex;
			public readonly float StartAngle;
			public readonly float SweepAngle;
			public readonly float2 Start;
			public readonly float2 End;

			public ContactArc(int circleIndex, float startAngle, float sweepAngle,
				float2 start, float2 end)
			{
				CircleIndex = circleIndex;
				StartAngle = startAngle;
				SweepAngle = sweepAngle;
				Start = start;
				End = end;
			}

			public float EndAngle => StartAngle + SweepAngle;
		}

		public static RubberAutofitResult Solve(IReadOnlyList<RubberGuideCircle> guides,
			float cordRadius, float epsilon = 1e-4f, float roundness = 0f)
		{
			var result = new RubberAutofitResult();
			if (guides == null) {
				result.Error = "Guide circles are required.";
				return result;
			}
			if (!float.IsFinite(cordRadius) || cordRadius < 0f) {
				result.Error = "Cord radius must be finite and non-negative.";
				return result;
			}
			if (!float.IsFinite(epsilon) || epsilon <= 0f) {
				result.Error = "Solver epsilon must be finite and positive.";
				return result;
			}
			if (!float.IsFinite(roundness) || roundness < 0f || roundness > 1f) {
				result.Error = "Roundness must be finite and between zero and one.";
				return result;
			}

			var circles = new List<Circle>(guides.Count);
			for (var i = 0; i < guides.Count; i++) {
				var guide = guides[i];
				if (!math.all(math.isfinite(guide.Center)) || !float.IsFinite(guide.Radius)
					|| guide.Radius <= 0f) {
					result.Error = $"Guide binding {guide.BindingIndex} has invalid circle geometry.";
					return result;
				}
				circles.Add(new Circle(guide, cordRadius, i));
			}

			if (circles.Count == 0) {
				result.Error = "At least one guide binding is required.";
				return result;
			}

			var enclosed = new HashSet<int>();
			circles = RemoveDuplicatesAndContained(circles, epsilon, enclosed);
			if (circles.Count == 1) {
				// With no free spans, Roundness has nothing to relax.
				var circle = circles[0];
				var start = circle.Center + new float2(-circle.Radius, 0f);
				result.Elements = new[] {
					new RubberPathElement {
						Type = RubberPathElementType.SupportedArc,
						Start = start,
						End = start,
						Center = circle.Center,
						Radius = circle.Radius,
						StartAngleRad = math.PI,
						SweepAngleRad = TwoPi,
						StartBindingIndex = circle.BindingIndex,
						EndBindingIndex = circle.BindingIndex,
						Length = TwoPi * circle.Radius,
					},
				};
				result.SupportingBindingIndices = new[] { circle.BindingIndex };
				result.EnclosedBindingIndices = enclosed.OrderBy(index => index).ToArray();
				result.IsValid = true;
				return result;
			}

			var tangents = FindHullTangents(circles, epsilon);
			tangents = RemoveSubsegmentTangents(tangents, epsilon);
			if (!TryOrderCycle(circles, tangents, epsilon, out var cycle, out var cycleError)) {
				result.Error = cycleError;
				return result;
			}

			var supporting = new HashSet<int>();
			foreach (var tangent in cycle) {
				supporting.Add(circles[tangent.StartCircle].BindingIndex);
				supporting.Add(circles[tangent.EndCircle].BindingIndex);
			}
			foreach (var circle in circles) {
				if (!supporting.Contains(circle.BindingIndex)) {
					enclosed.Add(circle.BindingIndex);
				}
			}

			var elements = EmitElements(circles, cycle, epsilon, roundness);
			if (elements.Count == 0) {
				result.Error = "The guide hull produced no path elements.";
				return result;
			}
			Normalize(elements, epsilon);
			if (!RubberPathValidator.TryValidate(elements, epsilon, out var validationError)) {
				result.Error = validationError;
				return result;
			}
			if (!TryValidateGuideClearance(elements, circles, epsilon,
				out var clearanceError)) {
				result.Error = clearanceError;
				return result;
			}

			result.Elements = elements.ToArray();
			result.SupportingBindingIndices = supporting.OrderBy(index => index).ToArray();
			result.EnclosedBindingIndices = enclosed.OrderBy(index => index).ToArray();
			result.IsValid = true;
			return result;
		}

		private static List<Circle> RemoveDuplicatesAndContained(IReadOnlyList<Circle> source,
			float epsilon, ISet<int> enclosed)
		{
			var ordered = source
				.OrderBy(circle => circle.BindingIndex)
				.ThenBy(circle => circle.InputIndex)
				.ToList();
			var duplicatesRemoved = new List<Circle>(ordered.Count);
			foreach (var circle in ordered) {
				var duplicate = duplicatesRemoved.FindIndex(candidate =>
					math.distance(candidate.Center, circle.Center) <= epsilon
					&& math.abs(candidate.Radius - circle.Radius) <= epsilon);
				if (duplicate >= 0) {
					enclosed.Add(circle.BindingIndex);
				} else {
					duplicatesRemoved.Add(circle);
				}
			}

			var retained = new List<Circle>(duplicatesRemoved.Count);
			for (var i = 0; i < duplicatesRemoved.Count; i++) {
				var circle = duplicatesRemoved[i];
				var container = -1;
				for (var j = 0; j < duplicatesRemoved.Count; j++) {
					if (i == j) {
						continue;
					}
					var candidate = duplicatesRemoved[j];
					if (math.distance(circle.Center, candidate.Center) + circle.Radius
						<= candidate.Radius + epsilon) {
						if (container < 0 || candidate.Radius > duplicatesRemoved[container].Radius
							|| candidate.Radius == duplicatesRemoved[container].Radius
							&& candidate.BindingIndex < duplicatesRemoved[container].BindingIndex) {
							container = j;
						}
					}
				}
				if (container >= 0) {
					enclosed.Add(circle.BindingIndex);
				} else {
					retained.Add(circle);
				}
			}
			return retained;
		}

		private static List<Tangent> FindHullTangents(IReadOnlyList<Circle> circles,
			float epsilon)
		{
			var tangents = new List<Tangent>();
			for (var i = 0; i < circles.Count; i++) {
				for (var j = 0; j < circles.Count; j++) {
					if (i == j) {
						continue;
					}
					var delta = circles[j].Center - circles[i].Center;
					var distance = math.length(delta);
					if (distance <= epsilon) {
						continue;
					}
					var radiusDelta = circles[j].Radius - circles[i].Radius;
					var ratio = radiusDelta / distance;
					if (math.abs(ratio) > 1f + epsilon) {
						continue;
					}
					ratio = math.clamp(ratio, -1f, 1f);
					var direction = delta / distance;
					var inward = direction * ratio
						+ new float2(-direction.y, direction.x) * math.sqrt(math.max(0f, 1f - ratio * ratio));
					var start = circles[i].Center - inward * circles[i].Radius;
					var end = circles[j].Center - inward * circles[j].Radius;
					if (math.distance(start, end) <= epsilon) {
						continue;
					}

					var valid = true;
					for (var k = 0; k < circles.Count; k++) {
						if (math.dot(inward, circles[k].Center - start) < circles[k].Radius - epsilon) {
							valid = false;
							break;
						}
					}
					if (valid) {
						tangents.Add(new Tangent(i, j, start, end, inward));
					}
				}
			}
			return tangents;
		}

		private static List<Tangent> RemoveSubsegmentTangents(IReadOnlyList<Tangent> source,
			float epsilon)
		{
			var retained = new List<Tangent>();
			for (var i = 0; i < source.Count; i++) {
				var tangent = source[i];
				var direction = math.normalize(tangent.End - tangent.Start);
				var startDistance = math.dot(direction, tangent.Start);
				var endDistance = math.dot(direction, tangent.End);
				var contained = false;
				for (var j = 0; j < source.Count; j++) {
					if (i == j) {
						continue;
					}
					var candidate = source[j];
					if (math.dot(tangent.InwardNormal, candidate.InwardNormal) < 1f - epsilon
						|| math.abs(math.dot(tangent.InwardNormal, candidate.Start - tangent.Start)) > epsilon) {
						continue;
					}
					var candidateStart = math.dot(direction, candidate.Start);
					var candidateEnd = math.dot(direction, candidate.End);
					if (candidateStart <= startDistance + epsilon && candidateEnd >= endDistance - epsilon
						&& candidate.Length > tangent.Length + epsilon) {
						contained = true;
						break;
					}
				}
				if (!contained) {
					retained.Add(tangent);
				}
			}
			return retained;
		}

		private static bool TryOrderCycle(IReadOnlyList<Circle> circles,
			IReadOnlyList<Tangent> tangents, float epsilon, out List<Tangent> cycle,
			out string error)
		{
			cycle = new List<Tangent>();
			error = null;
			if (tangents.Count < 2) {
				error = "The guide circles do not produce a closed outer tangent cycle.";
				return false;
			}

			var outgoing = tangents.GroupBy(tangent => tangent.StartCircle)
				.ToDictionary(group => group.Key, group => group
					.OrderByDescending(tangent => tangent.Length)
					.ThenBy(tangent => circles[tangent.EndCircle].BindingIndex)
					.ToList());
			foreach (var pair in outgoing) {
				if (pair.Value.Count > 1) {
					error = $"Guide binding {circles[pair.Key].BindingIndex} has an ambiguous hull tangent.";
					return false;
				}
			}

			var first = tangents
				.OrderBy(tangent => tangent.Start.x)
				.ThenBy(tangent => tangent.Start.y)
				.ThenBy(tangent => circles[tangent.StartCircle].BindingIndex)
				.ThenBy(tangent => circles[tangent.EndCircle].BindingIndex)
				.First();
			var current = first;
			var visited = new HashSet<(int, int)>();
			while (visited.Add((current.StartCircle, current.EndCircle))) {
				cycle.Add(current);
				if (!outgoing.TryGetValue(current.EndCircle, out var nextCandidates)) {
					error = $"Guide binding {circles[current.EndCircle].BindingIndex} breaks the tangent cycle.";
					return false;
				}
				current = nextCandidates[0];
				if (current.StartCircle == first.StartCircle && current.EndCircle == first.EndCircle) {
					break;
				}
				if (cycle.Count > tangents.Count) {
					error = "The hull tangent traversal did not close.";
					return false;
				}
			}

			if (current.StartCircle != first.StartCircle || current.EndCircle != first.EndCircle
				|| cycle.Count < 2) {
				error = "The hull tangents form multiple or open cycles.";
				return false;
			}
			return true;
		}

		private static List<RubberPathElement> EmitElements(IReadOnlyList<Circle> circles,
			IReadOnlyList<Tangent> cycle, float epsilon, float roundness)
		{
			var contactArcs = BuildContactArcs(circles, cycle, roundness);
			var elements = new List<RubberPathElement>(cycle.Count * 2);
			for (var i = 0; i < cycle.Count; i++) {
				var tangent = cycle[i];
				var startCircle = circles[tangent.StartCircle];
				var endCircle = circles[tangent.EndCircle];
				var previousArc = contactArcs[(i + cycle.Count - 1) % cycle.Count];
				var nextArc = contactArcs[i];
				if (previousArc.CircleIndex != tangent.StartCircle
					|| nextArc.CircleIndex != tangent.EndCircle) {
					return new List<RubberPathElement>();
				}

				var startTangent = TangentDirection(previousArc.EndAngle);
				var endTangent = TangentDirection(nextArc.StartAngle);
				var chord = nextArc.Start - previousArc.End;
				var chordLength = math.length(chord);
				if (chordLength > epsilon) {
					var handleLength = HandleLength(chordLength, startTangent,
						endTangent, roundness);
					var freeSpan = new RubberPathElement {
						Type = RubberPathElementType.FreeSpan,
						Start = previousArc.End,
						End = nextArc.Start,
						Control1 = roundness <= RoundnessEpsilon
							? previousArc.End + chord / 3f
							: previousArc.End + startTangent * handleLength,
						Control2 = roundness <= RoundnessEpsilon
							? previousArc.End + chord * (2f / 3f)
							: nextArc.Start - endTangent * handleLength,
						StartBindingIndex = startCircle.BindingIndex,
						EndBindingIndex = endCircle.BindingIndex,
					};
					freeSpan.Length = RubberPathMath.FreeSpanLength(freeSpan);
					elements.Add(freeSpan);
				}

				if (nextArc.SweepAngle * endCircle.Radius <= epsilon) {
					continue;
				}
				elements.Add(new RubberPathElement {
					Type = RubberPathElementType.SupportedArc,
					Start = nextArc.Start,
					End = nextArc.End,
					Center = endCircle.Center,
					Radius = endCircle.Radius,
					StartAngleRad = nextArc.StartAngle,
					SweepAngleRad = nextArc.SweepAngle,
					StartBindingIndex = endCircle.BindingIndex,
					EndBindingIndex = endCircle.BindingIndex,
					Length = nextArc.SweepAngle * endCircle.Radius,
				});
			}
			return elements;
		}

		private static ContactArc[] BuildContactArcs(IReadOnlyList<Circle> circles,
			IReadOnlyList<Tangent> cycle, float roundness)
		{
			var arcs = new ContactArc[cycle.Count];
			for (var i = 0; i < cycle.Count; i++) {
				var tangent = cycle[i];
				var next = cycle[(i + 1) % cycle.Count];
				var circle = circles[tangent.EndCircle];
				var originalStartAngle = math.atan2(tangent.End.y - circle.Center.y,
					tangent.End.x - circle.Center.x);
				var originalEndAngle = math.atan2(next.Start.y - circle.Center.y,
					next.Start.x - circle.Center.x);
				var originalSweep = PositiveAngle(originalEndAngle - originalStartAngle);
				var middleAngle = originalStartAngle + originalSweep * 0.5f;
				// Ease the contact anchors toward the midpoint so they remain farther apart
				// through most of the slider range, then still converge at full roundness.
				var contactSweep = originalSweep * (1f - roundness * roundness);
				var startAngle = middleAngle - contactSweep * 0.5f;
				var endAngle = middleAngle + contactSweep * 0.5f;
				arcs[i] = new ContactArc(tangent.EndCircle, startAngle, contactSweep,
					PointOnCircle(circle, startAngle), PointOnCircle(circle, endAngle));
			}
			return arcs;
		}

		internal static float HandleLength(float chordLength, float2 startTangent,
			float2 endTangent, float roundness)
		{
			if (roundness <= RoundnessEpsilon) {
				return chordLength / 3f;
			}
			var turn = math.clamp(math.atan2(Cross(startTangent, endTangent),
				math.dot(startTangent, endTangent)), 0f, math.PI);
			var cosine = math.cos(turn * 0.25f);
			return chordLength / (3f * cosine * cosine);
		}

		private static float2 TangentDirection(float angle)
			=> new(-math.sin(angle), math.cos(angle));

		private static float2 PointOnCircle(Circle circle, float angle)
			=> circle.Center + new float2(math.cos(angle), math.sin(angle)) * circle.Radius;

		private static bool TryValidateGuideClearance(
			IReadOnlyList<RubberPathElement> elements, IReadOnlyList<Circle> circles,
			float epsilon, out string error)
		{
			error = null;
			var clearanceTolerance = math.max(0.005f, epsilon * 4f);
			foreach (var element in elements) {
				var samples = element.Type == RubberPathElementType.SupportedArc
					? math.max(2, (int)math.ceil(element.SweepAngleRad / (math.PI / 64f)))
					: math.max(2, RubberPathMath.FreeSpanSubdivisionCount(element,
						clearanceTolerance));
				var previous = element.Start;
				for (var sample = 0; sample <= samples; sample++) {
					var point = RubberPathMath.PointAt(element, sample / (float)samples);
					for (var i = 0; i < circles.Count; i++) {
						if (math.distance(point, circles[i].Center)
							< circles[i].Radius - clearanceTolerance
							|| (element.Type == RubberPathElementType.FreeSpan && sample > 0
							&& RubberPathMath.DistanceToSegment(circles[i].Center,
								previous, point) < circles[i].Radius - 2f * clearanceTolerance)) {
							error = $"The rounded path intersects guide binding {circles[i].BindingIndex}.";
							return false;
						}
					}
					previous = point;
				}
			}
			return true;
		}

		private static void Normalize(List<RubberPathElement> elements, float epsilon)
		{
			var first = 0;
			for (var i = 1; i < elements.Count; i++) {
				if (LexicographicCompare(elements[i], elements[first], epsilon) < 0) {
					first = i;
				}
			}
			if (first != 0) {
				var rotated = elements.Skip(first).Concat(elements.Take(first)).ToArray();
				elements.Clear();
				elements.AddRange(rotated);
			}

			var distance = 0f;
			for (var i = 0; i < elements.Count; i++) {
				var element = elements[i];
				element.StartDistance = distance;
				elements[i] = element;
				distance += element.Length;
			}
		}

		private static int LexicographicCompare(RubberPathElement a, RubberPathElement b,
			float epsilon)
		{
			if (a.Start.x < b.Start.x - epsilon) {
				return -1;
			}
			if (a.Start.x > b.Start.x + epsilon) {
				return 1;
			}
			if (a.Start.y < b.Start.y - epsilon) {
				return -1;
			}
			if (a.Start.y > b.Start.y + epsilon) {
				return 1;
			}
			var binding = a.StartBindingIndex.CompareTo(b.StartBindingIndex);
			return binding != 0 ? binding : a.Type.CompareTo(b.Type);
		}

		private static float PositiveAngle(float angle)
		{
			angle %= TwoPi;
			return angle < 0f ? angle + TwoPi : angle;
		}

		private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
	}

	public static class RubberPathValidator
	{
		public static bool TryValidate(IReadOnlyList<RubberPathElement> elements,
			float epsilon, out string error)
		{
			error = null;
			if (elements == null || elements.Count == 0) {
				error = "The rubber path is empty.";
				return false;
			}

			var expectedDistance = 0f;
			for (var i = 0; i < elements.Count; i++) {
				var element = elements[i];
				if (!math.all(math.isfinite(element.Start)) || !math.all(math.isfinite(element.End))
					|| !float.IsFinite(element.Length) || element.Length <= epsilon) {
					error = $"Path element {i} has invalid geometry or length.";
					return false;
				}
				if (math.abs(element.StartDistance - expectedDistance) > epsilon * math.max(1f, expectedDistance)) {
					error = $"Path element {i} has a discontinuous accumulated distance.";
					return false;
				}
				if (element.Type == RubberPathElementType.FreeSpan) {
					var computedLength = RubberPathMath.FreeSpanLength(element);
					if (!math.all(math.isfinite(element.Control1))
						|| !math.all(math.isfinite(element.Control2))
						|| !float.IsFinite(computedLength)
						|| math.abs(element.Length - computedLength)
						> epsilon * math.max(1f, math.max(element.Length, computedLength))) {
						error = $"Free span {i} has an inconsistent length.";
						return false;
					}
				} else if (element.Type == RubberPathElementType.SupportedArc) {
					if (!math.all(math.isfinite(element.Center)) || !float.IsFinite(element.Radius)
						|| element.Radius <= 0f || !float.IsFinite(element.SweepAngleRad)
						|| element.SweepAngleRad <= 0f
						|| math.abs(element.Length - element.Radius * element.SweepAngleRad) > epsilon
						|| math.abs(math.distance(element.Start, element.Center) - element.Radius) > epsilon
						|| math.abs(math.distance(element.End, element.Center) - element.Radius) > epsilon) {
						error = $"Supported arc {i} has inconsistent circle geometry.";
						return false;
					}
				} else {
					error = $"Path element {i} has an unknown type.";
					return false;
				}

				var next = elements[(i + 1) % elements.Count];
				if (math.distance(element.End, next.Start) > epsilon) {
					error = $"Path elements {i} and {(i + 1) % elements.Count} are not connected.";
					return false;
				}
				expectedDistance += element.Length;
			}

			if (HasPolylineSelfIntersection(elements, epsilon)) {
				error = "The rubber path intersects itself.";
				return false;
			}
			return true;
		}

		private static bool HasPolylineSelfIntersection(IReadOnlyList<RubberPathElement> elements,
			float epsilon)
		{
			var points = new List<float2>();
			foreach (var element in elements) {
				points.Add(element.Start);
				var samples = element.Type == RubberPathElementType.SupportedArc
					? math.max(2, (int)math.ceil(element.SweepAngleRad / (math.PI / 32f)))
					: math.max(1, RubberPathMath.FreeSpanSubdivisionCount(element,
						math.max(0.05f, epsilon)));
				for (var i = 1; i < samples; i++) {
					points.Add(RubberPathMath.PointAt(element, i / (float)samples));
				}
			}

			for (var i = 0; i < points.Count; i++) {
				var iNext = (i + 1) % points.Count;
				for (var j = i + 2; j < points.Count; j++) {
					var jNext = (j + 1) % points.Count;
					if (i == jNext || iNext == j) {
						continue;
					}
					if (SegmentsIntersect(points[i], points[iNext], points[j], points[jNext], epsilon)) {
						return true;
					}
				}
			}
			return false;
		}

		private static bool SegmentsIntersect(float2 a, float2 b, float2 c, float2 d,
			float epsilon)
		{
			var ab = b - a;
			var cd = d - c;
			var denominator = Cross(ab, cd);
			if (math.abs(denominator) <= epsilon) {
				return false;
			}
			var ac = c - a;
			var t = Cross(ac, cd) / denominator;
			var u = Cross(ac, ab) / denominator;
			return t > epsilon && t < 1f - epsilon && u > epsilon && u < 1f - epsilon;
		}

		private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
	}
}
