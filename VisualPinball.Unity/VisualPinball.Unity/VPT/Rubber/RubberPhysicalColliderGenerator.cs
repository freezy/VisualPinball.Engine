// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VisualPinball.Unity
{
	internal sealed class RubberPhysicalColliderGenerator
	{
		internal const float ArcChordToleranceVpx = 0.05f;
		internal const float ArcChordRadiusFraction = 0.05f;
		internal const int MaximumResponseSegmentsPerElement = 4096;
		private static readonly ProfilerMarker PerfMarker = new("RubberPhysicalColliderGenerator");

		private readonly IApiColliderGenerator _api;
		private readonly RubberComponent _rubber;
		private readonly float4x4 _matrix;
		private readonly RubberGuideDistanceElasticitySettings _elasticitySettings;
		private readonly List<float2> _geometryPoints = new();
		private readonly List<float2> _finalPoints = new();

		internal RubberPhysicalColliderGenerator(IApiColliderGenerator api, RubberComponent rubber,
			float4x4 matrix, RubberGuideDistanceElasticitySettings elasticitySettings)
		{
			_api = api;
			_rubber = rubber;
			_matrix = matrix;
			_elasticitySettings = elasticitySettings;
		}

		internal void GenerateColliders(float zOffset, ref ColliderReference colliders,
			float margin)
		{
			using var _ = PerfMarker.Auto();
			var rubberRadius = _rubber.Thickness * 0.5f;
			var cordRadius = rubberRadius + margin;
			var colliderInfo = _api.GetColliderInfo();
			var responseCapWarningIssued = false;
			foreach (var element in _rubber.BakedPath) {
				if (element.Type == RubberPathElementType.FreeSpan) {
					AddFreeSpan(element, zOffset, cordRadius, in colliderInfo, ref colliders,
						ref responseCapWarningIssued);
					continue;
				}

				var maximumAngle = MaximumChordAngle(element.Radius,
					ChordTolerance(rubberRadius));
				var segmentCount = math.max(3,
					(int)math.ceil(element.SweepAngleRad / maximumAngle));
				AddSegments(element, segmentCount, zOffset, cordRadius, 1f, in colliderInfo,
					ref colliders);
			}
		}

		private void AddFreeSpan(RubberPathElement element, float zOffset, float cordRadius,
			in ColliderInfo colliderInfo, ref ColliderReference colliders,
			ref bool responseCapWarningIssued)
		{
			var geometrySegmentCount = RubberPathMath.FreeSpanSubdivisionCount(element,
				ChordTolerance(_rubber.Thickness * 0.5f));
			if (!_elasticitySettings.UsesResponseProfile) {
				AddSegments(element, geometrySegmentCount, zOffset, cordRadius, 1f,
					in colliderInfo, ref colliders);
				return;
			}

			_geometryPoints.Clear();
			if (_geometryPoints.Capacity < geometrySegmentCount + 1) {
				_geometryPoints.Capacity = geometrySegmentCount + 1;
			}
			for (var i = 0; i <= geometrySegmentCount; i++) {
				_geometryPoints.Add(RubberPathMath.PointAt(element,
					i / (float)geometrySegmentCount));
			}

			var effectiveMaximumLength = _elasticitySettings.EffectiveMaximumSegmentLength;
			var desiredSegmentCount = ResponseSegmentCount(_geometryPoints, effectiveMaximumLength);
			var splitForResponse = desiredSegmentCount <= MaximumResponseSegmentsPerElement;
			if (!splitForResponse && !responseCapWarningIssued) {
				Debug.LogWarning($"Physical rubber '{_rubber.name}' exceeded the {MaximumResponseSegmentsPerElement}-segment guide-distance response limit; keeping its existing geometry tessellation with a coarser elasticity profile.",
					_rubber);
				responseCapWarningIssued = true;
			}

			BuildFinalPoints(_geometryPoints,
				splitForResponse ? effectiveMaximumLength : float.PositiveInfinity,
				splitForResponse ? desiredSegmentCount : geometrySegmentCount, _finalPoints);
			AddProfiledSegments(_finalPoints, zOffset, cordRadius, in colliderInfo,
				ref colliders);
		}

		private void AddSegments(RubberPathElement element, int segmentCount,
			float zOffset, float cordRadius, float elasticityScale,
			in ColliderInfo colliderInfo, ref ColliderReference colliders)
		{
			var start = element.Start;
			for (var segment = 1; segment <= segmentCount; segment++) {
				var end = RubberPathMath.PointAt(element, segment / (float)segmentCount);
				Add(start, end, zOffset, cordRadius, elasticityScale, in colliderInfo,
					ref colliders);
				start = end;
			}
		}

		private static int ResponseSegmentCount(List<float2> geometryPoints,
			float maximumLength)
		{
			var count = 0;
			for (var i = 1; i < geometryPoints.Count; i++) {
				var length = math.distance(geometryPoints[i - 1], geometryPoints[i]);
				if (length <= 1e-5f) {
					continue;
				}
				// Widen before division so pathological finite inputs cannot overflow an int cast.
				var chordCount = global::System.Math.Ceiling((double)length / maximumLength);
				if (chordCount > MaximumResponseSegmentsPerElement - count) {
					return MaximumResponseSegmentsPerElement + 1;
				}
				count += (int)chordCount;
			}
			return count;
		}

		private static void BuildFinalPoints(List<float2> geometryPoints,
			float maximumLength, int segmentCapacity, List<float2> points)
		{
			points.Clear();
			if (points.Capacity < segmentCapacity + 1) {
				points.Capacity = segmentCapacity + 1;
			}
			points.Add(geometryPoints[0]);
			for (var i = 1; i < geometryPoints.Count; i++) {
				var start = geometryPoints[i - 1];
				var end = geometryPoints[i];
				var length = math.distance(start, end);
				if (length <= 1e-5f) {
					continue;
				}
				var chordCount = float.IsPositiveInfinity(maximumLength)
					? 1
					: math.max(1, (int)math.ceil(length / maximumLength));
				for (var chord = 1; chord <= chordCount; chord++) {
					points.Add(math.lerp(start, end, chord / (float)chordCount));
				}
			}
		}

		private void AddProfiledSegments(List<float2> points, float zOffset,
			float cordRadius, in ColliderInfo colliderInfo, ref ColliderReference colliders)
		{
			var totalLength = 0f;
			for (var i = 1; i < points.Count; i++) {
				totalLength += math.distance(points[i - 1], points[i]);
			}

			var cumulativeLength = 0f;
			// Each baked free-span element runs from one support to the next. If a
			// sub-epsilon supported arc was omitted between adjacent spans, their shared
			// tangency still represents a support and intentionally restarts the profile.
			for (var i = 1; i < points.Count; i++) {
				var start = points[i - 1];
				var end = points[i];
				var length = math.distance(start, end);
				if (length <= 1e-5f) {
					continue;
				}
				var midpointDistance = cumulativeLength + length * 0.5f;
				var supportDistance = math.min(midpointDistance, totalLength - midpointDistance);
				var weight = math.smoothstep(0f, _elasticitySettings.GuideInfluenceLength,
					supportDistance);
				var elasticityScale = math.lerp(1f,
					_elasticitySettings.FreeSpanElasticityScale, weight);
				Add(start, end, zOffset, cordRadius, elasticityScale, in colliderInfo,
					ref colliders);
				cumulativeLength += length;
			}
		}

		internal static float MaximumChordAngle(float radius, float tolerance)
		{
			if (radius <= tolerance) {
				return math.PI;
			}
			return math.max(1e-4f, 2f * math.acos(math.clamp(1f - tolerance / radius, -1f, 1f)));
		}

		internal static float ChordTolerance(float cordRadius)
			=> math.min(ArcChordToleranceVpx, ArcChordRadiusFraction * cordRadius);

		private void Add(float2 bakeStart, float2 bakeEnd, float zOffset,
			float cordRadius, float elasticityScale, in ColliderInfo colliderInfo,
			ref ColliderReference colliders)
		{
			var start = BakeToLocal(bakeStart, zOffset);
			var end = BakeToLocal(bakeEnd, zOffset);
			if (math.distancesq(start, end) <= 1e-10f) {
				return;
			}
			colliders.Add(new SweptCircleCollider(start, end, cordRadius, colliderInfo,
				elasticityScale), _matrix);
		}

		private float3 BakeToLocal(float2 point, float zOffset)
		{
			var local = _rubber.BakeFrameToLocal.MultiplyPoint3x4(
				new Vector3(point.x, point.y, zOffset));
			return new float3(local.x, local.y, local.z);
		}
	}
}
