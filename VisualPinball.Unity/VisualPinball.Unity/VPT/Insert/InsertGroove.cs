// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A thin line engraved into an insert's raised middle: a trough with a
	/// V or rounded floor running along a path inside the outline, the way a
	/// flash has a lightning bolt drawn into it a little way in from its edge.
	/// </summary>
	/// <remarks>
	/// The path is an outline of its own, the insert's outline eroded by the
	/// line's inset: convex corners sharpen or vanish, concave ones widen, so
	/// the line follows the shape the way an engraver's stylus would.
	/// </remarks>
	public sealed class InsertGroove
	{
		/// <summary>The line's middle, as an outline in VPX units.</summary>
		public InsertOutline Outline;

		/// <summary>The raised middle's outline the line lies on, which the trough must not leave.</summary>
		public List<float2> Surface;

		/// <summary>How wide the trough is across, in VPX units.</summary>
		public float Width;

		/// <summary>How deep its middle goes below the surface, in VPX units.</summary>
		public float Depth;

		/// <summary>How many steps each side of the trough's round is drawn with, edge to middle.</summary>
		public int Segments;

		/// <summary>
		/// The line offset sideways by <paramref name="lateral"/>, inwards when
		/// positive, welded where points fold together, with a map from the
		/// line's points to the ring's. A side too short for the offset runs
		/// backwards once offset: its ends are drawn together onto where its
		/// neighbours' offsets meet, as an erosion does. Outwards, a sharp
		/// tip's mitre is shortened where it would leave the surface, as far as
		/// it must and no further, so a tip stays a point wherever the whole
		/// mitre fits.
		/// </summary>
		public List<float2> Ring(float lateral, float weld, out int[] map)
		{
			var points = Outline.Points;
			var count = points.Count;
			var positions = new float2[count];
			for (var i = 0; i < count; i++) {
				positions[i] = Outline.OffsetPoint(i, lateral);
				var point = points[i];
				if (lateral >= 0f || Surface == null || point.Kind != InsertOutlinePointKind.Sharp || point.Miter <= 1f
					|| Outline.Corners[point.Corner].Concave || Contains(Surface, positions[i])) {
					continue;
				}
				// Shorten the mitre along the bisector until it fits, down to the
				// offset's own length, which a line kept clear of the edge always fits.
				var full = point.Miter;
				var low = 1f;
				var high = full;
				for (var step = 0; step < 24; step++) {
					var mid = (low + high) * 0.5f;
					if (Contains(Surface, point.Position - lateral * mid * point.Normal)) {
						low = mid;
					} else {
						high = mid;
					}
				}
				positions[i] = point.Position - lateral * low * point.Normal;
			}

			for (var pass = 0; pass < count; pass++) {
				var folded = false;
				for (var i = 0; i < count; i++) {
					var next = (i + 1) % count;
					var previous = (i - 1 + count) % count;
					var after = (next + 1) % count;
					var offset = positions[next] - positions[i];
					var along = points[next].Position - points[i].Position;
					if (math.lengthsq(offset) <= weld * weld || math.dot(offset, along) >= 0f) {
						continue;
					}
					if (!Meet(positions[previous], positions[i] - positions[previous], positions[next], positions[after] - positions[next], out var meeting)) {
						continue;
					}
					positions[i] = meeting;
					positions[next] = meeting;
					folded = true;
				}
				if (!folded) {
					break;
				}
			}

			var ring = new List<float2>(count);
			map = new int[count];
			for (var i = 0; i < count; i++) {
				if (ring.Count > 0 && math.distance(positions[i], ring[^1]) < weld) {
					map[i] = ring.Count - 1;
					continue;
				}
				map[i] = ring.Count;
				ring.Add(positions[i]);
			}
			if (ring.Count > 3 && math.distance(ring[^1], ring[0]) < weld) {
				for (var i = 0; i < count; i++) {
					if (map[i] == ring.Count - 1) {
						map[i] = 0;
					}
				}
				ring.RemoveAt(ring.Count - 1);
			}
			return ring;
		}

		/// <summary>Whether a closed polygon crosses itself anywhere.</summary>
		public static bool SelfCrossing(IReadOnlyList<float2> polygon)
		{
			var count = polygon.Count;
			for (var i = 0; i < count; i++) {
				for (var j = i + 2; j < count; j++) {
					if (i == 0 && j == count - 1) {
						continue;
					}
					if (Crosses(polygon[i], polygon[(i + 1) % count], polygon[j], polygon[(j + 1) % count])) {
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>Whether a point lies inside a closed polygon.</summary>
		public static bool Contains(IReadOnlyList<float2> polygon, float2 point)
		{
			var inside = false;
			for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) {
				var a = polygon[i];
				var b = polygon[j];
				if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) {
					inside = !inside;
				}
			}
			return inside;
		}

		/// <summary>Whether two segments cross properly, neither touching the other at a point.</summary>
		private static bool Crosses(float2 a, float2 b, float2 c, float2 d)
		{
			static double Side(double2 o, double2 p, double2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
			return Side(a, b, c) * Side(a, b, d) < 0.0 && Side(c, d, a) * Side(c, d, b) < 0.0;
		}

		/// <summary>Where two lines meet, or false when they run parallel.</summary>
		private static bool Meet(float2 originA, float2 directionA, float2 originB, float2 directionB, out float2 point)
		{
			var determinant = directionA.x * directionB.y - directionA.y * directionB.x;
			point = originA;
			if (math.abs(determinant) < 1e-12f) {
				return false;
			}
			var offset = originB - originA;
			var t = (offset.x * directionB.y - offset.y * directionB.x) / determinant;
			point = originA + t * directionA;
			return true;
		}
	}
}
