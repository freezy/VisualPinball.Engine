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
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A normal bake of one generated insert mesh: its optimized UVs and the
	/// tangents the map was baked against, per triangle corner, and the map
	/// itself with a material to show it.
	/// </summary>
	/// <remarks>
	/// It fits exactly the mesh whose <see cref="InsertMeshLayout.Hash"/> it was
	/// made from. For stipples that hash describes the detailed source, while
	/// the UVs and tangents follow the plain display mesh. A changed mesh keeps
	/// its generated UVs and tangents; its material remains as an approximate
	/// preview until rebaked.
	/// </remarks>
	public class InsertBakeData : ScriptableObject, ISerializationCallbackReceiver
	{
		[Tooltip("The hash of the generated mesh this bake was made from.")]
		public string Hash;

		[Tooltip("The stipple relief was baked onto the plain display mesh. UVs and tangents follow that mesh's triangle order.")]
		public bool PlainStipple;

		[Tooltip("The id of the insert this bake was made for, which is the only one allowed to write over it. Empty on bakes from before inserts had ids.")]
		public string Owner;

		/// <summary>The UV of every triangle corner, in the generated mesh's triangle order.</summary>
		/// <remarks>Saved in 16 bits per axis, which is how it reads after a save.</remarks>
		[NonSerialized] public Vector2[] Uvs;

		/// <summary>The tangent of every triangle corner, with the bitangent's sign in w.</summary>
		/// <remarks>Saved in 8 bits per component, as fine as the normal map baked against it.</remarks>
		[NonSerialized] public Vector4[] Tangents;

		// Each distinct UV and tangent is saved once, eight bytes, and every
		// corner as an index to it: a YAML line per corner made a bake a
		// megabyte of text.
		[SerializeField, HideInInspector] private int _cornerCount;
		[SerializeField, HideInInspector] private byte[] _cornerIndices;
		[SerializeField, HideInInspector] private byte[] _cornerData;

		// Bakes from before, with a YAML line per corner, read once.
		[SerializeField, HideInInspector, FormerlySerializedAs("Uvs")] private Vector2[] _legacyUvs;
		[SerializeField, HideInInspector, FormerlySerializedAs("Tangents")] private Vector4[] _legacyTangents;

		public Texture2D NormalMap;

		[Tooltip("Bevelled optical normals in insert local space, encoded as RGB = normal * 0.5 + 0.5. Linear data, independent of UV chart rotation.")]
		public Texture2D OpticalNormalMap;

		[Tooltip("Linear RGB: soft cavity illumination, rounded edge response, and transmission mask. These approximate internal light transport.")]
		public Texture2D OpticalResponseMap;

		[Tooltip("Suggested virtual lamp position in insert local metres, and its radius in w.")]
		public Vector4 OpticalLamp;

		[Tooltip("Local X/Z starburst centre, sector count, and lowest starburst height, used to keep optical variation aligned with the generated faces. For ribs: the direction across them in X/Z, the half pitch in metres made negative, and the lowest height. For rings: the centre in X/Z, the half pitch in metres made negative and pushed past minus a thousand, and the lowest height. For a gem: the centre in X/Z, the number of sides pushed past two thousand, and the lowest height.")]
		public Vector4 OpticalStarburst;

		[Tooltip("The material with the baked textures, retained as a preview between bakes.")]
		public Material Material;

		[Tooltip("Which UV packer laid the charts out.")]
		public string Packer;

		[Tooltip("How much of the texture the UV charts cover.")]
		public float UvFill;

		public bool Fits(Hash128 hash) => Hash == hash.ToString();

		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{
			_legacyUvs = null;
			_legacyTangents = null;
			if (Uvs == null || Tangents == null || Uvs.Length != Tangents.Length) {
				_cornerCount = 0;
				_cornerIndices = null;
				_cornerData = null;
				return;
			}
			var count = Uvs.Length;
			var distinct = new Dictionary<ulong, int>(count);
			var data = new List<byte>(count * 8);
			var indices = new int[count];
			for (var corner = 0; corner < count; corner++) {
				var u = QuantizeUv(Uvs[corner].x);
				var v = QuantizeUv(Uvs[corner].y);
				var t = Tangents[corner];
				var x = QuantizeTangent(t.x);
				var y = QuantizeTangent(t.y);
				var z = QuantizeTangent(t.z);
				var w = (sbyte)(t.w < 0f ? -127 : 127);
				// What is saved is what the insert wears from now on, not only after a reload.
				Uvs[corner] = new Vector2(u / UvScale, v / UvScale);
				Tangents[corner] = new Vector4(x / 127f, y / 127f, z / 127f, w / 127f);
				var key = (ulong)u | (ulong)v << 16 | (ulong)(byte)x << 32 | (ulong)(byte)y << 40 | (ulong)(byte)z << 48 | (ulong)(byte)w << 56;
				if (!distinct.TryGetValue(key, out var index)) {
					index = distinct.Count;
					distinct.Add(key, index);
					for (var i = 0; i < 8; i++) {
						data.Add((byte)(key >> (i * 8)));
					}
				}
				indices[corner] = index;
			}
			var width = distinct.Count > ushort.MaxValue + 1 ? 4 : 2;
			_cornerIndices = new byte[count * width];
			for (var corner = 0; corner < count; corner++) {
				for (var i = 0; i < width; i++) {
					_cornerIndices[corner * width + i] = (byte)(indices[corner] >> (i * 8));
				}
			}
			_cornerCount = count;
			_cornerData = data.ToArray();
		}

		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			if (_legacyUvs is { Length: > 0 }) {
				Uvs = _legacyUvs;
				Tangents = _legacyTangents;
				_legacyUvs = null;
				_legacyTangents = null;
				return;
			}
			Uvs = null;
			Tangents = null;
			// A damaged or half merged bake reads as no bake, which leaves the generated mesh as it is.
			if (_cornerCount <= 0 || _cornerIndices == null || _cornerData == null || _cornerData.Length % 8 != 0) {
				return;
			}
			var width = _cornerIndices.Length / _cornerCount;
			if (width != 2 && width != 4 || _cornerIndices.Length != _cornerCount * width) {
				return;
			}
			var records = _cornerData.Length / 8;
			var uvs = new Vector2[_cornerCount];
			var tangents = new Vector4[_cornerCount];
			for (var corner = 0; corner < _cornerCount; corner++) {
				var index = 0u;
				for (var i = 0; i < width; i++) {
					index |= (uint)_cornerIndices[corner * width + i] << (i * 8);
				}
				if (index >= records) {
					return;
				}
				var at = (int)index * 8;
				uvs[corner] = new Vector2(
					(_cornerData[at] | _cornerData[at + 1] << 8) / UvScale,
					(_cornerData[at + 2] | _cornerData[at + 3] << 8) / UvScale);
				tangents[corner] = new Vector4(
					(sbyte)_cornerData[at + 4] / 127f,
					(sbyte)_cornerData[at + 5] / 127f,
					(sbyte)_cornerData[at + 6] / 127f,
					(sbyte)_cornerData[at + 7] / 127f);
			}
			Uvs = uvs;
			Tangents = tangents;
		}

		// A power of two, so that halves and quarters stay exact.
		private const float UvScale = 65536f;

		private static ushort QuantizeUv(float value) => (ushort)math.clamp((int)math.round(value * UvScale), 0, ushort.MaxValue);

		private static sbyte QuantizeTangent(float value) => (sbyte)math.clamp((int)math.round(value * 127f), -127, 127);

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
