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
using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Everything about an insert body that is not the shape of its outline.
	/// </summary>
	[Serializable]
	public struct InsertBody
	{
		/// <summary>How far the bucket is sunk below the rim.</summary>
		public float Depth;

		/// <summary>How far the floor is inset before being raised again.</summary>
		public float ChannelWidth;

		/// <summary>How far the inset floor is raised, leaving a channel around it.</summary>
		public float ChannelDepth;

		/// <summary>Where the starburst's centre sits, relative to the insert's origin.</summary>
		public float2 StarburstCenter;

		/// <summary>
		/// How many bursts the whole wheel has, whether or not they all land on the
		/// insert. Below <see cref="InsertStarburst.MinBursts"/> there is no wheel
		/// and the raised floor stays flat.
		/// </summary>
		public int StarburstBursts;

		/// <summary>How far the starburst's rim rises above and drops below its centre.</summary>
		public float StarburstOffset;

		/// <summary>
		/// Round cut-outs through the raised floor, down to the channel's floor.
		/// Without a channel there is no raised floor to cut, and they are ignored.
		/// </summary>
		public InsertHole[] Holes;

		/// <summary>How many straight sides each hole's circle is drawn with.</summary>
		public int HoleSegments;

		/// <summary>
		/// How far the rounding where the outer wall meets the channel's floor
		/// reaches, both up the wall and in across the floor. Without a channel
		/// there is no bevel: the floor is the outline itself, straight down.
		/// </summary>
		public float BottomBevel;

		/// <summary>
		/// How many steps the bottom bevel is drawn with. One is a flat chamfer.
		/// </summary>
		public int BottomBevelSegments;

		/// <summary>
		/// A line engraved into the raised middle's surface, in the starburst's
		/// place: the two never share a surface. Null for none.
		/// </summary>
		public InsertGroove Groove;

		/// <summary>
		/// How far the starburst may rise and fall about its centre's plane. With a
		/// channel, it stays between the channel floor and the rim. Without one,
		/// it stays below the rim.
		/// </summary>
		public float StarburstLimit()
			=> ChannelWidth > InsertOutlineBuilder.Epsilon ? InsertStarburst.OffsetLimit(Depth, ChannelDepth) : math.max(0f, Depth);
	}

	/// <summary>
	/// Builds the insert mesh from a resolved outline.
	/// </summary>
	/// <remarks>
	/// The outline is a VPX space profile, so it lands in the world XZ plane, flat
	/// in the playfield at a rotation of identity, with a shape's +Y pointing up
	/// the table.
	/// <para>
	/// The body is a lidless bucket whose floor is inset and raised back up, which
	/// leaves a channel running round the bottom edge for step 5 to bevel. Every
	/// face is turned inwards, because an insert is only ever looked into, never
	/// at: the outer wall faces the middle, the raised floor's wall faces back out
	/// at it, and both horizontal surfaces face up.
	/// </para>
	/// <para>
	/// The UVs are one atlas without overlaps, at the same density everywhere,
	/// so a map can be baked onto it: the raised middle and the floor below are
	/// each projected from above, the outer wall and its bevel are unrolled
	/// together as a band, cut into pieces that pack, each side of the raised
	/// middle is its own chart, and each hole's wall is a band of its own. Every
	/// chart reads the right way round from the side it faces.
	/// </para>
	/// </remarks>
	public static class InsertMeshGenerator
	{
		/// <summary>
		/// Half a turn about the up axis, so a shape's +Y comes out pointing up the
		/// table instead of at the flippers. Spelled out rather than built from a
		/// trig helper, because sin(pi) is not quite zero in single precision and
		/// these axes should stay exact.
		/// </summary>
		private static readonly float3x3 HalfTurn = new(
			new float3(-1f, 0f, 0f),
			new float3(0f, 1f, 0f),
			new float3(0f, 0f, -1f));

		/// <summary>
		/// VPX space to the object's local space: the engine's own basis, turned to
		/// face up the table. Only the basis is taken, never the table translation
		/// that <see cref="Physics.VpxToWorld"/> also carries, which a local mesh
		/// must not inherit.
		/// </summary>
		private static readonly float3x3 VpxToLocal = math.mul(HalfTurn, new float3x3(
			Physics.VpxToWorld.c0.xyz, Physics.VpxToWorld.c1.xyz, Physics.VpxToWorld.c2.xyz));

		/// <summary>
		/// Converts an outline point, which is a VPX space profile, into the
		/// object's local space. Everything that draws the outline goes through
		/// here, so the mesh and the scene-view gizmo cannot drift apart.
		/// </summary>
		/// <remarks>
		/// The outline spans VPX X and Y, which is the playfield plane, and
		/// <paramref name="depth"/> is VPX Z, which is height. The bucket is dug
		/// downwards, so its floor sits at a negative depth.
		/// </remarks>
		public static Vector3 ToLocal(float2 position, float depth = 0f)
		{
			return math.mul(VpxToLocal, new float3(position.x, position.y, depth));
		}

		/// <summary>
		/// The inverse of <see cref="ToLocal"/>: where a local space point lies on
		/// the outline's plane, whatever its height.
		/// </summary>
		public static float2 FromLocal(Vector3 local) => math.mul(LocalToVpx, (float3)local).xy;

		private static readonly float3x3 LocalToVpx = math.inverse(VpxToLocal);

		/// <summary>
		/// Turns an outline normal, which points out of the shape, into a local
		/// space direction, either into the hollow or back out of it.
		/// </summary>
		public static Vector3 ToLocalNormal(float2 outward, bool inwards)
		{
			var sign = inwards ? -1f : 1f;
			return math.normalize(math.mul(VpxToLocal, new float3(sign * outward.x, sign * outward.y, 0f)));
		}

		/// <summary>
		/// One sample of a ring: where it sits, which way the segments either side
		/// of it face, and how far round it has travelled.
		/// </summary>
		/// <remarks>
		/// The two normals differ only where the ring turns a hard corner, which is
		/// the one place a wall must not share vertices between its segments.
		/// </remarks>
		private struct RingPoint
		{
			public float2 Position;

			public float2 NormalIn;
			public float2 NormalOut;
			public float Distance;
		}

		/// <summary>
		/// The mesh being built, with each vertex's UV chart alongside.
		/// </summary>
		/// <remarks>
		/// UVs are written in VPX units, laid out within each chart, and only
		/// packed into the unit square once the whole mesh is there.
		/// </remarks>
		private sealed class MeshBuffers
		{
			public readonly List<Vector3> Vertices = new();
			public readonly List<Vector3> Normals = new();
			public readonly List<Vector2> Uvs = new();
			public readonly List<int> Charts = new();
			public readonly List<int> Triangles = new();
			private int _chartCount;

			public int NewChart() => _chartCount++;

			public int Add(Vector3 position, Vector3 normal, Vector2 uv, int chart)
			{
				Vertices.Add(position);
				Normals.Add(normal);
				Uvs.Add(uv);
				Charts.Add(chart);
				return Vertices.Count - 1;
			}

			public void Triangle(int a, int b, int c)
			{
				Triangles.Add(a);
				Triangles.Add(b);
				Triangles.Add(c);
			}
		}

		/// <summary>
		/// Builds the mesh into <paramref name="mesh"/>, and describes it for the
		/// normal bake into <paramref name="layout"/> when one is given.
		/// </summary>
		public static Mesh Generate(InsertOutline outline, InsertBody body, Mesh mesh, InsertMeshLayout layout = null)
		{
			var buffers = new MeshBuffers();
			if (layout != null) {
				layout.OpticalStarburst = Vector4.zero;
			}
			var floor = -body.Depth;
			var rim = BuildRing(outline, 0f, out var rimMap);
			var channel = body.ChannelWidth > InsertOutlineBuilder.Epsilon;
			int[] islandMap = null;
			var island = channel ? BuildRing(outline, body.ChannelWidth, out islandMap) : null;
			// Without a channel the floor is the outline itself, straight down,
			// flat or with the starburst on it; holes need a raised floor to cut
			// through, as the bevel needs a channel's floor to round into. With
			// nothing to carve, it is a plain cap.
			var carved = channel || body.StarburstBursts >= InsertStarburst.MinBursts || body.Groove != null;
			if (!channel) {
				body.Holes = null;
			}

			// Projected from above, the floor and the raised middle would land on
			// each other, so each gets a chart. The outer wall is unrolled into a
			// band as long as the rim, which is cut into pieces that each fit the
			// square the rest will roughly need.
			var top = buffers.NewChart();
			var bottomChart = buffers.NewChart();
			var atlasSide = AtlasSide(outline, body, rim, island);
			var outerCharts = CutBand(rim, atlasSide, buffers);

			// The bottom bevel rounds off where the outer wall meets the channel's
			// floor. The wall stops where it starts, and the floor starts where it
			// ends, at the outline taken in by its width. Without a channel there is
			// none: the wall meets the floor square.
			var bevel = channel && body.BottomBevel > InsertOutlineBuilder.Epsilon ? body.BottomBevel : 0f;
			var bottom = rim;
			var bottomMap = rimMap;
			if (bevel > 0f) {
				bottom = BuildRing(outline, bevel, out bottomMap);
				AddOuterWall(rim, floor + bevel, outerCharts, buffers);
			}

			List<RunPoint>[] floorSides = null;
			int[] floorCharts = null;
			InsertStarburst starburst = null;
			InsertFlatIsland flatIsland = null;
			var surface = channel ? floor + body.ChannelDepth : floor;
			if (carved) {
				starburst = TryCoverWithStarburst(island ?? bottom, body, surface, outline.WeldTolerance);
				if (layout != null && starburst != null) {
					var center = ToLocal(starburst.Center, starburst.CenterDepth - starburst.Offset);
					layout.OpticalStarburst = new Vector4(center.x, center.z, starburst.Count, center.y);
				}
				floorCharts = CutFloor(bottom, atlasSide * 0.5f, buffers);
				floorSides = AddSunkenFloor(outline, body, bottom, bottomMap, bevel > 0f, island, islandMap, starburst,
					floor, surface, top, floorCharts, outerCharts, buffers, out flatIsland);

			} else {
				if (bevel <= 0f) {
					AddOuterWall(rim, floor, outerCharts, buffers);
				}
				AddCap(bottom, floor, bottomChart, buffers);
			}

			if (bevel > 0f) {
				AddBottomBevel(outline, bevel, math.max(1, body.BottomBevelSegments), floor, rim, bottom, bottomMap,
					floorSides, outerCharts, buffers);
			}

			InsertUvAtlas.Pack(buffers.Uvs, buffers.Charts);

			if (layout != null) {
				layout.VertexCharts = buffers.Charts.ToArray();
				layout.FlatChart = carved ? top : -1;
				var flatTriangles = new List<int>();
				if (carved) {
					for (var t = 0; t < buffers.Triangles.Count / 3; t++) {
						if (buffers.Charts[buffers.Triangles[t * 3]] == top) {
							flatTriangles.Add(t);
						}
					}
				}
				layout.FlatTriangles = flatTriangles.ToArray();
				// A line's trough is geometry for the bake alone: the middle keeps
				// its projection through the bake, so the plain island the insert
				// shows once baked can carry the map.
				layout.FlatProjected = flatIsland != null;
				layout.FlatIsland = flatIsland;
				// Everything that is not a floor, laid out from above, is a wall.
				var floors = new HashSet<int> { top, bottomChart };
				if (floorCharts != null) {
					floors.UnionWith(floorCharts);
				}
				var walls = new HashSet<int>();
				foreach (var chart in buffers.Charts) {
					if (!floors.Contains(chart)) {
						walls.Add(chart);
					}
				}
				layout.WallCharts = new int[walls.Count];
				walls.CopyTo(layout.WallCharts);
				layout.FlatHeight = ToLocal(float2.zero, surface).y;
				var hash = new Hash128();
				hash.Append(buffers.Vertices);
				hash.Append(buffers.Normals);
				hash.Append(buffers.Uvs);
				hash.Append(buffers.Triangles);
				layout.Hash = hash;
			}

			// The same mesh may previously have held Blender's baked tangents.
			// Clear the vertex layout too, then populate every shading attribute:
			// retaining an unwritten tangent stream can expose invalid values to HDRP.
			mesh.Clear(false);
			mesh.indexFormat = buffers.Vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.subMeshCount = 1;
			mesh.SetVertices(buffers.Vertices);
			mesh.SetNormals(buffers.Normals);
			mesh.SetUVs(0, buffers.Uvs);
			mesh.SetTriangles(buffers.Triangles, 0);
			mesh.RecalculateTangents();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>
		/// Appends a flat clear cover at the rim as submesh 1. The body's vertices,
		/// UVs and tangents are retained exactly, including any applied bake.
		/// </summary>
		public static void AddCover(InsertOutline outline, Mesh mesh)
		{
			var vertices = new List<Vector3>(mesh.vertices);
			var normals = new List<Vector3>(mesh.normals);
			var uvs = new List<Vector2>(mesh.uv);
			var tangents = new List<Vector4>(mesh.tangents);
			var bodyTriangles = mesh.GetTriangles(0);
			var first = vertices.Count;
			var polygon = new List<double2>(outline.Points.Count);
			var size = mesh.bounds.size;
			foreach (var point in outline.Points) {
				var position = ToLocal(point.Position);
				vertices.Add(position);
				normals.Add(Vector3.up);
				uvs.Add(new Vector2((position.x - mesh.bounds.min.x) / size.x, (position.z - mesh.bounds.min.z) / size.z));
				// UV X/Y run along local X/Z: cross(up, right) is -Z.
				tangents.Add(new Vector4(1f, 0f, 0f, -1f));
				polygon.Add(new double2(point.Position.x, point.Position.y));
			}
			var triangles = new List<int>();
			foreach (var triangle in InsertTriangulation.EarClip(polygon)) {
				triangles.Add(first + triangle.x);
				triangles.Add(first + triangle.y);
				triangles.Add(first + triangle.z);
			}
			mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTangents(tangents);
			mesh.subMeshCount = 2;
			mesh.SetTriangles(bodyTriangles, 0);
			mesh.SetTriangles(triangles, 1);
			mesh.RecalculateBounds();
		}

		/// <summary>
		/// Roughly how many VPX units the atlas's side will span, from the area
		/// its charts will take up: the floor and the middle from above, and the
		/// walls unrolled.
		/// </summary>
		private static float AtlasSide(InsertOutline outline, InsertBody body, List<RingPoint> rim, List<RingPoint> island)
		{
			var area = 2f * outline.Width * outline.Height + Perimeter(rim) * body.Depth;
			if (island != null) {
				area += Perimeter(island) * body.ChannelDepth * 1.5f;
			}
			if (island != null && body.Holes != null) {
				foreach (var hole in body.Holes) {
					area += 2f * math.PI * hole.Radius * body.ChannelDepth;
				}
			}
			return math.sqrt(area * 1.3f);
		}

		private static float Perimeter(List<RingPoint> ring)
			=> ring.Count == 0 ? 0f : ring[^1].Distance + math.distance(ring[^1].Position, ring[0].Position);

		/// <summary>
		/// A chart for each side of the rim, the same one along a run of sides
		/// until it grows longer than <paramref name="length"/>.
		/// </summary>
		private static int[] CutBand(List<RingPoint> rim, float length, MeshBuffers buffers)
		{
			var charts = new int[rim.Count];
			var chart = buffers.NewChart();
			var run = 0f;
			for (var i = 0; i < rim.Count; i++) {
				var side = math.distance(rim[i].Position, rim[(i + 1) % rim.Count].Position);
				if (run > 0f && run + side > length) {
					chart = buffers.NewChart();
					run = 0f;
				}
				charts[i] = chart;
				run += side;
			}
			return charts;
		}

		/// <summary>
		/// A chart for each side of the channel floor's outer edge, the same one
		/// along a run of sides until it turns further than <see cref="FloorTurn"/>
		/// or grows longer than <paramref name="length"/>.
		/// </summary>
		/// <remarks>
		/// The floor is laid out from above, so a piece keeps its bend in UV, and
		/// the whole ring would take up the square it goes round. Pieces that are
		/// nearly straight pack tightly.
		/// </remarks>
		private static int[] CutFloor(List<RingPoint> ring, float length, MeshBuffers buffers)
		{
			var charts = new int[ring.Count];
			var chart = buffers.NewChart();
			var run = 0f;
			var turned = 0f;
			var heading = float2.zero;
			for (var i = 0; i < ring.Count; i++) {
				var direction = ring[(i + 1) % ring.Count].Position - ring[i].Position;
				var side = math.length(direction);
				if (side > 0f) {
					direction /= side;
					if (math.any(heading != float2.zero)) {
						turned += math.abs(math.atan2(heading.x * direction.y - heading.y * direction.x, math.dot(heading, direction)));
					}
					heading = direction;
				}
				if (run > 0f && (run + side > length || turned > FloorTurn)) {
					chart = buffers.NewChart();
					run = 0f;
					turned = 0f;
				}
				charts[i] = chart;
				run += side;
			}
			return charts;
		}

		/// <summary>How far a piece of the channel floor may turn before the next one starts.</summary>
		private const float FloorTurn = math.PI / 3f;

		/// <summary>The side of a ring closest to a point.</summary>
		private static int NearestSide(List<RingPoint> ring, float2 point)
		{
			var nearest = 0;
			var best = float.MaxValue;
			for (var i = 0; i < ring.Count; i++) {
				var a = ring[i].Position;
				var ab = ring[(i + 1) % ring.Count].Position - a;
				var t = math.saturate(math.dot(point - a, ab) / math.max(math.lengthsq(ab), 1e-12f));
				var distance = math.distancesq(point, a + t * ab);
				if (distance < best) {
					best = distance;
					nearest = i;
				}
			}
			return nearest;
		}

		/// <summary>
		/// Where a point along the outer band sits in U: running clockwise, so the
		/// band reads left to right from inside the insert, which is where it is
		/// seen from.
		/// </summary>
		private static float BandU(List<RingPoint> rim, int side, float t)
			=> -(rim[side].Distance + t * math.distance(rim[side].Position, rim[(side + 1) % rim.Count].Position));

		/// <summary>
		/// The outline offset inwards by <paramref name="inset"/> and welded.
		/// </summary>
		/// <remarks>
		/// Points are shared between neighbouring wall segments wherever the ring
		/// is smooth. Because fillets leave their sides tangentially, that includes
		/// the join from a straight run into an arc, which needs no special casing
		/// at all. A sharp corner is the exception: its two sides carry genuinely
		/// different normals, so it keeps them both and stays crisp.
		/// <para>
		/// At an inset wide enough to erode a fillet away, every point of that
		/// fillet lands on one mitre and is welded into a single entry. Taking the
		/// first arrival normal and the last departure normal turns that entry into
		/// exactly the hard corner the erosion created.
		/// </para>
		/// <paramref name="map"/> comes back pointing each outline point at the ring
		/// entry it ended up in, so two rings can be tied together point for point.
		/// </remarks>
		private static List<RingPoint> BuildRing(InsertOutline outline, float inset, out int[] map)
		{
			var count = outline.Points.Count;
			var weld = outline.WeldTolerance;
			var ring = new List<RingPoint>(count);
			map = new int[count];

			for (var i = 0; i < count; i++) {
				var point = outline.Points[i];
				var sharp = point.Kind == InsertOutlinePointKind.Sharp;
				var corner = sharp ? outline.Corners[point.Corner] : null;
				var normalIn = sharp ? corner.NormalIn : point.Normal;
				var normalOut = sharp ? corner.NormalOut : point.Normal;
				var position = outline.OffsetPoint(i, inset);

				if (ring.Count > 0 && math.distance(position, ring[^1].Position) < weld) {
					var merged = ring[^1];
					merged.NormalOut = normalOut;
					ring[^1] = merged;
					map[i] = ring.Count - 1;
					continue;
				}

				map[i] = ring.Count;
				ring.Add(new RingPoint { Position = position, NormalIn = normalIn, NormalOut = normalOut });
			}

			// The ring closes, so its ends may have landed on each other too.
			if (ring.Count > 3 && math.distance(ring[^1].Position, ring[0].Position) < weld) {
				var first = ring[0];
				first.NormalIn = ring[^1].NormalIn;
				ring[0] = first;
				for (var i = 0; i < count; i++) {
					if (map[i] == ring.Count - 1) {
						map[i] = 0;
					}
				}
				ring.RemoveAt(ring.Count - 1);
			}

			Measure(ring);
			return ring;
		}

		/// <summary>
		/// The outer wall, from the rim down to <paramref name="bottom"/>, facing
		/// into the hollow.
		/// </summary>
		/// <remarks>
		/// Neighbouring sides share their vertices wherever the ring is smooth and
		/// the band carries on in the same chart, so the wall shades round a fillet
		/// without a crease. A sharp corner keeps a vertex per side, as does a cut
		/// between two pieces of the band, which lay the same point out in two
		/// places.
		/// </remarks>
		private static void AddOuterWall(List<RingPoint> rim, float bottom, int[] charts, MeshBuffers buffers)
		{
			var count = rim.Count;
			int previousTop = -1, previousBottom = -1;
			for (var i = 0; i < count; i++) {
				var next = (i + 1) % count;
				var chart = charts[i];
				var entry = rim[i];

				int topFrom, bottomFrom;
				var smooth = math.distance(entry.NormalIn, entry.NormalOut) <= InsertOutlineBuilder.AngleEpsilon;
				if (i > 0 && smooth && charts[i - 1] == chart) {
					topFrom = previousTop;
					bottomFrom = previousBottom;
				} else {
					var normal = ToLocalNormal(entry.NormalOut, true);
					var u = BandU(rim, i, 0f);
					topFrom = buffers.Add(ToLocal(entry.Position, 0f), normal, new Vector2(u, 0f), chart);
					bottomFrom = buffers.Add(ToLocal(entry.Position, bottom), normal, new Vector2(u, bottom), chart);
				}

				var arriving = ToLocalNormal(rim[next].NormalIn, true);
				var end = BandU(rim, i, 1f);
				var topTo = buffers.Add(ToLocal(rim[next].Position, 0f), arriving, new Vector2(end, 0f), chart);
				var bottomTo = buffers.Add(ToLocal(rim[next].Position, bottom), arriving, new Vector2(end, bottom), chart);

				// Along the top, then down the far side: that order faces it in.
				buffers.Triangle(topFrom, topTo, bottomTo);
				buffers.Triangle(topFrom, bottomTo, bottomFrom);
				previousTop = topTo;
				previousBottom = bottomTo;
			}
		}

		/// <summary>
		/// A horizontal surface filling a ring, facing up into the hollow.
		/// </summary>
		/// <remarks>
		/// Ear clipped rather than fanned from the middle, so a ring that bends in
		/// on itself, a banana say, is filled as it is. The outline runs
		/// counter-clockwise in VPX space, and the VPX to world basis flips Y into
		/// -Z, so a triangle in ring order comes out facing +Y without needing to
		/// be reversed. The half turn is a proper rotation and leaves that alone.
		/// </remarks>
		private static void AddCap(List<RingPoint> ring, float depth, int chart, MeshBuffers buffers)
		{
			var start = buffers.Vertices.Count;
			var polygon = new List<double2>(ring.Count);
			foreach (var entry in ring) {
				buffers.Add(ToLocal(entry.Position, depth), Vector3.up, (Vector2)entry.Position, chart);
				polygon.Add(entry.Position);
			}
			foreach (var piece in InsertTriangulation.EarClip(polygon)) {
				buffers.Triangle(start + piece.x, start + piece.y, start + piece.z);
			}
		}

		/// <summary>
		/// A starburst grown to cover <paramref name="ring"/>, or null when the
		/// body asks for too few bursts to make one.
		/// </summary>
		private static InsertStarburst TryCoverWithStarburst(List<RingPoint> ring, InsertBody body, float centerDepth,
			float weld)
		{
			if (body.StarburstBursts < InsertStarburst.MinBursts) {
				return null;
			}

			var footprint = new List<float2>(ring.Count);
			foreach (var entry in ring) {
				footprint.Add(entry.Position);
			}

			// Held to the bucket here as well as in the component, so any caller
			// gets a surface that neither pokes through the rim nor digs below the
			// channel floor, where its wall would fold through itself.
			// Its size is what is held, not its value: a negative offset only swaps
			// which spokes are ridges, and reaches just as far either way.
			var limit = body.StarburstLimit();
			var offset = math.clamp(body.StarburstOffset, -limit, limit);

			// A centre a hair inside the edge sends every spoke on the far side
			// out through it again a hair later, leaving a fan of faces nanometres
			// tall. Put it on the edge instead, which the arrangement handles like
			// any other point on the ring. The snap is kept to a few welds: moving
			// the centre also slides where a shallow spoke enters, by the move over
			// the tangent of its angle, so anything larger shows.
			var size = 0f;
			foreach (var point in footprint) {
				size = math.max(size, math.cmax(math.abs(point)));
			}
			var center = SnapToOutline(footprint, body.StarburstCenter, math.max(weld * 4f, size * 1e-5f));

			return InsertStarburst.Cover(footprint, center, body.StarburstBursts, offset, centerDepth);
		}

		/// <summary>
		/// <paramref name="point"/>, moved onto the nearest point of the polygon's
		/// outline if it is within <paramref name="snap"/> of it.
		/// </summary>
		private static float2 SnapToOutline(IReadOnlyList<float2> polygon, float2 point, float snap)
		{
			var nearest = point;
			var best = float.MaxValue;
			for (var i = 0; i < polygon.Count; i++) {
				var from = polygon[i];
				var edge = polygon[(i + 1) % polygon.Count] - from;
				var length = math.lengthsq(edge);
				var t = length > 0f ? math.saturate(math.dot(point - from, edge) / length) : 0f;
				var candidate = from + edge * t;
				var distance = math.distance(point, candidate);
				if (distance < best) {
					best = distance;
					nearest = candidate;
				}
			}
			return best <= snap ? nearest : point;
		}

		/// <summary>
		/// A point along one side of a ring, as a bevel meets it.
		/// </summary>
		private readonly struct RunPoint
		{
			public readonly float2 Position;
			public readonly double2 Exact;

			/// <summary>How far along its side it lies, 0 to 1.</summary>
			public readonly float T;

			public RunPoint(float2 position, double2 exact, float t)
			{
				Position = position;
				Exact = exact;
				T = t;
			}
		}

		/// <summary>
		/// The rounding where the outer wall meets the floor: a quarter circle in
		/// profile, <paramref name="width"/> up the wall and in across the floor.
		/// </summary>
		/// <remarks>
		/// Each step of the profile is the outline itself, taken in and dropped
		/// down by that step's share of the arc, so every step has the outline's
		/// shape: a fillet stays round and a sharp corner mitres, like a picture
		/// frame. Neighbouring steps are joined one outline edge at a time, cell by
		/// cell, as the channel's floor is.
		/// <para>
		/// The top step is the rim, where the wall stops. The bottom step is where
		/// the floor begins, and where there is a channel, the floor there came out
		/// of an arrangement, so <paramref name="floorSides"/> hands over every point
		/// the arrangement put along it, and the bevel meets those, not the ring
		/// it started from.
		/// </para>
		/// <para>
		/// With a single segment, the bevel is a flat chamfer and shades as one,
		/// crisp against the wall and the floor. With more, it is round, and its
		/// normals follow the arc, from the wall's at the top to straight up at the
		/// bottom, so it runs into both without a crease.
		/// </para>
		/// </remarks>
		/// <para>
		/// In UV, the bevel carries on down the outer wall's band, each step as far
		/// down it as the arc has run.
		/// </para>
		private static void AddBottomBevel(InsertOutline outline, float width, int segments, float floor,
			List<RingPoint> rim, List<RingPoint> bottom, int[] bottomMap, List<RunPoint>[] floorSides, int[] charts,
			MeshBuffers buffers)
		{
			// Each outline point's outward normal, arriving and leaving, which only
			// a sharp corner tells apart.
			var count = outline.Points.Count;
			var normalIn = new float2[count];
			var normalOut = new float2[count];
			for (var i = 0; i < count; i++) {
				var point = outline.Points[i];
				var sharp = point.Kind == InsertOutlinePointKind.Sharp;
				normalIn[i] = sharp ? outline.Corners[point.Corner].NormalIn : point.Normal;
				normalOut[i] = sharp ? outline.Corners[point.Corner].NormalOut : point.Normal;
			}

			var steps = new (List<RingPoint> Ring, int[] Map, float Height, float Angle)[segments + 1];
			for (var k = 0; k <= segments; k++) {
				var angle = 0.5f * math.PI * k / segments;
				var inset = width * (1f - math.cos(angle));
				var height = floor + width * (1f - math.sin(angle));
				if (k == segments) {
					steps[k] = (bottom, bottomMap, floor, angle);
				} else {
					var ring = BuildRing(outline, inset, out var map);
					steps[k] = (ring, map, height, angle);
				}
			}

			// How far down the band each step sits: the wall's foot, less the arc
			// run so far, step by step as it is drawn.
			var chord = 2f * width * math.sin(math.PI / (4f * segments));

			var cell = new List<double2>();
			var points = new List<(float2 Position, float Angle, float Along)>();
			for (var k = 0; k < segments; k++) {
				var upper = steps[k];
				var lower = steps[k + 1];
				var upperV = floor + width - k * chord;
				var lowerV = floor + width - (k + 1) * chord;

				// A chamfer is one flat face; a round bevel follows its arc.
				var upperAngle = segments == 1 ? math.PI * 0.25f : upper.Angle;
				var lowerAngle = segments == 1 ? math.PI * 0.25f : lower.Angle;

				for (var i = 0; i < count; i++) {
					var next = (i + 1) % count;
					cell.Clear();
					points.Clear();

					var top = Run(upper.Ring, upper.Map, i, null);
					foreach (var point in top) {
						cell.Add(point.Exact);
						points.Add((point.Position, upper.Angle, point.T));
					}
					var base_ = Run(lower.Ring, lower.Map, i, k + 1 == segments ? floorSides : null);
					for (var j = base_.Count - 1; j >= 0; j--) {
						cell.Add(base_[j].Exact);
						points.Add((base_[j].Position, lower.Angle, base_[j].T));
					}
					if (cell.Count < 3) {
						continue;
					}

					var first = buffers.Vertices.Count;
					for (var j = 0; j < points.Count; j++) {
						var (position, stepAngle, along) = points[j];
						var outward = math.normalizesafe(math.lerp(normalOut[i], normalIn[next], along), normalOut[i]);
						var upperRow = j < top.Count;
						var angle = upperRow ? upperAngle : lowerAngle;
						var height = floor + width * (1f - math.sin(stepAngle));
						var normal = math.normalize(math.mul(VpxToLocal, new float3(-outward * math.cos(angle), math.sin(angle))));
						var uv = new Vector2(BandU(rim, i, along), upperRow ? upperV : lowerV);
						buffers.Add(ToLocal(position, height), normal, uv, charts[i]);
					}
					foreach (var piece in InsertTriangulation.EarClip(cell)) {
						buffers.Triangle(first + piece.x, first + piece.y, first + piece.z);
					}
				}
			}
		}

		/// <summary>
		/// The points of a ring from outline point <paramref name="index"/> to the
		/// next one, each with how far along that outline edge it lies.
		/// </summary>
		/// <param name="sides">Where given, every point the arrangement put along each
		/// side of the ring, which then stand in for the ring's own.</param>
		private static List<RunPoint> Run(List<RingPoint> ring, int[] map, int index, List<RunPoint>[] sides)
		{
			var from = map[index];
			var to = map[(index + 1) % map.Length];
			var run = new List<RunPoint>();
			if (sides == null) {
				run.Add(new RunPoint(ring[from].Position, ring[from].Position, 0f));
				if (to != from) {
					run.Add(new RunPoint(ring[to].Position, ring[to].Position, 1f));
				}
				return run;
			}

			if (to == from) {
				var point = sides[from][0];
				run.Add(new RunPoint(point.Position, point.Exact, 0f));
				return run;
			}
			foreach (var point in sides[from]) {
				if (run.Count == 0 || !run[^1].Exact.Equals(point.Exact)) {
					run.Add(point);
				}
			}
			return run;
		}

		/// <summary>
		/// Which part of the sunken floor a face of its arrangement is. A wall
		/// stands wherever two faces of different tiers meet.
		/// </summary>
		private enum Tier
		{
			/// <summary>The channel's floor, or anywhere else down at the bottom.</summary>
			Floor,

			/// <summary>The raised middle, flat or starburst.</summary>
			Surface,

			/// <summary>Beyond the rim, where nothing is built.</summary>
			Outside,

			/// <summary>The engraved line's band on the raised middle, built from its own rings instead.</summary>
			Band,
		}

		/// <summary>
		/// The channel and the raised middle, all of it from one arrangement.
		/// </summary>
		/// <remarks>
		/// The rim, the raised middle's outline, rungs tying the two together, the
		/// starburst's spokes and the holes' circles are laid into one
		/// <see cref="InsertArrangement"/>. Every face it hands back is triangulated
		/// as it is, and told apart by a point well inside it: in the middle's
		/// outline and in no hole it is surface, beyond the rim it is nothing, and
		/// anything else is floor. Then a wall stands on every edge with different
		/// tiers either side, facing the lower one, which is all a hole's wall is.
		/// <para>
		/// So nothing is built from a ring walked separately from the faces. A
		/// walk round the middle's outline is not the edge of its faces once the
		/// arrangement has snapped them: where two sides of an acute tip pass
		/// through the same two pixels, they pass through them in the same order,
		/// and walking one side after the other goes round the tip the wrong way.
		/// An edge either has surface on one side and floor on the other, or not,
		/// and that is all a wall needs to know.
		/// </para>
		/// <para>
		/// The rungs run from each outline point on the rim to its place on the
		/// middle's outline, and cut the channel into cells. Without them the
		/// channel is one face with the middle as a hole in it, which a face walk
		/// hands back as the whole rim, middle and all.
		/// </para>
		/// </remarks>
		/// <param name="rim">The channel floor's outer edge: the rim itself, or where
		/// a bottom bevel lets go of the floor, in which case
		/// <paramref name="bevelled"/> is set and the bevel stands on that edge
		/// instead of a wall.</param>
		/// <returns>Every point each side of <paramref name="rim"/> passes through,
		/// in order, which is where a bevel meeting the floor has to meet it.</returns>
		/// <param name="outerCharts">The outer band's chart for each side of the rim,
		/// which a wall standing on <paramref name="rim"/> is laid out in when it is
		/// the rim itself.</param>
		/// <param name="island">The raised middle's outline, or null when there is
		/// no channel and the whole floor is the middle: then everything within the
		/// rim is surface, and the rim's own wall comes down onto that surface.</param>
		private static List<RunPoint>[] AddSunkenFloor(InsertOutline outline, InsertBody body, List<RingPoint> rim,
			int[] rimMap, bool bevelled, List<RingPoint> island, int[] islandMap, InsertStarburst starburst, float floor,
			float islandTop, int topChart, int[] floorCharts, int[] outerCharts, MeshBuffers buffers, out InsertFlatIsland flatIsland)
		{
			var weld = outline.WeldTolerance;
			var arrangement = new InsertArrangement(weld);
			flatIsland = null;
			var owners = new Dictionary<int, (List<RingPoint> Ring, int Side)>();

			var rimPoints = AddLoop(arrangement, rim, owners, out var rimSides);
			int[] islandPoints = null;
			if (island != null) {
				islandPoints = AddLoop(arrangement, island, owners, out _);
				var rungs = new HashSet<(int, int)>();
				for (var i = 0; i < rimMap.Length; i++) {
					if (rungs.Add((rimMap[i], islandMap[i]))) {
						arrangement.AddSegment(rimPoints[rimMap[i]], islandPoints[islandMap[i]]);
					}
				}
			}

			var outer = new List<float2>(rim.Count);
			foreach (var entry in rim) {
				outer.Add(entry.Position);
			}
			var polygon = outer;
			if (island != null) {
				polygon = new List<float2>(island.Count);
				foreach (var entry in island) {
					polygon.Add(entry.Position);
				}
			}
			// The tier the rim's wall stands over: the channel's floor, or the
			// surface itself without one.
			var inner = island != null ? Tier.Floor : Tier.Surface;

			var holes = AddHoles(arrangement, body.Holes, body.HoleSegments, polygon, starburst, weld, out var holeOwners);

			if (starburst != null) {
				// Spokes that start at the centre all share the one point, so however
				// many of them there are, they meet exactly rather than each arriving
				// at its own idea of where the centre is.
				var center = -1;
				for (var spoke = 0; spoke < starburst.Count; spoke++) {
					foreach (var (from, to, fromCenter) in starburst.SpokeWithin(spoke, polygon, weld)) {
						int start;
						if (fromCenter) {
							if (center < 0) {
								center = arrangement.AddPoint(starburst.Center);
							}
							start = center;
						} else {
							start = arrangement.AddPoint(from);
						}
						arrangement.AddSegment(start, arrangement.AddPoint(to));
					}
				}
			}

			// The engraved line's trough is stitched on its own from its profile
			// rings, and only its two edges go into the arrangement, as loops tied
			// in by a bridge each, so the face walk finds the flat top around the
			// trough and the flat middle inside it; the band between the edges
			// comes back as faces of its own, which are left out, the trough's
			// strips standing there instead. Kept out of the arrangement, the
			// trough's sharp mitres and thin faces are never snapped.
			var groove = starburst == null ? body.Groove : null;
			List<float2> bandOuter = null;
			List<float2> bandInner = null;
			if (groove != null) {
				AddGrooveStrips(groove, islandTop, topChart, weld, arrangement, buffers, out bandOuter, out bandInner);
				var surfacePoints = islandPoints ?? rimPoints;
				var surfaceRing = island ?? rim;
				var surfacePositions = new List<float2>(surfaceRing.Count);
				foreach (var entry in surfaceRing) {
					surfacePositions.Add(entry.Position);
				}
				var outerPoints = AddGrooveLoop(arrangement, bandOuter, surfacePositions, surfacePoints);
				if (outerPoints != null) {
					AddGrooveLoop(arrangement, bandInner, bandOuter, outerPoints);
				}
				flatIsland = PlainIsland(surfaceRing, polygon, body, holes, islandTop, weld);
			}

			arrangement.Build();
			var faces = arrangement.Faces(out var faceOf);

			float HeightOf(Tier tier, float2 position) => tier switch {
				Tier.Surface => starburst?.DepthAt(position) ?? islandTop,
				Tier.Floor => floor,
				_ => 0f,
			};

			var tiers = new Tier[faces.Count];
			for (var f = 0; f < faces.Count; f++) {
				var face = faces[f];
				var exact = new List<double2>(face.Count);
				foreach (var point in face) {
					exact.Add(arrangement.Exact(point));
				}

				// Clipped into ears rather than fanned. Snapping bends straight sides
				// by up to half a pixel either way, which leaves a thin face not quite
				// convex, and a fan can turn over on it.
				var pieces = InsertTriangulation.EarClip(exact);
				if (pieces.Count == 0) {
					tiers[f] = Tier.Floor;
					continue;
				}

				// The middle of the face's largest triangle is well inside it, which
				// the face's own centroid need not be.
				var largest = pieces[0];
				var largestArea = 0.0;
				foreach (var piece in pieces) {
					var area = TriangleArea(exact[piece.x], exact[piece.y], exact[piece.z]);
					if (area > largestArea) {
						largest = piece;
						largestArea = area;
					}
				}
				var middle = (float2)((exact[largest.x] + exact[largest.y] + exact[largest.z]) / 3.0);
				var tier = tiers[f] = !Contains(outer, middle) ? Tier.Outside
					: Contains(polygon, middle) && !InAnyHole(holes, middle)
						? bandOuter != null && Contains(bandOuter, middle) && !Contains(bandInner, middle) ? Tier.Band : Tier.Surface
					: Tier.Floor;
				if (tier == Tier.Outside || tier == Tier.Band) {
					continue;
				}

				// A starburst face lies within a single triangle of the wheel, which
				// any point inside it names, and is flat in that triangle's plane. It
				// carries that plane's normal, which keeps the creases crisp.
				Vector3 normal = Vector3.up;
				if (tier == Tier.Surface && starburst != null) {
					var sector = starburst.SectorOf(middle);
					float3 hub = ToLocal(starburst.Center, starburst.CenterDepth);
					float3 opening = ToLocal(starburst.Rim(sector), starburst.RimDepth(sector));
					float3 closing = ToLocal(starburst.Rim(sector + 1), starburst.RimDepth(sector + 1));
					var tilted = math.normalize(math.cross(opening - hub, closing - hub));
					normal = tilted.y < 0f ? -tilted : tilted;
				}

				// Points are lifted through the same height as the walls', so where
				// the two meet they agree to the bit. Both floors are projected from
				// above, the channel's in pieces along its length.
				var chart = tier == Tier.Surface ? topChart : floorCharts[NearestSide(rim, middle)];
				var first = buffers.Vertices.Count;
				foreach (var point in face) {
					var position = arrangement.Position(point);
					buffers.Add(ToLocal(position, HeightOf(tier, position)), normal, (Vector2)position, chart);
				}
				foreach (var piece in pieces) {
					buffers.Triangle(first + piece.x, first + piece.y, first + piece.z);
				}
			}

			Tier TierOf(int from, int to) => faceOf.TryGetValue((from, to), out var face) ? tiers[face] : Tier.Outside;

			var charts = new WallCharts(buffers, rim, island, outerCharts);
			var shared = new Dictionary<(int, float, float2, float, int), int>();
			int WallVertex(int point, float height, float2 outward, float u, int chart)
			{
				if (shared.TryGetValue((point, height, outward, u, chart), out var index)) {
					return index;
				}
				index = buffers.Add(ToLocal(arrangement.Position(point), height), ToLocalNormal(outward, false),
					new Vector2(u, height), chart);
				shared[(point, height, outward, u, chart)] = index;
				return index;
			}

			foreach (var (a, b) in arrangement.Edges) {
				// The band lies on the surface: no wall stands between the two.
				var left = TierOf(a, b);
				var right = TierOf(b, a);
				left = left == Tier.Band ? Tier.Surface : left;
				right = right == Tier.Band ? Tier.Surface : right;
				if (left == right) {
					continue;
				}

				// Turned so the lower tier is on its right, which is the way it faces:
				// floor before surface, and both before the outside, which stands at
				// the rim.
				var facing = (Tier)math.min((int)left, (int)right);
				var behind = (Tier)math.max((int)left, (int)right);

				// Where a bevel meets the floor, it is the bevel that climbs to the rim.
				// A hole cut across the bevel's foot stands its wall under the bevel,
				// from the surface down, rather than up to the rim through it.
				if (bevelled && behind == Tier.Outside) {
					if (facing == inner) {
						continue;
					}
					behind = inner;
				}
				var (from, to) = right == facing ? (a, b) : (b, a);
				var fromPosition = arrangement.Position(from);
				var toPosition = arrangement.Position(to);

				WallFrame(arrangement, owners, holeOwners, charts, from, to, behind == Tier.Outside ? rim : island,
					out var fromNormal, out var toNormal, out var fromU, out var toU, out var wallChart);

				var fromTop = HeightOf(behind, fromPosition);
				var toTop = HeightOf(behind, toPosition);
				var fromBottom = HeightOf(facing, fromPosition);
				var toBottom = HeightOf(facing, toPosition);

				// A wall may close to nothing at either end, where its top comes down
				// to meet the floor. Each triangle of the quad takes one end's height,
				// so a closed end costs only the triangle standing on it.
				var closedHere = math.abs(fromTop - fromBottom) < InsertOutlineBuilder.Epsilon;
				var closedThere = math.abs(toTop - toBottom) < InsertOutlineBuilder.Epsilon;
				if (closedHere && closedThere) {
					continue;
				}

				var bottomFrom = WallVertex(from, fromBottom, fromNormal, fromU, wallChart);
				var bottomTo = WallVertex(to, toBottom, toNormal, toU, wallChart);
				var topFrom = WallVertex(from, fromTop, fromNormal, fromU, wallChart);
				var topTo = WallVertex(to, toTop, toNormal, toU, wallChart);
				if (!closedThere) {
					buffers.Triangle(bottomFrom, bottomTo, topTo);
				}
				if (!closedHere) {
					buffers.Triangle(bottomFrom, topTo, topFrom);
				}
			}

			var sides = new List<RunPoint>[rimSides.Length];
			for (var j = 0; j < rimSides.Length; j++) {
				var cuts = arrangement.Cuts(rimSides[j]);
				sides[j] = new List<RunPoint>(cuts.Count);
				foreach (var (t, point) in cuts) {
					sides[j].Add(new RunPoint(arrangement.Position(point), arrangement.Exact(point), t));
				}
			}
			return sides;
		}

		/// <summary>
		/// Lays each hole into an arrangement as a closed polygon of
		/// <paramref name="segments"/> sides, noting which hole and side each
		/// segment is.
		/// </summary>
		/// <remarks>
		/// A hole that meets nothing else would float in the face around it as a
		/// piece of its own, and a face walk never attaches such a piece to that
		/// face, so the face would cover it over. Each hole is tied in by a bridge
		/// running from its centre straight out to the raised floor's outline.
		/// Away from the starburst's centre, the bridge runs along a line through
		/// it, and so stays within the one triangle of the wheel it starts in and
		/// never crosses a spoke. A hole whose centre is off the raised floor
		/// either reaches across its outline, which ties it in, swallows the
		/// raised floor whole, which leaves nothing of it to cut, or misses it
		/// altogether, and is left out.
		/// </remarks>
		/// <returns>Each hole's polygon, for telling faces inside it apart.</returns>
		private static List<List<float2>> AddHoles(InsertArrangement arrangement, InsertHole[] holes, int segments,
			List<float2> island, InsertStarburst starburst, float weld,
			out Dictionary<int, (float2 Center, int Index, float Chord, int Hole)> owners)
		{
			owners = new Dictionary<int, (float2, int, float, int)>();
			var polygons = new List<List<float2>>();
			if (holes == null) {
				return polygons;
			}

			var count = math.max(3, segments);
			for (var index = 0; index < holes.Length; index++) {
				var hole = holes[index];
				var center = new float2(hole.Center.x, hole.Center.y);
				if (hole.Radius <= weld) {
					continue;
				}

				var circle = new List<float2>(count);
				for (var k = 0; k < count; k++) {
					var angle = 2f * math.PI * k / count;
					circle.Add(center + hole.Radius * new float2(math.cos(angle), math.sin(angle)));
				}

				// A hole with the whole raised floor inside it cuts nothing to draw: its
				// circle would float round the outside as a piece of its own, and every
				// face is already floor for lying inside it.
				var reaches = Reaches(circle, island);
				if (!reaches && !Contains(circle, island[0])) {
					continue;
				}
				polygons.Add(circle);
				if (!reaches) {
					continue;
				}
				var points = new int[count];
				for (var k = 0; k < count; k++) {
					points[k] = arrangement.AddPoint(circle[k]);
				}
				for (var k = 0; k < count; k++) {
					var chord = 2f * hole.Radius * math.sin(math.PI / count);
					owners[arrangement.AddSegment(points[k], points[(k + 1) % count])] = (center, k, chord, index);
				}

				if (Contains(island, center)) {
					var away = starburst != null ? center - starburst.Center : float2.zero;
					var direction = math.lengthsq(away) > weld * weld ? math.normalize(away) : new float2(1f, 0f);
					if (TryLeave(island, center, direction, out var exit)) {
						arrangement.AddSegment(arrangement.AddPoint(center), arrangement.AddPoint(exit));
					}
				}
			}
			return polygons;
		}

		/// <summary>
		/// Where a ray from a point inside a polygon first leaves it: the nearest
		/// edge it actually crosses, not the nearest supporting line it meets,
		/// which on a shape that bends in on itself can lie well short of the
		/// boundary.
		/// </summary>
		private static bool TryLeave(List<float2> polygon, float2 origin, float2 direction, out float2 exit)
		{
			var leave = double.PositiveInfinity;
			var d = (double2)direction;
			for (var i = 0; i < polygon.Count; i++) {
				var a = (double2)polygon[i];
				var edge = (double2)polygon[(i + 1) % polygon.Count] - a;
				var denominator = d.x * edge.y - d.y * edge.x;
				if (math.abs(denominator) < 1e-12) {
					continue;
				}
				var delta = a - (double2)origin;
				var t = (delta.x * edge.y - delta.y * edge.x) / denominator;
				var s = (delta.x * d.y - delta.y * d.x) / denominator;
				if (t > 0.0 && s >= 0.0 && s <= 1.0 && t < leave) {
					leave = t;
				}
			}
			exit = (float2)((double2)origin + d * leave);
			return !double.IsPositiveInfinity(leave);
		}

		/// <summary>
		/// Whether a hole's polygon reaches into the raised floor's outline: has a
		/// corner inside it, or crosses it.
		/// </summary>
		private static bool Reaches(List<float2> circle, List<float2> island)
		{
			foreach (var point in circle) {
				if (Contains(island, point)) {
					return true;
				}
			}
			for (var i = 0; i < circle.Count; i++) {
				for (var j = 0; j < island.Count; j++) {
					if (Crosses(circle[i], circle[(i + 1) % circle.Count], island[j], island[(j + 1) % island.Count])) {
						return true;
					}
				}
			}
			return false;
		}

		private static bool Crosses(float2 a, float2 b, float2 c, float2 d)
		{
			static double Side(double2 o, double2 p, double2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
			return Side(a, b, c) * Side(a, b, d) < 0.0 && Side(c, d, a) * Side(c, d, b) < 0.0;
		}

		/// <summary>
		/// The raised middle as a plain flat surface, its holes cut out and
		/// nothing carved into it: what stands in for the middle's triangles once
		/// a bake fits and the engraved line lives in the maps. Its edge and its
		/// holes are the same points the walls stand on, so they meet exactly.
		/// </summary>
		private static InsertFlatIsland PlainIsland(List<RingPoint> ring, List<float2> polygon, InsertBody body,
			List<List<float2>> holes, float height, float weld)
		{
			var arrangement = new InsertArrangement(weld);
			AddLoop(arrangement, ring, new Dictionary<int, (List<RingPoint> Ring, int Side)>(), out _);
			AddHoles(arrangement, body.Holes, body.HoleSegments, polygon, null, weld, out _);
			arrangement.Build();
			var faces = arrangement.Faces(out _);
			var vertices = new List<Vector3>();
			var indices = new Dictionary<Vector3, int>();
			var triangles = new List<int>();
			foreach (var face in faces) {
				var exact = new List<double2>(face.Count);
				foreach (var point in face) {
					exact.Add(arrangement.Exact(point));
				}
				var pieces = InsertTriangulation.EarClip(exact);
				if (pieces.Count == 0) {
					continue;
				}
				var largest = pieces[0];
				var largestArea = 0.0;
				foreach (var piece in pieces) {
					var area = TriangleArea(exact[piece.x], exact[piece.y], exact[piece.z]);
					if (area > largestArea) {
						largest = piece;
						largestArea = area;
					}
				}
				var middle = (float2)((exact[largest.x] + exact[largest.y] + exact[largest.z]) / 3.0);
				if (!Contains(polygon, middle) || InAnyHole(holes, middle)) {
					continue;
				}
				var corners = new int[face.Count];
				for (var i = 0; i < face.Count; i++) {
					var position = ToLocal(arrangement.Position(face[i]), height);
					if (!indices.TryGetValue(position, out var index)) {
						index = vertices.Count;
						indices.Add(position, index);
						vertices.Add(position);
					}
					corners[i] = index;
				}
				foreach (var piece in pieces) {
					triangles.Add(corners[piece.x]);
					triangles.Add(corners[piece.y]);
					triangles.Add(corners[piece.z]);
				}
			}
			return new InsertFlatIsland { Vertices = vertices.ToArray(), Triangles = triangles.ToArray() };
		}

		/// <summary>
		/// Takes the raised middle's triangles out of a mesh wearing a fitting
		/// bake and puts the plain flat island in their place, the engraved
		/// line's trough gone from the mesh. The island's UVs and tangents come
		/// from the map's projection of the middle, fitted to the corners the
		/// bake laid out: the bake keeps that chart as the generator projected it
		/// from above, so the map lies on any triangulation of the middle.
		/// </summary>
		/// <returns>False, leaving the mesh as it is, when there is no island to
		/// put in, or the bake's chart is not a projection, as one made before
		/// the middle kept its projection is not.</returns>
		public static bool ReplaceMiddle(Mesh mesh, InsertMeshLayout layout)
		{
			var island = layout.FlatIsland;
			if (island == null || layout.FlatTriangles == null || layout.FlatTriangles.Length == 0 || island.Triangles.Length == 0) {
				return false;
			}
			var vertices = mesh.vertices;
			var normals = mesh.normals;
			var uvs = mesh.uv;
			var tangents = mesh.tangents;
			var triangles = mesh.triangles;
			if (uvs.Length != vertices.Length || tangents.Length != vertices.Length) {
				return false;
			}
			var flat = new bool[triangles.Length / 3];
			foreach (var t in layout.FlatTriangles) {
				if (t < flat.Length) {
					flat[t] = true;
				}
			}

			// The map's U and V, each an affine function of local X and Z, by
			// least squares over the middle's corners.
			var m = new double[3, 3];
			var ru = new double[3];
			var rv = new double[3];
			for (var t = 0; t < flat.Length; t++) {
				if (!flat[t]) {
					continue;
				}
				for (var c = 0; c < 3; c++) {
					var vertex = triangles[t * 3 + c];
					var q = new double[] { vertices[vertex].x, vertices[vertex].z, 1.0 };
					for (var i = 0; i < 3; i++) {
						for (var j = 0; j < 3; j++) {
							m[i, j] += q[i] * q[j];
						}
						ru[i] += q[i] * uvs[vertex].x;
						rv[i] += q[i] * uvs[vertex].y;
					}
				}
			}
			if (!Solve(m, ru, out var cu) || !Solve(m, rv, out var cv)) {
				return false;
			}
			// A chart that was unwrapped rather than projected does not fit an
			// affine map: its trough lies wider in the map than on the middle.
			for (var t = 0; t < flat.Length; t++) {
				if (!flat[t]) {
					continue;
				}
				for (var c = 0; c < 3; c++) {
					var vertex = triangles[t * 3 + c];
					var u = cu[0] * vertices[vertex].x + cu[1] * vertices[vertex].z + cu[2];
					var v = cv[0] * vertices[vertex].x + cv[1] * vertices[vertex].z + cv[2];
					if (math.abs(u - uvs[vertex].x) > 1e-4 || math.abs(v - uvs[vertex].y) > 1e-4) {
						return false;
					}
				}
			}

			// The tangent runs where U grows and the bitangent where V does, the
			// columns of the map's inverse; the sign says which way V turns.
			var determinant = cu[0] * cv[1] - cu[1] * cv[0];
			if (math.abs(determinant) < 1e-18) {
				return false;
			}
			var tangent = new Vector3((float)(cv[1] / determinant), 0f, (float)(-cv[0] / determinant)).normalized;
			var bitangent = new Vector3((float)(-cu[1] / determinant), 0f, (float)(cu[0] / determinant));
			var sign = tangent.z * bitangent.x - tangent.x * bitangent.z < 0f ? -1f : 1f;
			var islandTangent = new Vector4(tangent.x, 0f, tangent.z, sign);

			var outVertices = new List<Vector3>(vertices.Length);
			var outNormals = new List<Vector3>(vertices.Length);
			var outUvs = new List<Vector2>(vertices.Length);
			var outTangents = new List<Vector4>(vertices.Length);
			var outTriangles = new List<int>(triangles.Length);
			var remap = new int[vertices.Length];
			for (var i = 0; i < remap.Length; i++) {
				remap[i] = -1;
			}
			for (var t = 0; t < flat.Length; t++) {
				if (flat[t]) {
					continue;
				}
				for (var c = 0; c < 3; c++) {
					var vertex = triangles[t * 3 + c];
					if (remap[vertex] < 0) {
						remap[vertex] = outVertices.Count;
						outVertices.Add(vertices[vertex]);
						outNormals.Add(normals[vertex]);
						outUvs.Add(uvs[vertex]);
						outTangents.Add(tangents[vertex]);
					}
					outTriangles.Add(remap[vertex]);
				}
			}
			var first = outVertices.Count;
			foreach (var position in island.Vertices) {
				outVertices.Add(position);
				outNormals.Add(Vector3.up);
				outUvs.Add(new Vector2((float)(cu[0] * position.x + cu[1] * position.z + cu[2]), (float)(cv[0] * position.x + cv[1] * position.z + cv[2])));
				outTangents.Add(islandTangent);
			}
			foreach (var index in island.Triangles) {
				outTriangles.Add(first + index);
			}

			mesh.Clear();
			mesh.indexFormat = outVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.SetVertices(outVertices);
			mesh.SetNormals(outNormals);
			mesh.SetUVs(0, outUvs);
			mesh.SetTangents(outTangents);
			mesh.SetTriangles(outTriangles, 0);
			mesh.RecalculateBounds();
			return true;
		}

		/// <summary>Solves a three by three system by elimination with pivoting.</summary>
		private static bool Solve(double[,] m, double[] r, out double[] x)
		{
			var a = new double[3, 4];
			for (var i = 0; i < 3; i++) {
				for (var j = 0; j < 3; j++) {
					a[i, j] = m[i, j];
				}
				a[i, 3] = r[i];
			}
			for (var column = 0; column < 3; column++) {
				var pivot = column;
				for (var row = column + 1; row < 3; row++) {
					if (System.Math.Abs(a[row, column]) > System.Math.Abs(a[pivot, column])) {
						pivot = row;
					}
				}
				if (System.Math.Abs(a[pivot, column]) < 1e-18) {
					x = null;
					return false;
				}
				for (var j = 0; j < 4; j++) {
					(a[column, j], a[pivot, j]) = (a[pivot, j], a[column, j]);
				}
				for (var row = 0; row < 3; row++) {
					if (row == column) {
						continue;
					}
					var factor = a[row, column] / a[column, column];
					for (var j = column; j < 4; j++) {
						a[row, j] -= factor * a[column, j];
					}
				}
			}
			x = new[] { a[0, 3] / a[0, 0], a[1, 3] / a[1, 1], a[2, 3] / a[2, 2] };
			return true;
		}

		private static bool InAnyHole(List<List<float2>> holes, float2 point)
		{
			foreach (var hole in holes) {
				if (Contains(hole, point)) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Lays a ring into an arrangement as a closed run of segments, noting which
		/// ring and side each segment is.
		/// </summary>
		/// <returns>The arrangement's point for each ring entry.</returns>
		/// <summary>
		/// How far below the surface the engraved line dips at a point, zero
		/// beyond its width, and which way the trough faces there: a half round
		/// squashed to the line's depth, horizontal at its edges, up at its
		/// middle, always facing back towards the line's centre.
		/// </summary>
		/// <summary>
		/// The engraved line's trough: its profile drawn as rings, each the line's
		/// outline offset sideways and dropped to the floor's height there, from
		/// the outer edge in, and stitched into strips ring to ring. The rings
		/// share the line's points, so a strip is a quad per side of the line;
		/// where an offset folds points together or the ring back on itself, the
		/// fold is welded or cut out, and the stitching skips what is folded flat.
		/// The edges are creases: the ring on them faces across the trough, and
		/// the flat around and inside the trough keeps its own corners.
		/// </summary>
		private static void AddGrooveStrips(InsertGroove groove, float surface, int chart, float weld, InsertArrangement arrangement,
			MeshBuffers buffers, out List<float2> outerEdge, out List<float2> innerEdge)
		{
			var line = groove.Outline;
			var count = line.Points.Count;
			var half = groove.Width * 0.5f;
			var depth = math.max(0f, groove.Depth);
			var steps = math.max(1, groove.Segments) * 2;
			var rings = new List<float2>[steps + 1];
			var maps = new int[steps + 1][];
			var ringVertices = new int[steps + 1][];
			for (var k = 0; k <= steps; k++) {
				var angle = math.PI * k / steps;
				var lateral = -half * math.cos(angle);
				var height = -depth * math.sin(angle);
				// The ring is the line's own offset, welded and unfolded by the
				// line, with a map from the line's points to the ring's.
				var ring = groove.Ring(lateral, weld, out var map);
				// A half round squashed to the depth: across at the edges, up in
				// the middle, always facing back at the trough's middle; a ring
				// point that several line points welded onto takes the first's.
				var normals = new List<Vector3>(ring.Count);
				for (var j = 0; j < ring.Count; j++) {
					normals.Add(Vector3.up);
				}
				var given = new bool[ring.Count];
				for (var i = 0; i < count; i++) {
					var j = map[i];
					if (given[j]) {
						continue;
					}
					given[j] = true;
					var across = ToLocalNormal(line.Points[i].Normal, inwards: true);
					normals[j] = math.normalize((float3)across * (math.cos(angle) * depth) + new float3(0f, math.sin(angle) * half, 0f) + new float3(0f, 1e-6f, 0f));
				}
				Unfold(ring, map, normals);
				// The two edges are laid into the arrangement as loops, whose
				// points it snaps: the rings take the snapped positions, so the
				// strips meet the faces around and within them to the bit, and the
				// bake welds the middle into one piece.
				if (k == 0 || k == steps) {
					for (var j = 0; j < ring.Count; j++) {
						ring[j] = arrangement.Snap(ring[j]);
					}
				}
				rings[k] = ring;
				maps[k] = map;
				ringVertices[k] = new int[ring.Count];
				for (var j = 0; j < ring.Count; j++) {
					ringVertices[k][j] = buffers.Add(ToLocal(ring[j], surface + height), normals[j], (Vector2)ring[j], chart);
				}
			}

			// A triangle faces up when its footprint runs counter-clockwise; one
			// that a collapse has turned over, or folded flat, is left out.
			void Up(int k0, int j0, int k1, int j1, int k2, int j2)
			{
				var p0 = rings[k0][j0];
				var p1 = rings[k1][j1];
				var p2 = rings[k2][j2];
				var area = (p1.x - p0.x) * (p2.y - p0.y) - (p1.y - p0.y) * (p2.x - p0.x);
				if (area > weld * weld) {
					buffers.Triangle(ringVertices[k0][j0], ringVertices[k1][j1], ringVertices[k2][j2]);
				}
			}
			for (var k = 0; k < steps; k++) {
				for (var i = 0; i < count; i++) {
					var next = (i + 1) % count;
					Up(k, maps[k][i], k, maps[k][next], k + 1, maps[k + 1][next]);
					Up(k, maps[k][i], k + 1, maps[k + 1][next], k + 1, maps[k + 1][i]);
				}
			}
			outerEdge = rings[0];
			innerEdge = rings[steps];
		}

		/// <summary>
		/// Lays one of the trough's edges into the arrangement as a loop, tied
		/// by a bridge at its nearest pair to a loop already there, so the face
		/// walk, which knows nothing of one loop inside another, finds the face
		/// between the two. Returns the loop's points, or null for a loop too
		/// small to lay.
		/// </summary>
		private static int[] AddGrooveLoop(InsertArrangement arrangement, List<float2> loop, List<float2> around, int[] aroundPoints)
		{
			if (loop == null || loop.Count < 3) {
				return null;
			}
			var points = new int[loop.Count];
			for (var i = 0; i < loop.Count; i++) {
				points[i] = arrangement.AddPoint(loop[i]);
			}
			for (var i = 0; i < loop.Count; i++) {
				arrangement.AddSegment(points[i], points[(i + 1) % loop.Count]);
			}
			var nearest = float.MaxValue;
			int loopAt = 0, aroundAt = 0;
			for (var i = 0; i < loop.Count; i++) {
				for (var j = 0; j < around.Count; j++) {
					var apart = math.distancesq(loop[i], around[j]);
					if (apart < nearest) {
						nearest = apart;
						loopAt = i;
						aroundAt = j;
					}
				}
			}
			arrangement.AddSegment(points[loopAt], aroundPoints[aroundAt]);
			return points;
		}

		/// <summary>
		/// Takes the loops out of a ring offset past what a short side could
		/// take: where the ring crosses itself, everything between the two
		/// crossing sides is folded back on itself, and it is cut out and
		/// replaced by the crossing point, which every point that was in the
		/// loop now maps to.
		/// </summary>
		private static void Unfold(List<float2> ring, int[] map, List<Vector3> normals)
		{
			for (var guard = 0; guard < map.Length; guard++) {
				var n = ring.Count;
				if (n < 4) {
					return;
				}
				var found = false;
				for (var i = 0; i < n && !found; i++) {
					var a = ring[i];
					var b = ring[(i + 1) % n];
					for (var j = i + 2; j < n; j++) {
						if (i == 0 && j == n - 1) {
							continue;
						}
						if (!Crossing(a, b, ring[j], ring[(j + 1) % n], out var crossing)) {
							continue;
						}
						// The loop is the shorter way round, from i + 1 to j; the
						// longer way holds the ring itself.
						var inside = j - i;
						var outside = n - inside;
						int from, to;
						if (inside <= outside) {
							from = i + 1;
							to = j;
						} else {
							from = j + 1;
							to = i + n;
						}
						var kept = new List<float2>(n);
						var keptNormals = new List<Vector3>(n);
						var remap = new int[n];
						var crossingAt = -1;
						var start = from % n;
						var length = to - from;
						for (var m = 0; m < n; m++) {
							// Membership round the ring, however the loop wraps.
							var eaten = (m - start + n) % n <= length;
							if (eaten) {
								if (crossingAt < 0) {
									crossingAt = kept.Count;
									kept.Add(crossing);
									keptNormals.Add(normals[m]);
								}
								remap[m] = crossingAt;
							} else {
								remap[m] = kept.Count;
								kept.Add(ring[m]);
								keptNormals.Add(normals[m]);
							}
						}
						for (var p = 0; p < map.Length; p++) {
							map[p] = remap[map[p]];
						}
						ring.Clear();
						ring.AddRange(kept);
						normals.Clear();
						normals.AddRange(keptNormals);
						found = true;
						break;
					}
				}
				if (!found) {
					return;
				}
			}
		}

		/// <summary>Whether two segments cross properly, and where.</summary>
		private static bool Crossing(float2 a, float2 b, float2 c, float2 d, out float2 point)
		{
			point = a;
			var ab = b - a;
			var cd = d - c;
			var determinant = ab.x * cd.y - ab.y * cd.x;
			if (math.abs(determinant) < 1e-12f) {
				return false;
			}
			var ac = c - a;
			var t = (ac.x * cd.y - ac.y * cd.x) / determinant;
			var u = (ac.x * ab.y - ac.y * ab.x) / determinant;
			if (t <= 0f || t >= 1f || u <= 0f || u >= 1f) {
				return false;
			}
			point = a + t * ab;
			return true;
		}

		private static int[] AddLoop(InsertArrangement arrangement, List<RingPoint> ring,
			Dictionary<int, (List<RingPoint> Ring, int Side)> owners, out int[] sides)
		{
			var points = new int[ring.Count];
			for (var i = 0; i < ring.Count; i++) {
				points[i] = arrangement.AddPoint(ring[i].Position);
			}
			sides = new int[ring.Count];
			for (var i = 0; i < ring.Count; i++) {
				sides[i] = arrangement.AddSegment(points[i], points[(i + 1) % ring.Count]);
				owners[sides[i]] = (ring, i);
			}
			return points;
		}

		/// <summary>
		/// The UV charts walls are laid out in: the outer band's pieces, one chart
		/// per side of the raised middle, one band per hole, and a chart of its own
		/// for any other edge, made as they are first asked for.
		/// </summary>
		private sealed class WallCharts
		{
			private readonly MeshBuffers _buffers;
			private readonly Dictionary<(List<RingPoint>, int), int> _sides = new();
			private readonly Dictionary<int, int> _holes = new();

			public readonly List<RingPoint> Rim;
			public readonly List<RingPoint> Island;
			public readonly int[] Outer;

			public WallCharts(MeshBuffers buffers, List<RingPoint> rim, List<RingPoint> island, int[] outer)
			{
				_buffers = buffers;
				Rim = rim;
				Island = island;
				Outer = outer;
			}

			public int Side(List<RingPoint> ring, int side)
			{
				if (!_sides.TryGetValue((ring, side), out var chart)) {
					_sides[(ring, side)] = chart = _buffers.NewChart();
				}
				return chart;
			}

			public int Hole(int hole)
			{
				if (!_holes.TryGetValue(hole, out var chart)) {
					_holes[hole] = chart = _buffers.NewChart();
				}
				return chart;
			}

			public int Other() => _buffers.NewChart();
		}

		/// <summary>
		/// How a wall standing on the edge from <paramref name="from"/> to
		/// <paramref name="to"/> shades, facing right of that direction, and where
		/// it is laid out in UV.
		/// </summary>
		/// <remarks>
		/// Along a side of <paramref name="preferred"/>, the ring's own normals
		/// carry through, blended along the side exactly as the outline blends
		/// them, so the wall shades round a fillet without a crease. Any other edge
		/// only ever borders two tiers where snapping ran it together with a side,
		/// within a pixel, and simply faces square off itself.
		/// <para>
		/// In UV, U runs along the wall and V is its height, so every chart reads
		/// left to right as seen from the side the wall faces. The rim's walls
		/// belong to the outer band. Each side of the raised middle is a chart of
		/// its own, and each hole's wall a band of its own, round the hole.
		/// </para>
		/// </remarks>
		private static void WallFrame(InsertArrangement arrangement,
			Dictionary<int, (List<RingPoint> Ring, int Side)> owners,
			Dictionary<int, (float2 Center, int Index, float Chord, int Hole)> holeOwners, WallCharts charts,
			int from, int to, List<RingPoint> preferred, out float2 fromNormal, out float2 toNormal,
			out float fromU, out float toU, out int chart)
		{
			var fromPosition = arrangement.Position(from);
			var toPosition = arrangement.Position(to);
			var along = toPosition - fromPosition;
			var right = math.normalizesafe(new float2(along.y, -along.x));

			var chosen = -1;
			var circle = -1;
			foreach (var segment in arrangement.Sources(from, to)) {
				if (owners.TryGetValue(segment, out var owner) && (chosen < 0 || owner.Ring == preferred)) {
					chosen = segment;
				}
				if (holeOwners.ContainsKey(segment)) {
					circle = segment;
				}
			}

			if (chosen < 0 && circle >= 0) {
				// Round a hole, the wall shades as the circle it stands on. The band
				// runs clockwise, which reads left to right from inside the hole.
				var (center, index, chord, hole) = holeOwners[circle];
				fromNormal = math.normalizesafe(fromPosition - center, right);
				toNormal = math.normalizesafe(toPosition - center, right);
				fromU = -(index + arrangement.T(circle, from)) * chord;
				toU = -(index + arrangement.T(circle, to)) * chord;
				chart = charts.Hole(hole);
				if (math.dot(fromNormal + toNormal, right) < 0f) {
					fromNormal = -fromNormal;
					toNormal = -toNormal;
				}
				return;
			}

			if (chosen < 0) {
				fromNormal = toNormal = right;
				fromU = 0f;
				toU = math.length(along);
				chart = charts.Other();
				return;
			}

			var (ring, side) = owners[chosen];
			var next = (side + 1) % ring.Count;
			var length = math.distance(ring[side].Position, ring[next].Position);

			float2 NormalAt(float t) => t <= 0f ? ring[side].NormalOut
				: t >= 1f ? ring[next].NormalIn
				: math.normalize(math.lerp(ring[side].NormalOut, ring[next].NormalIn, t));

			var fromT = arrangement.T(chosen, from);
			var toT = arrangement.T(chosen, to);
			fromNormal = NormalAt(fromT);
			toNormal = NormalAt(toT);

			if (ring == charts.Rim && charts.Outer != null) {
				fromU = BandU(ring, side, fromT);
				toU = BandU(ring, side, toT);
				chart = charts.Outer[side];
			} else if (ring == charts.Island) {
				fromU = fromT * length;
				toU = toT * length;
				chart = charts.Side(ring, side);
			} else {
				fromU = 0f;
				toU = math.length(along);
				chart = charts.Other();
			}

			// The ring's normals point out of it, which is the way the middle's wall
			// faces but not the rim's.
			if (math.dot(fromNormal + toNormal, right) < 0f) {
				fromNormal = -fromNormal;
				toNormal = -toNormal;
			}
		}

		/// <summary>
		/// Whether a point lies inside a polygon, by counting crossings.
		/// </summary>
		internal static bool Contains(List<float2> polygon, float2 point)
		{
			var inside = false;
			for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) {
				var a = (double2)polygon[i];
				var b = (double2)polygon[j];
				if (a.y > point.y != b.y > point.y
					&& point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) {
					inside = !inside;
				}
			}
			return inside;
		}

		/// <summary>Area of a triangle in plan, positive counter-clockwise.</summary>
		private static double TriangleArea(double2 a, double2 b, double2 c)
		{
			var ab = b - a;
			var ac = c - a;
			return (ab.x * ac.y - ac.x * ab.y) * 0.5;
		}

		/// <summary>Walks a ring, recording how far round each point sits.</summary>
		private static void Measure(List<RingPoint> ring)
		{
			var distance = 0f;
			for (var i = 0; i < ring.Count; i++) {
				var entry = ring[i];
				entry.Distance = distance;
				ring[i] = entry;
				distance += math.distance(entry.Position, ring[(i + 1) % ring.Count].Position);
			}
		}


	}
}
