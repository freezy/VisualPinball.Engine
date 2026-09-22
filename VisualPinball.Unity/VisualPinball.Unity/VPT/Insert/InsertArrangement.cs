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
	/// A set of segments in the plane, cut into a planar subdivision.
	/// </summary>
	/// <remarks>
	/// Everything that divides the insert's raised floor goes in here: its outline,
	/// the starburst's spokes and, later, the holes. Every place two segments meet
	/// is worked out once and the resulting point is shared by index, so each part
	/// of the mesh built from the result reaches it the same way. That is the
	/// whole reason for this class. Clipping each piece of the surface separately
	/// left neighbouring pieces to compute their common points for themselves, and
	/// they disagreed in the last bits often enough to tear the mesh.
	/// <para>
	/// The subdivision is found by snap rounding, as Hobby and Guibas and Marimont
	/// describe it. The plane is divided into square pixels the size of the weld.
	/// Every pixel holding an end of a segment, or a point where two segments
	/// cross, is hot, and every segment is replaced by the path through the
	/// centre of each hot pixel it passes through, in the order it passes through
	/// them. What makes this worth doing is that it is proven: two such paths never
	/// cross. They meet at pixel centres or run together between them, and nothing
	/// else. Anything short of the full rule, such as only taking a point onto a
	/// segment when it is the end of another segment that does not share an end
	/// with it, leaves a case where rerouting one segment through a point carries
	/// it across a second one that passed just as close to that point but was not
	/// rerouted, and the face walk goes wrong on the crossing.
	/// </para>
	/// <para>
	/// The price is that a sliver narrower than a pixel does not survive. Where two
	/// segments run within a pixel of each other, as the sides of an acute tip do
	/// near it, they pass through the same hot pixels and share the edges between
	/// them, so what was a sliver is now a single edge, which a walk around the
	/// result steps out along and straight back. Every walk this class hands out
	/// has had those steps taken out by <see cref="Unspike"/>, and a caller
	/// walking the segments for itself runs its walk through the same. Points
	/// also move, by up to half a pixel's diagonal, to their pixel's centre.
	/// </para>
	/// <para>
	/// Positions are kept in double throughout. A path may pass extremely close
	/// to a pixel centre it does not go through, and rounding the centres to float
	/// before walking them could put one on the wrong side.
	/// </para>
	/// </remarks>
	public sealed class InsertArrangement
	{
		private readonly double _pixel;
		private readonly double2 _origin;
		private readonly List<float2> _points = new();
		private readonly List<Segment> _segments = new();

		private List<double2> _vertices;
		private List<(float T, int Vertex)>[] _paths;
		private Dictionary<(int, int), List<int>> _edges;

		private struct Segment
		{
			public int From;
			public int To;
		}

		/// <param name="weld">The pixel size, which is the distance below which
		/// two things are taken to meet.</param>
		public InsertArrangement(float weld)
		{
			_pixel = weld;

			// The grid is offset by irrational fractions of a pixel, so that the
			// round numbers inputs like to sit on, zero above all, never land on a
			// pixel's edge. A point exactly on one would be in two pixels at once.
			_origin = new double2(0.3183098861837907, 0.6180339887498949) * _pixel;
		}

		/// <summary>Where a vertex of the result lies, which is its pixel's centre.</summary>
		public float2 Position(int vertex) => (float2)_vertices[vertex];

		/// <summary>
		/// The same, unrounded. Vertices sit on a lattice, where no three that are
		/// not in line are closer to it than half a pixel squared over their
		/// spread, and a test on these sees that; the float positions do not.
		/// </summary>
		public double2 Exact(int vertex) => _vertices[vertex];

		public int AddPoint(float2 position)
		{
			_points.Add(position);
			return _points.Count - 1;
		}

		/// <returns>The index of the segment, for <see cref="Cuts"/>.</returns>
		public int AddSegment(int from, int to)
		{
			_segments.Add(new Segment { From = from, To = to });
			return _segments.Count - 1;
		}

		/// <summary>
		/// Finds the hot pixels and routes every segment through the ones it
		/// passes through.
		/// </summary>
		public void Build()
		{
			_vertices = new List<double2>();
			var hot = new Dictionary<(long, long), int>();
			foreach (var segment in _segments) {
				Heat(hot, _points[segment.From]);
				Heat(hot, _points[segment.To]);
			}

			// Only segments sharing a cell of a coarse grid can meet, and only hot
			// pixels in a cell a segment passes through can lie on it. Without the
			// grid, both are every pair there is, which a rounded outline and its
			// rungs alone make a million of.
			var grid = new Grid(_points, _segments, _pixel);
			var tested = new int[_segments.Count];
			for (var i = 0; i < tested.Length; i++) {
				tested[i] = -1;
			}
			for (var i = 0; i < _segments.Count; i++) {
				foreach (var cell in grid.Near(i)) {
					if (!grid.TryGetCrossing(cell, out var others)) {
						continue;
					}
					foreach (var j in others) {
						if (j > i && tested[j] != i) {
							tested[j] = i;
							if (TryCross(_segments[i], _segments[j], out var crossing)) {
								Heat(hot, crossing);
							}
						}
					}
				}
			}

			var nearby = new Dictionary<(long, long), List<(long, long)>>();
			foreach (var key in hot.Keys) {
				var cell = grid.CellOf(_origin + (new double2(key.Item1, key.Item2) + 0.5) * _pixel);
				if (!nearby.TryGetValue(cell, out var pixels)) {
					nearby[cell] = pixels = new List<(long, long)>();
				}
				pixels.Add(key);
			}

			_paths = new List<(float, int)>[_segments.Count];
			_edges = new Dictionary<(int, int), List<int>>();
			for (var i = 0; i < _segments.Count; i++) {
				_paths[i] = Route(_segments[i], hot, grid.Near(i), nearby);
				for (var k = 1; k < _paths[i].Count; k++) {
					var from = _paths[i][k - 1].Vertex;
					var to = _paths[i][k].Vertex;
					var key = (math.min(from, to), math.max(from, to));
					if (!_edges.TryGetValue(key, out var sources)) {
						_edges[key] = sources = new List<int>();
					}
					sources.Add(i);
				}
			}
		}

		/// <summary>Every edge of the result, each once, lower vertex first.</summary>
		public IEnumerable<(int, int)> Edges => _edges.Keys;

		/// <summary>
		/// The segments routed along the edge between two vertices, more than one
		/// where snapping ran segments together.
		/// </summary>
		public IReadOnlyList<int> Sources(int a, int b) => _edges[(math.min(a, b), math.max(a, b))];

		/// <summary>
		/// How far along segment <paramref name="segment"/> it passes through
		/// vertex <paramref name="vertex"/>, or zero if it does not.
		/// </summary>
		public float T(int segment, int vertex)
		{
			foreach (var (t, point) in _paths[segment]) {
				if (point == vertex) {
					return t;
				}
			}
			return 0f;
		}

		/// <summary>
		/// The vertices segment <paramref name="segment"/> was routed through, in
		/// order from its start, each with how far along it lies.
		/// </summary>
		/// <remarks>
		/// The first and last are always the pixels of the segment's own ends, and
		/// no vertex comes up twice. A segment whose ends share a pixel is a single
		/// vertex.
		/// </remarks>
		public List<(float T, int Point)> Cuts(int segment)
		{
			var path = _paths[segment];
			var result = new List<(float T, int Point)>(path.Count);
			foreach (var (t, vertex) in path) {
				result.Add((t, vertex));
			}
			return result;
		}

		/// <summary>
		/// Every bounded face, as a counter-clockwise cycle of vertices.
		/// </summary>
		/// <remarks>
		/// Found by walking half-edges: arriving at a vertex, leave along the edge
		/// that comes next clockwise from the one arrived on. That keeps the face
		/// on the left, so bounded faces come out counter-clockwise and the region
		/// around each connected component comes out clockwise, which is how the
		/// latter are told apart and dropped.
		/// </remarks>
		public List<List<int>> Faces() => Faces(out _);

		/// <param name="faceOf">Which face each half-edge has on its left, by its
		/// index in the result. A half-edge with no bounded face on its left, or
		/// that a face only stepped out along and straight back, is missing.</param>
		public List<List<int>> Faces(out Dictionary<(int, int), int> faceOf)
		{
			var around = new Dictionary<int, List<int>>();
			foreach (var (from, to) in _edges.Keys) {
				Neighbours(around, from).Add(to);
				Neighbours(around, to).Add(from);
			}
			foreach (var (vertex, neighbours) in around) {
				var origin = _vertices[vertex];
				neighbours.Sort((a, b) => Angle(origin, _vertices[a]).CompareTo(Angle(origin, _vertices[b])));
			}

			var walked = new HashSet<(int, int)>();
			var faces = new List<List<int>>();
			faceOf = new Dictionary<(int, int), int>();
			foreach (var (u, v) in _edges.Keys) {
				foreach (var start in new[] { (u, v), (v, u) }) {
					if (walked.Contains(start)) {
						continue;
					}

					var cycle = new List<int>();
					var edge = start;
					while (walked.Add(edge)) {
						cycle.Add(edge.Item1);
						var ring = around[edge.Item2];
						var back = ring.IndexOf(edge.Item1);
						edge = (edge.Item2, ring[(back - 1 + ring.Count) % ring.Count]);
					}

					cycle = Unspike(cycle, out _);
					if (cycle.Count >= 3 && SignedArea(cycle) > 0.0) {
						for (var k = 0; k < cycle.Count; k++) {
							faceOf[(cycle[k], cycle[(k + 1) % cycle.Count])] = faces.Count;
						}
						faces.Add(cycle);
					}
				}
			}
			return faces;
		}

		/// <summary>
		/// A closed walk with every step out to a point and straight back taken
		/// out, and every point it stays on for more than one step kept once.
		/// </summary>
		/// <remarks>
		/// Each step taken out is merged into one that stays, and the ones merged
		/// into any one step always follow each other around the walk, wrapping
		/// round its end at most. A walk that collapses altogether comes back with
		/// two points or fewer.
		/// </remarks>
		/// <param name="survivor">For each step of <paramref name="walk"/>, the
		/// index of the step in the result it was merged into, or that it became.</param>
		public static List<int> Unspike(IReadOnlyList<int> walk, out int[] survivor)
		{
			var into = new int[walk.Count];
			var kept = new List<int>(walk.Count);
			for (var i = 0; i < walk.Count; i++) {
				into[i] = i;
				if (kept.Count > 0 && walk[kept[^1]] == walk[i]) {
					into[i] = kept[^1];

				} else if (kept.Count > 1 && walk[kept[^2]] == walk[i]) {
					into[kept[^1]] = kept[^2];
					into[i] = kept[^2];
					kept.RemoveAt(kept.Count - 1);

				} else {
					kept.Add(i);
				}
			}

			// The same again where the end runs into the start.
			var head = 0;
			while (kept.Count - head > 2) {
				var first = kept[head];
				var last = kept[^1];
				if (walk[last] == walk[first]) {
					into[last] = first;
					kept.RemoveAt(kept.Count - 1);

				} else if (walk[kept[^2]] == walk[first]) {
					into[last] = first;
					into[kept[^2]] = first;
					kept.RemoveRange(kept.Count - 2, 2);

				} else if (walk[last] == walk[kept[head + 1]]) {
					into[first] = last;
					into[kept[head + 1]] = last;
					head += 2;

				} else {
					break;
				}
			}

			var result = new List<int>(kept.Count - head);
			var position = new Dictionary<int, int>(kept.Count - head);
			for (var i = head; i < kept.Count; i++) {
				position[kept[i]] = result.Count;
				result.Add(walk[kept[i]]);
			}

			survivor = new int[walk.Count];
			for (var i = 0; i < walk.Count; i++) {
				var root = i;
				while (into[root] != root) {
					root = into[root];
				}
				survivor[i] = position.TryGetValue(root, out var index) ? index : -1;
			}
			return result;
		}

		/// <remarks>
		/// Taken about the face's first point, and in double precision. About the
		/// origin, a small face far from it sums products of large coordinates that
		/// cancel almost completely, and a real face a tenth of a unit across, fifty
		/// units out, comes back with no area at all.
		/// </remarks>
		public double SignedArea(List<int> cycle)
		{
			var origin = _vertices[cycle[0]];
			var area = 0.0;
			for (var i = 1; i < cycle.Count - 1; i++) {
				var current = _vertices[cycle[i]] - origin;
				var next = _vertices[cycle[i + 1]] - origin;
				area += current.x * next.y - next.x * current.y;
			}
			return area * 0.5;
		}

		/// <summary>
		/// Marks the pixel holding <paramref name="position"/> hot, giving it a
		/// vertex at its centre.
		/// </summary>
		private void Heat(Dictionary<(long, long), int> hot, double2 position)
		{
			var key = Pixel(position);
			if (!hot.ContainsKey(key)) {
				hot[key] = _vertices.Count;
				_vertices.Add(_origin + (new double2(key.Item1, key.Item2) + 0.5) * _pixel);
			}
		}

		private (long, long) Pixel(double2 position)
		{
			var cell = math.floor((position - _origin) / _pixel);
			return ((long)cell.x, (long)cell.y);
		}

		/// <summary>
		/// Where two segments meet, including at an end of either, unless they are
		/// parallel. A parallel pair can only meet along a run bounded by their
		/// own ends, which are hot already.
		/// </summary>
		private bool TryCross(Segment first, Segment second, out double2 crossing)
		{
			crossing = default;
			var a = (double2)_points[first.From];
			var b = (double2)_points[first.To];
			var c = (double2)_points[second.From];
			var d = (double2)_points[second.To];

			if (math.max(a.x, b.x) < math.min(c.x, d.x) || math.max(c.x, d.x) < math.min(a.x, b.x)
				|| math.max(a.y, b.y) < math.min(c.y, d.y) || math.max(c.y, d.y) < math.min(a.y, b.y)) {
				return false;
			}

			var r = b - a;
			var s = d - c;
			var denominator = Cross(r, s);
			if (math.abs(denominator) <= 1e-12 * math.length(r) * math.length(s)) {
				return false;
			}

			var t = Cross(c - a, s) / denominator;
			var u = Cross(c - a, r) / denominator;
			if (t < 0.0 || t > 1.0 || u < 0.0 || u > 1.0) {
				return false;
			}
			crossing = a + r * t;
			return true;
		}

		/// <summary>
		/// The hot pixels a segment passes through, in the order it passes
		/// through them.
		/// </summary>
		/// <remarks>
		/// A segment passes through a convex pixel at most once, so the path never
		/// comes back to a vertex. The pixels are grown by a hair for the test, so
		/// a crossing that lands within rounding of a pixel's edge heats a pixel
		/// both segments are then sure to be seen passing through.
		/// </remarks>
		private List<(float, int)> Route(Segment segment, Dictionary<(long, long), int> hot,
			IEnumerable<(long, long)> cells, Dictionary<(long, long), List<(long, long)>> nearby)
		{
			var a = (double2)_points[segment.From];
			var b = (double2)_points[segment.To];
			var start = hot[Pixel(a)];
			var end = hot[Pixel(b)];
			var grow = _pixel * 1e-6;

			var passes = new List<(double T, int Vertex)>();
			var low = math.min(a, b) - _pixel;
			var high = math.max(a, b) + _pixel;
			foreach (var cell in cells) {
				if (!nearby.TryGetValue(cell, out var pixels)) {
					continue;
				}
				foreach (var key in pixels) {
					var vertex = hot[key];
					if (vertex == start || vertex == end) {
						continue;
					}
					var min = _origin + new double2(key.Item1, key.Item2) * _pixel - grow;
					var max = min + _pixel + 2.0 * grow;
					if (max.x < low.x || min.x > high.x || max.y < low.y || min.y > high.y) {
						continue;
					}
					if (TryClip(a, b, min, max, out var enter, out var leave)) {
						passes.Add(((enter + leave) * 0.5, vertex));
					}
				}
			}
			passes.Sort((x, y) => x.T.CompareTo(y.T));

			var path = new List<(float, int)>(passes.Count + 2) { (0f, start) };
			foreach (var (t, vertex) in passes) {
				path.Add(((float)t, vertex));
			}
			if (end != start) {
				path.Add((1f, end));
			}
			return path;
		}

		/// <summary>
		/// The stretch of the segment from <paramref name="a"/> to
		/// <paramref name="b"/> inside a closed box, as fractions along it, if
		/// there is any.
		/// </summary>
		private static bool TryClip(double2 a, double2 b, double2 min, double2 max, out double enter, out double leave)
		{
			enter = 0.0;
			leave = 1.0;
			var delta = b - a;
			return Clip(-delta.x, a.x - min.x, ref enter, ref leave)
				&& Clip(delta.x, max.x - a.x, ref enter, ref leave)
				&& Clip(-delta.y, a.y - min.y, ref enter, ref leave)
				&& Clip(delta.y, max.y - a.y, ref enter, ref leave);
		}

		/// <summary>One side of a Liang-Barsky clip.</summary>
		private static bool Clip(double direction, double distance, ref double enter, ref double leave)
		{
			if (direction == 0.0) {
				return distance >= 0.0;
			}
			var t = distance / direction;
			if (direction < 0.0) {
				if (t > leave) {
					return false;
				}
				enter = math.max(enter, t);
			} else {
				if (t < enter) {
					return false;
				}
				leave = math.min(leave, t);
			}
			return true;
		}

		/// <summary>
		/// A coarse grid over the segments, each entered in the cells it crosses.
		/// </summary>
		/// <remarks>
		/// Two segments that meet cross cells that are the same or side by side,
		/// and a hot pixel a segment passes through has its centre within a cell of
		/// one it crosses, so looking one cell round each crossed cell finds both.
		/// Entering every segment in that wider ring instead crowds each cell nine
		/// times over, and the pairs to try with it.
		/// </remarks>
		private sealed class Grid
		{
			private readonly double _cell;
			private readonly Dictionary<(long, long), List<int>> _crossing = new();
			private readonly List<(long, long)>[] _near;

			public Grid(List<float2> points, List<Segment> segments, double pixel)
			{
				var low = new double2(double.MaxValue);
				var high = new double2(double.MinValue);
				foreach (var segment in segments) {
					low = math.min(low, math.min((double2)points[segment.From], (double2)points[segment.To]));
					high = math.max(high, math.max((double2)points[segment.From], (double2)points[segment.To]));
				}
				var extent = segments.Count > 0 ? math.cmax(high - low) : 1.0;

				// About as many cells across as there are segments along a side, and
				// never finer than a few pixels, so a pixel's centre is never more
				// than a cell from where a segment passing through it crosses.
				_cell = math.max(extent / math.max(1.0, math.sqrt(segments.Count)), pixel * 8.0);

				_near = new List<(long, long)>[segments.Count];
				var near = new HashSet<(long, long)>();
				for (var i = 0; i < segments.Count; i++) {
					near.Clear();
					foreach (var (x, y) in Traverse((double2)points[segments[i].From], (double2)points[segments[i].To])) {
						if (!_crossing.TryGetValue((x, y), out var bucket)) {
							_crossing[(x, y)] = bucket = new List<int>();
						}
						bucket.Add(i);
						for (var dx = -1; dx <= 1; dx++) {
							for (var dy = -1; dy <= 1; dy++) {
								near.Add((x + dx, y + dy));
							}
						}
					}
					_near[i] = new List<(long, long)>(near);
				}
			}

			/// <summary>The cells the segment crosses, and every cell next to one.</summary>
			public List<(long, long)> Near(int segment) => _near[segment];

			/// <summary>The segments crossing a cell.</summary>
			public bool TryGetCrossing((long, long) cell, out List<int> segments) => _crossing.TryGetValue(cell, out segments);

			public (long, long) CellOf(double2 position)
			{
				var cell = math.floor(position / _cell);
				return ((long)cell.x, (long)cell.y);
			}

			/// <summary>
			/// The cells a segment crosses, from its start, stepping into whichever
			/// neighbour it reaches first, as Amanatides and Woo do.
			/// </summary>
			private IEnumerable<(long, long)> Traverse(double2 a, double2 b)
			{
				var (x, y) = CellOf(a);
				var (endX, endY) = CellOf(b);
				var delta = b - a;
				var stepX = delta.x > 0.0 ? 1 : -1;
				var stepY = delta.y > 0.0 ? 1 : -1;
				var nextX = delta.x != 0.0 ? ((x + (stepX > 0 ? 1 : 0)) * _cell - a.x) / delta.x : double.PositiveInfinity;
				var nextY = delta.y != 0.0 ? ((y + (stepY > 0 ? 1 : 0)) * _cell - a.y) / delta.y : double.PositiveInfinity;
				var spanX = delta.x != 0.0 ? _cell / math.abs(delta.x) : double.PositiveInfinity;
				var spanY = delta.y != 0.0 ? _cell / math.abs(delta.y) : double.PositiveInfinity;

				// Rounding can walk a step past the end cell along the way, so the
				// walk is held to the cells between, which is all a segment can cross.
				var steps = math.abs(endX - x) + math.abs(endY - y);
				yield return (x, y);
				for (var step = 0; step < steps; step++) {
					if (nextX < nextY) {
						x += stepX;
						nextX += spanX;
					} else {
						y += stepY;
						nextY += spanY;
					}
					yield return (x, y);
				}
				if (x != endX || y != endY) {
					yield return (endX, endY);
				}
			}
		}

		private static List<int> Neighbours(Dictionary<int, List<int>> around, int vertex)
		{
			if (!around.TryGetValue(vertex, out var list)) {
				around[vertex] = list = new List<int>();
			}
			return list;
		}

		private static double Angle(double2 origin, double2 target) => math.atan2(target.y - origin.y, target.x - origin.x);

		private static double Cross(double2 a, double2 b) => a.x * b.y - b.x * a.y;
	}
}
