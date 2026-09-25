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
	/// made from. A changed mesh keeps its generated UVs and tangents; the
	/// material can remain visible as an approximate preview until rebaked.
	/// </remarks>
	public class InsertBakeData : ScriptableObject
	{
		[Tooltip("The hash of the generated mesh this bake was made from.")]
		public string Hash;

		[Tooltip("The id of the insert this bake was made for, which is the only one allowed to write over it. Empty on bakes from before inserts had ids.")]
		public string Owner;

		[Tooltip("The UV of every triangle corner, in the generated mesh's triangle order.")]
		public Vector2[] Uvs;

		[Tooltip("The tangent of every triangle corner, with the bitangent's sign in w.")]
		public Vector4[] Tangents;

		public Texture2D NormalMap;

		[Tooltip("Bevelled optical normals in insert local space, encoded as RGB = normal * 0.5 + 0.5. Linear data, independent of UV chart rotation.")]
		public Texture2D OpticalNormalMap;

		[Tooltip("Linear RGB: soft cavity illumination, rounded edge response, and transmission mask. These approximate internal light transport.")]
		public Texture2D OpticalResponseMap;

		[Tooltip("Suggested virtual lamp position in insert local metres, and its radius in w.")]
		public Vector4 OpticalLamp;

		[Tooltip("Local X/Z starburst centre, sector count, and lowest starburst height, used to keep optical variation aligned with the generated faces.")]
		public Vector4 OpticalStarburst;

		[Tooltip("The material with the baked textures, retained as a preview between bakes.")]
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
		/// <param name="mesh">The generated mesh, which the bake must fit.</param>
		/// <param name="hash">The generated mesh's hash.</param>
		/// <param name="flat">
		/// Per generated vertex, whether it is pressed flat: laid at
		/// <paramref name="flatHeight"/>, facing up, with its tangent taken from
		/// the baked UVs over the flat face, the basis the bake's own flat copy
		/// of the raised middle was baked against. What the mesh carved into the
		/// middle for the bake is carried by the maps from then on.
		/// </param>
		/// <param name="flatHeight">The local height the flat vertices are laid at.</param>
		/// <returns>False, leaving the mesh as it was, when the bake does not fit it.</returns>
		public bool TryApply(Mesh mesh, Hash128 hash, bool[] flat = null, float flatHeight = 0f)
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
			var outFlat = flat != null ? new List<bool>(vertices.Length) : null;
			var split = new Dictionary<(int, Vector2, Vector4), int>(triangles.Length);
			for (var corner = 0; corner < triangles.Length; corner++) {
				var vertex = triangles[corner];
				var pressed = flat != null && vertex < flat.Length && flat[vertex];
				// A pressed vertex's baked tangent is replaced below, so it does
				// not split the vertex.
				var key = (vertex, Uvs[corner], pressed ? Vector4.zero : Tangents[corner]);
				if (!split.TryGetValue(key, out var index)) {
					index = outVertices.Count;
					split.Add(key, index);
					var position = vertices[vertex];
					if (pressed) {
						position.y = flatHeight;
					}
					outVertices.Add(position);
					outNormals.Add(pressed ? Vector3.up : normals[vertex]);
					outUvs.Add(Uvs[corner]);
					outTangents.Add(Tangents[corner]);
					outFlat?.Add(pressed);
				}
				triangles[corner] = index;
			}
			if (outFlat != null) {
				FlatTangents(outVertices, outUvs, outTangents, triangles, outFlat);
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

		/// <summary>
		/// The tangents of the pressed vertices, from the baked UVs over the flat
		/// faces they share, the way a tangent-space bake onto a flat mesh sets
		/// its basis: along U, with the bitangent's sign in w.
		/// </summary>
		private static void FlatTangents(List<Vector3> vertices, List<Vector2> uvs, List<Vector4> tangents, int[] triangles, List<bool> flat)
		{
			var along = new Vector3[vertices.Count];
			var across = new Vector3[vertices.Count];
			for (var corner = 0; corner < triangles.Length; corner += 3) {
				var a = triangles[corner];
				var b = triangles[corner + 1];
				var c = triangles[corner + 2];
				if (!flat[a] || !flat[b] || !flat[c]) {
					continue;
				}
				var e1 = vertices[b] - vertices[a];
				var e2 = vertices[c] - vertices[a];
				var d1 = uvs[b] - uvs[a];
				var d2 = uvs[c] - uvs[a];
				var determinant = d1.x * d2.y - d2.x * d1.y;
				if (Mathf.Abs(determinant) < 1e-12f) {
					continue;
				}
				var r = 1f / determinant;
				var tangent = (e1 * d2.y - e2 * d1.y) * r;
				var bitangent = (e2 * d1.x - e1 * d2.x) * r;
				along[a] += tangent;
				along[b] += tangent;
				along[c] += tangent;
				across[a] += bitangent;
				across[b] += bitangent;
				across[c] += bitangent;
			}
			for (var i = 0; i < vertices.Count; i++) {
				if (!flat[i]) {
					continue;
				}
				var tangent = along[i];
				tangent.y = 0f;
				if (tangent.sqrMagnitude < 1e-18f) {
					continue;
				}
				tangent.Normalize();
				var sign = Vector3.Dot(Vector3.Cross(Vector3.up, tangent), across[i]) < 0f ? -1f : 1f;
				tangents[i] = new Vector4(tangent.x, 0f, tangent.z, sign);
			}
		}
	}
}
