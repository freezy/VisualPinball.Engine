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

namespace VisualPinball.Unity
{
	public readonly struct RubberBakePlane
	{
		public readonly Vector3 Origin;
		public readonly Vector3 AxisX;
		public readonly Vector3 AxisY;
		public readonly Vector3 Normal;
		public readonly Matrix4x4 BakeFrameToLocal;

		public RubberBakePlane(Vector3 origin, Vector3 axisX, Vector3 axisY,
			Vector3 normal, Matrix4x4 bakeFrameToLocal)
		{
			Origin = origin;
			AxisX = axisX;
			AxisY = axisY;
			Normal = normal;
			BakeFrameToLocal = bakeFrameToLocal;
		}

		public Vector3 BakeToWorld(float2 point)
		{
			return Origin + AxisX * Physics.ScaleToWorld(point.x)
				+ AxisY * Physics.ScaleToWorld(point.y);
		}
	}

	public sealed class RubberGuideResolution
	{
		public bool IsValid { get; internal set; }
		public RubberGuideCircle[] Circles { get; internal set; } = Array.Empty<RubberGuideCircle>();
		public RubberBakePlane Plane { get; internal set; }
		public Hash128 InputHash { get; internal set; }
		public string Error { get; internal set; }
	}

	public static class RubberGuideResolver
	{
		public const float DefaultCoplanarToleranceVpx = 0.05f;
		public const float DefaultAngularToleranceDegrees = 0.1f;
		public const float DefaultScaleTolerance = 1e-3f;

		public static RubberGuideResolution Resolve(RubberComponent rubber,
			float coplanarToleranceVpx = DefaultCoplanarToleranceVpx,
			float angularToleranceDegrees = DefaultAngularToleranceDegrees,
			float scaleTolerance = DefaultScaleTolerance)
		{
			var result = new RubberGuideResolution();
			if (!rubber) {
				result.Error = "A rubber component is required.";
				return result;
			}
			if (rubber.GuideBindings.Count == 0) {
				result.Error = "At least one guide binding is required.";
				return result;
			}

			var firstBinding = rubber.GuideBindings[0];
			if (!TryGetSlot(firstBinding, 0, out var firstSlot, out var error)) {
				result.Error = error;
				return result;
			}
			if (!TryCreatePlane(rubber, firstBinding.Guide, firstSlot, coplanarToleranceVpx,
				angularToleranceDegrees, scaleTolerance, out var plane, out error)) {
				result.Error = error;
				return result;
			}

			var circles = new RubberGuideCircle[rubber.GuideBindings.Count];
			var maximumAngularError = math.cos(math.radians(angularToleranceDegrees));
			for (var i = 0; i < rubber.GuideBindings.Count; i++) {
				var binding = rubber.GuideBindings[i];
				if (!TryGetSlot(binding, i, out var slot, out error)) {
					result.Error = error;
					return result;
				}
				var guide = binding.Guide;
				var axis = guide.transform.localToWorldMatrix.MultiplyVector(Vector3.up).normalized;
				if (Vector3.Dot(axis, plane.Normal) < maximumAngularError) {
					result.Error = $"Guide binding {i} is not parallel to the rubber bake plane.";
					return result;
				}

				var center = SlotWorldCenter(guide, slot);
				var offset = center - plane.Origin;
				var planeDistanceVpx = Physics.ScaleToVpx(Vector3.Dot(offset, plane.Normal));
				if (math.abs(planeDistanceVpx) > coplanarToleranceVpx) {
					result.Error = $"Guide binding {i} is {math.abs(planeDistanceVpx):0.###} VPX units outside the rubber plane.";
					return result;
				}

				if (!TryResolveRadius(guide, slot, plane, scaleTolerance, out var radiusVpx,
					out error)) {
					result.Error = $"Guide binding {i}: {error}";
					return result;
				}
				circles[i] = new RubberGuideCircle(new float2(
					Physics.ScaleToVpx(Vector3.Dot(offset, plane.AxisX)),
					Physics.ScaleToVpx(Vector3.Dot(offset, plane.AxisY))), radiusVpx, i);
			}

			result.Circles = circles;
			result.Plane = plane;
			result.InputHash = ComputeInputHash(rubber, circles, plane.BakeFrameToLocal);
			result.IsValid = true;
			return result;
		}

