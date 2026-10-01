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
	/// The crown of a brilliant cut across the raised floor of an insert: a
	/// flat table in the middle, a zigzag of star triangles and bezel kites
	/// round it, and pairs of girdle triangles out to the edge.
	/// </summary>
	/// <remarks>
	/// The table is a regular polygon. From each of its corners a kite runs out
	/// to the girdle, and from each of its sides a triangle, the star facet,
	/// runs out to the junction between the two kites next to it, the point
	/// where the kites and the two girdle triangles beyond them meet. The
	/// girdle lies beyond the surface, so the outermost facets always reach
	/// its edge. A kite is a plane only when its junctions lie on the line from
	/// its table corner down to its girdle point, so the junctions' depth
	/// follows from the table's and the girdle's; every other facet is a
	/// triangle, flat by nature.
	/// </remarks>
	public class InsertGem : InsertRelief
	{
		/// <summary>The fewest sides the table can have, and the most.</summary>
		public const int MinSides = 4;
		public const int MaxSides = 16;

		/// <summary>How far the star facets reach from the table's side towards the girdle, as a share of the way.</summary>
		public const float StarLength = 0.5f;

		private readonly float2 _center;
		private readonly float _centerDepth;
		private readonly float _offset;

		/// <summary>How many sides the table has, and kites round it.</summary>
		public readonly int Sides;

		/// <summary>The radius of the table's corners, in VPX units.</summary>
		public readonly float TableRadius;

		/// <summary>The radius of the girdle, beyond every point of the surface.</summary>
		public readonly float Reach;

		/// <summary>The radius of the junctions where the kites, the star facets and the girdle facets meet.</summary>
		public readonly float JunctionRadius;

		public override float2 Center => _center;
		public override float CenterDepth => _centerDepth;
		public override float Offset => _offset;

		/// <summary>The depth of the table: a plateau the offset above the middle plane.</summary>
		public float TableDepth => _centerDepth + _offset;

		/// <summary>The depth of the girdle, the offset below the middle plane.</summary>
		public float GirdleDepth => _centerDepth - _offset;

		/// <summary>The depth of the junctions, where they lie on the kites' slope.</summary>
		public float JunctionDepth { get; }

		private InsertGem(float2 center, int sides, float tableRadius, float reach, float offset, float centerDepth)
		{
			_center = center;
			Sides = sides;
			TableRadius = tableRadius;
			Reach = reach;
			_offset = offset;
			_centerDepth = centerDepth;
			var apothem = tableRadius * math.cos(math.PI / sides);
			JunctionRadius = math.lerp(apothem, reach, StarLength);
			// Along a kite's axis the junctions sit at their projection onto it.
			var along = JunctionRadius * math.cos(math.PI / sides);
			JunctionDepth = math.lerp(TableDepth, GirdleDepth, (along - tableRadius) / math.max(reach - tableRadius, 1e-6f));
		}

		/// <summary>
		/// A gem round <paramref name="center"/> with a table of
		/// <paramref name="sides"/> sides whose corners lie <paramref name="table"/>
		/// of the way out to the girdle, which sits just beyond every point of
		/// <paramref name="footprint"/>.
		/// </summary>
		/// <remarks>
		/// The table is held small enough that the junctions lie beyond its
		/// corners along the kites' axes, where the kites slope outwards; a
		/// larger table would turn them back up.
		/// </remarks>
		public static InsertGem Cover(IReadOnlyList<float2> footprint, float2 center, int sides, float table, float offset, float centerDepth)
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
			// The girdle facets end on chords between the girdle's points, a side
			// of a polygon of twice the sides, which must clear the surface too.
			reach *= 1.01f / math.cos(math.PI / (2 * sides));
			return new InsertGem(center, sides, reach * math.clamp(table, 0.05f, TableLimit(sides)), reach, offset, centerDepth);
		}

		/// <summary>
		/// The largest share of the girdle's radius the table's corners can
		/// take, so that the junctions still lie beyond them along the kites.
		/// </summary>
		public static float TableLimit(int sides)
		{
			var cos = math.cos(math.PI / math.clamp(sides, MinSides, MaxSides));
			// Along the axis the junction sits at cos * lerp(table * cos, 1, StarLength)
			// of the girdle's radius, which must exceed the table's share.
			return cos * StarLength / (1f - (1f - StarLength) * cos * cos) * 0.95f;
		}

		/// <summary>Unit direction from the centre out through table corner <paramref name="corner"/>.</summary>
		public float2 Direction(int corner)
		{
			var index = (corner % Sides + Sides) % Sides;
			var angle = math.PI * 2f * index / Sides;
			return new float2(math.cos(angle), math.sin(angle));
		}

		/// <summary>Unit direction from the centre out through junction <paramref name="index"/>, between corners <paramref name="index"/> and the next.</summary>
		public float2 Bisector(int index)
		{
			var angle = math.PI * 2f * (index + 0.5f) / Sides;
			return new float2(math.cos(angle), math.sin(angle));
		}

		/// <summary>Table corner <paramref name="corner"/>.</summary>
		public float2 Table(int corner) => _center + Direction(corner) * TableRadius;

		/// <summary>Junction <paramref name="index"/>, between corners <paramref name="index"/> and the next.</summary>
		public float2 Junction(int index) => _center + Bisector(index) * JunctionRadius;

		/// <summary>The girdle point kite <paramref name="corner"/> runs out to.</summary>
		public float2 Girdle(int corner) => _center + Direction(corner) * Reach;

		/// <summary>The girdle point the two girdle facets beyond junction <paramref name="index"/> meet at.</summary>
		public float2 GirdleMiddle(int index) => _center + Bisector(index) * Reach;

		/// <summary>The facet index of the table.</summary>
		public const int TableSector = 0;

		/// <summary>The facet index of the star triangle on the table's side <paramref name="index"/>.</summary>
		public int StarSector(int index) => 1 + (index % Sides + Sides) % Sides;

		/// <summary>The facet index of the kite from table corner <paramref name="corner"/>.</summary>
		public int KiteSector(int corner) => 1 + Sides + (corner % Sides + Sides) % Sides;

		/// <summary>The facet index of the girdle triangle beyond junction <paramref name="index"/>, before or after its middle.</summary>
		public int GirdleSector(int index, bool after) => 1 + 2 * Sides + 2 * ((index % Sides + Sides) % Sides) + (after ? 1 : 0);

		/// <summary>How many facets there are altogether.</summary>
		public int SectorCount => 1 + 4 * Sides;

		private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

		public override int SectorOf(float2 position)
		{
			var delta = position - _center;
			if (math.lengthsq(delta) < InsertOutlineBuilder.Epsilon * InsertOutlineBuilder.Epsilon) {
				return TableSector;
			}
			var angle = math.atan2(delta.y, delta.x);
			if (angle < 0f) {
				angle += math.PI * 2f;
			}
			var index = math.clamp((int)(angle / (math.PI * 2f / Sides)), 0, Sides - 1);
			var bisector = Bisector(index);
			if (math.dot(delta, bisector) <= TableRadius * math.cos(math.PI / Sides)) {
				return TableSector;
			}
			// Mirrored about the junction's line, so only the corner nearer the
			// position is looked at.
			var after = Cross(bisector, delta) > 0f;
			var corner = after ? index + 1 : index;
			var table = Table(corner) - _center;
			var junction = bisector * JunctionRadius;
			var edgeMiddle = bisector * (TableRadius * math.cos(math.PI / Sides));
			// The star triangle lies on the table's side of the line from the
			// corner to the junction, where the middle of the table's side is.
			var side = Cross(junction - table, delta - table);
			if (side * Cross(junction - table, edgeMiddle - table) > 0f) {
				return StarSector(index);
			}
			// The kite lies on the inner side of the line from the junction out
			// to its girdle point, where the table corner is.
			var girdle = Direction(corner) * Reach;
			var beyond = Cross(girdle - junction, delta - junction);
			if (beyond * Cross(girdle - junction, table - junction) > 0f) {
				return KiteSector(corner);
			}
			return GirdleSector(index, after);
		}

		/// <summary>Three points that span facet <paramref name="sector"/>'s plane, with their depths.</summary>
		public (float3 A, float3 B, float3 C) Corners(int sector)
		{
			if (sector == TableSector) {
				return (new float3(_center, TableDepth), new float3(Table(0), TableDepth), new float3(Table(1), TableDepth));
			}
			var kind = math.min((sector - 1) / Sides, 2);
			var index = sector - 1 - kind * Sides;
			switch (kind) {
				case 0:
					return (new float3(Table(index), TableDepth), new float3(Table(index + 1), TableDepth), new float3(Junction(index), JunctionDepth));
				case 1:
					return (new float3(Table(index), TableDepth), new float3(Junction(index), JunctionDepth), new float3(Girdle(index), GirdleDepth));
				default:
					// Two girdle facets a junction, before and after its middle.
					var junction = index / 2;
					var after = index % 2 == 1;
					return (new float3(Junction(junction), JunctionDepth),
						new float3(GirdleMiddle(junction), GirdleDepth),
						new float3(Girdle(after ? junction + 1 : junction), GirdleDepth));
			}
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
		/// Every facet's sides inside the polygon: the table's, the star
		/// triangles' out to the junctions, the kites' out to the girdle, and
		/// the line from each junction to the girdle between its two girdle
		/// facets. The girdle itself lies outside and needs no crease.
		/// </summary>
		public override IEnumerable<(float2 From, float2 To, bool FromCenter)> CreasesWithin(IReadOnlyList<float2> polygon, float tolerance)
		{
			for (var i = 0; i < Sides; i++) {
				var table = Table(i);
				var next = Table(i + 1);
				var junction = Junction(i);
				foreach (var (from, to) in Segments(polygon, tolerance,
					(table, next), (table, junction), (next, junction),
					(junction, Girdle(i)), (junction, Girdle(i + 1)), (junction, GirdleMiddle(i)))) {
					yield return (from, to, false);
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

		/// <summary>Away from the centre, like the kites.</summary>
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
