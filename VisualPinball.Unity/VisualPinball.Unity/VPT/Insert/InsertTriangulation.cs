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

using System.Collections.Generic;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Cuts the faces of an <see cref="InsertArrangement"/> into triangles.
	/// </summary>
	/// <remarks>
	/// The faces are simple polygons at best and weakly simple at worst: a face
	/// walked along both sides of a bridge, or pinched where two holes touch,
	/// visits the same point twice. They are rarely convex once holes cut them,
	/// and they are full of collinear runs, wherever spokes and circles land on a
	/// straight side. Ear clipping takes all of that, given one rule: a corner is
	/// an ear only if it is strictly convex and no other point lies inside its
	/// triangle <em>or on it</em>. A point on the ear's closing edge would
	/// otherwise be stepped over, and left as a crack.
	/// </remarks>
	public static class InsertTriangulation
	{
		/// <summary>
		/// Triangles covering a counter-clockwise polygon, as index triples into
		/// it, each counter-clockwise too.
		/// </summary>
		/// <remarks>
		/// Meant for exact positions, such as an arrangement's vertices, whose
		/// collinear runs really are collinear. The only tolerance is for the last
		/// bits of double arithmetic, and a face whose sides the arrangement bent by
		/// up to half a pixel is simply a face that is not quite convex.
		/// <para>
		/// Ears are tried fattest first, and the first that holds is clipped, which
		/// keeps slivers out for as long as there is any choice and usually finds
		/// its ear at the first try. A simple polygon always has an ear, but should
		/// the rule ever find none, it relaxes: first to any convex corner with
		/// nothing strictly inside, then to the fattest corner that is at least not
		/// reflex. A thin triangle is a blemish; a face left open is a hole.
		/// </para>
		/// <para>
		/// Clipping greedily can still leave a sliver for last that a different
		/// diagonal would have avoided, so the result is flipped towards Delaunay
		/// afterwards, which turns the smallest angle up as far as the face allows
		/// without touching its outline.
		/// </para>
		/// </remarks>
		public static List<int3> EarClip(IReadOnlyList<double2> polygon)
		{
			var triangles = new List<int3>(math.max(0, polygon.Count - 2));
			if (polygon.Count < 3) {
				return triangles;
			}

			// About the first point, as with the face areas.
			var origin = polygon[0];
			var points = new double2[polygon.Count];
			var reach = 0.0;
			for (var i = 0; i < polygon.Count; i++) {
				points[i] = polygon[i] - origin;
				reach = math.max(reach, math.cmax(math.abs(points[i])));
			}

			// Twice an area, below which three points count as in line: rounding,
			// and no more. Three pixel centres not in line span at least a pixel
			// squared, which is many orders above it.
			var tolerance = reach * reach * 1e-14;

			var remaining = new List<int>(polygon.Count);
			for (var i = 0; i < polygon.Count; i++) {
				remaining.Add(i);
			}

			var candidates = new List<(double Quality, int Corner)>();
			while (remaining.Count > 3) {
				var clip = FattestEar(points, remaining, candidates, tolerance, tolerance);
				if (clip < 0) {
					clip = FattestEar(points, remaining, candidates, 0.0, 0.0);
				}
				if (clip < 0) {
					clip = FattestEar(points, remaining, candidates, double.NegativeInfinity, double.NaN);
				}
				if (clip < 0) {
					// Nothing but reflex corners, which a counter-clockwise polygon
					// cannot have. Whatever this is, it has no area left to cover.
					break;
				}

				triangles.Add(new int3(
					remaining[(clip - 1 + remaining.Count) % remaining.Count],
					remaining[clip],
					remaining[(clip + 1) % remaining.Count]));
				remaining.RemoveAt(clip);
			}

			if (remaining.Count == 3 && Cross(points[remaining[0]], points[remaining[1]], points[remaining[2]]) > 0.0) {
				triangles.Add(new int3(remaining[0], remaining[1], remaining[2]));
			}

			Flip(points, triangles, tolerance);
			return triangles;
		}

		/// <summary>
		/// The corner with the fattest ear more than <paramref name="convex"/>
		/// convex and with nothing inside, to within <paramref name="inside"/>, or
		/// -1.
		/// </summary>
		/// <param name="convex">Negative infinity for any corner not reflex.</param>
		/// <param name="inside">NaN to skip looking inside at all.</param>
		private static int FattestEar(double2[] points, List<int> remaining, List<(double, int)> candidates,
			double convex, double inside)
		{
			candidates.Clear();
			for (var k = 0; k < remaining.Count; k++) {
				var a = points[remaining[(k - 1 + remaining.Count) % remaining.Count]];
				var b = points[remaining[k]];
				var c = points[remaining[(k + 1) % remaining.Count]];
				var cross = Cross(a, b, c);
				if (cross < 0.0 || !double.IsNegativeInfinity(convex) && !(cross > convex)) {
					continue;
				}
				var span = math.max(math.distancesq(a, b), math.max(math.distancesq(b, c), math.distancesq(c, a)));
				if (span > 0.0) {
					candidates.Add((cross / span, k));
				}
			}
			candidates.Sort((x, y) => y.Item1.CompareTo(x.Item1));

			foreach (var (_, k) in candidates) {
				if (double.IsNaN(inside)) {
					return k;
				}
				var a = points[remaining[(k - 1 + remaining.Count) % remaining.Count]];
				var b = points[remaining[k]];
				var c = points[remaining[(k + 1) % remaining.Count]];
				if (IsEmpty(points, remaining, k, a, b, c, inside)) {
					return k;
				}
			}
			return -1;
		}

		/// <summary>
		/// Whether no remaining point other than the ear's own lies inside the ear
		/// at <paramref name="k"/>, or on its outline too unless
		/// <paramref name="tolerance"/> is zero.
		/// </summary>
		private static bool IsEmpty(double2[] points, List<int> remaining, int k, double2 a, double2 b, double2 c,
			double tolerance)
		{
			var count = remaining.Count;
			for (var j = 0; j < count; j++) {
				var offset = (j - k + count) % count;
				if (offset == 0 || offset == 1 || offset == count - 1) {
					continue;
				}
				var p = points[remaining[j]];

				// A point visited twice, where a bridge is walked both ways or two
				// holes pinch the face, sits on one of the ear's own corners, and is
				// no reason to refuse it.
				if (p.Equals(a) || p.Equals(b) || p.Equals(c)) {
					continue;
				}
				if (tolerance > 0.0
					? Cross(a, b, p) >= -tolerance && Cross(b, c, p) >= -tolerance && Cross(c, a, p) >= -tolerance
					: Cross(a, b, p) > 0.0 && Cross(b, c, p) > 0.0 && Cross(c, a, p) > 0.0) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Lawson's flips: any diagonal whose far point lies inside the circle
		/// through the triangle on its near side is swapped for the other diagonal
		/// of the pair, where the pair is strictly convex, until none is left.
		/// </summary>
		/// <remarks>
		/// Only diagonals shared by two triangles are ever flipped, so the
		/// polygon's own edges stay, and so does a bridge walked both ways, whose
		/// two sides are different corners of the polygon.
		/// </remarks>
		private static void Flip(double2[] points, List<int3> triangles, double tolerance)
		{
			var owner = new Dictionary<(int, int), int>(triangles.Count * 3);
			var pending = new Stack<(int, int)>();
			for (var t = 0; t < triangles.Count; t++) {
				Own(owner, triangles[t], t);
				pending.Push((triangles[t].x, triangles[t].y));
				pending.Push((triangles[t].y, triangles[t].z));
				pending.Push((triangles[t].z, triangles[t].x));
			}

			// Each flip strictly improves the triangulation, so this ends, but it
			// is bounded anyway against rounding going round in a circle.
			var budget = 16 * triangles.Count + 16;
			while (pending.Count > 0 && budget-- > 0) {
				var (u, v) = pending.Pop();
				if (!owner.TryGetValue((u, v), out var near) || !owner.TryGetValue((v, u), out var far)) {
					continue;
				}
				var w = Third(triangles[near], u, v);
				var x = Third(triangles[far], v, u);

				// The pair must stay strictly convex, or one of the new triangles
				// would turn over or flatten.
				if (Cross(points[u], points[x], points[w]) <= tolerance
					|| Cross(points[x], points[v], points[w]) <= tolerance) {
					continue;
				}
				if (!InCircle(points[u], points[v], points[w], points[x])) {
					continue;
				}

				Disown(owner, triangles[near]);
				Disown(owner, triangles[far]);
				triangles[near] = new int3(u, x, w);
				triangles[far] = new int3(x, v, w);
				Own(owner, triangles[near], near);
				Own(owner, triangles[far], far);

				pending.Push((u, x));
				pending.Push((x, v));
				pending.Push((v, w));
				pending.Push((w, u));
			}
		}

		private static void Own(Dictionary<(int, int), int> owner, int3 triangle, int index)
		{
			owner[(triangle.x, triangle.y)] = index;
			owner[(triangle.y, triangle.z)] = index;
			owner[(triangle.z, triangle.x)] = index;
		}

		private static void Disown(Dictionary<(int, int), int> owner, int3 triangle)
		{
			owner.Remove((triangle.x, triangle.y));
			owner.Remove((triangle.y, triangle.z));
			owner.Remove((triangle.z, triangle.x));
		}

		private static int Third(int3 triangle, int u, int v)
			=> triangle.x != u && triangle.x != v ? triangle.x : triangle.y != u && triangle.y != v ? triangle.y : triangle.z;

		/// <summary>
		/// Whether <paramref name="d"/> lies clearly inside the circle through the
		/// counter-clockwise triangle <paramref name="a"/>, <paramref name="b"/>,
		/// <paramref name="c"/>.
		/// </summary>
		private static bool InCircle(double2 a, double2 b, double2 c, double2 d)
		{
			var ad = a - d;
			var bd = b - d;
			var cd = c - d;
			var al = math.lengthsq(ad);
			var bl = math.lengthsq(bd);
			var cl = math.lengthsq(cd);
			var determinant = al * (bd.x * cd.y - cd.x * bd.y)
				- bl * (ad.x * cd.y - cd.x * ad.y)
				+ cl * (ad.x * bd.y - bd.x * ad.y);

			// Four points on one circle go either way; only a clear gain flips.
			var scale = math.max(al, math.max(bl, cl));
			return determinant > 1e-10 * scale * scale;
		}

		/// <summary>Twice the signed area of the triangle, positive counter-clockwise.</summary>
		private static double Cross(double2 o, double2 a, double2 b)
			=> (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
	}
}
