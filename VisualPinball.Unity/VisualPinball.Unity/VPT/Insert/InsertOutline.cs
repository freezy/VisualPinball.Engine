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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Where a point of an insert outline came from.
	/// </summary>
	public enum InsertOutlinePointKind
	{
		/// <summary>A corner with no fillet: a single point carrying a mitred normal.</summary>
		Sharp,

		/// <summary>A sample on a corner fillet.</summary>
		Arc,

		/// <summary>A subdivision along a straight run, between two fillets.</summary>
		Side,
	}

	/// <summary>
	/// Thrown when parameters cannot describe a valid outline at all.
	/// </summary>
	public class InsertOutlineException : Exception
	{
		public InsertOutlineException(string message) : base(message) { }
	}

	/// <summary>
	/// One corner of the sharp base polygon, before filleting.
	/// </summary>
	public readonly struct InsertCorner
	{
		public readonly float2 Position;
		public readonly float Radius;
		public readonly int Segments;

		/// <summary>
		/// Free-form label, so later build steps can find a specific corner
		/// (the apex of a triangle, say) without relying on its index.
		/// </summary>
		/// <remarks>
		/// Named in the shape's own 2D space, which the mesh mapping turns half a
		/// revolution on its way into the playfield. A tag saying "left" is
		/// therefore on the table's right.
		/// </remarks>
		public readonly string Tag;

		/// <summary>
		/// Whether the edge leaving this corner is a chord of a sampled curve
		/// rather than a side: side segments leave it alone, since cutting a
		/// chord into slivers only limits how far the outline can be inset.
		/// </summary>
		public readonly bool Chord;

		public InsertCorner(float2 position, float radius, int segments, string tag = null, bool chord = false)
		{
			Position = position;
			Radius = radius;
			Segments = segments;
			Tag = tag;
			Chord = chord;
		}

		public InsertCorner WithRadius(float radius) => new(Position, radius, Segments, Tag, Chord);
	}

	/// <summary>
	/// A sampled point of the closed outline, in counter-clockwise order.
	/// </summary>
	public struct InsertOutlinePoint
	{
		public float2 Position;

		/// <summary>Outward unit normal of the shape at this point.</summary>
		public float2 Normal;

		public InsertOutlinePointKind Kind;

		/// <summary>Index of the corner this point belongs to, or -1 on a straight run.</summary>
		public int Corner;

		/// <summary>Index of the straight run this point belongs to, or -1 on a fillet.</summary>
		public int Side;

		/// <summary>Position across the fillet or straight run, 0..1.</summary>
		public float T;

		/// <summary>Distance travelled along the perimeter, for the rim UVs in step 5.</summary>
		public float Distance;

		/// <summary>Centre of the fillet this point sits on. Meaningless unless <see cref="Kind"/> is Arc.</summary>
		public float2 Center;

		/// <summary>Radius of the fillet this point sits on, zero on sharp corners and straight runs.</summary>
		public float Radius;

		/// <summary>Whether the fillet is carved out of the shape rather than rounding it off.</summary>
		public bool Concave;

		/// <summary>
		/// Scale applied to the normal when offsetting. One everywhere except on
		/// sharp corners, where the mitre has to reach further than the sides do.
		/// </summary>
		public float Miter;

		/// <summary>
		/// The point <paramref name="distance"/> inwards along the parallel curve.
		/// Used by the border channel in step 2.
		/// </summary>
		public float2 Offset(float distance) => Position - distance * Miter * Normal;

		/// <summary>
		/// Fillet radius after offsetting inwards by <paramref name="distance"/>.
		/// Convex fillets tighten and eventually turn themselves inside out, which
		/// shows up as a negative radius; concave ones open up.
		/// </summary>
		public float OffsetRadius(float distance)
		{
			if (Kind != InsertOutlinePointKind.Arc) {
				return 0f;
			}
			return Concave ? Radius + distance : Radius - distance;
		}
	}

	/// <summary>
	/// A corner measured in context: angles, fillet placement and arc sweep.
	/// </summary>
	public class InsertResolvedCorner
	{
		public int Index;
		public float2 Position;
		public float Radius;
		public int Segments;
		public string Tag;
		public bool Chord;

		/// <summary>Unit direction of the side arriving at this corner.</summary>
		public float2 Incoming;

		/// <summary>Unit direction of the side leaving this corner.</summary>
		public float2 Outgoing;

		public float2 NormalIn;
		public float2 NormalOut;

		/// <summary>Interior angle of the CCW loop. Reflex corners land above pi.</summary>
		public float Interior;

		/// <summary>Half the angle the fillet actually sits in.</summary>
		public float Half;

		/// <summary>How far along each side the fillet's tangent point sits.</summary>
		public float Tangent;

		public bool Concave;
		public float2 Center;
		public float ArcStart;
		public float Sweep;
	}

	/// <summary>
	/// A closed, filleted, tagged 2D profile centred on its bounding box.
	/// </summary>
	public class InsertOutline
	{
		public readonly List<InsertOutlinePoint> Points;
		public readonly List<InsertResolvedCorner> Corners;
		public readonly float Perimeter;
		public readonly float Width;
		public readonly float Height;
		public readonly float2 Centroid;
		public readonly List<string> Warnings;

		public InsertOutline(List<InsertOutlinePoint> points, List<InsertResolvedCorner> corners,
			float perimeter, float width, float height, float2 centroid, List<string> warnings)
		{
			Points = points;
			Corners = corners;
			Perimeter = perimeter;
			Width = width;
			Height = height;
			Centroid = centroid;
			Warnings = warnings;
		}

		/// <summary>
		/// Point indices belonging to corner <paramref name="index"/>.
		/// </summary>
		public List<int> CornerSpan(int index)
		{
			var span = new List<int>();
			for (var i = 0; i < Points.Count; i++) {
				if (Points[i].Corner == index) {
					span.Add(i);
				}
			}
			return span;
		}

		/// <summary>
		/// Point <paramref name="index"/> walked inwards by <paramref name="distance"/>,
		/// as a true erosion of the shape.
		/// </summary>
		/// <remarks>
		/// A convex fillet narrower than the distance cannot simply shrink, because
		/// a negative radius would turn it inside out. Eroding the shape instead
		/// squares that corner off: the two sides, each moved in by the distance,
		/// now meet at a mitre, and the whole fillet collapses onto that one point.
		/// The formula is continuous, since at a radius of exactly the distance the
		/// mitre and the fillet centre are the same place.
		/// <para>
		/// Several points therefore share a position once fillets start collapsing,
		/// and a caller building geometry is expected to weld them.
		/// </para>
		/// </remarks>
		public float2 OffsetPoint(int index, float distance)
		{
			var point = Points[index];
			if (point.Kind == InsertOutlinePointKind.Arc && !point.Concave && point.Radius <= distance) {
				var corner = Corners[point.Corner];
				var bisector = math.normalize(corner.NormalIn + corner.NormalOut);
				return corner.Position - distance / math.sin(corner.Half) * bisector;
			}
			return point.Offset(distance);
		}

		/// <summary>
		/// <paramref name="distance"/> reduced until the inward offset stops folding
		/// the outline through itself.
		/// </summary>
		/// <remarks>
		/// Shrinking an inset only ever makes it easier to fit, so a bisection is
		/// safe, and it lets a slider be dragged past what the shape can take
		/// without the geometry coming apart on the way.
		/// </remarks>
		public float ClampInset(float distance)
		{
			if (distance <= 0f) {
				return 0f;
			}
			if (IsInsetValid(distance)) {
				return distance;
			}

			// Bracket against the shape, not against what was asked for. Halving an
			// arbitrarily large request a fixed number of times need never reach the
			// feasible range at all, and the search would report that nothing fits.
			// No convex shape can be inset by more than half its smaller side.
			var low = 0f;
			var high = math.min(distance, math.min(Width, Height) * 0.5f);
			for (var i = 0; i < 24; i++) {
				var mid = (low + high) * 0.5f;
				if (IsInsetValid(mid)) {
					low = mid;
				} else {
					high = mid;
				}
			}
			return low;
		}

		/// <summary>
		/// How close two points have to be before geometry built from this outline
		/// treats them as one.
		/// </summary>
		public float WeldTolerance =>
			math.max(InsertOutlineBuilder.Epsilon, math.max(Width, Height) * 1e-6f);

		private bool IsInsetValid(float distance)
		{
			var min = new float2(float.MaxValue);
			var max = new float2(float.MinValue);
			var area = 0f;
			var count = Points.Count;
			var offset = new float2[count];
			for (var i = 0; i < count; i++) {
				offset[i] = OffsetPoint(i, distance);
			}

			for (var i = 0; i < count; i++) {
				var next = (i + 1) % count;
				var a = offset[i];
				var b = offset[next];

				var moved = b - a;
				if (math.lengthsq(moved) > InsertOutlineBuilder.Epsilon * InsertOutlineBuilder.Epsilon) {
					// A segment that now runs against its original direction means
					// the ring has folded back through itself.
					if (math.dot(moved, Points[next].Position - Points[i].Position) <= 0f) {
						return false;
					}
				}

				min = math.min(min, a);
				max = math.max(max, a);
				area += a.x * b.y - b.x * a.y;
			}

			// Right at the limit the ring survives these tests as a sliver thinner
			// than the weld, which then collapses it into a lopsided scrap of a
			// shape. Stop while there is still something left to weld.
			// The extent is what rules out a sliver; the area only has to say the
			// ring has not turned inside out.
			var extent = math.min(max.x - min.x, max.y - min.y);
			if (area <= 0f || extent <= WeldTolerance * 8f) {
				return false;
			}

			// A ring can also cross itself with every edge still running its way,
			// where two parts of it far apart along the ring meet: a notch closing
			// onto the side across from it, say. Only edges that are not
			// neighbours can cross; neighbours meet at their shared point.
			for (var i = 0; i < count; i++) {
				var a = offset[i];
				var b = offset[(i + 1) % count];
				for (var j = i + 2; j < count; j++) {
					if (i == 0 && j == count - 1) {
						continue;
					}
					if (Crosses(a, b, offset[j], offset[(j + 1) % count])) {
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>Whether two segments cross properly, neither touching the other at a point.</summary>
		private static bool Crosses(float2 a, float2 b, float2 c, float2 d)
		{
			static double Side(double2 o, double2 p, double2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
			return Side(a, b, c) * Side(a, b, d) < 0.0 && Side(c, d, a) * Side(c, d, b) < 0.0;
		}

		/// <summary>
		/// Whether straight run <paramref name="index"/> has any length left, or
		/// whether the fillets either side of it have eaten it completely.
		/// </summary>
		/// <remarks>
		/// False for every side of a fully rounded shape such as a circle, where
		/// the outline is all fillet and the sides are just the seams between them.
		/// </remarks>
		public bool HasStraightRun(int index)
		{
			var corner = Corners[index];
			var following = Corners[(index + 1) % Corners.Count];
			return math.distance(corner.Position, following.Position) - corner.Tangent - following.Tangent
				> InsertOutlineBuilder.Epsilon;
		}

		/// <summary>
		/// Point indices covering straight run <paramref name="index"/>, tangent points included.
		/// </summary>
		/// <remarks>
		/// Side <paramref name="index"/> runs from corner <paramref name="index"/> to
		/// the next one, and normally carries at least the two tangent points that
		/// cap it. It comes back empty when fillets have grown until their samples
		/// coincide and were welded away, leaving a corner no points of its own: a
		/// square whose radius reaches half its width, cut at one segment a corner,
		/// is a diamond of four points shared between four corners.
		/// </remarks>
		public List<int> SideSpan(int index)
		{
			var from = CornerSpan(index);
			var to = CornerSpan((index + 1) % Corners.Count);
			if (from.Count == 0 || to.Count == 0) {
				return new List<int>();
			}

			var start = from[^1];
			var end = to[0];
			var span = new List<int> { start };
			var cursor = start;
			while (cursor != end) {
				cursor = (cursor + 1) % Points.Count;
				span.Add(cursor);
			}
			return span;
		}
	}

	/// <summary>
	/// Turns a sharp counter-clockwise polygon into the sampled, filleted, tagged
	/// outline that every later insert build step consumes.
	/// </summary>
	/// <remarks>
	/// The outline is centred on its <i>analytic</i> bounding box, so a shape's
	/// width and height describe the true fillet arcs no matter how coarsely the
	/// arcs are sampled. Raising the segment count refines the silhouette without
	/// resizing it.
	/// </remarks>
	public static class InsertOutlineBuilder
	{
		/// <summary>
		/// A hundredth of a VPX unit's thousandth, so around five nanometres of real
		/// size: far below anything that matters geometrically, comfortably above
		/// float noise on parts that are tens of VPX units across.
		/// </summary>
		public const float Epsilon = 1e-5f;

		/// <summary>
		/// The tolerance for quantities with no length: angles, and differences
		/// between unit vectors. <see cref="Epsilon"/> is a length and would mean
		/// something different at every scale.
		/// </summary>
		public const float AngleEpsilon = 1e-5f;

		private const float Tau = 2f * math.PI;

		public static InsertOutline Build(IReadOnlyList<InsertCorner> corners, bool center = true)
		{
			if (corners.Count < 3) {
				throw new InsertOutlineException("An outline needs at least three corners.");
			}

			var warnings = new List<string>();
			var scales = ClampRadii(corners);
			var worst = 1f;
			foreach (var scale in scales) {
				worst = math.min(worst, scale);
			}

			// Always apply the correction, however small. A clamp of a fraction of a
			// percent still answers a real overlap, and skipping it leaves the two
			// tangent points crossed over by far more than the weld will absorb.
			// Only the warning has a threshold, so float noise stays quiet.
			if (worst < 1f) {
				var scaled = new InsertCorner[corners.Count];
				for (var i = 0; i < corners.Count; i++) {
					scaled[i] = corners[i].WithRadius(corners[i].Radius * scales[i]);
				}
				corners = scaled;
			}
			if (worst < 1f - 1e-4f) {
				warnings.Add($"Corner radius reduced to {worst * 100f:0}% to fit the sides.");
			}

			var resolved = Resolve(corners);
			AnalyticBounds(resolved, out var min, out var max);
			if (center) {
				var shift = -(min + max) * 0.5f;
				foreach (var corner in resolved) {
					corner.Position += shift;
					corner.Center += shift;
				}
			}

			var points = new List<InsertOutlinePoint>();
			foreach (var corner in resolved) {
				if (corner.Radius <= Epsilon) {
					// Both edge lines move in by the offset and meet on the bisector,
					// a corner's width behind it. That holds whichever way the corner
					// turns: at a reflex corner the bisector still points out of the
					// material, into the notch, so the mitre still steps back along
					// it and the scale stays positive.
					points.Add(new InsertOutlinePoint {
						Position = corner.Position,
						Normal = math.normalize(corner.NormalIn + corner.NormalOut),
						Kind = InsertOutlinePointKind.Sharp,
						Corner = corner.Index,
						Side = -1,
						T = 0f,
						Miter = 1f / math.sin(corner.Half),
					});

				} else {
					for (var step = 0; step <= corner.Segments; step++) {
						var t = (float)step / corner.Segments;
						ArcPoint(corner, t, out var position, out var normal);
						points.Add(new InsertOutlinePoint {
							Position = position,
							Normal = normal,
							Kind = InsertOutlinePointKind.Arc,
							Corner = corner.Index,
							Side = -1,
							T = t,
							Center = corner.Center,
							Radius = corner.Radius,
							Concave = corner.Concave,
							Miter = 1f,
						});
					}
				}
			}

			// When fillets eat a side whole -- a circle is just a square whose
			// corner radius reached half its width -- the tangent points either
			// end of that side land on top of each other. Dropping the duplicate
			// keeps degenerate triangles out of the cap fan and, later, degenerate
			// quads out of the border channel. The tolerance scales with the shape
			// because these points are reached from two different fillet centres,
			// so they only agree to single precision.
			var merged = math.max(Epsilon, math.cmax(max - min) * 1e-6f);
			var deduped = new List<InsertOutlinePoint>(points.Count);
			foreach (var point in points) {
				if (deduped.Count == 0 || math.distance(point.Position, deduped[^1].Position) >= merged) {
					deduped.Add(point);
				}
			}
			if (deduped.Count > 3 && math.distance(deduped[^1].Position, deduped[0].Position) < merged) {
				deduped.RemoveAt(deduped.Count - 1);
			}
			if (deduped.Count < 3) {
				throw new InsertOutlineException("These parameters collapse the outline to nothing.");
			}
			points = deduped;

			var perimeter = 0f;
			for (var i = 0; i < points.Count; i++) {
				var point = points[i];
				point.Distance = perimeter;
				points[i] = point;
				perimeter += math.distance(point.Position, points[(i + 1) % points.Count].Position);
			}

			var size = max - min;
			return new InsertOutline(points, resolved, perimeter, size.x, size.y, Centroid(points), warnings);
		}

		/// <summary>
		/// Samples a resolved corner's fillet at <paramref name="t"/> in 0..1.
		/// </summary>
		public static void ArcPoint(InsertResolvedCorner corner, float t, out float2 position, out float2 normal)
		{
			var angle = corner.ArcStart + corner.Sweep * t;
			var direction = new float2(math.cos(angle), math.sin(angle));
			position = corner.Center + corner.Radius * direction;

			// The outward normal points away from the centre on a convex fillet and
			// back towards it on a concave one, which is exactly +/- the direction.
			normal = corner.Concave ? -direction : direction;
		}

		/// <summary>
		/// Per-corner scales that stop neighbouring tangent points from overlapping.
		/// </summary>
		/// <remarks>
		/// Tangent offsets scale linearly with their radius, so shrinking both ends
		/// of an overlong side by its overshoot ratio fixes that side exactly.
		/// <para>
		/// A corner takes the smaller of the two ratios its sides ask for rather
		/// than both in turn. Applying them one after the other would compound: on a
		/// square whose radius overruns every side by the same amount, each corner
		/// would be reduced twice and end up far tighter than either side needed.
		/// A few passes then settle the corners whose neighbours also moved.
		/// </para>
		/// </remarks>
		public static float[] ClampRadii(IReadOnlyList<InsertCorner> corners)
		{
			var count = corners.Count;
			var scales = new float[count];
			var ratios = new float[count];
			for (var i = 0; i < count; i++) {
				scales[i] = 1f;
			}

			var scaled = new InsertCorner[count];
			for (var pass = 0; pass < 16; pass++) {
				for (var i = 0; i < count; i++) {
					scaled[i] = corners[i].WithRadius(corners[i].Radius * scales[i]);
					ratios[i] = 1f;
				}

				var resolved = Resolve(scaled);
				var worst = 1f;
				for (var i = 0; i < count; i++) {
					var following = resolved[(i + 1) % count];
					var length = math.distance(resolved[i].Position, following.Position);
					var needed = resolved[i].Tangent + following.Tangent;
					if (needed > length + Epsilon) {
						ratios[i] = length / needed;
						worst = math.min(worst, ratios[i]);
					}
				}

				for (var i = 0; i < count; i++) {
					scales[i] *= math.min(ratios[i], ratios[(i - 1 + count) % count]);
				}

				if (worst >= 1f - 1e-6f) {
					break;
				}
			}
			return scales;
		}

		/// <summary>
		/// Bounding box of the ideal shape, including the peak of every fillet.
		/// </summary>
		public static void AnalyticBounds(IReadOnlyList<InsertResolvedCorner> resolved, out float2 min, out float2 max)
		{
			min = new float2(float.MaxValue);
			max = new float2(float.MinValue);
			foreach (var corner in resolved) {
				if (corner.Radius <= Epsilon) {
					min = math.min(min, corner.Position);
					max = math.max(max, corner.Position);
					continue;
				}

				ArcPoint(corner, 0f, out var entry, out _);
				ArcPoint(corner, 1f, out var exit, out _);
				min = math.min(min, math.min(entry, exit));
				max = math.max(max, math.max(entry, exit));

				for (var axis = 0; axis < 4; axis++) {
					// Does the fillet actually sweep past this axis direction?
					var angle = axis * math.PI * 0.5f;
					var delta = corner.Sweep < 0f
						? Wrap(corner.ArcStart - angle)
						: Wrap(angle - corner.ArcStart);
					if (delta <= math.abs(corner.Sweep) + AngleEpsilon) {
						var extreme = corner.Center + corner.Radius * new float2(math.cos(angle), math.sin(angle));
						min = math.min(min, extreme);
						max = math.max(max, extreme);
					}
				}
			}
		}

		/// <summary>
		/// Measures every corner: interior angle, tangent offset and fillet placement.
		/// </summary>
		public static List<InsertResolvedCorner> Resolve(IReadOnlyList<InsertCorner> corners)
		{
			var resolved = new List<InsertResolvedCorner>(corners.Count);
			for (var index = 0; index < corners.Count; index++) {
				var corner = corners[index];
				var previous = corners[(index - 1 + corners.Count) % corners.Count].Position;
				var following = corners[(index + 1) % corners.Count].Position;
				var incoming = Unit(corner.Position - previous);
				var outgoing = Unit(following - corner.Position);

				var cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
				var dot = math.dot(incoming, outgoing);

				// Interior angle of a CCW loop. atan2 returns the reflex case negative.
				var interior = math.atan2(cross, -dot);
				var concave = interior < 0f;
				if (concave) {
					interior += Tau;
				}

				// Half the angle the fillet actually sits in. A reflex corner is
				// filleted from the other side, so it uses the complement.
				var half = concave ? (Tau - interior) * 0.5f : interior * 0.5f;
				if (half <= AngleEpsilon || half >= math.PI - AngleEpsilon) {
					throw new InsertOutlineException($"Corner {index} is degenerate: its two sides are collinear.");
				}

				var radius = math.max(corner.Radius, 0f);
				resolved.Add(new InsertResolvedCorner {
					Index = index,
					Position = corner.Position,
					Radius = radius,
					Segments = math.max(corner.Segments, 1),
					Tag = corner.Tag,
					Chord = corner.Chord,
					Incoming = incoming,
					Outgoing = outgoing,
					NormalIn = OutwardNormal(incoming),
					NormalOut = OutwardNormal(outgoing),
					Interior = interior,
					Half = half,
					Tangent = radius / math.tan(half),
					Concave = concave,
				});
			}

			PlaceFillets(resolved);
			return resolved;
		}

		/// <summary>
		/// Derives each fillet's centre and swept angle from its tangent offset.
		/// </summary>
		private static void PlaceFillets(List<InsertResolvedCorner> resolved)
		{
			foreach (var corner in resolved) {
				if (corner.Radius <= Epsilon) {
					corner.Center = corner.Position;
					corner.ArcStart = 0f;
					corner.Sweep = 0f;
					continue;
				}

				var tangentIn = corner.Position - corner.Tangent * corner.Incoming;

				// Convex fillets curve away from the material and sit inside it;
				// concave ones are carved out, so their centre lies outside.
				var sign = corner.Concave ? 1f : -1f;
				corner.Center = tangentIn + sign * corner.Radius * corner.NormalIn;

				var start = math.atan2(corner.NormalIn.y, corner.NormalIn.x);
				if (corner.Concave) {
					corner.ArcStart = start + math.PI;
					corner.Sweep = -(corner.Interior - math.PI);
				} else {
					corner.ArcStart = start;
					corner.Sweep = math.PI - corner.Interior;
				}
			}
		}

		/// <summary>
		/// Where a corner's outline contribution starts, fillet or not.
		/// </summary>
		private static float2 CornerEntry(InsertResolvedCorner corner)
		{
			if (corner.Radius <= Epsilon) {
				return corner.Position;
			}
			ArcPoint(corner, 0f, out var entry, out _);
			return entry;
		}

		/// <summary>
		/// Outward normal of an edge running along <paramref name="direction"/> in a CCW loop.
		/// </summary>
		private static float2 OutwardNormal(float2 direction) => new(direction.y, -direction.x);

		private static float2 Unit(float2 vector)
		{
			var length = math.length(vector);
			if (length < Epsilon) {
				throw new InsertOutlineException("Two corners of the outline share a position.");
			}
			return vector / length;
		}

		private static float Wrap(float angle)
		{
			var wrapped = angle % Tau;
			return wrapped < 0f ? wrapped + Tau : wrapped;
		}

		private static float2 Centroid(IReadOnlyList<InsertOutlinePoint> points)
		{
			var area = 0f;
			var reach = 0f;
			var accumulated = float2.zero;
			for (var i = 0; i < points.Count; i++) {
				var current = points[i].Position;
				var next = points[(i + 1) % points.Count].Position;
				var cross = current.x * next.y - next.x * current.y;
				area += cross;
				accumulated += (current + next) * cross;
				reach = math.max(reach, math.cmax(math.abs(current)));
			}

			// An area, so it is judged against the size squared.
			return math.abs(area) <= 1e-9f * reach * reach ? float2.zero : accumulated / (3f * area);
		}
	}
}
