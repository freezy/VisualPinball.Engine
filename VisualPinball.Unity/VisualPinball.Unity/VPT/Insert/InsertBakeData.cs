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
using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A normal bake of one generated insert mesh: its optimized UVs and the
	/// tangents the map was baked against, per triangle corner, and the map
	/// itself with a material to show it.
	/// </summary>
	/// <remarks>
	/// It fits exactly the mesh whose <see cref="InsertMeshLayout.Hash"/> it was
	/// made from. Any other mesh gets it not at all, so a stale bake can never
	/// put its map on UVs it was not baked for.
	/// </remarks>
	public class InsertBakeData : ScriptableObject
	{
		[Tooltip("The hash of the generated mesh this bake was made from.")]
		public string Hash;

		[Tooltip("The UV of every triangle corner, in the generated mesh's triangle order.")]
		public Vector2[] Uvs;

		[Tooltip("The tangent of every triangle corner, with the bitangent's sign in w.")]
		public Vector4[] Tangents;

		public Texture2D NormalMap;

		[Tooltip("A copy of the insert's material with the normal map set, which the insert wears while the bake is fresh.")]
		public Material Material;

		[Tooltip("Which UV packer laid the charts out.")]
		public string Packer;

		[Tooltip("How much of the texture the UV charts cover.")]
		public float UvFill;

		public bool Fits(Hash128 hash) => Hash == hash.ToString();

		/// <summary>
		/// Replaces <paramref name="mesh"/>'s UVs with the baked ones and adds the
		/// baked tangents, splitting a vertex wherever its corners disagree.
		/// </summary>
		/// <returns>False, leaving the mesh as it was, when the bake does not fit it.</returns>
		public bool TryApply(Mesh mesh, Hash128 hash)
		{
			if (!Fits(hash)) {
				return false;
			}
			var triangles = mesh.triangles;
			if (Uvs == null || Tangents == null || Uvs.Length != triangles.Length || Tangents.Length != triangles.Length) {
				return false;
			}

			var vertices = mesh.vertices;
			var normals = mesh.normals;
			var outVertices = new List<Vector3>(vertices.Length);
			var outNormals = new List<Vector3>(vertices.Length);
			var outUvs = new List<Vector2>(vertices.Length);
			var outTangents = new List<Vector4>(vertices.Length);
			var split = new Dictionary<(int, Vector2, Vector4), int>(triangles.Length);
			for (var corner = 0; corner < triangles.Length; corner++) {
				var vertex = triangles[corner];
				var key = (vertex, Uvs[corner], Tangents[corner]);
				if (!split.TryGetValue(key, out var index)) {
					index = outVertices.Count;
					split.Add(key, index);
					outVertices.Add(vertices[vertex]);
					outNormals.Add(normals[vertex]);
					outUvs.Add(Uvs[corner]);
					outTangents.Add(Tangents[corner]);
				}
				triangles[corner] = index;
			}

			mesh.Clear();
			mesh.indexFormat = outVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.SetVertices(outVertices);
			mesh.SetNormals(outNormals);
			mesh.SetUVs(0, outUvs);
			mesh.SetTangents(outTangents);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return true;
		}
	}
}
