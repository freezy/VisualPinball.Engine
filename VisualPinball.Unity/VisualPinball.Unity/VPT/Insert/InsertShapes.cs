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
		Circle,
		Banana,
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
		/// How flat a base angle the solve accepts at all.
		/// </summary>
		/// <remarks>
		/// A floor against numerical collapse, not the guard against oversized
		/// fillets. Those can always be absorbed by flattening the sharp polygon into
		/// a sliver that the arcs round back into the requested box, but a genuinely
		/// flat triangle has an angle just as shallow, so no floor can tell the two
		/// apart. The radius clamp does that instead, by keeping to the family of
		/// triangles connected to the sharp one.
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
		/// A circle of the given diameter, drawn as a regular polygon of that many
		/// sharp corners. There is nothing to fillet: the corners are the circle's
		/// own sampling, and the sides between them are what a fillet's arc would
		/// be. The first corner sits at the right, so an even count spans the
		/// diameter exactly across, and a count divisible by four spans it both ways.
		/// </summary>
		public static InsertCorner[] CircleCorners(float diameter, int vertices)
		{
			if (diameter <= Epsilon) {
				throw new InsertOutlineException("Diameter must be positive.");
			}
			vertices = math.max(3, vertices);
			var radius = diameter * 0.5f;
			var corners = new InsertCorner[vertices];
			for (var i = 0; i < vertices; i++) {
				var angle = math.PI * 2f * i / vertices;
				corners[i] = new InsertCorner(radius * new float2(math.cos(angle), math.sin(angle)), 0f, 1, i == 0 ? "Right" : null);
			}
			return corners;
		}

		/// <summary>
		/// A band bent round a circle, with ends rounded off to half circles: the
		/// classic banana. The circle is the one the band's middle line follows,
		/// half the thickness reaching out either side of it. Its centre lies
		/// above the band, so the ends turn up. Every point is a sharp corner of
		/// the polygon the shape is drawn as, like a circle's; the arcs get
		/// <paramref name="arcSegments"/> sides each, and each end
		/// <paramref name="endSegments"/>.
		/// </summary>
		/// <param name="diameter">Diameter of the circle the band is bent round.</param>
		/// <param name="thickness">How thick the band is, across the arc.</param>
		/// <param name="angle">How far round the circle the band reaches, in degrees, up to half a turn.</param>
		/// <param name="arcCenter">Where the circle's centre lands, relative to the outline's origin once it is centred on its bounding box: where a starburst wants to be.</param>
		public static InsertCorner[] BananaCorners(float diameter, float thickness, float angle, int arcSegments, int endSegments,
			out float2 arcCenter)
		{
			if (diameter <= Epsilon || thickness <= Epsilon) {
				throw new InsertOutlineException("Diameter and thickness must be positive.");
			}
			if (angle <= 0f || angle > 180f) {
				throw new InsertOutlineException("The angle must be between zero and half a turn.");
			}
			var radius = diameter * 0.5f;
			var half = thickness * 0.5f;
			var inner = radius - half;
			if (inner <= math.max(Epsilon, thickness * 0.01f)) {
				throw new InsertOutlineException("The band is thicker than the circle it is bent round: reduce the thickness or widen the diameter.");
			}
			var outer = radius + half;
			arcSegments = math.max(2, arcSegments);
			endSegments = math.max(2, endSegments);

			// The band sits below the circle's centre, from one end to the other,
			// so the outer arc runs counter-clockwise about the centre and the
			// shape with it: its inside is on the left of every arc and end.
			var sweep = math.radians(angle);
			var from = -math.PI * 0.5f - sweep * 0.5f;
			var to = -math.PI * 0.5f + sweep * 0.5f;
			var corners = new List<InsertCorner>(2 * arcSegments + 2 * endSegments);
			static float2 Polar(float r, float a) => r * new float2(math.cos(a), math.sin(a));
			for (var i = 0; i <= arcSegments; i++) {
				corners.Add(new InsertCorner(Polar(outer, math.lerp(from, to, (float)i / arcSegments)), 0f, 1, i == 0 ? "OuterStart" : i == arcSegments ? "OuterEnd" : null));
			}
			var endCenter = Polar(radius, to);
			for (var i = 1; i < endSegments; i++) {
				corners.Add(new InsertCorner(endCenter + Polar(half, to + math.PI * i / endSegments), 0f, 1));
			}
			for (var i = 0; i <= arcSegments; i++) {
				corners.Add(new InsertCorner(Polar(inner, math.lerp(to, from, (float)i / arcSegments)), 0f, 1, i == 0 ? "InnerEnd" : i == arcSegments ? "InnerStart" : null));
			}
			endCenter = Polar(radius, from);
			for (var i = 1; i < endSegments; i++) {
				corners.Add(new InsertCorner(endCenter + Polar(half, from + math.PI + math.PI * i / endSegments), 0f, 1));
			}

			// The outline is centred on its bounding box; the circle's centre moves with it.
			var low = new float2(float.MaxValue);
			var high = new float2(float.MinValue);
			foreach (var corner in corners) {
				low = math.min(low, corner.Position);
				high = math.max(high, corner.Position);
			}
			arcCenter = -(low + high) * 0.5f;
			return corners.ToArray();
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
			var failure = TrySolveTriangle(width, height, baseRadius, apexRadius, out beta, out a, out h);
			if (failure != null) {
				throw new InsertOutlineException(failure);
			}
		}

		/// <summary>
		/// <see cref="SolveTriangle"/> without the exception, for the clamp, which
		/// asks about candidate scales and expects some of the answers to be no.
		/// </summary>
		/// <returns>Null on success, otherwise why there is no triangle.</returns>
		private static string TrySolveTriangle(float width, float height, float baseRadius, float apexRadius,
			out float beta, out float a, out float h)
		{
			beta = a = h = 0f;
			if (width <= Epsilon || height <= Epsilon) {
				return "Width and height must be positive.";
			}

			var p = width * 0.5f - baseRadius;
			var q = baseRadius - apexRadius;
			var c = height - apexRadius - baseRadius;
			var radius = math.sqrt(p * p + c * c);
			if (radius < Epsilon || math.abs(q) > radius) {
				return $"Corner radii are too large for a {width} x {height} triangle.";
			}

			beta = math.atan2(c, p) - math.asin(q / radius);
			if (beta <= MinAngle || beta >= math.PI * 0.5f - MinAngle) {
				return "No triangle fits these corner radii; reduce them or change the proportions.";
			}

			a = width * 0.5f + baseRadius * (Cot(beta * 0.5f) - 1f);
			h = a * math.tan(beta);
			if (a <= Epsilon || h <= Epsilon) {
				return "Corner radii leave no room for the triangle body.";
			}
			return null;
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
		/// The largest common radius scale in 0..1 whose triangle is still of the
		/// family that grows out of the sharp one.
		/// </summary>
		/// <remarks>
		/// Scaling the radii towards zero ends at the sharp triangle, which always
		/// fits, so the answer is where that family first stops fitting on the way
		/// back up. It is not simply the largest scale that fits anywhere: when the
		/// radii differ sharply, feasibility lapses and then returns at larger
		/// scales, but what returns is a different shape altogether. There the sharp
		/// triangle is tens of times wider than the box and its base fillets sweep
		/// nearly half a turn each, rounding a vast flat sliver back down into a
		/// lozenge of the requested size. The one-degree floor on the base angle
		/// cannot keep that out, since a genuinely flat triangle has just as shallow
		/// an angle; being connected to the sharp triangle can.
		/// <para>
		/// Requested radii that fit as they are still have to belong to the family:
		/// they can land straight in the lozenge region, which is exactly the case
		/// that most needs clamping. See <see cref="FamilyEdge"/> for where the
		/// family ends.
		/// </para>
		/// </remarks>
		private static float LargestFittingScale(float width, float height, float baseRadius, float apexRadius)
		{
			if (baseRadius <= Epsilon && apexRadius <= Epsilon) {
				return 1f;
			}

			// Without a sharp triangle to start from, no radius is to blame, and the
			// message should say it is the proportions.
			var sharp = TrySolveTriangle(width, height, 0f, 0f, out _, out _, out _);
			if (sharp != null) {
				throw new InsertOutlineException(sharp);
			}

			var edge = FamilyEdge(width, height, baseRadius, apexRadius);
			if (edge > 1.0 && ScaleWorks(width, height, baseRadius, apexRadius, 1f)) {
				return 1f;
			}

			// At the edge itself the limiting constraint holds with equality, so
			// step back from it until single precision agrees the triangle fits.
			var limit = math.min(edge, 1.0);
			for (var margin = 1e-6; margin < 0.5; margin *= 10.0) {
				var scale = (float)(limit * (1.0 - margin));
				if (scale > 0f && ScaleWorks(width, height, baseRadius, apexRadius, scale)) {
					return scale;
				}
			}
			throw new InsertOutlineException($"Corner radii are far too large for a {width} x {height} triangle.");
		}

		/// <summary>
		/// The scale at which the family of triangles grown from the sharp one first
		/// stops fitting, or infinity if it never does.
		/// </summary>
		/// <remarks>
		/// Scaling both radii by s moves p, q and c of the closed-form solve along
		/// straight lines, and each of the family's limits then has an exact root:
		/// <list type="bullet">
		/// <item>The solve needs |q| no larger than the length of (p, c), and the
		/// difference of their squares is a quadratic in s.</item>
		/// <item>Each bound on the base angle turns p sin(beta) - c cos(beta) = -q
		/// into an equation linear in s. It is also met by the solve's other
		/// branch, so a root only counts where the branch actually taken has that
		/// angle.</item>
		/// <item>The base fillets fit along the base exactly while rb is at most
		/// W/2.</item>
		/// </list>
		/// The fourth limit, apex and base fillets sharing a slanted side, never
		/// binds first. It reduces to (ra - rb) sin(beta) not exceeding W/2 - rb,
		/// and substituting the solve shows its boundary only where
		/// (q^2 - p^2)(q^2 - p^2 - c^2) = 0: at a right-angled base, which the
		/// angle bound excludes, or on the first limit above. It holds at the sharp
		/// triangle, so it holds across the family. Worked in double precision,
		/// since these are differences of nearly equal squares.
		/// </remarks>
		private static double FamilyEdge(double width, double height, double baseRadius, double apexRadius)
		{
			var size = math.max(math.max(width, height), math.max(baseRadius, apexRadius));
			var edge = double.PositiveInfinity;

			void Consider(double root)
			{
				if (root > 1e-12 && root < edge) {
					edge = root;
				}
			}

			// p^2 + c^2 - q^2 = a s^2 + b s + c0, positive at the sharp triangle,
			// and the solve runs out where it goes negative. With a never negative
			// the parabola opens upwards, so it only goes negative between two
			// distinct roots, from the smaller one. A double root is a parabola
			// touching zero and rising again: the family carries straight on, so
			// it does not count. The smaller root is taken in the form that does
			// not subtract two nearly equal numbers.
			var a = baseRadius * baseRadius + 4.0 * apexRadius * baseRadius;
			var b = -width * baseRadius - 2.0 * height * (apexRadius + baseRadius);
			var c0 = width * width * 0.25 + height * height;
			if (a <= 1e-12 * size * size) {
				if (math.abs(b) > 1e-12 * size) {
					Consider(-c0 / b);
				}
			} else {
				var discriminant = b * b - 4.0 * a * c0;
				if (discriminant > 1e-9 * b * b) {
					Consider(2.0 * c0 / (-b + math.sqrt(discriminant)));
				}
			}

			if (baseRadius > 0.0) {
				Consider(width * 0.5 / baseRadius);
			}

			foreach (var target in new[] { (double)MinAngle, math.PI_DBL * 0.5 - MinAngle }) {
				var sin = math.sin(target);
				var cos = math.cos(target);
				var constant = width * 0.5 * sin - height * cos;
				var slope = -baseRadius * sin + (apexRadius + baseRadius) * cos + (baseRadius - apexRadius);
				if (math.abs(slope) <= 1e-12 * size) {
					continue;
				}

				var root = -constant / slope;
				if (BaseAngle(width, height, baseRadius * root, apexRadius * root) is { } angle
					&& math.abs(angle - target) < 1e-7) {
					Consider(root);
				}
			}
			return edge;
		}

		/// <summary>The closed-form base angle, in double precision, or null where there is none.</summary>
		private static double? BaseAngle(double width, double height, double baseRadius, double apexRadius)
		{
			var p = width * 0.5 - baseRadius;
			var q = baseRadius - apexRadius;
			var c = height - apexRadius - baseRadius;
			var length = math.sqrt(p * p + c * c);
			if (length <= 0.0 || math.abs(q) > length) {
				return null;
			}
			return math.atan2(c, p) - math.asin(q / length);
		}

		private static bool ScaleWorks(float width, float height, float baseRadius, float apexRadius, float scale)
		{
			var failure = TrySolveTriangle(width, height, baseRadius * scale, apexRadius * scale,
				out var beta, out var a, out _);
			return failure == null && TriangleFit(beta, a, baseRadius * scale, apexRadius * scale) >= 1f - 1e-6f;
		}

		private static float Cot(float angle) => 1f / math.tan(angle);
	}
}
