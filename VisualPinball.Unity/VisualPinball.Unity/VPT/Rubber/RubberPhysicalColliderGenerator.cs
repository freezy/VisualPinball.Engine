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
		private readonly RubberColliderComponent _colliderComponent;
		private readonly RubberSpanReboundCollector _reboundCollector;
		private readonly List<float2> _geometryPoints = new();
		private readonly List<float2> _finalPoints = new();

		/// <summary>Path element index -> index of the span inside this rubber.</summary>
		private readonly Dictionary<int, int> _reboundSpanIndices = new();

		internal RubberPhysicalColliderGenerator(IApiColliderGenerator api, RubberComponent rubber,
			float4x4 matrix, RubberGuideDistanceElasticitySettings elasticitySettings)
			: this(api, rubber, matrix, elasticitySettings, null, null)
		{
		}

		/// <summary>
		/// The runtime overload: with a collider component and a collector the generator
		/// also tags the capsules of every compliant free span and appends that span's
		/// record. Without them it emits exactly the capsules it emits today.
		/// </summary>
		internal RubberPhysicalColliderGenerator(IApiColliderGenerator api, RubberComponent rubber,
			float4x4 matrix, RubberGuideDistanceElasticitySettings elasticitySettings,
			RubberColliderComponent colliderComponent,
			RubberSpanReboundCollector reboundCollector)
		{
			_api = api;
			_rubber = rubber;
			_matrix = matrix;
			_elasticitySettings = elasticitySettings;
			_colliderComponent = colliderComponent;
			_reboundCollector = reboundCollector;
		}

		internal void GenerateColliders(float zOffset, ref ColliderReference colliders,
			float margin)
		{
			using var _ = PerfMarker.Auto();
			var rubberRadius = _rubber.Thickness * 0.5f;
			var cordRadius = rubberRadius + margin;
			var colliderInfo = _api.GetColliderInfo();
			var responseCapWarningIssued = false;
			var guideElasticityScale = _elasticitySettings.UsesResponseProfile
				? _elasticitySettings.GuideElasticityScale
				: 1f;
			PrepareRebound(colliderInfo.ItemId, zOffset);
			var elementIndex = -1;
			foreach (var element in _rubber.BakedPath) {
				elementIndex++;
				if (element.Type == RubberPathElementType.FreeSpan) {
					AddFreeSpan(element, elementIndex, zOffset, cordRadius, in colliderInfo,
						ref colliders, ref responseCapWarningIssued);
					continue;
				}

				var maximumAngle = MaximumChordAngle(element.Radius,
					ChordTolerance(rubberRadius));
				var segmentCount = math.max(3,
					(int)math.ceil(element.SweepAngleRad / maximumAngle));
				AddSegments(element, segmentCount, zOffset, cordRadius, guideElasticityScale,
					in colliderInfo,
					ref colliders);
			}
		}

		private void AddFreeSpan(RubberPathElement element, int elementIndex, float zOffset,
			float cordRadius, in ColliderInfo colliderInfo, ref ColliderReference colliders,
			ref bool responseCapWarningIssued)
		{
			var isCompliant = _reboundSpanIndices.TryGetValue(elementIndex, out var spanIndex);
			var geometrySegmentCount = RubberPathMath.FreeSpanSubdivisionCount(element,
				ChordTolerance(_rubber.Thickness * 0.5f));
			if (!_elasticitySettings.UsesResponseProfile) {
				if (!isCompliant) {
					AddSegments(element, geometrySegmentCount, zOffset, cordRadius, 1f,
						in colliderInfo, ref colliders);
					return;
				}
				BuildGeometryPoints(element, geometrySegmentCount);
				AddTaggedSegments(_geometryPoints, spanIndex, zOffset, cordRadius, false,
					in colliderInfo, ref colliders);
				return;
			}

			BuildGeometryPoints(element, geometrySegmentCount);

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
			if (isCompliant) {
				AddTaggedSegments(_finalPoints, spanIndex, zOffset, cordRadius, true,
					in colliderInfo, ref colliders);
				return;
			}
			AddProfiledSegments(_finalPoints, zOffset, cordRadius, in colliderInfo,
				ref colliders);
		}

		private void BuildGeometryPoints(RubberPathElement element, int geometrySegmentCount)
		{
			_geometryPoints.Clear();
			if (_geometryPoints.Capacity < geometrySegmentCount + 1) {
				_geometryPoints.Capacity = geometrySegmentCount + 1;
			}
			for (var i = 0; i <= geometrySegmentCount; i++) {
				_geometryPoints.Add(RubberPathMath.PointAt(element,
					i / (float)geometrySegmentCount));
			}
		}

		private void AddSegments(RubberPathElement element, int segmentCount,
			float zOffset, float cordRadius, float elasticityScale,
			in ColliderInfo colliderInfo, ref ColliderReference colliders)
		{
			var start = element.Start;
			for (var segment = 1; segment <= segmentCount; segment++) {
				var end = RubberPathMath.PointAt(element, segment / (float)segmentCount);
				Add(start, end, zOffset, cordRadius, elasticityScale, -1, 0f, 1f,
					in colliderInfo, ref colliders);
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
			var totalLength = TotalLength(points);

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
				var elasticityScale = ProfileScale(cumulativeLength, length, totalLength);
				Add(start, end, zOffset, cordRadius, elasticityScale, -1, 0f, 1f,
					in colliderInfo, ref colliders);
				cumulativeLength += length;
			}
		}

		/// <summary>
		/// Emits the capsules of a compliant free span, each tagged with the span index
		/// and the arc-length fractions of its ends, measured on this very point list so
		/// the tags match the geometry that collides.
		/// </summary>
		private void AddTaggedSegments(List<float2> points, int spanIndex, float zOffset,
			float cordRadius, bool profiled, in ColliderInfo colliderInfo,
			ref ColliderReference colliders)
		{
			var totalLength = TotalLength(points);
			if (!(totalLength > 0f)) {
				return;
			}

			var cumulativeLength = 0f;
			for (var i = 1; i < points.Count; i++) {
				var start = points[i - 1];
				var end = points[i];
				var length = math.distance(start, end);
				if (length <= 1e-5f) {
					continue;
				}
				var elasticityScale = profiled
					? ProfileScale(cumulativeLength, length, totalLength)
					: 1f;
				Add(start, end, zOffset, cordRadius, elasticityScale, spanIndex,
					cumulativeLength / totalLength, (cumulativeLength + length) / totalLength,
					in colliderInfo, ref colliders);
				cumulativeLength += length;
			}
		}

		private float ProfileScale(float cumulativeLength, float length, float totalLength)
		{
			var midpointDistance = cumulativeLength + length * 0.5f;
			var supportDistance = math.min(midpointDistance, totalLength - midpointDistance);
			var weight = math.smoothstep(0f, _elasticitySettings.GuideInfluenceLength,
				supportDistance);
			return math.lerp(_elasticitySettings.GuideElasticityScale, 1f, weight);
		}

		private static float TotalLength(List<float2> points)
		{
			var totalLength = 0f;
			for (var i = 1; i < points.Count; i++) {
				totalLength += math.distance(points[i - 1], points[i]);
			}
			return totalLength;
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
			float cordRadius, float elasticityScale, int spanId, float u0, float u1,
			in ColliderInfo colliderInfo, ref ColliderReference colliders)
		{
			var start = BakeToLocal(bakeStart, zOffset);
			var end = BakeToLocal(bakeEnd, zOffset);
			if (math.distancesq(start, end) <= 1e-10f) {
				return;
			}
			colliders.Add(new SweptCircleCollider(start, end, cordRadius, colliderInfo,
				elasticityScale, spanId, u0, u1), _matrix);
		}

		private float3 BakeToLocal(float2 point, float zOffset)
		{
			var local = _rubber.BakeFrameToLocal.MultiplyPoint3x4(
				new Vector3(point.x, point.y, zOffset));
			return new float3(local.x, local.y, local.z);
		}

		#region Compliant rebound

		/// <summary>
		/// Decides which free spans of this rubber get the compliant rebound and builds
		/// their records. Every fallback logs exactly one warning.
		/// </summary>
		private void PrepareRebound(int itemId, float zOffset)
		{
			_reboundSpanIndices.Clear();
			if (!_colliderComponent || !_colliderComponent.DeformationEnabled) {
				return;
			}
			if (!RubberSpanReboundEligibility.TryValidate(_colliderComponent, out var reason)) {
				Debug.LogWarning($"Physical rubber '{_rubber.name}' keeps the static rubber response: {reason}",
					_rubber);
				return;
			}

			var path = _rubber.BakedPath;
			var material = _colliderComponent.CompiledDeformationMaterial;
			var descriptors = RubberSpanReboundEligibility.EligibleDescriptors(_colliderComponent);
			var localIndex = 0;
			foreach (var descriptor in descriptors) {
				if (descriptor.PathElementIndex < 0 || descriptor.PathElementIndex >= path.Count) {
					continue;
				}
				var element = path[descriptor.PathElementIndex];
				if (element.Type != RubberPathElementType.FreeSpan) {
					continue;
				}
				if (!TryBuildRecord(itemId, in descriptor, element, in material, zOffset,
					out var record)) {
					continue;
				}
				_reboundSpanIndices[descriptor.PathElementIndex] = localIndex;
				_reboundCollector?.Add(itemId, localIndex, in record);
				localIndex++;
			}
			if (localIndex == 0) {
				Debug.LogWarning($"Physical rubber '{_rubber.name}' keeps the static rubber response: no free span is eligible for the compliant rebound.",
					_rubber);
			}
		}

		private bool TryBuildRecord(int itemId, in RubberSpanDescriptor descriptor,
			RubberPathElement element, in RubberCompiledDeformationMaterial material,
			float zOffset, out RubberSpanRebound record)
		{
			record = default;
			var anchorA = _matrix.MultiplyPoint(BakeToLocal(element.Start, zOffset));
			var anchorB = _matrix.MultiplyPoint(BakeToLocal(element.End, zOffset));
			var chord = anchorB - anchorA;
			var length = math.length(chord);
			if (!(length > 1e-4f) || !(descriptor.InstalledLength > 0f)
				|| !(descriptor.UnloadedLength > 0f)) {
				return false;
			}

			// The descriptors are baked in the rubber's own frame; a uniformly scaled item
			// transform scales the emitted capsules, so scale the string's length and
			// cross-section with it and leave the installed stretch invariant.
			var scale = length / descriptor.InstalledLength;
			if (!float.IsFinite(scale) || !(scale > 0f)) {
				return false;
			}
			var thickness = (float)_rubber.Thickness * scale;
			var area = math.PI * thickness * thickness * 0.25f;
			if (!(area > 0f)) {
				return false;
			}

			var chordDirection = chord / length;
			if (!TryBuildOutwardNormal(in descriptor, element, chordDirection, zOffset,
				out var outwardNormal)) {
				return false;
			}

			var table = new global::Unity.Collections.FixedList4096Bytes<float>();
			var stress = material.StressLut;
			if (stress == null || stress.Length < 2
				|| stress.Length > table.Capacity) {
				return false;
			}
			foreach (var sample in stress) {
				table.Add(sample);
			}

			record = new RubberSpanRebound {
				AnchorA = anchorA,
				AnchorB = anchorB,
				ChordDirection = chordDirection,
				OutwardNormal = outwardNormal,
				Length = length,
				UnloadedLength = descriptor.UnloadedLength * scale,
				CrossSectionArea = area,
				MaximumStretchRatio = material.MaximumStretchRatio,
				ItemId = itemId,
				PathElementIndex = descriptor.PathElementIndex,
				StressTable = table,
			};
			return record.IsUsable;
		}

		/// <summary>
		/// The rest chord's outward normal in collider space, from the descriptor's
		/// inward sign in the bake plane.
		/// </summary>
		private bool TryBuildOutwardNormal(in RubberSpanDescriptor descriptor,
			RubberPathElement element, float3 chordDirection, float zOffset,
			out float3 outwardNormal)
		{
			outwardNormal = float3.zero;
			var bakeChord = element.End - element.Start;
			var bakeLength = math.length(bakeChord);
			if (!(bakeLength > 1e-5f)) {
				return false;
			}
			var tangent = bakeChord / bakeLength;
			var inward = descriptor.InwardSign >= 0
				? new float2(-tangent.y, tangent.x)
				: new float2(tangent.y, -tangent.x);
			var midpoint = (element.Start + element.End) * 0.5f;
			var origin = _matrix.MultiplyPoint(BakeToLocal(midpoint, zOffset));
			var displaced = _matrix.MultiplyPoint(BakeToLocal(midpoint - inward, zOffset));
			var outward = displaced - origin;
			outward -= chordDirection * math.dot(outward, chordDirection);
			var outwardLength = math.length(outward);
			if (!(outwardLength > 1e-5f)) {
				return false;
			}
			outwardNormal = outward / outwardLength;
			return true;
		}

		#endregion
	}
}
