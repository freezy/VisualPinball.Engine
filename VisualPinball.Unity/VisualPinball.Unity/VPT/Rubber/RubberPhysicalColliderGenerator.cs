// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VisualPinball.Unity
{
	internal sealed class RubberPhysicalColliderGenerator
	{
		internal const float ArcChordToleranceVpx = 0.05f;
		internal const float ArcChordRadiusFraction = 0.05f;
		private static readonly ProfilerMarker PerfMarker = new("RubberPhysicalColliderGenerator");

		private readonly RubberApi _api;
		private readonly RubberComponent _rubber;
		private readonly float4x4 _matrix;

		internal RubberPhysicalColliderGenerator(RubberApi api, RubberComponent rubber,
			float4x4 matrix)
		{
			_api = api;
			_rubber = rubber;
			_matrix = matrix;
		}

		internal void GenerateColliders(float zOffset, ref ColliderReference colliders,
			float margin)
		{
			using var _ = PerfMarker.Auto();
			var rubberRadius = _rubber.Thickness * 0.5f;
			var cordRadius = rubberRadius + margin;
			foreach (var element in _rubber.BakedPath) {
				if (element.Type == RubberPathElementType.FreeSpan) {
					Add(element.Start, element.End, zOffset, cordRadius, ref colliders);
					continue;
				}

				var maximumAngle = MaximumChordAngle(element.Radius,
					ChordTolerance(rubberRadius));
				var segmentCount = math.max(3,
					(int)math.ceil(element.SweepAngleRad / maximumAngle));
				var start = element.Start;
				for (var segment = 1; segment <= segmentCount; segment++) {
					var angle = element.StartAngleRad
						+ element.SweepAngleRad * segment / segmentCount;
					var end = element.Center
						+ new float2(math.cos(angle), math.sin(angle)) * element.Radius;
					Add(start, end, zOffset, cordRadius, ref colliders);
					start = end;
				}
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
			float cordRadius, ref ColliderReference colliders)
		{
			var start = BakeToLocal(bakeStart, zOffset);
			var end = BakeToLocal(bakeEnd, zOffset);
			if (math.distancesq(start, end) <= 1e-10f) {
				return;
			}
			colliders.Add(new SweptCircleCollider(start, end, cordRadius,
				_api.GetColliderInfo()), _matrix);
		}

		private float3 BakeToLocal(float2 point, float zOffset)
		{
			var local = _rubber.BakeFrameToLocal.MultiplyPoint3x4(
				new Vector3(point.x, point.y, zOffset));
			return new float3(local.x, local.y, local.z);
		}
	}
}
