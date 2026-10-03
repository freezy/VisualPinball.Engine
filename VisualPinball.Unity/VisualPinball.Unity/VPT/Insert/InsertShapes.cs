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
		Diamond,
		Flash,
		Bullet,
		Arrow,
		Custom,
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
		/// A diamond: a rhombus standing on its points, the left and right pair
		/// filleted alike and the top and bottom pair alike, sheared by an angle
		/// so the top leans over to one side. The finished shape, fillets and
		/// all, fills the width by the height exactly.
		/// </summary>
		/// <remarks>
		/// A fillet on a corner that is not a right angle pulls the shape's
		/// extreme in from the sharp corner, by an amount that depends on the
		/// corner's angle, which the shear changes as well. Rather than solve
		/// that in closed form for every combination, the sharp rhombus is
		/// measured as the outline builder itself would build it, and its
		/// half-diagonals are corrected by what is missing, a few times over,
		/// which converges in a handful of steps since the pull barely moves.
		/// Fillets too round for the shape are scaled down until it does
		/// converge, with a warning, as a triangle's are.
		/// </remarks>
		/// <param name="shear">How far the top and bottom points lean over, in
		/// degrees from upright, positive to the right as seen on the table,
		/// which is the shape's left, since the mesh turns the shape half round.
		/// Held short of the sides' points at every step, or the shape stops
		/// being a diamond.</param>
		public static InsertCorner[] DiamondCorners(float width, float height, InsertCornerSettings side, InsertCornerSettings tip,
			float shear, List<string> warnings)
		{
			if (width <= Epsilon || height <= Epsilon) {
				throw new InsertOutlineException("Width and height must be positive.");
			}
			var requested = math.tan(math.radians(math.clamp(shear, -80f, 80f)));
			var slope = requested;
			var corners = FitBox(width, height, "diamond", warnings, (a, b, scale, _) => {
				// The lean is held short of the sides at every step, since the
				// diagonals move under it.
				var limit = 0.9f * a / b;
				if (math.abs(slope) > limit) {
					slope = limit * math.sign(slope);
				}
				return new[] {
					new InsertCorner(new float2(a, 0f), side.Radius * scale, side.Segments, "Right"),
					new InsertCorner(new float2(-b * slope, b), tip.Radius * scale, tip.Segments, "Top"),
					new InsertCorner(new float2(-a, 0f), side.Radius * scale, side.Segments, "Left"),
					new InsertCorner(new float2(b * slope, -b), tip.Radius * scale, tip.Segments, "Bottom"),
				};
			});
			if (math.abs(slope - requested) > 1e-6f) {
				warnings?.Add($"Shear reduced to {math.degrees(math.atan(slope)):0.#} degrees, so the top and bottom stay between the sides.");
			}
			return corners;
		}

		/// <summary>
		/// A flash: the lightning bolt. Two arms, one above the other and turned
		/// half round from each other, each with a flat end, an outer edge
		/// running from that end down to a spike, and a step back in from the
		/// spike to a notch where the other arm's outer edge takes over. Six
		/// numbers draw the sharp shape: its width and height, how wide the flat
		/// ends are, how far the steps go in from the spikes, and how far above
		/// and below the middle the two steps sit, which below zero swaps them
		/// over so the arms overlap further. The tips' corners, the spikes
		/// and the notches each round off with fillets of their own, the notches'
		/// turning inwards.
		/// </summary>
		/// <remarks>
		/// The flat ends touch the top and bottom whatever their fillets, but the
		/// spikes and the tips' outer corners are the widest points, and a fillet
		/// pulls them in, so the sharp width is fitted to the box the way a
		/// diamond's is.
		/// </remarks>
		public static InsertCorner[] FlashCorners(float width, float height, float tipWidth, float notchDepth, float step,
			InsertCornerSettings tipCorner, InsertCornerSettings spikeCorner, InsertCornerSettings notchCorner, List<string> warnings)
		{
			if (width <= Epsilon || height <= Epsilon) {
				throw new InsertOutlineException("Width and height must be positive.");
			}
			if (tipWidth <= Epsilon || tipWidth >= width - Epsilon) {
				throw new InsertOutlineException("The tips must be narrower than the flash and wider than nothing.");
			}
			if (notchDepth <= Epsilon || notchDepth >= width * 0.5f - Epsilon) {
				throw new InsertOutlineException("The notches must reach in, and less than halfway across.");
			}
			if (math.abs(step) >= height * 0.5f - Epsilon) {
				throw new InsertOutlineException("The steps must sit between the ends.");
			}
			// Below the middle, the left step sits where the lower arm's outer
			// edge runs; that edge has to pass the notch on the inside, or the arms
			// cut through each other.
			if (step < 0f) {
				var b = height * 0.5f;
				var a = width * 0.5f;
				var crossing = -a + tipWidth + (2f * a - tipWidth) * (b + step) / (b - step);
				if (crossing <= -a + notchDepth + Epsilon) {
					throw new InsertOutlineException("The steps overlap too far for the notches: raise the step height, or make the notches shallower or the tips wider.");
				}
			}
			return FitBox(width, height, "flash", warnings, (a, b, scale, _) => new[] {
				new InsertCorner(new float2(a, b), tipCorner.Radius * scale, tipCorner.Segments, "TopRight"),
				new InsertCorner(new float2(a - tipWidth, b), tipCorner.Radius * scale, tipCorner.Segments, "TopLeft"),
				new InsertCorner(new float2(-a, step), spikeCorner.Radius * scale, spikeCorner.Segments, "SpikeLeft"),
				new InsertCorner(new float2(-a + notchDepth, step), notchCorner.Radius * scale, notchCorner.Segments, "NotchLeft"),
				new InsertCorner(new float2(-a, -b), tipCorner.Radius * scale, tipCorner.Segments, "BottomLeft"),
				new InsertCorner(new float2(-a + tipWidth, -b), tipCorner.Radius * scale, tipCorner.Segments, "BottomRight"),
				new InsertCorner(new float2(a, -step), spikeCorner.Radius * scale, spikeCorner.Segments, "SpikeRight"),
				new InsertCorner(new float2(a - notchDepth, -step), notchCorner.Radius * scale, notchCorner.Segments, "NotchRight"),
			});
		}

		/// <summary>
		/// A bullet: a flat base, straight sides running up to the shoulders,
		/// and from there flanks that lean in as tangents to the round tip, a
		/// circle of the tip radius touching the top. Where the flanks lean
		/// follows from the width, the tip and the shoulders, so the shape
		/// fills its width and height exactly without any solving.
		/// </summary>
		/// <remarks>
		/// The tip is sampled along its circle as sharp corners, the way the
		/// banana's arcs are, rather than built as a fillet on an apex: a tip as
		/// wide as the bullet has vertical flanks and no apex at all. The whole
		/// arc takes the tip's segment count, rounded up to an even number so the
		/// topmost point is a vertex and the height is exact.
		/// </remarks>
		/// <param name="shoulder">How far up from the base the straight sides
		/// run before the flanks turn in, in VPX units. Held below the tip's
		/// centre, or the flanks would lean out instead of in.</param>
		public static InsertCorner[] BulletCorners(float width, float height, InsertCornerSettings tip, float shoulder,
			InsertCornerSettings shoulderCorner, InsertCornerSettings baseCorner, List<string> warnings)
		{
			if (width <= Epsilon || height <= Epsilon) {
				throw new InsertOutlineException("Width and height must be positive.");
			}
			// The tip's circle can be no wider than the bullet, and its centre
			// can drop no further than the base: half a disc.
			var radius = math.clamp(tip.Radius, 0f, math.min(width * 0.5f, height));
			if (tip.Radius > width * 0.5f + 1e-6f) {
				warnings?.Add("Tip radius reduced to half the width.");
			}
			if (tip.Radius > height + 1e-6f) {
				warnings?.Add("Tip radius reduced to the height.");
			}
			shoulder = math.max(0f, shoulder);
			if (shoulder > height - radius + 1e-6f) {
				warnings?.Add($"Shoulders lowered to {height - radius:0.#} to stay below the tip's centre.");
				shoulder = height - radius;
			}

			// With no straight side to hold it, a base fillet pulls the widest
			// point in from the corner, which the fit makes up for.
			return FitBox(width, height, "bullet", warnings, (a, b, scale, _) => {
				var center = b - radius;
				var shoulderY = math.min(-b + shoulder, center);

				// The flank from the right shoulder touches the tip's circle where
				// the direction from the centre to the shoulder turns by the angle
				// whose cosine is the radius over that distance. A tip as wide as
				// the bullet is touched vertically, right above the side, and the
				// shoulder is no corner at all; nor is it one when the sides have no
				// length.
				var toShoulder = new float2(a, shoulderY - center);
				var touch = radius <= Epsilon
					? math.PI * 0.5f
					: math.atan2(toShoulder.y, toShoulder.x) + math.acos(math.min(1f, radius / math.length(toShoulder)));
				var flanked = touch > math.radians(0.5f);
				var shouldered = flanked && shoulderY > -b + Epsilon;
				// The one bullet whose tip meets the base at the base's own corners:
				// half a disc, which is nothing but its arc.
				var based = flanked || center > -b + Epsilon;

				var corners = new List<InsertCorner>();
				if (based) {
					corners.Add(new InsertCorner(new float2(a, -b), baseCorner.Radius * scale, baseCorner.Segments, "BaseRight"));
				}
				if (shouldered) {
					corners.Add(new InsertCorner(new float2(a, shoulderY), shoulderCorner.Radius * scale, shoulderCorner.Segments, "ShoulderRight"));
				}
				// A tip too small to sample, its whole arc shorter than the least
				// chord the builder tells apart, is a point; a larger one takes no
				// more samples than keep its chords that long. The chords are not
				// sides: side segments leave them alone.
				var top = math.PI * 0.5f;
				var minChord = math.max(Epsilon * 10f, math.max(a, b) * 2e-5f);
				var arc = radius * (top - touch);
				if (radius <= Epsilon || arc < minChord) {
					corners.Add(new InsertCorner(new float2(0f, b), 0f, 1, "Tip"));
				} else {
					var halves = math.clamp((tip.Segments + 1) / 2, 1, (int)math.floor(arc / minChord));
					var origin = new float2(0f, center);
					static float2 Polar(float r, float angle) => r * new float2(math.cos(angle), math.sin(angle));
					for (var i = 0; i <= halves; i++) {
						corners.Add(new InsertCorner(origin + Polar(radius, math.lerp(touch, top, (float)i / halves)), 0f, 1, i == 0 ? "TipRight" : i == halves ? "Tip" : null, true));
					}
					for (var i = 1; i <= halves; i++) {
						corners.Add(new InsertCorner(origin + Polar(radius, math.lerp(top, math.PI - touch, (float)i / halves)), 0f, 1, i == halves ? "TipLeft" : null, i < halves));
					}
				}
				if (shouldered) {
					corners.Add(new InsertCorner(new float2(-a, shoulderY), shoulderCorner.Radius * scale, shoulderCorner.Segments, "ShoulderLeft"));
				}
				if (based) {
					corners.Add(new InsertCorner(new float2(-a, -b), baseCorner.Radius * scale, baseCorner.Segments, "BaseLeft"));
				}
				return corners.ToArray();
			});
		}

		/// <summary>
		/// An arrow: an arrowhead standing on two feet, with a notch cut up into
		/// its base between them. The notch's depth, its width at the base and
		/// the tilt of the feet, with the fillets at the tip, the notch, the
		/// inner corners and the feet, cover a rounded arrow with a scooped
		/// base, an arrowhead whose whole base is one hollow arc, and a chevron
		/// with its feet cut off square. The finished shape fills the width by
		/// the height exactly.
		/// </summary>
		/// <param name="notchDepth">From the shape's bottom up to the notch's
		/// apex, in VPX units. Zero leaves the base flat.</param>
		/// <param name="notchWidth">Between the two inner corners, where the
		/// notch opens into the base, in VPX units. The full width leaves no
		/// feet: the notch runs from one outer corner to the other.</param>
		/// <param name="footAngle">How far the feet tilt, in degrees: positive
		/// lifts their inner corners up towards the notch, negative drops them
		/// below the outer corners.</param>
		public static InsertCorner[] ArrowCorners(float width, float height, InsertCornerSettings tip, float notchDepth,
			float notchWidth, float footAngle, InsertCornerSettings notchCorner, InsertCornerSettings innerCorner,
			InsertCornerSettings footCorner, List<string> warnings)
		{
			if (width <= Epsilon || height <= Epsilon) {
				throw new InsertOutlineException("Width and height must be positive.");
			}
			var depth = math.max(0f, notchDepth);
			if (depth > height * 0.9f) {
				depth = height * 0.9f;
				warnings?.Add($"Notch depth reduced to {depth:0.#}, so the notch stays below the tip.");
			}
			var opening = math.clamp(notchWidth, 0f, width);
			if (notchWidth > width + 1e-6f) {
				warnings?.Add("Notch width reduced to the width: the whole base is the notch.");
			}

			// A foot, or a notch, too narrow to hold its own corners is none.
			ArrowTopology(width, depth, opening, out var notched, out var footed);
			var slope = math.tan(math.radians(math.clamp(footAngle, -85f, 85f)));
			var held = slope;

			// The tip's fillet pulls the top down and the feet's pull the width
			// in, and dropped feet's inner corners are the bottom: the fit makes
			// up for all of it. The inner corners stay where the notch width
			// puts them, so the notch opens as wide as asked whatever the fit
			// does to the outer corners, and the notch's apex is measured from
			// the finished bottom, which the fit reports as the lift. The feet
			// tilt by their angle as fitted, so it stays what was asked however
			// wide the fit makes them.
			var fitted = FitBox(width, height, "arrow", warnings, (a, b, scale, lift) => {
				var rise = 0f;
				if (footed) {
					var foot = a - opening * 0.5f;
					rise = foot * slope;
					// Lifted feet keep their inner corners below the notch and
					// inside the flanks; dropped feet leave a head above them.
					var most = rise > 0f ? 0.9f * math.min(depth, 2f * b * foot / a) : 0.9f * height;
					if (math.abs(rise) > most) {
						rise = most * math.sign(rise);
					}
					held = rise / foot;
				}
				var corners = new List<InsertCorner>(6) {
					new(new float2(0f, b), tip.Radius * scale, tip.Segments, "Apex"),
					new(new float2(-a, -b), footCorner.Radius * scale, footCorner.Segments, "FootLeft"),
				};
				if (notched) {
					var bottom = -b + math.min(0f, rise) + lift;
					if (footed) {
						corners.Add(new InsertCorner(new float2(-opening * 0.5f, -b + rise), innerCorner.Radius * scale, innerCorner.Segments, "InnerLeft"));
					}
					corners.Add(new InsertCorner(new float2(0f, bottom + depth), notchCorner.Radius * scale, notchCorner.Segments, "Notch"));
					if (footed) {
						corners.Add(new InsertCorner(new float2(opening * 0.5f, -b + rise), innerCorner.Radius * scale, innerCorner.Segments, "InnerRight"));
					}
				}
				corners.Add(new InsertCorner(new float2(a, -b), footCorner.Radius * scale, footCorner.Segments, "FootRight"));
				return corners.ToArray();
			});
			if (math.abs(held - slope) > 1e-6f) {
				warnings?.Add($"Foot angle reduced to {math.degrees(math.atan(held)):0.#} degrees, so the feet stay inside the arrow.");
			}
			return fitted;
		}

		/// <summary>
		/// What an arrow's notch amounts to: none at all without depth or an
		/// opening to speak of, and one without inner corners when the feet it
		/// would leave are too narrow to hold their own. The inspector greys
		/// out by the same rule.
		/// </summary>
		public static void ArrowTopology(float width, float notchDepth, float notchWidth, out bool notched, out bool footed)
		{
			var least = math.max(Epsilon * 10f, width * 0.01f);
			var opening = math.min(notchWidth, width);
			notched = notchDepth > Epsilon && opening > least;
			footed = notched && (width - opening) * 0.5f > least;
		}

		/// <summary>
		/// The sharp corners of a shape whose fillets pull its extremes in,
		/// fitted so the finished shape fills the width by the height exactly.
		/// </summary>
		/// <remarks>
		/// The sharp shape is measured as the outline builder itself would build
		/// it, and its half-width and half-height are corrected by what is
		/// missing, a few times over, which converges in a handful of steps since
		/// the pull barely moves. Fillets too round for the shape are scaled down
		/// until it does converge, with a warning, as a triangle's are.
		/// </remarks>
		/// <param name="sharp">The corners for a half-width, a half-height, a
		/// scale on every fillet radius, and the lift: how far the fillets at the
		/// bottom pulled the finished bottom up from the lowest sharp corner in
		/// the previous pass, for a shape that places something relative to the
		/// finished bottom.</param>
		private static InsertCorner[] FitBox(float width, float height, string shape, List<string> warnings,
			Func<float, float, float, float, InsertCorner[]> sharp)
		{
			var scale = 1f;
			InsertCorner[] corners = null;
			var converged = false;
			for (var attempt = 0; attempt < 12 && !converged; attempt++) {
				var a = width * 0.5f;
				var b = height * 0.5f;
				var lift = 0f;
				for (var step = 0; step < 64; step++) {
					corners = sharp(a, b, scale, lift);
					var measured = InsertOutlineBuilder.Build(corners, false);
					var missingWidth = width - measured.Width;
					var missingHeight = height - measured.Height;
					InsertOutlineBuilder.AnalyticBounds(measured.Corners, out var low, out _);
					var lifted = low.y - corners.Min(c => c.Position.y);
					var settled = math.abs(lifted - lift) < 1e-5f;
					lift = lifted;
					if (math.abs(missingWidth) < 1e-5f && math.abs(missingHeight) < 1e-5f && settled) {
						converged = true;
						break;
					}
					a += missingWidth * 0.5f;
					b += missingHeight * 0.5f;
					if (a <= Epsilon || b <= Epsilon) {
						break;
					}
				}
				if (!converged) {
					scale *= 0.8f;
				}
			}
			if (!converged) {
				throw new InsertOutlineException($"The corners are too round for a {shape} this size.");
			}
			if (scale < 1f - 1e-4f) {
				warnings?.Add($"Corner radius reduced to {scale * 100f:0}% to fit the {shape}.");
			}
			return corners;
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

		/// <summary>
		/// The outline's sharp polygon eroded by <paramref name="distance"/>: every
		/// side moved in by that much, neighbouring sides meeting again where they
		/// cross, and a side its neighbours cross before they reach it, such as
		/// the flat end of a flash, gone, its two corners one sharp point. Convex
		/// fillets shrink by the distance, never below
		/// <paramref name="minConvexRadius"/>; concave ones grow by it, with more
		/// segments to match, so the eroded shape keeps its rounds where it can.
		/// </summary>
		/// <remarks>
		/// This is what an engraved line inside a shape follows: unlike an inset
		/// clamped to keep every side, it lets narrow ends close up into points.
		/// The corners come back in the outline's own space, already centred,
		/// so the outline built from them must not be centred again.
		/// </remarks>
		/// <returns>The eroded corners, or null when nothing is left.</returns>
		public static InsertCorner[] Erode(InsertOutline outline, float distance, float minConvexRadius, List<string> warnings)
		{
			var corners = outline.Corners;
			var count = corners.Count;
			var origins = new float2[count];
			var directions = new float2[count];
			for (var i = 0; i < count; i++) {
				var from = corners[i].Position;
				var to = corners[(i + 1) % count].Position;
				var side = to - from;
				var length = math.length(side);
				var direction = length > Epsilon ? side / length : corners[i].Outgoing;
				directions[i] = direction;
				// The polygon runs counter-clockwise, so inwards is to the left.
				origins[i] = from + distance * new float2(-direction.y, direction.x);
			}

			// Sides that the erosion turns back on themselves go, one at a time,
			// the most reversed first, until every side that is left still runs
			// its own way.
			var active = new List<int>(count);
			for (var i = 0; i < count; i++) {
				active.Add(i);
			}
			var vertices = new float2[count];
			while (true) {
				var m = active.Count;
				if (m < 3) {
					warnings?.Add($"Eroding the outline by {distance:0.##} leaves nothing of it.");
					return null;
				}
				var gone = -1;
				var worst = float.MaxValue;
				for (var j = 0; j < m; j++) {
					var previous = active[(j - 1 + m) % m];
					var current = active[j];
					if (!Intersect(origins[previous], directions[previous], origins[current], directions[current], out vertices[j])) {
						// Parallel neighbours meet nowhere: the one between them is a
						// side of no width, which goes.
						gone = current;
						worst = float.MinValue;
						break;
					}
				}
				if (gone < 0) {
					for (var j = 0; j < m; j++) {
						var run = math.dot(vertices[(j + 1) % m] - vertices[j], directions[active[j]]);
						if (run <= Epsilon && run < worst) {
							worst = run;
							gone = active[j];
						}
					}
				}
				if (gone < 0) {
					break;
				}
				active.Remove(gone);
			}

			// An erosion deeper than the shape turns what is left inside out, or
			// carries it outside the shape altogether: nothing is left then.
			var area = 0f;
			for (var j = 0; j < active.Count; j++) {
				var a = vertices[j];
				var b = vertices[(j + 1) % active.Count];
				area += a.x * b.y - b.x * a.y;
			}
			if (area <= Epsilon) {
				warnings?.Add($"Eroding the outline by {distance:0.##} leaves nothing of it.");
				return null;
			}
			for (var j = 0; j < active.Count; j++) {
				var inside = false;
				for (int i = 0, k = count - 1; i < count; k = i++) {
					var a = corners[i].Position;
					var b = corners[k].Position;
					if (a.y > vertices[j].y != b.y > vertices[j].y && vertices[j].x < (b.x - a.x) * (vertices[j].y - a.y) / (b.y - a.y) + a.x) {
						inside = !inside;
					}
				}
				if (!inside) {
					warnings?.Add($"Eroding the outline by {distance:0.##} leaves nothing of it.");
					return null;
				}
			}

			// Two parts of the shape far apart along its edge can meet under the
			// erosion, the arms of a flash whose notches nearly touch, say. The
			// corners alone cannot tell: a concave corner's mitre overshoots
			// where its widened fillet would not, so that is judged on the
			// outline built from these, fillets and all.

			var result = new InsertCorner[active.Count];
			for (var j = 0; j < active.Count; j++) {
				var previous = active[(j - 1 + active.Count) % active.Count];
				var current = active[j];
				if ((previous + 1) % count == current) {
					// The corner between two sides that were neighbours all along.
					var corner = corners[current];
					var radius = corner.Concave ? corner.Radius + distance : math.max(corner.Radius - distance, minConvexRadius);
					// A wider arc takes more segments, but not in proportion: a
					// segment's chord may grow with the radius before its sag does.
					var segments = corner.Segments;
					if (corner.Concave && corner.Radius > Epsilon) {
						segments = math.min(64, math.max(segments, (int)math.ceil(segments * math.sqrt(radius / corner.Radius))));
					}
					result[j] = new InsertCorner(vertices[j], radius, math.max(1, segments), corner.Tag);
				} else {
					// Two sides that met across the sides between them: those and
					// their corners are gone, and what is left is a point.
					var tags = new List<string>();
					for (var eaten = (previous + 1) % count; eaten != (current + 1) % count; eaten = (eaten + 1) % count) {
						tags.Add(corners[eaten].Tag ?? eaten.ToString());
					}
					result[j] = new InsertCorner(vertices[j], minConvexRadius, 2, string.Join("+", tags));
				}
			}
			return result;
		}

		/// <summary>Where two lines meet, or false when they run parallel.</summary>
		private static bool Intersect(float2 originA, float2 directionA, float2 originB, float2 directionB, out float2 point)
		{
			var determinant = directionA.x * directionB.y - directionA.y * directionB.x;
			point = originA;
			if (math.abs(determinant) < 1e-9f) {
				return false;
			}
			var offset = originB - originA;
			var t = (offset.x * directionB.y - offset.y * directionB.x) / determinant;
			point = originA + t * directionA;
			return true;
		}
	}
}
