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

			var rim = BuildRing(outline, 0f, out _);
			AddWall(rim, 0f, -body.Depth, true, vertices, normals, uvs, triangles);

			var floor = -body.Depth;
			if (body.ChannelWidth > InsertOutlineBuilder.Epsilon) {
				var inner = BuildRing(outline, body.ChannelWidth, out var map);

				// The channel floor, then the raised middle's wall facing back out
				// across it, then the middle itself.
				AddStrip(rim, inner, map, floor, size, vertices, normals, uvs, triangles);

				// A channel with no depth is a legal setting and just means an inset
				// flat floor. Building its wall anyway would put a ring of zero area
				// triangles between two coincident rings.
				if (body.ChannelDepth > InsertOutlineBuilder.Epsilon) {
					AddWall(inner, floor + body.ChannelDepth, floor, false, vertices, normals, uvs, triangles);
				}
				AddCap(inner, floor + body.ChannelDepth, size, vertices, normals, uvs, triangles);

			} else {
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
		/// entry it ended up in, so a strip can be built across the two.
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

			var distance = 0f;
			for (var i = 0; i < ring.Count; i++) {
				var entry = ring[i];
				entry.Distance = distance;
				ring[i] = entry;
				distance += math.distance(entry.Position, ring[(i + 1) % ring.Count].Position);
			}
			return ring;
		}

		/// <summary>
		/// A wall between two heights, facing either into the hollow or out of it.
		/// </summary>
		private static void AddWall(List<RingPoint> ring, float top, float bottom, bool inwards,
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
				var split = math.distance(entry.NormalIn, entry.NormalOut) > InsertOutlineBuilder.Epsilon;

				// The ring closes back onto its first entry, which therefore needs a
				// second copy carrying the far end of the U range instead of zero.
				var seam = i == 0;
				var arrivingDistance = seam ? perimeter : entry.Distance;

				arrivingTop[i] = AddWallVertex(entry.Position, entry.NormalIn, arrivingDistance, perimeter,
					top, bottom, inwards, out arrivingBottom[i], vertices, normals, uvs);

				if (split || seam) {
					leavingTop[i] = AddWallVertex(entry.Position, entry.NormalOut, entry.Distance, perimeter,
						top, bottom, inwards, out leavingBottom[i], vertices, normals, uvs);
				} else {
					leavingTop[i] = arrivingTop[i];
					leavingBottom[i] = arrivingBottom[i];
				}
			}

			for (var i = 0; i < count; i++) {
				var next = (i + 1) % count;
				if (inwards) {
					// Along the top, then down the far side: that order faces it in.
					AddQuad(triangles, leavingTop[i], arrivingTop[next], arrivingBottom[next], leavingBottom[i]);
				} else {
					AddQuad(triangles, leavingBottom[i], arrivingBottom[next], arrivingTop[next], leavingTop[i]);
				}
			}
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
		/// The flat channel floor between the outer ring and the inset one.
		/// </summary>
		/// <remarks>
		/// Where the inset has eroded a fillet away, several outer points share one
		/// inner point and the quad between them closes as a triangle instead.
		/// </remarks>
		private static void AddStrip(List<RingPoint> outer, List<RingPoint> inner, int[] map, float depth,
			float2 size, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
		{
			var outerStart = vertices.Count;
			foreach (var entry in outer) {
				vertices.Add(ToLocal(entry.Position, depth));
				normals.Add(Vector3.up);
				uvs.Add(PlanarUv(entry.Position, size));
			}

			var innerStart = vertices.Count;
			foreach (var entry in inner) {
				vertices.Add(ToLocal(entry.Position, depth));
				normals.Add(Vector3.up);
				uvs.Add(PlanarUv(entry.Position, size));
			}

			for (var i = 0; i < outer.Count; i++) {
				var next = (i + 1) % outer.Count;
				if (map[i] == map[next]) {
					AddTriangle(triangles, outerStart + i, outerStart + next, innerStart + map[i]);
				} else {
					AddQuad(triangles, outerStart + i, outerStart + next,
						innerStart + map[next], innerStart + map[i]);
				}
			}
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
		/// Area centroid of a ring, which for a convex shape is always strictly
		/// inside it and so is always safe to fan from.
		/// </summary>
		/// <remarks>
		/// Averaging the points instead would drag the centre towards whichever
		/// corner happened to be sampled most finely.
		/// </remarks>
		private static float2 RingCentroid(List<RingPoint> ring)
		{
			var area = 0f;
			var accumulated = float2.zero;
			for (var i = 0; i < ring.Count; i++) {
				var current = ring[i].Position;
				var next = ring[(i + 1) % ring.Count].Position;
				var cross = current.x * next.y - next.x * current.y;
				area += cross;
				accumulated += (current + next) * cross;
			}

			if (math.abs(area) < InsertOutlineBuilder.Epsilon) {
				var fallback = float2.zero;
				foreach (var entry in ring) {
					fallback += entry.Position;
				}
				return fallback / ring.Count;
			}
			return accumulated / (3f * area);
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
