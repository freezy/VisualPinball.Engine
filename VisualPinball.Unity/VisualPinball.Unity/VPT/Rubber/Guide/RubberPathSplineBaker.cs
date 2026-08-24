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
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	public static class RubberPathSplineBaker
	{
		private const int MaximumSubdivisionsPerElement = 4096;
		private const int DeviationSamplesPerSegment = 16;

		private struct SampleNode
		{
			public float2 Position;
			public bool IsSmooth;
			public int OutgoingElementIndex;
		}

		public static bool TryCreateDragPoints(IReadOnlyList<RubberPathElement> elements,
			Matrix4x4 bakeFrameToLocal, float renderTolerance, out DragPointData[] dragPoints,
			out float maximumDeviation, out string error)
		{
			dragPoints = Array.Empty<DragPointData>();
			maximumDeviation = float.PositiveInfinity;
			error = null;
			if (elements == null || elements.Count == 0) {
				error = "The exact rubber path is empty.";
				return false;
			}
			if (!float.IsFinite(renderTolerance) || renderTolerance <= 0f) {
				error = "Render tolerance must be finite and positive.";
				return false;
			}

			var subdivisions = new int[elements.Count];
			for (var i = 0; i < elements.Count; i++) {
				if (elements[i].Type == RubberPathElementType.FreeSpan) {
					subdivisions[i] = RubberPathMath.FreeSpanSubdivisionCount(elements[i],
						renderTolerance);
					continue;
				}
				var radius = elements[i].Radius;
				var ratio = math.clamp(1f - renderTolerance / radius, -1f, 1f);
				var maximumAngle = 2f * math.acos(ratio);
				if (!float.IsFinite(maximumAngle) || maximumAngle <= 1e-5f) {
					maximumAngle = math.PI / 16f;
				}
				subdivisions[i] = math.max(3,
					(int)math.ceil(elements[i].SweepAngleRad / maximumAngle));
			}

			for (;;) {
				var nodes = CreateNodes(elements, subdivisions);
				if (nodes.Count < 3) {
					error = "The sampled rubber path contains fewer than three distinct points.";
					return false;
				}
				var bakeFrameDragPoints = ToDragPoints(nodes, Matrix4x4.identity);
				maximumDeviation = MeasureMaximumDeviation(elements, bakeFrameDragPoints,
					nodes.ConvertAll(node => node.OutgoingElementIndex));
				if (maximumDeviation <= renderTolerance) {
					dragPoints = ToDragPoints(nodes, bakeFrameToLocal);
					return true;
				}

				var refined = false;
				for (var i = 0; i < elements.Count; i++) {
					if (elements[i].Type == RubberPathElementType.FreeSpan
						&& RubberPathMath.IsStraightFreeSpan(elements[i])) {
						continue;
					}
					if (subdivisions[i] >= MaximumSubdivisionsPerElement) {
						error = $"The sampled spline exceeds its {renderTolerance} render tolerance.";
						return false;
					}
					subdivisions[i] = math.min(MaximumSubdivisionsPerElement,
						subdivisions[i] * 2);
					refined = true;
				}
				if (!refined) {
					error = $"The sampled spline exceeds its {renderTolerance} render tolerance.";
					return false;
				}
			}
		}

		public static float MeasureMaximumDeviation(IReadOnlyList<RubberPathElement> elements,
			IReadOnlyList<DragPointData> dragPoints, IReadOnlyList<int> outgoingElementIndices)
		{
			if (dragPoints == null || outgoingElementIndices == null
				|| dragPoints.Count != outgoingElementIndices.Count) {
				throw new ArgumentException("Every sampled drag point must identify its outgoing exact path element.");
			}

			var maximum = 0f;
			for (var i = 0; i < dragPoints.Count; i++) {
				var next = (i + 1) % dragPoints.Count;
				var previous = dragPoints[i].IsSmooth ? (i + dragPoints.Count - 1) % dragPoints.Count : i;
				var following = dragPoints[next].IsSmooth ? (i + 2) % dragPoints.Count : next;
				var curve = CatmullCurve<RenderVertex2D>.GetInstance<CatmullCurve2DCatmullCurveFactory>(
					dragPoints[previous].Center, dragPoints[i].Center, dragPoints[next].Center,
					dragPoints[following].Center);
				var elementIndex = outgoingElementIndices[i];
				if (elementIndex < 0 || elementIndex >= elements.Count) {
					throw new ArgumentOutOfRangeException(nameof(outgoingElementIndices));
				}
				for (var sample = 0; sample <= DeviationSamplesPerSegment; sample++) {
					var vertex = curve.GetPointAt(sample / (float)DeviationSamplesPerSegment);
					var point = new float2(vertex.X, vertex.Y);
					maximum = math.max(maximum, DistanceToElement(point, elements[elementIndex]));
				}
			}
			return maximum;
		}

		private static List<SampleNode> CreateNodes(IReadOnlyList<RubberPathElement> elements,
			IReadOnlyList<int> subdivisions)
		{
			var nodes = new List<SampleNode>();
			var fullCircle = IsFullCircle(elements);
			for (var elementIndex = 0; elementIndex < elements.Count; elementIndex++) {
				var element = elements[elementIndex];
				if (nodes.Count == 0) {
					nodes.Add(new SampleNode {
						Position = element.Start,
						IsSmooth = fullCircle,
						OutgoingElementIndex = elementIndex,
					});
				} else {
					var node = nodes[^1];
					node.OutgoingElementIndex = elementIndex;
					if (!fullCircle) {
						node.IsSmooth = IsSmoothJunction(elements[elementIndex - 1], element);
					}
					nodes[^1] = node;
				}

				var count = subdivisions[elementIndex];
				for (var sample = 1; sample <= count; sample++) {
					var isLast = sample == count;
					var position = RubberPathMath.PointAt(element, sample / (float)count);
					if (elementIndex == elements.Count - 1 && isLast
						&& math.distance(position, nodes[0].Position) <= 1e-4f) {
						continue;
					}
					nodes.Add(new SampleNode {
						Position = position,
						IsSmooth = fullCircle || !isLast
							&& (element.Type == RubberPathElementType.SupportedArc
								|| !RubberPathMath.IsStraightFreeSpan(element)),
						OutgoingElementIndex = isLast && elementIndex + 1 < elements.Count
							? elementIndex + 1
							: elementIndex,
					});
				}
			}
			if (!fullCircle && nodes.Count > 0) {
				var first = nodes[0];
				first.IsSmooth = IsSmoothJunction(elements[elements.Count - 1], elements[0]);
				nodes[0] = first;
			}
			return nodes;
		}

		private static bool IsSmoothJunction(RubberPathElement previous,
			RubberPathElement next)
		{
			if (RubberPathMath.IsStraightFreeSpan(previous)
				|| RubberPathMath.IsStraightFreeSpan(next)) {
				return false;
			}
			var incoming = RubberPathMath.TangentAt(previous, 1f);
			var outgoing = RubberPathMath.TangentAt(next, 0f);
			return math.dot(incoming, outgoing) >= 1f - 1e-4f;
		}

		private static bool IsFullCircle(IReadOnlyList<RubberPathElement> elements)
		{
			if (elements.Count == 0) {
				return false;
			}
			var first = elements[0];
			var sweep = 0f;
			for (var i = 0; i < elements.Count; i++) {
				if (elements[i].Type != RubberPathElementType.SupportedArc
					|| math.distance(elements[i].Center, first.Center) > 1e-4f
					|| math.abs(elements[i].Radius - first.Radius) > 1e-4f) {
					return false;
				}
				sweep += elements[i].SweepAngleRad;
			}
			return sweep >= 2f * math.PI - 1e-4f;
		}

		private static DragPointData[] ToDragPoints(IReadOnlyList<SampleNode> nodes,
			Matrix4x4 bakeFrameToLocal)
		{
			var dragPoints = new DragPointData[nodes.Count];
			for (var i = 0; i < nodes.Count; i++) {
				var point = bakeFrameToLocal.MultiplyPoint3x4(new Vector3(
					nodes[i].Position.x, nodes[i].Position.y, 0f));
				dragPoints[i] = new DragPointData(new Vertex3D(point.x, point.y, point.z)) {
					IsSmooth = nodes[i].IsSmooth,
				};
			}
			return dragPoints;
		}

		private static float DistanceToElement(float2 point, RubberPathElement element)
			=> RubberPathMath.DistanceToElement(point, element);
	}
}
