// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor-only data for rebuilding editable inserts. The player never reads meta/inserts.json;
	/// it uses the finished meshes and material slots in table.glb.
	/// </summary>
	[Serializable]
	public sealed class InsertAuthoringPayload
	{
		public const int Version = 4;
		public int FormatVersion = Version;
		public InsertAuthoringRecord[] Inserts = Array.Empty<InsertAuthoringRecord>();

		/// <summary>
		/// The sidecar as it is stored: its JSON, and next to it the buffer with the per-vertex arrays of the records
		/// (<c>meta/inserts.bin</c>). As JSON those arrays were nine tenths of the file and half of what it took in the
		/// package.
		/// </summary>
		public byte[] Pack(out byte[] buffer)
		{
			using var stream = new MemoryStream();
			foreach (var record in Inserts ?? Array.Empty<InsertAuthoringRecord>()) {
				if (record?.Bake != null) {
					record.Bake.UvsView = InsertBufferView.Append(stream, record.Bake.Uvs);
					record.Bake.TangentsView = InsertBufferView.Append(stream, record.Bake.Tangents);
				}
				if (record?.CustomMesh != null) {
					record.CustomMesh.VerticesView = InsertBufferView.Append(stream, record.CustomMesh.Vertices);
					foreach (var submesh in record.CustomMesh.Submeshes ?? Array.Empty<InsertCustomSubmeshAuthoringRecord>()) {
						submesh.IndicesView = InsertBufferView.Append(stream, submesh.Indices);
					}
				}
			}
			buffer = stream.ToArray();
			return PackageApi.Packer.Pack(this);
		}

		/// <summary>Reads the sidecar from its JSON and its buffer, which is null when the package has none.</summary>
		/// <exception cref="InvalidDataException">A record points outside the buffer.</exception>
		public static InsertAuthoringPayload Unpack(byte[] json, byte[] buffer)
		{
			var payload = PackageApi.Packer.Unpack<InsertAuthoringPayload>(json);
			buffer ??= Array.Empty<byte>();
			foreach (var record in payload.Inserts ?? Array.Empty<InsertAuthoringRecord>()) {
				if (record?.Bake != null) {
					record.Bake.Uvs = InsertBufferView.Read<Vector2>(buffer, record.Bake.UvsView);
					record.Bake.Tangents = InsertBufferView.Read<Vector4>(buffer, record.Bake.TangentsView);
				}
				if (record?.CustomMesh != null) {
					record.CustomMesh.Vertices = InsertBufferView.Read<Vector3>(buffer, record.CustomMesh.VerticesView);
					foreach (var submesh in record.CustomMesh.Submeshes ?? Array.Empty<InsertCustomSubmeshAuthoringRecord>()) {
						submesh.Indices = InsertBufferView.Read<int>(buffer, submesh.IndicesView);
					}
				}
			}
			return payload;
		}

		public static InsertAuthoringPayload Capture(Transform root,
			IReadOnlyDictionary<Transform, string> nodeIds,
			IReadOnlyDictionary<Material, string> materialIds,
			IReadOnlyDictionary<Texture2D, string> textureIds)
		{
			var records = new List<InsertAuthoringRecord>();
			foreach (var insert in root.GetComponentsInChildren<InsertComponent>(true)) {
				if (!nodeIds.TryGetValue(insert.transform, out var nodeId)) { continue; }
				var serialized = JObject.Parse(JsonUtility.ToJson(insert));
				serialized.Remove("_bake");
				serialized.Remove("_unbakedMaterial");
				serialized.Remove("_coverMaterial");
				serialized.Remove("_coverPlayfield");
				serialized.Remove("_usesPackagedMesh");
				serialized.Remove("_customMesh");
				var record = new InsertAuthoringRecord {
					NodeId = nodeId,
					GeneratorJson = serialized.ToString(Newtonsoft.Json.Formatting.None),
					PreBakeMaterialId = MaterialId(insert.PreBakeMaterial, materialIds),
					CustomMesh = serialized.Value<int>("_shape") == (int)InsertShape.Custom
						? InsertCustomMeshAuthoringRecord.Capture(insert.CustomMesh) : null,
				};
				if (insert.Bake != null) {
					var bake = insert.Bake;
					record.Bake = new InsertBakeAuthoringRecord {
						Hash = bake.Hash,
						PlainStipple = bake.PlainStipple,
						Owner = bake.Owner,
						Uvs = bake.Uvs,
						Tangents = bake.Tangents,
						NormalMapId = TextureId(bake.NormalMap, textureIds),
						OpticalNormalMapId = TextureId(bake.OpticalNormalMap, textureIds),
						OpticalResponseMapId = TextureId(bake.OpticalResponseMap, textureIds),
						OpticalLamp = bake.OpticalLamp,
						OpticalStarburst = bake.OpticalStarburst,
						MaterialId = MaterialId(bake.Material, materialIds),
						Packer = bake.Packer,
						UvFill = bake.UvFill,
					};
				}
				records.Add(record);
			}
			return new InsertAuthoringPayload { Inserts = records.ToArray() };
		}

		public void Restore(Func<string, Transform> resolveNode,
			IReadOnlyDictionary<string, Material> materialsById,
			IReadOnlyDictionary<string, Texture2D> texturesById,
			string assetFolder)
		{
			if (FormatVersion != Version) {
				throw new InvalidOperationException($"Unsupported insert authoring sidecar version {FormatVersion}.");
			}
			if (Inserts == null || Inserts.Length == 0) { return; }
			var bakeFolder = Path.Combine(assetFolder, "InsertBakes");
			Directory.CreateDirectory(bakeFolder);
			var projectRoot = Path.Combine(Application.dataPath, "..");
			foreach (var record in Inserts) {
				var insert = resolveNode(record.NodeId)?.GetComponent<InsertComponent>();
				if (!insert) { throw new InvalidOperationException($"Insert authoring node '{record.NodeId}' is missing."); }
				JsonUtility.FromJsonOverwrite(record.GeneratorJson, insert);
				if (record.CustomMesh != null) {
					var mesh = record.CustomMesh.CreateMesh();
					var sourcePath = Path.Combine(bakeFolder, $"{record.NodeId}-source.asset");
					AssetDatabase.CreateAsset(mesh, Path.GetRelativePath(projectRoot, sourcePath).Replace('\\', '/'));
					insert.CustomMesh = mesh;
				}
				InsertBakeData bake = null;
				if (record.Bake != null) {
					var source = record.Bake;
					bake = ScriptableObject.CreateInstance<InsertBakeData>();
					bake.name = $"Insert Bake {insert.name}";
					bake.Hash = source.Hash;
					bake.PlainStipple = source.PlainStipple;
					bake.Owner = source.Owner;
					bake.Uvs = source.Uvs;
					bake.Tangents = source.Tangents;
					bake.NormalMap = Texture(source.NormalMapId, texturesById);
					bake.OpticalNormalMap = Texture(source.OpticalNormalMapId, texturesById);
					bake.OpticalResponseMap = Texture(source.OpticalResponseMapId, texturesById);
					bake.OpticalLamp = source.OpticalLamp;
					bake.OpticalStarburst = source.OpticalStarburst;
					bake.Material = Material(source.MaterialId, materialsById);
					bake.Packer = source.Packer;
					bake.UvFill = source.UvFill;
					var fullPath = Path.Combine(bakeFolder, $"{record.NodeId}.asset");
					var assetPath = Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/');
					AssetDatabase.CreateAsset(bake, assetPath);
				}
				insert.RestoreAuthoring(bake, Material(record.PreBakeMaterialId, materialsById));
			}
			AssetDatabase.SaveAssets();
		}

		private static string MaterialId(Material material, IReadOnlyDictionary<Material, string> ids)
		{
			if (!material) { return null; }
			if (ids != null && ids.TryGetValue(material, out var id)) { return id; }
			Debug.LogWarning($"Insert authoring material '{material.name}' could not be packaged; its reference will be empty on reimport.");
			return null;
		}

		private static string TextureId(Texture2D texture, IReadOnlyDictionary<Texture2D, string> ids)
		{
			if (!texture) { return null; }
			if (ids != null && ids.TryGetValue(texture, out var id)) { return id; }
			Debug.LogWarning($"Insert bake texture '{texture.name}' could not be packaged; its reference will be empty on reimport.");
			return null;
		}

		private static Material Material(string id, IReadOnlyDictionary<string, Material> materials)
		{
			if (string.IsNullOrEmpty(id)) { return null; }
			if (materials != null && materials.TryGetValue(id, out var material) && material) { return material; }
			Debug.LogWarning($"Insert authoring material profile '{id}' could not be rebuilt; its reference will be empty.");
			return null;
		}

		private static Texture2D Texture(string id, IReadOnlyDictionary<string, Texture2D> textures)
		{
			if (string.IsNullOrEmpty(id)) { return null; }
			if (textures != null && textures.TryGetValue(id, out var texture) && texture) { return texture; }
			Debug.LogWarning($"Insert bake texture '{id}' could not be imported; its reference will be empty.");
			return null;
		}
	}

	[Serializable]
	public sealed class InsertAuthoringRecord
	{
		public string NodeId;
		public string GeneratorJson;
		public string PreBakeMaterialId;
		public InsertBakeAuthoringRecord Bake;
		public InsertCustomMeshAuthoringRecord CustomMesh;
	}

	/// <summary>The original source mesh lives only in the editor sidecar, never the player's GLB.</summary>
	[Serializable]
	public sealed class InsertCustomMeshAuthoringRecord
	{
		public string Name;
		[JsonIgnore] public Vector3[] Vertices;
		/// <summary>Three numbers per vertex.</summary>
		[JsonProperty("vertices")] public InsertBufferView VerticesView;
		public InsertCustomSubmeshAuthoringRecord[] Submeshes;

		public static InsertCustomMeshAuthoringRecord Capture(Mesh mesh)
		{
			if (!mesh) { return null; }
			if (!mesh.isReadable) { throw new InvalidOperationException("Enable Read/Write on the custom insert's source mesh before exporting."); }
			var submeshes = new InsertCustomSubmeshAuthoringRecord[mesh.subMeshCount];
			for (var i = 0; i < submeshes.Length; i++) {
				submeshes[i] = new InsertCustomSubmeshAuthoringRecord { Topology = mesh.GetTopology(i), Indices = mesh.GetIndices(i) };
			}
			return new InsertCustomMeshAuthoringRecord { Name = mesh.name, Vertices = mesh.vertices, Submeshes = submeshes };
		}

		public Mesh CreateMesh()
		{
			var mesh = new Mesh { name = Name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
			mesh.vertices = Vertices;
			mesh.subMeshCount = Submeshes.Length;
			for (var i = 0; i < Submeshes.Length; i++) { mesh.SetIndices(Submeshes[i].Indices, Submeshes[i].Topology, i); }
			mesh.RecalculateBounds();
			return mesh;
		}
	}

	[Serializable]
	public sealed class InsertCustomSubmeshAuthoringRecord
	{
		public MeshTopology Topology;
		[JsonIgnore] public int[] Indices;
		/// <summary>One integer per index.</summary>
		[JsonProperty("indices")] public InsertBufferView IndicesView;
	}

	/// <summary>
	/// Where an array of numbers is in <c>meta/inserts.bin</c>, the way a glTF accessor says it: by its offset in
	/// bytes and its number of elements. The numbers are little-endian and 32 bits wide, floats unless the field
	/// says otherwise, and the field says how many of them make an element.
	/// </summary>
	[Serializable]
	public sealed class InsertBufferView
	{
		public int ByteOffset;
		public int Count;

		internal static InsertBufferView Append<T>(MemoryStream buffer, T[] values) where T : unmanaged
		{
			if (values == null) { return null; }
			if (!BitConverter.IsLittleEndian) { throw new PlatformNotSupportedException("The insert authoring buffer is little-endian."); }
			var view = new InsertBufferView { ByteOffset = checked((int)buffer.Length), Count = values.Length };
			buffer.Write(MemoryMarshal.AsBytes(values.AsSpan()));
			return view;
		}

		internal static T[] Read<T>(byte[] buffer, InsertBufferView view) where T : unmanaged
		{
			if (view == null) { return null; }
			if (!BitConverter.IsLittleEndian) { throw new PlatformNotSupportedException("The insert authoring buffer is little-endian."); }
			var size = UnsafeUtility.SizeOf<T>();
			if (view.ByteOffset < 0 || view.Count < 0 || view.ByteOffset + (long)view.Count * size > buffer.Length) {
				throw new InvalidDataException("An insert authoring record points outside the insert buffer.");
			}
			return MemoryMarshal.Cast<byte, T>(buffer.AsSpan(view.ByteOffset, view.Count * size)).ToArray();
		}
	}

	[Serializable]
	public sealed class InsertBakeAuthoringRecord
	{
		public string Hash;
		public bool PlainStipple;
		public string Owner;
		[JsonIgnore] public Vector2[] Uvs;
		[JsonIgnore] public Vector4[] Tangents;
		/// <summary>Two numbers per vertex.</summary>
		[JsonProperty("uvs")] public InsertBufferView UvsView;
		/// <summary>Four numbers per vertex.</summary>
		[JsonProperty("tangents")] public InsertBufferView TangentsView;
		public string NormalMapId;
		public string OpticalNormalMapId;
		public string OpticalResponseMapId;
		public Vector4 OpticalLamp;
		public Vector4 OpticalStarburst;
		public string MaterialId;
		public string Packer;
		public float UvFill;
	}
}
