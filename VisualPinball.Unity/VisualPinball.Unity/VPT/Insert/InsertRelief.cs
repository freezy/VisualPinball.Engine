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
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A faceted surface that replaces the raised floor of an insert: flat
	/// facets meeting along creases, ridges and grooves by turns, the rays an
	/// insert appears to have.
	/// </summary>
	/// <remarks>
	/// The starburst is one, its facets the triangles of a wheel; the ribs are
	/// another, their facets the strips between parallel creases. The generator
	/// lays the creases into its arrangement, so every face it walks lies within
	/// one facet in plan view, lifts each face's points onto the facet's plane,
	/// and gives it that plane's normal. Everything it asks of the surface is
	/// here, so it does not care which it has.
	/// </remarks>
	public abstract class InsertRelief
	{
		/// <summary>The depth the surface straddles: its middle plane.</summary>
		public abstract float CenterDepth { get; }

		/// <summary>How far the creases rise above and drop below the middle plane.</summary>
		public abstract float Offset { get; }

		/// <summary>The depth of the surface above or below <paramref name="position"/>.</summary>
		public abstract float DepthAt(float2 position);

		/// <summary>Which facet <paramref name="position"/> falls in, looking from above.</summary>
		public abstract int SectorOf(float2 position);

		/// <summary>The unit normal of facet <paramref name="sector"/>'s plane, in local space, pointing up.</summary>
		public abstract float3 LocalNormal(int sector);

		/// <summary>
		/// The unit normal the surface shades with at <paramref name="position"/>
		/// within facet <paramref name="sector"/>: the plane's, unless the facet
		/// only approximates a curved surface and shades as that surface would.
		/// </summary>
		public virtual float3 LocalNormal(int sector, float2 position) => LocalNormal(sector);

		/// <summary>
		/// The parts of every crease lying inside a counter-clockwise polygon,
		/// looking from above, each from where it enters to where it leaves. A
		/// part that starts at a point every crease shares, such as a wheel's
		/// centre, says so, so the arrangement can give them the one point.
		/// </summary>
		public abstract IEnumerable<(float2 From, float2 To, bool FromCenter)> CreasesWithin(IReadOnlyList<float2> polygon, float tolerance);

		/// <summary>The point the creases share when one says it starts there.</summary>
		public abstract float2 Center { get; }

		/// <summary>
		/// A direction from <paramref name="from"/> along which a line never
		/// crosses a crease, for tying a hole into the surface around it within
		/// the one facet it starts in.
		/// </summary>
		public abstract float2 BridgeDirection(float2 from);

		/// <summary>
		/// What the hybrid material needs to find the facets again, in local
		/// space: for a wheel its centre's X and Z, the sector count and the
		/// lowest height; for ribs the direction across them in X and Z, the
		/// half pitch in metres made negative, which tells the two apart, and the
		/// lowest height.
		/// </summary>
		public abstract Vector4 Optical();

		/// <summary>
		/// The largest offset that keeps the surface between the bucket's floor
		/// and its rim, with the middle plane <paramref name="channelDepth"/>
		/// above the floor of a bucket <paramref name="depth"/> deep.
		/// </summary>
		/// <remarks>
		/// Nowhere on the surface strays further from the middle plane than the
		/// offset's size, whichever way it points, so keeping both extremes inside
		/// the bucket keeps all of it inside. The raised floor never reaches the
		/// creases' far ends, so at the limit it still stops short of the channel
		/// floor and the wall around it never closes to nothing.
		/// </remarks>
		public static float OffsetLimit(float depth, float channelDepth) =>
			math.max(0f, math.min(channelDepth, depth - channelDepth));

		/// <summary>
		/// The parts of a ray from <paramref name="origin"/> along
		/// <paramref name="direction"/>, out to <paramref name="reach"/>, lying
		/// inside a counter-clockwise polygon: from where it enters to where it
		/// leaves, as often as it does, and a part starting at the origin
		/// itself, which is the case when the origin is inside the polygon or on
		/// its edge, says so.
		/// </summary>
		/// <remarks>
		/// Only trims the ray to the polygon. Where it meets the polygon's edges,
		/// or anything else, is left to <see cref="InsertArrangement"/>, which
		/// works each such point out once for everyone who needs it.
		/// </remarks>
		protected static List<(float2 From, float2 To, bool FromCenter)> RayWithin(float2 origin, float2 direction, float reach,
			IReadOnlyList<float2> polygon, float tolerance)
		{
			var crossings = new List<float>();
			foreach (var crossing in Crossings(origin, direction, polygon)) {
				if (crossing >= 0f) {
					crossings.Add(crossing);
				}
			}
			// An odd number of crossings ahead means the origin is inside, and the
			// first part starts there.
			var parts = new List<(float2, float2, bool)>();
			var inside = crossings.Count % 2 == 1;
			var index0 = 0;
			var near = 0f;
			if (!inside) {
				if (crossings.Count == 0) {
					return parts;
				}
				near = crossings[0];
				index0 = 1;
			}
			for (var i = index0; i <= crossings.Count; i += 2) {
				var far = i < crossings.Count ? crossings[i] : reach;
				far = math.min(far, reach);
				if (far - near > tolerance) {
					var fromCenter = near <= tolerance;
					parts.Add((fromCenter ? origin : origin + direction * near, origin + direction * far, fromCenter));
				}
				if (i + 1 < crossings.Count) {
					near = crossings[i + 1];
				} else {
					break;
				}
			}
			return parts;
		}

		/// <summary>
		/// The parts of the segment from <paramref name="from"/> to
		/// <paramref name="to"/> lying inside a polygon, judged by where the
		/// segment's line crosses the polygon's edges and whether its start is in.
		/// </summary>
		protected static IEnumerable<(float2 From, float2 To)> SegmentWithin(float2 from, float2 to, IReadOnlyList<float2> polygon, float tolerance)
		{
			var length = math.distance(from, to);
			if (length <= tolerance) {
				yield break;
			}
			var direction = (to - from) / length;
			var inside = InsertGroove.Contains(polygon, from);
			var start = 0f;
			foreach (var crossing in Crossings(from, direction, polygon)) {
				if (crossing <= 0f) {
					// Behind the start: each one flips which side the start is on,
					// which the test of the start already settled.
					continue;
				}
				if (crossing >= length) {
					break;
				}
				if (inside && crossing - start > tolerance) {
					yield return (from + direction * start, from + direction * crossing);
				}
				inside = !inside;
				start = crossing;
			}
			if (inside && length - start > tolerance) {
				yield return (from + direction * start, to);
			}
		}

		/// <summary>
		/// Where a line through <paramref name="origin"/> along <paramref name="direction"/>
		/// crosses the edges of a polygon, as distances along the line, sorted.
		/// An edge crosses the line when its ends lie either side of it, so a
		/// corner the line passes through counts once and one it only grazes
		/// twice or not at all, which keeps the count's parity right.
		/// </summary>
		protected static List<float> Crossings(float2 origin, float2 direction, IReadOnlyList<float2> polygon)
		{
			var crossings = new List<float>();
			for (var i = 0; i < polygon.Count; i++) {
				var a = polygon[i];
				var b = polygon[(i + 1) % polygon.Count];
				var sideA = direction.x * (a.y - origin.y) - direction.y * (a.x - origin.x) > 0f;
				var sideB = direction.x * (b.y - origin.y) - direction.y * (b.x - origin.x) > 0f;
				if (sideA == sideB) {
					continue;
				}
				var edge = b - a;
				var denominator = direction.x * edge.y - direction.y * edge.x;
				var delta = a - origin;
				crossings.Add((delta.x * edge.y - delta.y * edge.x) / denominator);
			}
			crossings.Sort();
			return crossings;
		}
	}
}