		public static Vector3 SlotWorldCenter(RubberGuideComponent guide, RubberGuideSlot slot)
		{
			return guide.transform.TransformPoint(new Vector3(slot.Profile.LocalCenter.x,
				slot.LocalHeight, slot.Profile.LocalCenter.y));
		}

		private static bool TryGetSlot(RubberGuideBinding binding, int bindingIndex,
			out RubberGuideSlot slot, out string error)
		{
			slot = default;
			error = null;
			if (!binding.Guide) {
				error = $"Guide binding {bindingIndex} has no guide component.";
				return false;
			}
			if (!binding.Guide.TryGetSlot(binding.SlotId, out slot)) {
				error = $"Guide binding {bindingIndex} references missing slot {binding.SlotId}.";
				return false;
			}
			if (slot.Profile.Type != RubberGuideProfileType.Circle) {
				error = $"Guide binding {bindingIndex} uses unsupported profile {slot.Profile.Type}.";
				return false;
			}
			if (!float.IsFinite(slot.LocalHeight) || !math.all(math.isfinite(slot.Profile.LocalCenter))
				|| !float.IsFinite(slot.Profile.Radius) || slot.Profile.Radius <= 0f) {
				error = $"Guide binding {bindingIndex} has invalid slot geometry.";
				return false;
			}
			return true;
		}

		private static bool TryCreatePlane(RubberComponent rubber, RubberGuideComponent guide,
			RubberGuideSlot slot, float coplanarToleranceVpx, float angularToleranceDegrees,
			float scaleTolerance,
			out RubberBakePlane plane, out string error)
		{
			plane = default;
			error = null;
			var matrix = guide.transform.localToWorldMatrix;
			var normal = matrix.MultiplyVector(Vector3.up).normalized;
			var axisX = Vector3.ProjectOnPlane(matrix.MultiplyVector(Vector3.right), normal).normalized;
			if (!IsFinite(normal) || !IsFinite(axisX) || normal.sqrMagnitude < 0.99f
				|| axisX.sqrMagnitude < 0.99f) {
				error = "The first guide has a degenerate transform.";
				return false;
			}
			var axisY = Vector3.Cross(normal, axisX).normalized;
			var angularThreshold = math.cos(math.radians(angularToleranceDegrees));
			var rubberNormal = rubber.transform.localToWorldMatrix.MultiplyVector(Vector3.up).normalized;
			if (Vector3.Dot(rubberNormal, normal) < angularThreshold) {
				error = "The rubber plane is not parallel to the first guide slot.";
				return false;
			}

			var origin = SlotWorldCenter(guide, slot);
			var rubberPlaneDistance = Physics.ScaleToVpx(Vector3.Dot(
				rubber.transform.position - origin, normal));
			if (math.abs(rubberPlaneDistance) > coplanarToleranceVpx) {
				error = $"The rubber origin is {math.abs(rubberPlaneDistance):0.###} VPX units outside the guide plane.";
				return false;
			}

			var rubberX = rubber.transform.localToWorldMatrix.MultiplyVector(Vector3.right);
			var rubberY = rubber.transform.localToWorldMatrix.MultiplyVector(Vector3.back);
			var xLength = rubberX.magnitude;
			var yLength = rubberY.magnitude;
			if (xLength <= 0f || yLength <= 0f
				|| math.abs(xLength - yLength) > scaleTolerance * math.max(xLength, yLength)
				|| math.abs(Vector3.Dot(rubberX / xLength, rubberY / yLength)) > scaleTolerance) {
				error = "The rubber transform is sheared or nonuniformly scaled in its profile plane.";
				return false;
			}

			var bakeFrameToLocal = CreateBakeFrameToLocal(rubber.transform, origin, axisX, axisY,
				normal);
			plane = new RubberBakePlane(origin, axisX, axisY, normal, bakeFrameToLocal);
			return true;
		}

