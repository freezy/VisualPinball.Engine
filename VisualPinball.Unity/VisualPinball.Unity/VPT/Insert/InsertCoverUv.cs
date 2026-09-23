// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Projects a playfield's planar XZ-to-UV mapping onto a transformed insert cover.</summary>
	public readonly struct InsertCoverUv
	{
		private sealed class CachedProjection
		{
			public CachedProjection() { }
			public Bounds Bounds;
			public int Vertices, Indices, Version;
			public bool Valid;
			public InsertCoverUv Mapping;
		}

		private static ConditionalWeakTable<Mesh, CachedProjection> _cache = new();
		private static int _version;

#if UNITY_EDITOR
		static InsertCoverUv() => UnityEditor.EditorApplication.projectChanged += () => _cache = new();
#endif

		/// <summary>Call after changing source UVs in place without changing mesh bounds or topology.</summary>
		public static void Invalidate(Mesh mesh)
		{
			if (mesh != null) { _cache.Remove(mesh); }
		}

		public static bool TryCreate(Mesh mesh, out InsertCoverUv mapping) => TryCreate(mesh, out mapping, out _);

		public static bool TryCreate(Mesh mesh, out InsertCoverUv mapping, out int version)
		{
			mapping = default;
			version = 0;
			if (mesh == null || !mesh.isReadable || mesh.subMeshCount == 0 || mesh.GetTopology(0) != MeshTopology.Triangles) { return false; }
			var cached = _cache.GetOrCreateValue(mesh);
			var indices = (int)mesh.GetIndexCount(0);
			if (cached.Version == 0 || cached.Bounds != mesh.bounds || cached.Vertices != mesh.vertexCount || cached.Indices != indices) {
				cached.Valid = TryCreateUncached(mesh, out cached.Mapping);
				cached.Bounds = mesh.bounds;
				cached.Vertices = mesh.vertexCount;
				cached.Indices = indices;
				cached.Version = ++_version;
			}
			mapping = cached.Mapping;
			version = cached.Version;
			return cached.Valid;
		}

		private readonly Vector3 _u;
		private readonly Vector3 _v;

		private InsertCoverUv(Vector3 u, Vector3 v) { _u = u; _v = v; }

		public Vector2 Project(Vector3 position) => new(
			_u.x * position.x + _u.y * position.z + _u.z,
			_v.x * position.x + _v.y * position.z + _v.z);

		private static bool TryCreateUncached(Mesh mesh, out InsertCoverUv mapping)
		{
			mapping = default;
			if (mesh == null || !mesh.isReadable || mesh.subMeshCount == 0) {
				return false;
			}
			var vertices = mesh.vertices;
			var uv = mesh.uv;
			if (uv.Length != vertices.Length) {
				return false;
			}
			// Use a large top-facing triangle for numerical stability. A bounds
			// projection would miss margins, flips and offsets in the actual UVs.
			var triangles = mesh.GetTriangles(0);
			var topVertices = new bool[vertices.Length];
			var best = -1;
			var area = 0f;
			var top = mesh.bounds.max.y - Mathf.Max(mesh.bounds.size.y * 0.001f, 1e-5f);
			for (var i = 0; i < triangles.Length; i += 3) {
				var a = vertices[triangles[i]];
				var b = vertices[triangles[i + 1]];
				var c = vertices[triangles[i + 2]];
				if (a.y < top || b.y < top || c.y < top) { continue; }
				var cross = Vector3.Cross(b - a, c - a).y;
				if (cross <= 1e-12f) { continue; }
				topVertices[triangles[i]] = topVertices[triangles[i + 1]] = topVertices[triangles[i + 2]] = true;
				if (cross > area) { best = i; area = cross; }
			}
			if (best < 0 || area < 1e-12f) { return false; }
			var first = triangles[best];
			var p = vertices[first];
			var d1 = vertices[triangles[best + 1]] - p;
			var d2 = vertices[triangles[best + 2]] - p;
			var t1 = uv[triangles[best + 1]] - uv[first];
			var t2 = uv[triangles[best + 2]] - uv[first];
			var det = d1.x * d2.z - d2.x * d1.z;
			var u = new Vector3((t1.x * d2.z - t2.x * d1.z) / det, (t2.x * d1.x - t1.x * d2.x) / det, 0f);
			var v = new Vector3((t1.y * d2.z - t2.y * d1.z) / det, (t2.y * d1.x - t1.y * d2.x) / det, 0f);
			u.z = uv[first].x - u.x * p.x - u.y * p.z;
			v.z = uv[first].y - v.x * p.x - v.y * p.z;
			mapping = new InsertCoverUv(u, v);
			if (Mathf.Abs(u.x * v.y - u.y * v.x) < 1e-10f) { return false; }
			// Side walls often duplicate top-edge vertices with unrelated UVs.
			// Only vertices belonging to the top surface constrain its projection.
			for (var i = 0; i < vertices.Length; i++) {
				if (topVertices[i] && (mapping.Project(vertices[i]) - uv[i]).sqrMagnitude > 1e-6f) {
					return false;
				}
			}
			return true;
		}

		public void Apply(Mesh mesh, int firstVertex, Matrix4x4 insertToPlayfield, Vector2 scale, Vector2 offset)
		{
			var vertices = mesh.vertices;
			var uvs = mesh.uv;
			var tangents = mesh.tangents;
			// Derive the tangent basis from the same projection. Rotating an insert
			// must not rotate its scratches away from the playfield's scratches.
			// Material tiling changes sampling, not the playfield mesh's tangent frame.
			var origin = Project(insertToPlayfield.MultiplyPoint3x4(Vector3.zero));
			var dx = Project(insertToPlayfield.MultiplyPoint3x4(Vector3.right)) - origin;
			var dz = Project(insertToPlayfield.MultiplyPoint3x4(Vector3.forward)) - origin;
			var determinant = dx.x * dz.y - dz.x * dx.y;
			var tangent = Mathf.Abs(determinant) > 1e-10f ? new Vector3(dz.y, 0f, -dx.y) / determinant : Vector3.right;
			var bitangent = Mathf.Abs(determinant) > 1e-10f ? new Vector3(-dz.x, 0f, dx.x) / determinant : Vector3.forward;
			tangent.Normalize();
			var frame = new Vector4(tangent.x, tangent.y, tangent.z, Vector3.Dot(Vector3.Cross(Vector3.up, tangent), bitangent) < 0f ? -1f : 1f);
			for (var i = firstVertex; i < vertices.Length; i++) {
				uvs[i] = Vector2.Scale(Project(insertToPlayfield.MultiplyPoint3x4(vertices[i])), scale) + offset;
				tangents[i] = frame;
			}
			mesh.uv = uvs;
			mesh.tangents = tangents;
		}

		/// <summary>Restores the cover's local 0–1 UVs when playfield mapping is disabled or unavailable.</summary>
		public static void ApplyLocal(Mesh mesh, int firstVertex)
		{
			var vertices = mesh.vertices;
			var uvs = mesh.uv;
			var tangents = mesh.tangents;
			var size = mesh.bounds.size;
			for (var i = firstVertex; i < vertices.Length; i++) {
				uvs[i] = new Vector2((vertices[i].x - mesh.bounds.min.x) / size.x, (vertices[i].z - mesh.bounds.min.z) / size.z);
				tangents[i] = new Vector4(1f, 0f, 0f, -1f);
			}
			mesh.uv = uvs;
			mesh.tangents = tangents;
		}
	}
}
