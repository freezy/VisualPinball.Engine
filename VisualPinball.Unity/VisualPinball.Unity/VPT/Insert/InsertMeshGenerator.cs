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

		/// <summary>Round cut-outs through the raised floor, down to the channel's floor.</summary>
		public InsertHole[] Holes;

		/// <summary>How many straight sides each hole's circle is drawn with.</summary>
		public int HoleSegments;
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

		public static Mesh Generate(InsertOutline outline, InsertBody body, Mesh mesh)
		{
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var uvs = new List<Vector2>();
			var triangles = new List<int>();
			var size = new float2(outline.Width, outline.Height);

			var floor = -body.Depth;
			var rim = BuildRing(outline, 0f, out var rimMap);

			if (body.ChannelWidth > InsertOutlineBuilder.Epsilon) {
				var island = BuildRing(outline, body.ChannelWidth, out var islandMap);
				var islandTop = floor + body.ChannelDepth;
				var starburst = TryCoverWithStarburst(island, body, islandTop, outline.WeldTolerance);
				AddSunkenFloor(outline, body, rim, rimMap, island, islandMap, starburst, floor, islandTop, size,
					vertices, normals, uvs, triangles);

			} else {
				AddWall(rim, Level(rim.Count, 0f), Level(rim.Count, floor), true, vertices, normals, uvs, triangles);
				AddCap(rim, floor, size, vertices, normals, uvs, triangles);
			}

			mesh.Clear();
			mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

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
		/// A wall between two heights, facing either into the hollow or out of it.
		/// </summary>
		private static void AddWall(List<RingPoint> ring, float[] top, float[] bottom, bool inwards,
			List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
		{
			var count = ring.Count;
			var perimeter = 0f;
			for (var i = 0; i < count; i++) {
				perimeter += math.distance(ring[i].Position, ring[(i + 1) % count].Position);
			}

			var arrivingTop = new int[count];
			var arrivingBottom = new int[count];
			var leavingTop = new int[count];
			var leavingBottom = new int[count];

			for (var i = 0; i < count; i++) {
				var entry = ring[i];
				var split = math.distance(entry.NormalIn, entry.NormalOut) > InsertOutlineBuilder.AngleEpsilon;

				// The ring closes back onto its first entry, which therefore needs a
				// second copy carrying the far end of the U range instead of zero.
				var seam = i == 0;
				var arrivingDistance = seam ? perimeter : entry.Distance;

				arrivingTop[i] = AddWallVertex(entry.Position, entry.NormalIn, arrivingDistance, perimeter,
					top[i], bottom[i], inwards, out arrivingBottom[i], vertices, normals, uvs);

				if (split || seam) {
					leavingTop[i] = AddWallVertex(entry.Position, entry.NormalOut, entry.Distance, perimeter,
						top[i], bottom[i], inwards, out leavingBottom[i], vertices, normals, uvs);
				} else {
					leavingTop[i] = arrivingTop[i];
					leavingBottom[i] = arrivingBottom[i];
				}
			}

			for (var i = 0; i < count; i++) {
				var next = (i + 1) % count;

				// A wall may close to nothing at either end: all the way round when
				// a channel has no depth, or at one end where its top edge comes
				// down to meet the floor. Each triangle of the quad takes one end's
				// height, so a closed end costs only the triangle standing on it,
				// and closing both leaves nothing to draw.
				var closedHere = math.abs(top[i] - bottom[i]) < InsertOutlineBuilder.Epsilon;
				var closedThere = math.abs(top[next] - bottom[next]) < InsertOutlineBuilder.Epsilon;

				if (inwards) {
					// Along the top, then down the far side: that order faces it in.
					if (!closedThere) {
						AddTriangle(triangles, leavingTop[i], arrivingTop[next], arrivingBottom[next]);
					}
					if (!closedHere) {
						AddTriangle(triangles, leavingTop[i], arrivingBottom[next], leavingBottom[i]);
					}
				} else {
					if (!closedThere) {
						AddTriangle(triangles, leavingBottom[i], arrivingBottom[next], arrivingTop[next]);
					}
					if (!closedHere) {
						AddTriangle(triangles, leavingBottom[i], arrivingTop[next], leavingTop[i]);
					}
				}
			}
		}

		/// <summary>A flat run of depths, for a wall whose edge does not vary.</summary>
		private static float[] Level(int count, float depth)
		{
			var depths = new float[count];
			for (var i = 0; i < count; i++) {
				depths[i] = depth;
			}
			return depths;
		}

		private static int AddWallVertex(float2 position, float2 outward, float distance, float perimeter,
			float top, float bottom, bool inwards, out int bottomIndex,
			List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
		{
			var normal = ToLocalNormal(outward, inwards);
			var u = perimeter > InsertOutlineBuilder.Epsilon ? distance / perimeter : 0f;

			var topIndex = vertices.Count;
			vertices.Add(ToLocal(position, top));
			normals.Add(normal);
			uvs.Add(new Vector2(u, 0f));

			bottomIndex = vertices.Count;
			vertices.Add(ToLocal(position, bottom));
			normals.Add(normal);
			uvs.Add(new Vector2(u, 1f));
			return topIndex;
		}

		/// <summary>
		/// A horizontal surface filling a ring, facing up into the hollow.
		/// </summary>
		/// <remarks>
		/// The fan radiates from the outline's centroid, which is not merely a way
		/// to triangulate it: the starburst in a later step radiates from that same
		/// point.
		/// </remarks>
		private static void AddCap(List<RingPoint> ring, float depth, float2 size,
			List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
		{
			var count = ring.Count;
			var start = vertices.Count;

			foreach (var entry in ring) {
				vertices.Add(ToLocal(entry.Position, depth));
				normals.Add(Vector3.up);
				uvs.Add(PlanarUv(entry.Position, size));
			}

			var centroid = RingCentroid(ring);
			var center = vertices.Count;
			vertices.Add(ToLocal(centroid, depth));
			normals.Add(Vector3.up);
			uvs.Add(PlanarUv(centroid, size));

			// The outline runs counter-clockwise in VPX space, and the VPX to world
			// basis flips Y into -Z, so a fan in ring order comes out facing +Y
			// without needing to be reversed. The half turn is a proper rotation and
			// leaves that alone.
			for (var i = 0; i < count; i++) {
				AddTriangle(triangles, center, start + i, start + (i + 1) % count);
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
			var limit = InsertStarburst.OffsetLimit(body.Depth, body.ChannelDepth);
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
		private static void AddSunkenFloor(InsertOutline outline, InsertBody body, List<RingPoint> rim, int[] rimMap,
			List<RingPoint> island, int[] islandMap, InsertStarburst starburst, float floor, float islandTop,
			float2 size, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
		{
			var weld = outline.WeldTolerance;
			var arrangement = new InsertArrangement(weld);
			var owners = new Dictionary<int, (List<RingPoint> Ring, int Side)>();

			var rimPoints = AddLoop(arrangement, rim, owners);
			var islandPoints = AddLoop(arrangement, island, owners);

			var rungs = new HashSet<(int, int)>();
			for (var i = 0; i < rimMap.Length; i++) {
				if (rungs.Add((rimMap[i], islandMap[i]))) {
					arrangement.AddSegment(rimPoints[rimMap[i]], islandPoints[islandMap[i]]);
				}
			}

			var polygon = new List<float2>(island.Count);
			foreach (var entry in island) {
				polygon.Add(entry.Position);
			}
			var outer = new List<float2>(rim.Count);
			foreach (var entry in rim) {
				outer.Add(entry.Position);
			}

			var holes = AddHoles(arrangement, body.Holes, body.HoleSegments, polygon, starburst, weld, out var holeOwners);

			if (starburst != null) {
				// Spokes that start at the centre all share the one point, so however
				// many of them there are, they meet exactly rather than each arriving
				// at its own idea of where the centre is.
				var center = -1;
				for (var spoke = 0; spoke < starburst.Count; spoke++) {
					if (!starburst.TrySpokeWithin(spoke, polygon, weld, out var from, out var to, out var fromCenter)) {
						continue;
					}

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
					: Contains(polygon, middle) && !InAnyHole(holes, middle) ? Tier.Surface
					: Tier.Floor;
				if (tier == Tier.Outside) {
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
				// the two meet they agree to the bit.
				var first = vertices.Count;
				foreach (var point in face) {
					var position = arrangement.Position(point);
					vertices.Add(ToLocal(position, HeightOf(tier, position)));
					normals.Add(normal);
					uvs.Add(PlanarUv(position, size));
				}
				foreach (var piece in pieces) {
					AddTriangle(triangles, first + piece.x, first + piece.y, first + piece.z);
				}
			}

			Tier TierOf(int from, int to) => faceOf.TryGetValue((from, to), out var face) ? tiers[face] : Tier.Outside;

			var shared = new Dictionary<(int, float, float2, float), int>();
			int WallVertex(int point, float height, float2 outward, float u, float v)
			{
				if (shared.TryGetValue((point, height, outward, u), out var index)) {
					return index;
				}
				index = vertices.Count;
				vertices.Add(ToLocal(arrangement.Position(point), height));
				normals.Add(ToLocalNormal(outward, false));
				uvs.Add(new Vector2(u, v));
				shared[(point, height, outward, u)] = index;
				return index;
			}

			foreach (var (a, b) in arrangement.Edges) {
				var left = TierOf(a, b);
				var right = TierOf(b, a);
				if (left == right) {
					continue;
				}

				// Turned so the lower tier is on its right, which is the way it faces:
				// floor before surface, and both before the outside, which stands at
				// the rim.
				var facing = (Tier)math.min((int)left, (int)right);
				var behind = (Tier)math.max((int)left, (int)right);
				var (from, to) = right == facing ? (a, b) : (b, a);
				var fromPosition = arrangement.Position(from);
				var toPosition = arrangement.Position(to);

				WallNormals(arrangement, owners, holeOwners, from, to, behind == Tier.Outside ? rim : island,
					out var fromNormal, out var toNormal, out var fromU, out var toU);

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

				var bottomFrom = WallVertex(from, fromBottom, fromNormal, fromU, 1f);
				var bottomTo = WallVertex(to, toBottom, toNormal, toU, 1f);
				var topFrom = WallVertex(from, fromTop, fromNormal, fromU, 0f);
				var topTo = WallVertex(to, toTop, toNormal, toU, 0f);
				if (!closedThere) {
					AddTriangle(triangles, bottomFrom, bottomTo, topTo);
				}
				if (!closedHere) {
					AddTriangle(triangles, bottomFrom, topTo, topFrom);
				}
			}
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
			out Dictionary<int, (float2 Center, int Index, int Count)> owners)
		{
			owners = new Dictionary<int, (float2, int, int)>();
			var polygons = new List<List<float2>>();
			if (holes == null) {
				return polygons;
			}

			var count = math.max(3, segments);
			foreach (var hole in holes) {
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
					owners[arrangement.AddSegment(points[k], points[(k + 1) % count])] = (center, k, count);
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
		/// Where a ray from a point inside a convex, counter-clockwise polygon
		/// leaves it.
		/// </summary>
		private static bool TryLeave(List<float2> polygon, float2 origin, float2 direction, out float2 exit)
		{
			var leave = double.PositiveInfinity;
			for (var i = 0; i < polygon.Count; i++) {
				var a = (double2)polygon[i];
				var edge = (double2)polygon[(i + 1) % polygon.Count] - a;
				var outward = new double2(edge.y, -edge.x);
				var rate = math.dot((double2)direction, outward);
				if (rate <= 0.0) {
					continue;
				}
				var t = math.dot(a - (double2)origin, outward) / rate;
				if (t >= 0.0 && t < leave) {
					leave = t;
				}
			}
			exit = (float2)((double2)origin + (double2)direction * leave);
			return !double.IsPositiveInfinity(leave) && leave > 0.0;
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
		private static int[] AddLoop(InsertArrangement arrangement, List<RingPoint> ring,
			Dictionary<int, (List<RingPoint> Ring, int Side)> owners)
		{
			var points = new int[ring.Count];
			for (var i = 0; i < ring.Count; i++) {
				points[i] = arrangement.AddPoint(ring[i].Position);
			}
			for (var i = 0; i < ring.Count; i++) {
				owners[arrangement.AddSegment(points[i], points[(i + 1) % ring.Count])] = (ring, i);
			}
			return points;
		}

		/// <summary>
		/// How a wall standing on the edge from <paramref name="from"/> to
		/// <paramref name="to"/> shades, facing right of that direction, and where
		/// it sits in U.
		/// </summary>
		/// <remarks>
		/// Along a side of <paramref name="preferred"/>, the ring's own normals
		/// carry through, blended along the side exactly as the outline blends
		/// them, so the wall shades round a fillet without a crease. Any other edge
		/// only ever borders two tiers where snapping ran it together with a side,
		/// within a pixel, and simply faces square off itself.
		/// </remarks>
		private static void WallNormals(InsertArrangement arrangement,
			Dictionary<int, (List<RingPoint> Ring, int Side)> owners,
			Dictionary<int, (float2 Center, int Index, int Count)> holeOwners, int from, int to,
			List<RingPoint> preferred, out float2 fromNormal, out float2 toNormal, out float fromU, out float toU)
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
				// Round a hole, the wall shades as the circle it stands on, and runs
				// once round U.
				var (center, index, count) = holeOwners[circle];
				fromNormal = math.normalizesafe(fromPosition - center, right);
				toNormal = math.normalizesafe(toPosition - center, right);
				fromU = (index + arrangement.T(circle, from)) / count;
				toU = (index + arrangement.T(circle, to)) / count;
				if (math.dot(fromNormal + toNormal, right) < 0f) {
					fromNormal = -fromNormal;
					toNormal = -toNormal;
				}
				return;
			}

			if (chosen < 0) {
				fromNormal = toNormal = right;
				fromU = toU = 0f;
				return;
			}

			var (ring, side) = owners[chosen];
			var next = (side + 1) % ring.Count;
			var length = math.distance(ring[side].Position, ring[next].Position);
			var perimeter = ring[^1].Distance + math.distance(ring[^1].Position, ring[0].Position);

			float2 NormalAt(float t) => t <= 0f ? ring[side].NormalOut
				: t >= 1f ? ring[next].NormalIn
				: math.normalize(math.lerp(ring[side].NormalOut, ring[next].NormalIn, t));
			float UAt(float t) => perimeter > InsertOutlineBuilder.Epsilon
				? (ring[side].Distance + t * length) / perimeter
				: 0f;

			var fromT = arrangement.T(chosen, from);
			var toT = arrangement.T(chosen, to);
			fromNormal = NormalAt(fromT);
			toNormal = NormalAt(toT);
			fromU = UAt(fromT);
			toU = UAt(toT);

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
		private static bool Contains(List<float2> polygon, float2 point)
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

		/// <summary>
		/// Area centroid of a ring, which for a convex shape is always strictly
		/// inside it and so is always safe to fan from.
		/// </summary>
		/// <remarks>
		/// Averaging the points instead would drag the centre towards whichever
		/// corner happened to be sampled most finely.
		/// </remarks>
		private static float2 RingCentroid(List<RingPoint> ring)
		{
			var origin = (double2)ring[0].Position;
			var area = 0.0;
			var reach = 0.0;
			var accumulated = double2.zero;
			for (var i = 1; i < ring.Count - 1; i++) {
				var current = (double2)ring[i].Position - origin;
				var next = (double2)ring[i + 1].Position - origin;
				var cross = current.x * next.y - next.x * current.y;
				area += cross;
				accumulated += (current + next) * cross;
				reach = math.max(reach, math.cmax(math.abs(current)));
			}

			// An area, so it is judged against the size squared.
			if (math.abs(area) <= 1e-9 * reach * reach) {
				var fallback = float2.zero;
				foreach (var entry in ring) {
					fallback += entry.Position;
				}
				return fallback / ring.Count;
			}
			return (float2)(origin + accumulated / (3.0 * area));
		}

		private static void AddTriangle(List<int> triangles, int a, int b, int c)
		{
			triangles.Add(a);
			triangles.Add(b);
			triangles.Add(c);
		}

		private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
		{
			AddTriangle(triangles, a, b, c);
			AddTriangle(triangles, a, c, d);
		}

		/// <summary>
		/// Placeholder mapping that just normalises the bounding box, so a material
		/// renders sensibly before step 5 lays out the real UVs.
		/// </summary>
		private static Vector2 PlanarUv(float2 position, float2 size)
		{
			return new Vector2(
				size.x > InsertOutlineBuilder.Epsilon ? position.x / size.x + 0.5f : 0.5f,
				size.y > InsertOutlineBuilder.Epsilon ? position.y / size.y + 0.5f : 0.5f);
		}
	}
}
