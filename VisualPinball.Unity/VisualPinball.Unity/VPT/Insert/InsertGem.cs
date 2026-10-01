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
	/// A gem cut across the raised floor of an insert: a flat table in the
	/// middle and rows of triangular facets zigzagging out from it to the
	/// edge, every row a step lower than the last.
	/// </summary>
	/// <remarks>
	/// The table is a regular polygon, and round it lie rings of as many
	/// points, each ring turned half a side on from the one before, so a
	/// point of one ring sits between two points of the next. The facets of
	/// a row are the triangles between two rings: one off every side of the
	/// inner ring out to the point beyond it, and one off every point of the
	/// inner ring in between, its base on the outer ring. The outermost ring
	/// lies beyond the surface, so the last row always reaches its edge, and
	/// it can hold two points where the others hold one, spread either side
	/// of where the one would be, so the lines of the last row meet the edge
	/// evenly instead of two by two; the facet off a side is then a trapezoid,
	/// its outer side parallel to the inner, which keeps it flat. One row, the
	/// table's sides running straight out to the edge, is the usual insert; a
	/// row length adds a ring every that far out, which is how a brilliant's
	/// crown is cut.
	/// </remarks>
	public class InsertGem : InsertRelief
	{
		/// <summary>The fewest sides the table can have, and the most.</summary>
		public const int MinSides = 4;
		public const int MaxSides = 16;

		/// <summary>The largest share of the way out to the edge the table's corners can take.</summary>
		public const float MaxTable = 0.9f;

		/// <summary>The shortest row, so a slider dragged near zero cannot ask for rings by the hundred, and the most rows there can be.</summary>
		public const float MinRowLength = 1f;
		public const int MaxRows = 12;

		private readonly float2 _center;
		private readonly float _centerDepth;
		private readonly float _offset;

		/// <summary>How many sides the table has, and points every ring round it.</summary>
		public readonly int Sides;

		/// <summary>The radius of the table's corners, in VPX units.</summary>
		public readonly float TableRadius;

		/// <summary>How far out each ring lies beyond the one before, from the table's sides.</summary>
		public readonly float RowLength;

		/// <summary>How many rows of facets there are, from the table out to the edge.</summary>
		public readonly int Rows;

		/// <summary>The radius of the outermost ring, beyond every point of the surface.</summary>
		public readonly float Reach;

		/// <summary>
		/// How far the outermost ring's two points either side of where one
		/// point would be are spread apart, as a share of the way to evenly
		/// spaced: at zero they are one point, at one every point of the ring
		/// is as far from the next as from the last.
		/// </summary>
		public readonly float Spread;

		/// <summary>The angle from where one point of the outermost ring would be to each of its two, in radians.</summary>
		public float SpreadAngle => math.PI / Sides * (1f - 0.5f * Spread);

		public override float2 Center => _center;
		public override float CenterDepth => _centerDepth;
		public override float Offset => _offset;

		/// <summary>The depth of the table: a plateau the offset above the middle plane.</summary>
		public float TableDepth => _centerDepth + _offset;

		/// <summary>The depth of the outermost ring, the offset below the middle plane.</summary>
		public float EdgeDepth => _centerDepth - _offset;

		private InsertGem(float2 center, int sides, float tableRadius, float rowLength, int rows, float reach, float spread, float offset, float centerDepth)
		{
			_center = center;
			Sides = sides;
			TableRadius = tableRadius;
			RowLength = rowLength;
			Rows = rows;
			Reach = reach;
			Spread = spread;
			_offset = offset;
			_centerDepth = centerDepth;
		}

		/// <summary>
		/// A gem round <paramref name="center"/> with a table of
		/// <paramref name="sides"/> sides whose corners lie <paramref name="table"/>
		/// of the way out to the edge, and a ring every <paramref name="rowLength"/>
		/// beyond the table's sides, or none before the edge when that is zero;
		/// the outermost ring sits just beyond every point of
		/// <paramref name="footprint"/>, its points spread by <paramref name="spread"/>.
		/// </summary>
		public static InsertGem Cover(IReadOnlyList<float2> footprint, float2 center, int sides, float table, float rowLength, float spread, float offset, float centerDepth)
		{
			if (footprint.Count == 0) {
				throw new InsertOutlineException("A gem needs a surface to cover.");
			}
			sides = math.clamp(sides, MinSides, MaxSides);
			var reach = 0f;
			foreach (var point in footprint) {
				reach = math.max(reach, math.distance(point, center));
			}
			if (reach < InsertOutlineBuilder.Epsilon) {
				throw new InsertOutlineException("The surface the gem should cover has no size.");
			}
			// The rows end on the outermost ring's sides, which must clear the
			// surface, not only its points: the widest side is the one spanning
			// the larger of the two angles its points are spread at.
			spread = math.saturate(spread);
			var half = math.PI / sides * (1f - 0.5f * spread);
			var widest = math.max(half, math.PI / sides - half);
			var cos = math.cos(math.PI / sides);
			var tableRadius = reach * 1.01f / cos * math.clamp(table, 0.05f, MaxTable);
			var apothem = tableRadius * cos;
			reach = math.max(reach * 1.01f / math.cos(widest), apothem + MinRowLength);
			var rows = 1;
			if (rowLength > InsertOutlineBuilder.Epsilon && apothem + rowLength < reach) {
				rowLength = math.max(rowLength, MinRowLength);
				rows = math.clamp((int)math.ceil((reach - apothem) / rowLength), 1, MaxRows);
				rowLength = math.max(rowLength, (reach - apothem) / rows);
				reach = apothem + rows * rowLength;
			} else {
				rowLength = reach - apothem;
			}
			return new InsertGem(center, sides, tableRadius, rowLength, rows, reach, spread, offset, centerDepth);
		}

		/// <summary>The radius of ring <paramref name="ring"/>'s points: the table's corners, then a row length further out each.</summary>
		public float Radius(int ring) => ring == 0 ? TableRadius : TableRadius * math.cos(math.PI / Sides) + ring * RowLength;

		/// <summary>The depth of ring <paramref name="ring"/>: a step down from the table to the edge each.</summary>
		public float RingDepth(int ring) => math.lerp(TableDepth, EdgeDepth, (float)ring / Rows);

		/// <summary>Which way point <paramref name="index"/> of ring <paramref name="ring"/> lies: every ring turned half a side on from the last.</summary>
		public float2 Direction(int ring, int index)
		{
			var angle = math.PI * 2f * (index + (ring & 1) * 0.5f) / Sides;
			return new float2(math.cos(angle), math.sin(angle));
		}

		/// <summary>Point <paramref name="index"/> of ring <paramref name="ring"/>, short of the outermost, which has two a point.</summary>
		public float2 Point(int ring, int index) => _center + Direction(ring, index) * Radius(ring);

		/// <summary>
		/// The outermost ring's point spread <paramref name="after"/> or before
		/// where its point <paramref name="index"/> would be: at no spread the
		/// two are one, where the ring before's point <paramref name="index"/>
		/// and the next have their lines meet.
		/// </summary>
		public float2 Edge(int index, bool after)
		{
			var angle = math.PI * 2f * (index + ((Rows - 1) & 1) * 0.5f) / Sides + (after ? SpreadAngle : -SpreadAngle);
			return _center + new float2(math.cos(angle), math.sin(angle)) * Reach;
		}

		/// <summary>Whether row <paramref name="row"/> is the last, whose outer ring is the spread one.</summary>
		private bool Last(int row) => row == Rows - 1;

		/// <summary>Which way the middle of ring <paramref name="ring"/>'s side from point <paramref name="index"/> to the next lies.</summary>
		public float2 SideDirection(int ring, int index)
		{
			var angle = math.PI * 2f * (index + (ring & 1) * 0.5f + 0.5f) / Sides;
			return new float2(math.cos(angle), math.sin(angle));
		}

		/// <summary>The facet index of the table.</summary>
		public const int TableSector = 0;

		/// <summary>The facet off side <paramref name="side"/> of ring <paramref name="ring"/>, pointing out to the ring beyond.</summary>
		public int OutwardSector(int ring, int side) => 1 + ring * 2 * Sides + (side % Sides + Sides) % Sides;

		/// <summary>The facet off point <paramref name="index"/> of ring <paramref name="ring"/>, its base on the ring beyond.</summary>
		public int InwardSector(int ring, int index) => 1 + ring * 2 * Sides + Sides + (index % Sides + Sides) % Sides;

		/// <summary>How many facets there are altogether.</summary>
		public int SectorCount => 1 + Rows * 2 * Sides;

		private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

		/// <summary>Whether <paramref name="delta"/>, from the centre, lies within ring <paramref name="ring"/>'s polygon.</summary>
		private bool Within(int ring, float2 delta)
		{
			var angle = math.atan2(delta.y, delta.x) / (math.PI * 2f) * Sides - (ring & 1) * 0.5f;
			var side = (int)math.floor(angle);
			return math.dot(delta, SideDirection(ring, side)) <= Radius(ring) * math.cos(math.PI / Sides);
		}

		public override int SectorOf(float2 position)
		{
			var delta = position - _center;
			if (math.lengthsq(delta) < InsertOutlineBuilder.Epsilon * InsertOutlineBuilder.Epsilon || Within(0, delta)) {
				return TableSector;
			}
			var row = 0;
			while (row + 1 < Rows && !Within(row + 1, delta)) {
				row++;
			}
			// Within the row, the span between two points of its inner ring,
			// mirrored about the outer point beyond it so only the nearer inner
			// point is looked at: the outward facet lies on the inner side of
			// the line from that point out to the outer one.
			var angle = math.atan2(delta.y, delta.x) / (math.PI * 2f) * Sides - (row & 1) * 0.5f;
			var span = (int)math.floor(angle);
			var sideMiddle = SideDirection(row, span) * Radius(row) * math.cos(math.PI / Sides);
			var after = Cross(sideMiddle, delta) > 0f;
			var inner = Point(row, after ? span + 1 : span) - _center;
			// In the last row the nearer point's own line out to the edge is the
			// boundary; before it, the line to the one point beyond the span.
			var outer = (Last(row) ? Edge(after ? span + 1 : span, !after) : Point(row + 1, span + (row & 1))) - _center;
			var test = Cross(outer - inner, delta - inner);
			return test * Cross(outer - inner, sideMiddle - inner) > 0f
				? OutwardSector(row, span)
				: InwardSector(row, after ? span + 1 : span);
		}

		/// <summary>Three points that span facet <paramref name="sector"/>'s plane, with their depths.</summary>
		public (float3 A, float3 B, float3 C) Corners(int sector)
		{
			if (sector == TableSector) {
				return (new float3(_center, TableDepth), new float3(Point(0, 0), TableDepth), new float3(Point(0, 1), TableDepth));
			}
			var row = (sector - 1) / (2 * Sides);
			var rest = (sector - 1) % (2 * Sides);
			if (rest < Sides) {
				// Off a side: a triangle to the one point beyond, or in the last
				// row a trapezoid, whose fourth corner lies in the same plane.
				var beyond = Last(row) ? Edge(rest, true) : Point(row + 1, rest + (row & 1));
				return (new float3(Point(row, rest), RingDepth(row)), new float3(Point(row, rest + 1), RingDepth(row)),
					new float3(beyond, RingDepth(row + 1)));
			}
			var index = rest - Sides;
			var before = Last(row) ? Edge(index, false) : Point(row + 1, index - 1 + (row & 1));
			var next = Last(row) ? Edge(index, true) : Point(row + 1, index + (row & 1));
			return (new float3(Point(row, index), RingDepth(row)),
				new float3(before, RingDepth(row + 1)),
				new float3(next, RingDepth(row + 1)));
		}

		public override float DepthAt(float2 position) => DepthInSector(SectorOf(position), position);

		/// <summary>The depth of facet <paramref name="sector"/>'s plane at <paramref name="position"/>, exact anywhere on that plane.</summary>
		public float DepthInSector(int sector, float2 position)
		{
			var (a, b, c) = Corners(sector);
			var normal = math.cross(b - a, c - a);
			if (math.abs(normal.z) < 1e-12f) {
				return a.z;
			}
			return a.z - (normal.x * (position.x - a.x) + normal.y * (position.y - a.y)) / normal.z;
		}

		public override float3 LocalNormal(int sector)
		{
			var (a, b, c) = Corners(sector);
			float3 la = InsertMeshGenerator.ToLocal(a.xy, a.z);
			float3 lb = InsertMeshGenerator.ToLocal(b.xy, b.z);
			float3 lc = InsertMeshGenerator.ToLocal(c.xy, c.z);
			var tilted = math.normalize(math.cross(lb - la, lc - la));
			return tilted.y < 0f ? -tilted : tilted;
		}

		/// <summary>
		/// Every facet's sides inside the polygon: each ring's sides but the
		/// outermost's, which lie beyond the surface, and the zigzag from every
		/// point of a ring to the two points of the next either side of it.
		/// </summary>
		public override IEnumerable<(float2 From, float2 To, bool FromCenter)> CreasesWithin(IReadOnlyList<float2> polygon, float tolerance)
		{
			for (var ring = 0; ring < Rows; ring++) {
				for (var index = 0; index < Sides; index++) {
					var point = Point(ring, index);
					var before = Last(ring) ? Edge(index, false) : Point(ring + 1, index - 1 + (ring & 1));
					var next = Last(ring) ? Edge(index, true) : Point(ring + 1, index + (ring & 1));
					foreach (var (from, to) in Segments(polygon, tolerance,
						(point, Point(ring, index + 1)), (point, before), (point, next))) {
						yield return (from, to, false);
					}
				}
			}
		}

		private static IEnumerable<(float2 From, float2 To)> Segments(IReadOnlyList<float2> polygon, float tolerance, params (float2 From, float2 To)[] segments)
		{
			foreach (var (from, to) in segments) {
				foreach (var part in SegmentWithin(from, to, polygon, tolerance)) {
					yield return part;
				}
			}
		}

		/// <summary>Away from the centre.</summary>
		public override float2 BridgeDirection(float2 from)
		{
			var away = from - _center;
			return math.lengthsq(away) > 1e-12f ? math.normalize(away) : new float2(1f, 0f);
		}

		/// <summary>
		/// The centre in local X and Z, the number of sides pushed past two
		/// thousand, which tells a gem from a wheel, and the lowest height.
		/// </summary>
		public override Vector4 Optical()
		{
			var lowest = InsertMeshGenerator.ToLocal(_center, _centerDepth - math.abs(_offset));
			return new Vector4(lowest.x, lowest.z, 2000f + Sides, lowest.y);
		}
	}
}
