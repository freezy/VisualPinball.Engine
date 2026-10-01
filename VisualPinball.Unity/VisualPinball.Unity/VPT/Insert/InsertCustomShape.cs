// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Reads one planar, closed boundary from a wire mesh or a filled surface.</summary>
	public static class InsertCustomShape
	{
		/// <summary>
		/// Whether the mesh's vertices can be read here: a player discards an
		/// imported mesh's data unless its import keeps it readable, but the
		/// editor keeps every imported mesh's data, whatever the import says.
		/// </summary>
		public static bool IsReadable(Mesh mesh)
		{
			if (mesh.isReadable) {
				return true;
			}
#if UNITY_EDITOR
			return UnityEditor.EditorUtility.IsPersistent(mesh);
#else
			return false;
#endif
		}

		/// <summary>Returns centered, counter-clockwise generator coordinates in VPX units.</summary>
		public static Vector2[] Read(Mesh mesh)
		{
			if (!mesh) { throw new InsertOutlineException("Assign a source mesh for the custom shape."); }
			if (!IsReadable(mesh)) {
				throw new InsertOutlineException("The custom shape mesh is not readable. Enable Read/Write in its import settings.");
			}
			var vertices = mesh.vertices;
			if (vertices.Length < 3) { throw new InsertOutlineException("The custom shape needs at least three vertices."); }
			var projected = Project(vertices);
			// Imported meshes often split vertices at UV or normal seams. Those
			// splits are still one geometric vertex when checking the boundary.
			var ids = new int[vertices.Length];
			var welded = new Dictionary<Vector3, int>();
			for (var i = 0; i < vertices.Length; i++) {
				if (!welded.TryGetValue(vertices[i], out var id)) {
					id = i;
					welded.Add(vertices[i], id);
				}
				ids[i] = id;
			}
			var edges = new Dictionary<(int, int), int>();
			bool? surface = null;
			for (var submesh = 0; submesh < mesh.subMeshCount; submesh++) {
				var indices = mesh.GetIndices(submesh);
				if (indices.Length == 0) { continue; }
				var topology = mesh.GetTopology(submesh);
				var faces = topology is MeshTopology.Triangles or MeshTopology.Quads;
				if (!faces && topology is not MeshTopology.Lines and not MeshTopology.LineStrip) {
					throw new InsertOutlineException("The custom shape must contain lines, triangles, or quads.");
				}
				if (surface.HasValue && surface != faces) {
					throw new InsertOutlineException("The custom shape cannot mix a filled surface with loose edges.");
				}
				surface = faces;
				var stride = topology == MeshTopology.Triangles ? 3 : topology == MeshTopology.Quads ? 4 : 2;
				if (topology == MeshTopology.LineStrip) {
					for (var i = 1; i < indices.Length; i++) { AddEdge(indices[i - 1], indices[i]); }
				} else {
					for (var i = 0; i < indices.Length; i += stride) {
						if (faces) {
							for (var j = 0; j < stride; j++) {
								var a = indices[i + j];
								var b = indices[i + (j + 1) % stride];
								AddEdge(a, b);
							}
						} else {
							AddEdge(indices[i], indices[i + 1]);
						}
					}
				}
			}
			var neighbors = new Dictionary<int, List<int>>();
			foreach (var pair in edges) {
				if (pair.Value > (surface == true ? 2 : 1)) {
					throw new InsertOutlineException("The custom shape contains duplicate or non-manifold edges.");
				}
				if (pair.Value != 1) { continue; }
				var (a, b) = pair.Key;
				Link(a, b);
				Link(b, a);
			}
			if (neighbors.Count < 3) { throw new InsertOutlineException("The custom shape has no closed boundary."); }
			foreach (var links in neighbors.Values) {
				if (links.Count < 2) { throw new InsertOutlineException("The custom shape is not closed: its boundary has an open end."); }
				if (links.Count > 2) { throw new InsertOutlineException("The custom shape boundary branches or touches itself at a vertex."); }
			}
			var start = -1;
			foreach (var id in neighbors.Keys) { if (start < 0 || id < start) { start = id; } }
			var loop = new List<Vector2>();
			var previous = -1;
			var current = start;
			do {
				loop.Add(projected[current]);
				var links = neighbors[current];
				var next = links[0] != previous ? links[0] : links[1];
				previous = current;
				current = next;
			} while (current != start);
			if (loop.Count != neighbors.Count) {
				throw new InsertOutlineException("The custom mesh contains more than one shape or boundary. Supply one outline without holes; add holes with the insert controls.");
			}
			Simplify(loop);
			ValidateLoop(loop);
			var signedArea = 0.0;
			for (var i = 0; i < loop.Count; i++) { signedArea += Cross(loop[i], loop[(i + 1) % loop.Count]); }
			if (math.abs(signedArea) <= 1e-8) { throw new InsertOutlineException("The custom shape has zero area."); }
			if (signedArea < 0) { loop.Reverse(); }
			// Canonical ordering keeps an equivalent clockwise wire or reordered
			// triangle mesh from changing the generated mesh and bake hash.
			var first = 0;
			var low = loop[0];
			var high = low;
			for (var i = 1; i < loop.Count; i++) {
				low = Vector2.Min(low, loop[i]);
				high = Vector2.Max(high, loop[i]);
				if (loop[i].x < loop[first].x || loop[i].x == loop[first].x && loop[i].y < loop[first].y) { first = i; }
			}
			var center = (low + high) * 0.5f;
			var result = new Vector2[loop.Count];
			for (var i = 0; i < result.Length; i++) { result[i] = loop[(first + i) % loop.Count] - center; }
			return result;

			void AddEdge(int a, int b)
			{
				a = ids[a];
				b = ids[b];
				if (a == b) { throw new InsertOutlineException("The custom shape contains a zero-length edge."); }
				var key = a < b ? (a, b) : (b, a);
				edges.TryGetValue(key, out var count);
				edges[key] = count + 1;
			}
			void Link(int a, int b)
			{
				if (!neighbors.TryGetValue(a, out var links)) { neighbors[a] = links = new List<int>(2); }
				links.Add(b);
			}
		}

		public static InsertCorner[] Corners(Mesh mesh, InsertCornerSettings settings)
		{
			var loop = Read(mesh);
			var corners = new InsertCorner[loop.Length];
			for (var i = 0; i < loop.Length; i++) {
				corners[i] = new InsertCorner((float2)loop[i], settings.Radius, settings.Segments);
			}
			return corners;
		}

		private static Vector2[] Project(Vector3[] vertices)
		{
			var low = vertices[0];
			var high = low;
			foreach (var vertex in vertices) {
				if (!math.all(math.isfinite((float3)vertex))) { throw new InsertOutlineException("The custom mesh contains invalid vertex coordinates."); }
				low = Vector3.Min(low, vertex);
				high = Vector3.Max(high, vertex);
			}
			var tolerance = math.max(1e-7f, (high - low).magnitude * 1e-5f);
			var origin = vertices[0];
			var baseline = Vector3.zero;
			foreach (var vertex in vertices) {
				if ((vertex - origin).sqrMagnitude > baseline.sqrMagnitude) { baseline = vertex - origin; }
			}
			var normal = Vector3.zero;
			foreach (var vertex in vertices) {
				var cross = Vector3.Cross(baseline, vertex - origin);
				if (cross.sqrMagnitude > normal.sqrMagnitude) { normal = cross; }
			}
			if (normal.magnitude <= tolerance * baseline.magnitude) {
				throw new InsertOutlineException("The custom shape is degenerate: its vertices are collinear.");
			}
			normal.Normalize();
			foreach (var vertex in vertices) {
				if (math.abs(Vector3.Dot(normal, vertex - origin)) > tolerance) {
					throw new InsertOutlineException("The custom shape is not flat: all vertices must lie in one plane.");
				}
			}
			// Pick the same plane orientation regardless of face or edge winding.
			var absolute = math.abs((float3)normal);
			var axis = absolute.y >= absolute.x && absolute.y >= absolute.z ? 1 : absolute.z >= absolute.x ? 2 : 0;
			if (normal[axis] < 0f) { normal = -normal; }
			var rotation = Quaternion.FromToRotation(normal, Vector3.up);
			var result = new Vector2[vertices.Length];
			for (var i = 0; i < vertices.Length; i++) {
				var point = rotation * (vertices[i] - origin);
				result[i] = new Vector2(-point.x, point.z) / Physics.ScaleInv;
			}
			return result;
		}

		private static void Simplify(List<Vector2> loop)
		{
			// A straight run may contain any number of imported samples. The
			// outline builder needs actual turns, not collinear pseudo-corners.
			for (var i = 0; i < loop.Count && loop.Count >= 3;) {
				var a = (loop[i] - loop[(i + loop.Count - 1) % loop.Count]).normalized;
				var b = (loop[(i + 1) % loop.Count] - loop[i]).normalized;
				if (math.abs(Cross(a, b)) < InsertOutlineBuilder.AngleEpsilon) {
					if (Vector2.Dot(a, b) <= 0f) { throw new InsertOutlineException("The custom shape boundary doubles back on itself."); }
					loop.RemoveAt(i);
					i = 0;
				} else { i++; }
			}
			if (loop.Count < 3) { throw new InsertOutlineException("The custom shape has zero area."); }
		}

		private static void ValidateLoop(IReadOnlyList<Vector2> loop)
		{
			for (var i = 0; i < loop.Count; i++) {
				var a = loop[i];
				var b = loop[(i + 1) % loop.Count];
				for (var j = i + 2; j < loop.Count; j++) {
					if (i == 0 && j == loop.Count - 1) { continue; }
					var c = loop[j];
					var d = loop[(j + 1) % loop.Count];
					if (Vector2.Min(a, b).x > Vector2.Max(c, d).x || Vector2.Min(c, d).x > Vector2.Max(a, b).x
						|| Vector2.Min(a, b).y > Vector2.Max(c, d).y || Vector2.Min(c, d).y > Vector2.Max(a, b).y) { continue; }
					if (Cross(b - a, c - a) * Cross(b - a, d - a) <= 0.0
						&& Cross(d - c, a - c) * Cross(d - c, b - c) <= 0.0) {
						throw new InsertOutlineException("The custom shape boundary crosses or touches itself.");
					}
				}
			}
		}

		private static double Cross(Vector2 a, Vector2 b) => (double)a.x * b.y - (double)a.y * b.x;
	}
}
