// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
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
		public const int Version = 3;
		public int FormatVersion = Version;
		public InsertAuthoringRecord[] Inserts = Array.Empty<InsertAuthoringRecord>();

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
		public Vector3[] Vertices;
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
		public int[] Indices;
	}

	[Serializable]
	public sealed class InsertBakeAuthoringRecord
	{
		public string Hash;
		public bool PlainStipple;
		public string Owner;
		public Vector2[] Uvs;
		public Vector4[] Tangents;
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
