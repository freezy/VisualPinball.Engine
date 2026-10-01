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
	/// Concentric rings across the raised floor of an insert: a zigzag of
	/// ridges and grooves round a centre, each a circle a half pitch further
	/// out than the last.
	/// </summary>
	/// <remarks>
	/// The strip between two circles at different depths is a cone, and the
	/// generator wants flat facets, so every ring is the same polygon, and
	/// lines from the centre out through the polygons' corners cut the strips
	/// into trapezoids. Two parallel chords at different depths lie in one
	/// plane, so each trapezoid is flat, and neighbours along a ring share a
	/// line from the centre, so the surface is closed. The centre itself is
	/// the innermost ridge, a point, and the first groove a half pitch out.
	/// The trapezoids only stand in for the cone, so they shade as the cone
	/// would: each point's normal leans straight away from or towards the
	/// centre, and nothing folds along the lines out of it.
	/// </remarks>
	public class InsertRings : InsertRelief
	{
		/// <summary>A ring narrower than this is noise.</summary>
		public const float MinWidth = 0.2f;

		/// <summary>The fewest sides a ring's polygon has, and the most.</summary>
		public const int MinSegments = 48;
		public const int MaxSegments = 128;

		private readonly float2 _center;
		private readonly float _centerDepth;
		private readonly float _offset;

		/// <summary>Ridge to ridge, in VPX units.</summary>
		public readonly float Width;

		/// <summary>How many sides every ring's polygon has, and how many lines run out from the centre.</summary>
		public readonly int Segments;

		/// <summary>How many rings there are, the outermost beyond the surface they cover.</summary>
		public readonly int Count;

		/// <summary>How far the lines from the centre run, beyond the outermost ring.</summary>
		public readonly float Reach;

		public override float2 Center => _center;
		public override float CenterDepth => _centerDepth;
		public override float Offset => _offset;

		private InsertRings(float2 center, float width, float offset, float centerDepth, int segments, int count)
		{
			_center = center;
			Width = width;
			_offset = offset;
			_centerDepth = centerDepth;
			Segments = segments;
			Count = count;
			Reach = Apothem(count) / math.cos(math.PI / segments) * 1.01f;
		}

		/// <summary>
		/// Rings round <paramref name="center"/>, <paramref name="width"/> apart
		/// from ridge to ridge, enough of them to cover every point of
		/// <paramref name="footprint"/>, with sides about a pitch and a half long.
		/// </summary>
		public static InsertRings Cover(IReadOnlyList<float2> footprint, float2 center, float width, float offset, float centerDepth)
		{
			if (width < MinWidth) {
				throw new InsertOutlineException($"Rings need a width of at least {MinWidth}.");
			}
			if (footprint.Count == 0) {
				throw new InsertOutlineException("Rings need a surface to cover.");
			}
			var reach = 0f;
			foreach (var point in footprint) {
				reach = math.max(reach, math.distance(point, center));
			}
			if (reach < InsertOutlineBuilder.Epsilon) {
				throw new InsertOutlineException("The surface the rings should cover has no size.");
			}
			var half = width * 0.5f;
			// A ring beyond the surface as well, so the outermost strip has both
			// its creases and a point on the edge is never a borderline case.
			var count = (int)math.ceil(reach / half) + 1;
			// Sides no longer than a pitch on the outermost ring, and never so few
			// that a ridge reads as a polygon.
			var segments = math.clamp((int)math.ceil(2f * math.PI * count * half / width), MinSegments, MaxSegments);
			return new InsertRings(center, width, offset, centerDepth, segments, count);
		}

		/// <summary>The depth of ring <paramref name="index"/>: ridges at even indices, the centre included, grooves at odd.</summary>
		public float RingDepth(int index) => _centerDepth + ((index & 1) == 0 ? _offset : -_offset);

		/// <summary>The radius ring <paramref name="index"/>'s corners sit at.</summary>
		public float Radius(int index) => index * Width * 0.5f;

		/// <summary>How far ring <paramref name="index"/>'s sides lie from the centre.</summary>
		public float Apothem(int index) => Radius(index) * math.cos(math.PI / Segments);

		/// <summary>Unit direction of the line from the centre out through corner <paramref name="corner"/>.</summary>
		public float2 Direction(int corner)
		{
			var index = (corner % Segments + Segments) % Segments;
			var angle = math.PI * 2f * index / Segments;
			return new float2(math.cos(angle), math.sin(angle));
		}

		/// <summary>Unit direction through the middle of angular sector <paramref name="sector"/>, where its chords are nearest the centre.</summary>
		public float2 Bisector(int sector)
		{
			var angle = math.PI * 2f * (sector + 0.5f) / Segments;
			return new float2(math.cos(angle), math.sin(angle));
		}

		/// <summary>Corner <paramref name="corner"/> of ring <paramref name="ring"/>.</summary>
		public float2 Corner(int ring, int corner) => _center + Direction(corner) * Radius(ring);

		/// <summary>Which angular sector <paramref name="position"/> lies in, counting from the first line out of the centre.</summary>
		public int AngularSector(float2 position)
		{
			var delta = position - _center;
			if (math.lengthsq(delta) < InsertOutlineBuilder.Epsilon * InsertOutlineBuilder.Epsilon) {
				return 0;
			}
			var angle = math.atan2(delta.y, delta.x);
			if (angle < 0f) {
				angle += math.PI * 2f;
			}
			return math.clamp((int)(angle / (math.PI * 2f / Segments)), 0, Segments - 1);
		}

		/// <summary>
		/// Which ring's strip <paramref name="position"/> lies in, within its
		/// angular sector: the chords are judged where they are, a side of the
		/// polygon, not the circle through its corners, so a point between the
		/// two belongs to the strip outside the side.
		/// </summary>
		public int Ring(float2 position, int angularSector)
		{
			var along = math.dot(position - _center, Bisector(angularSector));
			var spacing = Width * 0.5f * math.cos(math.PI / Segments);
			return math.clamp((int)math.floor(along / spacing), 0, Count - 1);
		}

		/// <summary>One index for the facet: the ring's strip and the angular sector within it.</summary>
		public override int SectorOf(float2 position)
		{
			var angular = AngularSector(position);
			return Ring(position, angular) * Segments + angular;
		}

		public override float DepthAt(float2 position) => DepthInSector(SectorOf(position), position);

		/// <summary>
		/// The depth of facet <paramref name="sector"/>'s plane at
		/// <paramref name="position"/>: linear across the strip between its two
		/// chords, exact anywhere on that plane. Two strips sharing a chord agree
		/// along it, and two facets sharing a line from the centre agree along
		/// that, since both measure the same distance from it.
		/// </summary>
		public float DepthInSector(int sector, float2 position)
		{
			var ring = sector / Segments;
			var angular = sector % Segments;
			var along = math.dot(position - _center, Bisector(angular));
			var t = (along - Apothem(ring)) / math.max(Apothem(ring + 1) - Apothem(ring), 1e-9f);
			return math.lerp(RingDepth(ring), RingDepth(ring + 1), t);
		}

		public override float3 LocalNormal(int sector)
		{
			var ring = sector / Segments;
			var angular = sector % Segments;
			float3 a = InsertMeshGenerator.ToLocal(Corner(ring + 1, angular), RingDepth(ring + 1));
			float3 b = InsertMeshGenerator.ToLocal(Corner(ring + 1, angular + 1), RingDepth(ring + 1));
			float3 c = ring == 0
				? InsertMeshGenerator.ToLocal(_center, RingDepth(0))
				: InsertMeshGenerator.ToLocal(Corner(ring, angular), RingDepth(ring));
			var tilted = math.normalize(math.cross(b - a, c - a));
			return tilted.y < 0f ? -tilted : tilted;
		}

		/// <summary>
		/// The cone's normal at <paramref name="position"/>: the facet's lean,
		/// which is along its sector's bisector, turned to point straight out
		/// from the centre through the position. Along a line out of the centre
		/// both facets lean the same way, so the shading runs across it, and at
		/// the centre itself, where there is no such direction, the facet's own.
		/// </summary>
		public override float3 LocalNormal(int sector, float2 position)
		{
			var planar = LocalNormal(sector);
			var origin = (float3)InsertMeshGenerator.ToLocal(_center, 0f);
			var radial = ((float3)InsertMeshGenerator.ToLocal(position, 0f) - origin).xz;
			if (math.lengthsq(radial) < 1e-12f) {
				return planar;
			}
			var bisector = ((float3)InsertMeshGenerator.ToLocal(_center + Bisector(sector % Segments), 0f) - origin).xz;
			var lean = math.dot(planar.xz, math.normalize(bisector));
			radial = math.normalize(radial) * lean;
			return math.normalize(new float3(radial.x, planar.y, radial.y));
		}

		/// <summary>
		/// Every ring's sides and every line out from the centre, the parts of
		/// them inside the polygon. The lines are not creases, the surface does
		/// not fold along them, but the face walk needs them to cut each strip
		/// into its flat trapezoids.
		/// </summary>
		public override IEnumerable<(float2 From, float2 To, bool FromCenter)> CreasesWithin(IReadOnlyList<float2> polygon, float tolerance)
		{
			for (var ring = 1; ring <= Count; ring++) {
				for (var corner = 0; corner < Segments; corner++) {
					var from = Corner(ring, corner);
					var to = Corner(ring, corner + 1);
					foreach (var part in SegmentWithin(from, to, polygon, tolerance)) {
						yield return (part.From, part.To, false);
					}
				}
			}
			for (var corner = 0; corner < Segments; corner++) {
				foreach (var part in RayWithin(_center, Direction(corner), Reach, polygon, tolerance)) {
					yield return part;
				}
			}
		}

		/// <summary>Away from the centre: a line that way crosses no line out of it.</summary>
		public override float2 BridgeDirection(float2 from)
		{
			var away = from - _center;
			return math.lengthsq(away) > 1e-12f ? math.normalize(away) : new float2(1f, 0f);
		}

		/// <summary>
		/// The centre in local X and Z, the half pitch in metres made negative
		/// and pushed past minus a thousand, which tells rings from ribs and
		/// from a wheel, and the lowest height.
		/// </summary>
		public override Vector4 Optical()
		{
			var lowest = InsertMeshGenerator.ToLocal(_center, _centerDepth - math.abs(_offset));
			var halfPitch = (InsertMeshGenerator.ToLocal(_center + new float2(Width * 0.5f, 0f), 0f) - InsertMeshGenerator.ToLocal(_center, 0f)).magnitude;
			return new Vector4(lowest.x, lowest.z, -1000f - halfPitch, lowest.y);
		}
	}
}