		private static bool TryResolveRadius(RubberGuideComponent guide, RubberGuideSlot slot,
			RubberBakePlane plane, float scaleTolerance, out float radiusVpx, out string error)
		{
			radiusVpx = 0f;
			error = null;
			var radialX = guide.transform.localToWorldMatrix.MultiplyVector(
				Vector3.right * slot.Profile.Radius);
			var radialY = guide.transform.localToWorldMatrix.MultiplyVector(
				Vector3.forward * slot.Profile.Radius);
			var x = new float2(Vector3.Dot(radialX, plane.AxisX),
				Vector3.Dot(radialX, plane.AxisY));
			var y = new float2(Vector3.Dot(radialY, plane.AxisX),
				Vector3.Dot(radialY, plane.AxisY));
			var xLength = math.length(x);
			var yLength = math.length(y);
			if (xLength <= 0f || yLength <= 0f
				|| math.abs(xLength - yLength) > scaleTolerance * math.max(xLength, yLength)
				|| math.abs(math.dot(x / xLength, y / yLength)) > scaleTolerance) {
				error = "circle profile scale is sheared or nonuniform.";
				return false;
			}
			radiusVpx = Physics.ScaleToVpx((xLength + yLength) * 0.5f);
			return float.IsFinite(radiusVpx) && radiusVpx > 0f;
		}

		private static Matrix4x4 CreateBakeFrameToLocal(Transform rubberTransform,
			Vector3 origin, Vector3 axisX, Vector3 axisY, Vector3 normal)
		{
			var originLocal = WorldToRubberVpx(rubberTransform, origin);
			var xLocal = WorldToRubberVpx(rubberTransform,
				origin + axisX * Physics.ScaleToWorld(1f)) - originLocal;
			var yLocal = WorldToRubberVpx(rubberTransform,
				origin + axisY * Physics.ScaleToWorld(1f)) - originLocal;
			var zLocal = WorldToRubberVpx(rubberTransform,
				origin + normal * Physics.ScaleToWorld(1f)) - originLocal;
			var matrix = Matrix4x4.identity;
			matrix.SetColumn(0, new Vector4(xLocal.x, xLocal.y, xLocal.z, 0f));
			matrix.SetColumn(1, new Vector4(yLocal.x, yLocal.y, yLocal.z, 0f));
			matrix.SetColumn(2, new Vector4(zLocal.x, zLocal.y, zLocal.z, 0f));
			matrix.SetColumn(3, new Vector4(originLocal.x, originLocal.y, originLocal.z, 1f));
			return matrix;
		}

		private static Vector3 WorldToRubberVpx(Transform rubberTransform, Vector3 worldPoint)
		{
			return rubberTransform.worldToLocalMatrix.MultiplyPoint(worldPoint).TranslateToVpx();
		}

		private static Hash128 ComputeInputHash(RubberComponent rubber,
			IReadOnlyList<RubberGuideCircle> circles, Matrix4x4 bakeFrameToLocal)
		{
			var hash = new RubberInputHash();
			hash.Add((uint)rubber.Thickness);
			hash.Add((uint)circles.Count);
			for (var i = 0; i < circles.Count; i++) {
				var binding = rubber.GuideBindings[i];
				hash.Add(binding.SlotId.A);
				hash.Add(binding.SlotId.B);
				hash.Add(circles[i].Center.x);
				hash.Add(circles[i].Center.y);
				hash.Add(circles[i].Radius);
			}
			for (var row = 0; row < 4; row++) {
				for (var column = 0; column < 4; column++) {
					hash.Add(bakeFrameToLocal[row, column]);
				}
			}
			return hash.ToHash128();
		}

		private static bool IsFinite(Vector3 value)
		{
			return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
		}

		private sealed class RubberInputHash
		{
			private uint _a = 2166136261u;
			private uint _b = 2246822519u;
			private uint _c = 3266489917u;
			private uint _d = 668265263u;

			public void Add(float value) => Add(math.asuint(value));

			public void Add(ulong value)
			{
				Add((uint)value);
				Add((uint)(value >> 32));
			}

			public void Add(uint value)
			{
				Mix(ref _a, value, 16777619u);
				Mix(ref _b, value ^ 0x9e3779b9u, 2246822519u);
				Mix(ref _c, value ^ 0x85ebca6bu, 3266489917u);
				Mix(ref _d, value ^ 0xc2b2ae35u, 668265263u);
			}

			public Hash128 ToHash128() => new(_a, _b, _c, _d);

			private static void Mix(ref uint state, uint value, uint multiplier)
			{
				state ^= value;
				state *= multiplier | 1u;
				state ^= state >> 13;
			}
		}
	}
}
