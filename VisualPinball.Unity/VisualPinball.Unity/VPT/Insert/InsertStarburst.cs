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
	/// The faceted fan that replaces the raised floor of an insert.
	/// </summary>
	/// <remarks>
	/// A wheel of triangles: one centre, an even ring of rim vertices around it,
	/// and an edge from the centre out to each. Rim vertices alternate a fixed
	/// distance above and below the centre's plane, so consecutive triangles tilt
	/// opposite ways and meet along their shared spoke in a ridge or a groove.
	/// That is the whole shape; the rays an insert appears to have are those
	/// creases.
	/// <para>
	/// The centre is usually outside the insert, off past its narrow end, and the
	/// wheel is sized so the part that lands on the insert covers it completely.
	/// What has to reach is the <i>inscribed polygon</i>, not the circle: a
	/// triangle is closed by the chord between its two rim vertices, so a point
	/// beyond that chord but inside the circle would sit in no triangle at all.
	/// </para>
	/// <para>
	/// The surface is flat within each triangle, which is what makes it possible
	/// to cut the insert's outline out of it exactly. The outline and the spokes
	/// are laid into one <see cref="InsertArrangement"/>, whose every face lies
	/// inside a single triangle in plan view, so lifting its points onto the
	/// surface keeps each face flat.
	/// </para>
	/// </remarks>
	public class InsertStarburst
	{
		/// <summary>Fewer than two bursts cannot make a wheel.</summary>
		public const int MinBursts = 2;

		public readonly float2 Center;

		/// <summary>The depth the centre sits at, which the rim straddles.</summary>
		public readonly float CenterDepth;

		/// <summary>How far the rim rises above and drops below the centre.</summary>
		public readonly float Offset;

		public readonly float Radius;

		private readonly float2[] _rim;
		private readonly float[] _rimDepth;

		/// <summary>Number of rim vertices, which is twice the number of bursts.</summary>
		public int Count => _rim.Length;

		public float2 Rim(int index) => _rim[Mod(index)];

		public float RimDepth(int index) => _rimDepth[Mod(index)];

		/// <summary>Unit direction of the spoke running out to rim vertex <paramref name="index"/>.</summary>
		public float2 Direction(int index) => math.normalize(Rim(index) - Center);

		private InsertStarburst(float2 center, float centerDepth, float offset, float radius, int count)
		{
			Center = center;
			CenterDepth = centerDepth;
			Offset = offset;
			Radius = radius;

			_rim = new float2[count];
			_rimDepth = new float[count];
			for (var i = 0; i < count; i++) {
				var angle = math.PI * 2f * i / count;
				_rim[i] = center + radius * new float2(math.cos(angle), math.sin(angle));

				// Alternating, and the count is even, so the run closes on a change
				// of sign rather than repeating one.
				_rimDepth[i] = centerDepth + (i % 2 == 0 ? offset : -offset);
			}
		}

		/// <summary>
		/// A wheel centred on <paramref name="center"/> and grown until its
		/// triangles cover every point of <paramref name="footprint"/>.
		/// </summary>
		public static InsertStarburst Cover(IReadOnlyList<float2> footprint, float2 center, int bursts,
			float offset, float centerDepth)
		{
			if (bursts < MinBursts) {
				throw new InsertOutlineException($"A starburst needs at least {MinBursts} bursts.");
			}
			if (footprint.Count == 0) {
				throw new InsertOutlineException("A starburst needs a surface to cover.");
			}

			var reach = 0f;
			foreach (var point in footprint) {
				reach = math.max(reach, math.distance(point, center));
			}
			if (reach < InsertOutlineBuilder.Epsilon) {
				throw new InsertOutlineException("The surface the starburst should cover has no size.");
			}

			var count = bursts * 2;

			// The inscribed polygon's apothem is what must clear the footprint, and
			// a little over, so a point exactly on the far edge is not a borderline
			// case for the sector lookup.
			var radius = reach / math.cos(math.PI / count) * 1.01f;
			return new InsertStarburst(center, centerDepth, offset, radius, count);
		}

		/// <summary>
		/// Which triangle <paramref name="position"/> falls in, looking from above.
		/// </summary>
		public int SectorOf(float2 position)
		{
			var delta = position - Center;
			if (math.lengthsq(delta) < InsertOutlineBuilder.Epsilon * InsertOutlineBuilder.Epsilon) {
				return 0;
			}

			var angle = math.atan2(delta.y, delta.x);
			if (angle < 0f) {
				angle += math.PI * 2f;
			}
			return math.clamp((int)(angle / (math.PI * 2f / Count)), 0, Count - 1);
		}

		/// <summary>
		/// The depth of the surface above or below <paramref name="position"/>.
		/// </summary>
		public float DepthAt(float2 position) => DepthInSector(SectorOf(position), position);

		/// <summary>
		/// The depth of triangle <paramref name="sector"/>'s plane at
		/// <paramref name="position"/>.
		/// </summary>
		/// <remarks>
		/// Barycentric, so it is exact anywhere on that plane, including just
		/// outside the triangle. Two triangles sharing a spoke agree along it,
		/// which is what keeps the surface closed across a crease.
		/// </remarks>
		public float DepthInSector(int sector, float2 position)
		{
			var b = Rim(sector) - Center;
			var c = Rim(sector + 1) - Center;
			var p = position - Center;

			// Twice the triangle's area, so it is measured against the radius
			// squared. A length tolerance here would call every triangle of a small
			// enough wheel degenerate and flatten it, while the facet normals went
			// on describing the slopes it was meant to have. Cover rules out a zero
			// radius and a wheel has at least four rim vertices, so in practice
			// this never fires; it stays as a guard, in the right units.
			var denominator = b.x * c.y - c.x * b.y;
			if (math.abs(denominator) <= 1e-9f * Radius * Radius) {
				return CenterDepth;
			}

			var along = (p.x * c.y - c.x * p.y) / denominator;
			var across = (b.x * p.y - p.x * b.y) / denominator;
			return CenterDepth
				+ along * (RimDepth(sector) - CenterDepth)
				+ across * (RimDepth(sector + 1) - CenterDepth);
		}

		/// <summary>
		/// The part of spoke <paramref name="index"/> lying inside a convex,
		/// counter-clockwise polygon, looking from above.
		/// </summary>
		/// <remarks>
		/// Only trims the spoke to the polygon. Where it meets the polygon's edges,
		/// or anything else, is left to <see cref="InsertArrangement"/>, which works
		/// each such point out once for everyone who needs it.
		/// </remarks>
		/// <param name="fromCenter">Whether the part starts at the centre itself,
		/// which is the case when the centre is inside the polygon or on its edge,
		/// so every spoke leaving it can share the one point.</param>
		public bool TrySpokeWithin(int index, IReadOnlyList<float2> polygon, float tolerance,
			out float2 from, out float2 to, out bool fromCenter)
		{
			var direction = Direction(index);
			var near = 0f;
			var far = Radius;

			for (var i = 0; i < polygon.Count; i++) {
				var start = polygon[i];
				var edge = polygon[(i + 1) % polygon.Count] - start;
				var outward = new float2(edge.y, -edge.x);
				var facing = math.dot(outward, direction);
				var clearance = math.dot(outward, start - Center);

				// Both are the edge's length times a sine or a distance, so the
				// parallel test is against that length, not a fixed tolerance.
				if (math.abs(facing) <= 1e-7f * math.length(outward)) {
					if (clearance < -tolerance * math.length(outward)) {
						from = to = default;
						fromCenter = false;
						return false;
					}
					continue;
				}

				var t = clearance / facing;
				if (facing > 0f) {
					far = math.min(far, t);
				} else {
					near = math.max(near, t);
				}
			}

			fromCenter = near <= tolerance;
			from = fromCenter ? Center : Center + direction * near;
			to = Center + direction * far;
			return far - near > tolerance;
		}

		/// <summary>
		/// The largest offset that keeps the surface between the bucket's floor
		/// and its rim, with the centre <paramref name="channelDepth"/> above the
		/// floor of a bucket <paramref name="depth"/> deep.
		/// </summary>
		/// <remarks>
		/// Nowhere on the surface strays further from the centre's plane than the
		/// offset's size, whichever way it points, so keeping both extremes inside
		/// the bucket keeps all of it inside. The raised floor never reaches out to
		/// the rim of the wheel, which was grown past it, so at the limit it still
		/// stops short of the channel floor and the wall around it never closes to
		/// nothing.
		/// </remarks>
		public static float OffsetLimit(float depth, float channelDepth) =>
			math.max(0f, math.min(channelDepth, depth - channelDepth));

		private int Mod(int index) => (index % Count + Count) % Count;
	}
}
