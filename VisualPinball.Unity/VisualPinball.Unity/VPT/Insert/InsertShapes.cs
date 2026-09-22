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
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The outline an insert is built from.
	/// </summary>
	public enum InsertShape
	{
		Triangle,
		Rectangle,
	}

	/// <summary>
	/// Radius and sampling density of one filleted corner.
	/// </summary>
	[Serializable]
	public class InsertCornerSettings
	{
		[Min(0f)]
		[Tooltip("Radius of the corner fillet, in VPX units. Measured on the finished shape, so the width and height stay put when it changes.")]
		public float Radius = 4f;

		[Range(1, 64)]
		[Tooltip("Arc segments across the fillet. A segment count of one gives a chamfer; the arc carries one more vertex than it has segments.")]
		public int Segments = 8;

		public InsertCornerSettings() { }

		public InsertCornerSettings(float radius, int segments)
		{
			Radius = radius;
			Segments = segments;
		}
	}

	/// <summary>
	/// Shape types for step 1: parameters in, a sharp counter-clockwise polygon out.
	/// </summary>
	/// <remarks>
	/// Each shape is responsible for one thing only, which is placing the corners
	/// of the <i>sharp</i> polygon so that the finished, filleted outline fills the
	/// requested width and height exactly. Everything after that -- filleting,
	/// sampling, tagging, centring -- is shared and lives in
	/// <see cref="InsertOutlineBuilder"/>.
	/// </remarks>
	public static class InsertShapes
	{
		private const float Epsilon = InsertOutlineBuilder.Epsilon;

		public const float MetresPerInch = 0.0254f;

		/// <summary>
		/// VPX units per inch, since insert parts are catalogued in inches.
		/// </summary>
		public static readonly float VpxUnitsPerInch = MetresPerInch / Physics.ScaleInv;

		/// <summary>
		/// How flat a corner may get before a shape counts as collapsed.
		/// </summary>
		/// <remarks>
		/// Oversized fillets can always be absorbed by flattening the sharp polygon
		/// until it is a sliver that the arcs round back into the requested box. The
		/// result is technically the right size but no longer the shape that was
		/// asked for, so the clamp stops short of it and says so.
		/// </remarks>
		public static readonly float MinAngle = math.radians(1f);

		/// <summary>
		/// Isosceles triangle, base along X, apex towards +Y.
		/// </summary>
		/// <remarks>
		/// The two base corners share one fillet, the apex has its own, and the
		/// bounding box of the finished rounded shape is exactly width by height.
		/// <para>
		/// Writing the sharp triangle as half-base a and height h = a tan(beta) with
		/// base angle beta, the fillets change the bounding box like this: the base
		/// fillets are tangent to the base line, so the bottom does not move; each
		/// base fillet pulls its side in by rb (cot(beta/2) - 1); and the apex
		/// fillet pulls the top down by ra (sec(beta) - 1), because the apex
		/// half-angle is pi/2 - beta.
		/// </para>
		/// <para>
		/// Substituting a from the width equation and using the identity
		/// cot(beta/2) tan(beta) = sec(beta) + 1 collapses the height equation to
		/// H = (W/2 - rb) tan(beta) + (rb - ra) sec(beta) + rb + ra, which is
		/// p sin(beta) - c cos(beta) = -q in disguise and solves in closed form.
		/// </para>
		/// </remarks>
		public static InsertCorner[] TriangleCorners(float width, float height,
			InsertCornerSettings baseCorner, InsertCornerSettings apexCorner, List<string> warnings)
		{
			var scale = LargestFittingScale(width, height, baseCorner.Radius, apexCorner.Radius);
			if (scale < 1f - 1e-4f) {
				warnings?.Add($"Corner radius reduced to {scale * 100f:0}% to fit the triangle.");
			}

			var baseRadius = baseCorner.Radius * scale;
			var apexRadius = apexCorner.Radius * scale;
			SolveTriangle(width, height, baseRadius, apexRadius, out _, out var a, out var h);

			return new[] {
				new InsertCorner(new float2(-a, 0f), baseRadius, baseCorner.Segments, "BaseLeft"),
				new InsertCorner(new float2(a, 0f), baseRadius, baseCorner.Segments, "BaseRight"),
				new InsertCorner(new float2(0f, h), apexRadius, apexCorner.Segments, "Apex"),
			};
		}

		/// <summary>
		/// Rectangle with one fillet radius shared by all four corners.
		/// </summary>
		/// <remarks>
		/// Nothing to solve here. A fillet is tangent to both of the edges that meet
		/// at its corner, so it touches the bounding box exactly where the sharp
		/// corner did, which means the sharp rectangle is already the requested
		/// size. An oversized radius is caught by the shared clamp in
		/// <see cref="InsertOutlineBuilder"/>, and that cannot move the bounding box
		/// either, since shrinking a fillet only pulls it back towards its corner.
		/// </remarks>
		public static InsertCorner[] RectangleCorners(float width, float height, InsertCornerSettings corner)
		{
			if (width <= Epsilon || height <= Epsilon) {
				throw new InsertOutlineException("Width and height must be positive.");
			}

			var w = width * 0.5f;
			var h = height * 0.5f;
			return new[] {
				new InsertCorner(new float2(-w, -h), corner.Radius, corner.Segments, "BottomLeft"),
				new InsertCorner(new float2(w, -h), corner.Radius, corner.Segments, "BottomRight"),
				new InsertCorner(new float2(w, h), corner.Radius, corner.Segments, "TopRight"),
				new InsertCorner(new float2(-w, h), corner.Radius, corner.Segments, "TopLeft"),
			};
		}

		/// <summary>
		/// Base angle and sharp dimensions that hit width by height exactly.
		/// </summary>
		private static void SolveTriangle(float width, float height, float baseRadius, float apexRadius,
			out float beta, out float a, out float h)
		{
			if (width <= Epsilon || height <= Epsilon) {
				throw new InsertOutlineException("Width and height must be positive.");
			}

			var p = width * 0.5f - baseRadius;
			var q = baseRadius - apexRadius;
			var c = height - apexRadius - baseRadius;
			var radius = math.sqrt(p * p + c * c);
			if (radius < Epsilon || math.abs(q) > radius) {
				throw new InsertOutlineException($"Corner radii are too large for a {width} x {height} triangle.");
			}

			beta = math.atan2(c, p) - math.asin(q / radius);
			if (beta <= MinAngle || beta >= math.PI * 0.5f - MinAngle) {
				throw new InsertOutlineException("No triangle fits these corner radii; reduce them or change the proportions.");
			}

			a = width * 0.5f + baseRadius * (Cot(beta * 0.5f) - 1f);
			h = a * math.tan(beta);
			if (a <= Epsilon || h <= Epsilon) {
				throw new InsertOutlineException("Corner radii leave no room for the triangle body.");
			}
		}

		/// <summary>
		/// How far the fillets may grow before their tangent points collide.
		/// </summary>
		/// <remarks>
		/// One means everything fits; below that it is the factor both radii have to
		/// shrink by. Both tangent offsets are linear in their radius, so each of
		/// the two sides gives an exact ratio.
		/// </remarks>
		private static float TriangleFit(float beta, float a, float baseRadius, float apexRadius)
		{
			var tangentBase = baseRadius * Cot(beta * 0.5f);

			// The apex half-angle is pi/2 - beta, and cot(pi/2 - beta) is tan(beta).
			var tangentApex = apexRadius * math.tan(beta);

			var ratio = 1f;
			if (tangentBase > a) {
				ratio = math.min(ratio, a / tangentBase);
			}

			var slant = a / math.cos(beta);
			if (tangentBase + tangentApex > slant) {
				ratio = math.min(ratio, slant / (tangentBase + tangentApex));
			}
			return ratio;
		}

		/// <summary>
		/// The largest common radius scale in 0..1 that still yields a valid
		/// triangle, to the resolution of the scan below.
		/// </summary>
		/// <remarks>
		/// Shrinking the radii does not simply make the triangle easier. When the
		/// two radii differ sharply, feasibility can lapse and return as they scale,
		/// because the angle limit and the two tangent-fit limits do not carve one
		/// unbroken stretch out of 0..1. A plain bisection latches onto the first
		/// gap it meets and reports a far smaller scale than the shape can take.
		/// Scanning coarsely downwards finds the highest feasible sample instead,
		/// and the bisection then refines only the step above it, where the boundary
		/// really is crossed once.
		/// </remarks>
		private static float LargestFittingScale(float width, float height, float baseRadius, float apexRadius)
		{
			if (baseRadius <= Epsilon && apexRadius <= Epsilon) {
				return 1f;
			}
			if (ScaleWorks(width, height, baseRadius, apexRadius, 1f)) {
				return 1f;
			}

			const int steps = 64;
			var low = 0f;
			var high = 1f / steps;
			for (var i = steps - 1; i >= 1; i--) {
				var sample = (float)i / steps;
				if (ScaleWorks(width, height, baseRadius, apexRadius, sample)) {
					low = sample;
					high = (float)(i + 1) / steps;
					break;
				}
			}

			if (low <= 0f) {
				throw new InsertOutlineException($"Corner radii are far too large for a {width} x {height} triangle.");
			}

			for (var i = 0; i < 20; i++) {
				var mid = (low + high) * 0.5f;
				if (ScaleWorks(width, height, baseRadius, apexRadius, mid)) {
					low = mid;
				} else {
					high = mid;
				}
			}
			return low;
		}

		private static bool ScaleWorks(float width, float height, float baseRadius, float apexRadius, float scale)
		{
			try {
				SolveTriangle(width, height, baseRadius * scale, apexRadius * scale, out var beta, out var a, out _);
				return TriangleFit(beta, a, baseRadius * scale, apexRadius * scale) >= 1f - 1e-6f;

			} catch (InsertOutlineException) {
				return false;
			}
		}

		private static float Cot(float angle) => 1f / math.tan(angle);
	}
}
