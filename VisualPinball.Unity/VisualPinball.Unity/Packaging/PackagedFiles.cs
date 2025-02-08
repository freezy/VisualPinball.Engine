// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using System.IO;
using System.Threading.Tasks;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Editor.Packaging;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Packaging
{
	public class PackagedFiles
	{
		public readonly Dictionary<int, string> ColliderMeshInstanceIdToGuid = new();

		private readonly Dictionary<string, Mesh> _colliderMeshes = new();
		private Dictionary<string, ColliderMeshMetaPackable> _colliderMeshMeta;

		private readonly HashSet<ScriptableObject> _scriptableObjects = new();
		private readonly Dictionary<int, ScriptableObject> _deserializedObjects = new();
		private readonly IPackageFolder _tableFolder;
		private readonly PackNameLookup _typeLookup;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PackagedFiles(IPackageFolder tableFolder, PackNameLookup typeLookup)
		{
			_tableFolder = tableFolder;
			_typeLookup = typeLookup;
		}

		public string GetColliderMeshGuid(IColliderMesh cm)
		{
			var instanceId = (cm as Component)!.GetInstanceID();
			return ColliderMeshInstanceIdToGuid.GetValueOrDefault(instanceId);
		}

		public int AddAsset(ScriptableObject scriptableObject)
		{
			if (scriptableObject == null) {
				return 0;
			}

			if (!_typeLookup.HasType(scriptableObject.GetType())) {
				throw new Exception($"Unsupported asset type {scriptableObject.GetType().FullName}");
			}

			_scriptableObjects.Add(scriptableObject);
			return scriptableObject.GetInstanceID();
		}

		public T GetAsset<T>(int instanceId) where T : ScriptableObject
		{
			if (_deserializedObjects.TryGetValue(instanceId, out var asset)) {
				return asset as T;
			}
			return null;
		}

		public void PackAssets()
		{
			if (_scriptableObjects.Count == 0) {
				return;
			}
			var assetFolder = _tableFolder.AddFolder(PackageApi.AssetFolder);
			foreach (var so in _scriptableObjects) {
				var subFolder = _typeLookup.GetName(so.GetType());
				if (!assetFolder.TryGetFolder(subFolder, out var assetTypeFolder)) {
					assetTypeFolder = assetFolder.AddFolder(subFolder);
				}
				var name = UniqueName(assetTypeFolder, so.name);

				// pack file
				var file = assetTypeFolder.AddFile(name, PackageApi.Packer.FileExtension);
				file.SetData(MetaPackable.Pack(so));

				// pack meta
				var fileMeta = assetTypeFolder.AddFile($"{name}.meta", PackageApi.Packer.FileExtension);
				fileMeta.SetData(MetaPackable.PackMeta(so));
			}
		}

		public void UnpackAssets(string assetPath)
		{
			if (!_tableFolder.TryGetFolder(PackageApi.AssetFolder, out var assetFolder)) {
				return;
			}
			assetFolder.VisitFolders(assetTypeFolder => {
				assetTypeFolder.VisitFiles(assetFile => {

					var folder = Path.Combine(assetPath, assetTypeFolder.Name);

					if (assetFile.Name.Contains(".meta")) {
						return;
					}
					var metaFilename = $"{Path.GetFileNameWithoutExtension(assetFile.Name)}.meta{Path.GetExtension(assetFile.Name)}";
					if (!assetTypeFolder.TryGetFile(metaFilename, out var metaFile)) {
						throw new Exception($"Cannot find meta file {metaFilename} for {assetFile.Name}");
					}
					var meta = MetaPackable.UnpackMeta(metaFile.GetData());
					var type = _typeLookup.GetType(assetTypeFolder.Name);
					if (type == null) {
						throw new Exception($"Unknown asset type {assetTypeFolder.Name}");
					}

					var asset = PackageApi.Packer.Unpack(type, assetFile.GetData()) as ScriptableObject;
					if (asset == null) {
						throw new Exception($"Failed to unpack asset {assetFile.Name}");
					}

					if (!Directory.Exists(folder)) {
						Directory.CreateDirectory(folder);
					}
					var relativePath = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), folder);
					AssetDatabase.CreateAsset(asset, Path.Combine(relativePath, Path.GetFileNameWithoutExtension(assetFile.Name) + ".asset"));

					_deserializedObjects.Add(meta.InstanceId, asset);
				});
			});
		}

		private string UniqueName(IPackageFolder folder, string name)
		{
			var baseName = name;
			var i = 1;
			while (true) {
				if (folder.TryGetFile(name, out _, PackageApi.Packer.FileExtension)) {
					name = $"{baseName} ({++i})";
				} else {
					return name;
				}
			}
		}

		public async Task UnpackMeshes(string assetPath)
		{
			if (!_tableFolder.TryGetFolder(PackageApi.MetaFolder, out var metaFolder)) {
				return;
			}
			if (!_tableFolder.TryGetFile(PackageApi.ColliderMeshesFile, out var colliderMeshes)) {
				return;
			}
			if (!metaFolder.TryGetFile(PackageApi.ColliderMeshesMeta, out var colliderMeta, PackageApi.Packer.FileExtension)) {
				return;
			}
			var glbPath = Path.Combine(assetPath, "colliders.glb");
			try {
				AssetDatabase.StartAssetEditing();

				// dump glb
				await using var glbFileStream = new FileStream(glbPath, FileMode.Create, FileAccess.Write);
				await colliderMeshes.AsStream().CopyToAsync(glbFileStream);

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			var glbRelativePath = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), glbPath);
			var glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbRelativePath);
			if (glbPrefab == null) {
				throw new Exception($"Could not load colliders.glb at path: {glbRelativePath}");
			}

			_colliderMeshMeta = ColliderMeshMetaPackable.Unpack(colliderMeta.GetData());
			var n = glbPrefab.transform.childCount;
			_colliderMeshes.Clear();
			for (var i = 0; i < n; i++) {
				var collider = glbPrefab.transform.GetChild(i);
				var guid = collider.name;
				if (_colliderMeshMeta.TryGetValue(guid, out var meta)) {
					if (meta.PrefabGuid != null) {
						var prefabPath = AssetDatabase.GUIDToAssetPath(meta.PrefabGuid);
						if (prefabPath != null) {
							var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
							if (prefab != null) {
								// this is only half-tested (with empty string), since we currently don't have prefabs with a deeper hierarchy
								var meshGo = prefab.transform.Find(meta.PathWithinPrefab);
								if (meshGo != null) {
									var meshFilter = meshGo.GetComponent<MeshFilter>();
									if (meshFilter != null) {
										var mesh = meshGo.GetComponent<MeshFilter>().sharedMesh;
										if (mesh != null) {
											collider.GetComponent<MeshFilter>().sharedMesh = mesh;
										}
									} else {
										Logger.Warn($"Cannot find mesh at path {meta.PathWithinPrefab} in prefab {prefabPath}.");
									}
								} else {
									Logger.Warn($"Cannot find mesh at path {meta.PathWithinPrefab} in prefab {prefabPath}.");
								}
							} else {
								Logger.Warn($"Cannot load prefab for collider mesh at {prefabPath}.");
							}
						} else {
							Logger.Warn($"Cannot find prefab for collider mesh {guid}.");
						}
					}
				} else {
					Logger.Warn($"Cannot fine meta data for collider mesh {guid}.");
				}
				_colliderMeshes.Add(guid, collider.GetComponent<MeshFilter>().sharedMesh);
			}
		}

		public Mesh GetColliderMesh(string guid)
		{
			return _colliderMeshes[guid];
		}
	}
}
