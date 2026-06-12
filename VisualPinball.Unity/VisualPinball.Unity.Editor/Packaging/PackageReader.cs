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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class PackageReader
	{
		private readonly string _vpePath;
		private IPackageFolder _tableFolder;
		private string _assetPath;
		private string _tableName;
		private GameObject _table;
		private Transform _tableRoot;
		private VpePackageManifest _manifest;
		private Dictionary<string, Transform> _transformByNodeId;
		private PackagedRefs _refs;
		private PackagedFiles _files;
		private readonly HashSet<string> _unknownTypeNames = new(StringComparer.Ordinal);
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PackageReader(string vpePath)
		{
			_vpePath = vpePath;
		}

		public async Task ImportIntoScene(string tableName)
		{
			var sw = new Stopwatch();
			sw.Start();
			_tableName = tableName;

			using var storage = PackageApi.StorageManager.OpenStorage(_vpePath);
			try {
				Setup(storage);
				await ImportModels();

				_refs = new PackagedRefs(_tableRoot);
				_refs.SetNodeIdsForRead(_transformByNodeId);
				_files = new PackagedFiles(_tableFolder, _refs);
				WarnMissingComponentTypes();

				await ReadAssets();

				// create components and update game objects
				ReadPackables(PackageApi.ItemFolder, (go, file) => ItemPackable.Unpack(file.GetData()).Apply(go), (item, type, file, index) => {
					// add or update component
					var comps = item.gameObject.GetComponents(type);
					var comp = comps.Length > index
						? comps[index]
						: item.gameObject.AddComponent(type);
					if (comp is IPackable packable) {
						packable.Unpack(file.GetData());

					} else {
						PackageApi.Packer.Unpack(file.GetData(), comp);
					}
				});

				// add references
				ReadPackables(PackageApi.ItemReferencesFolder, null, (item, type, stream, _) => {
					// add the component and unpack it.
					var comp = item.gameObject.GetComponent(type) as IPackable;
					comp?.UnpackReferences(stream.GetData(), _tableRoot, _refs, _files);
				});

				ReadGlobals();
				ReadTableMetadata();
				RestoreLights();
				ImportMaterials();

			} finally {
				storage.Close();
				Logger.Info($"Scene import took {sw.ElapsedMilliseconds}ms.");
			}
		}

		/// <summary>
		/// Imports only the GLB and rebuilds materials from the packaged payload, skipping the
		/// item, reference, global and collider-mesh restore. Useful for exercising the
		/// material/texture reconstruction path on packages whose full item restore is slow or
		/// problematic.
		/// </summary>
		public async Task ImportMaterialsOnly(string tableName)
		{
			var sw = Stopwatch.StartNew();
			_tableName = tableName;

			using var storage = PackageApi.StorageManager.OpenStorage(_vpePath);
			try {
				Setup(storage);
				await ImportModels();
				RestoreLights();
				ImportMaterials();
			} finally {
				storage.Close();
				Logger.Info($"Materials-only import took {sw.ElapsedMilliseconds}ms.");
			}
		}

		private void WarnMissingComponentTypes()
		{
			if (_manifest?.ComponentTypes == null || _manifest.ComponentTypes.Count == 0) {
				return;
			}
			var missing = _manifest.ComponentTypes
				.Where(name => !string.IsNullOrEmpty(name) && !_refs.TryGetType(name, out _))
				.ToList();
			if (missing.Count > 0) {
				Logger.Warn(
					$"This package uses component types that are not registered in this project: {string.Join(", ", missing)}. " +
					"Their data will be skipped — a plugin may be missing.");
			}
		}

		// Restores the authored light state. The glTF export boosts intensities by
		// LightIntensityFactor; without normalization plus the lights payload, an exported and
		// re-imported table would come back with blown-out lights.
		private void RestoreLights()
		{
			VpeLightRestore.NormalizeImportedLightIntensities(_tableRoot.gameObject);
			VpeLightRestore.RestoreLightProfiles(_tableRoot.gameObject, _tableFolder,
				id => _transformByNodeId.GetValueOrDefault(id));
		}

		// Rebuilds authoring materials from the packaged payload: writes the package's lossless
		// source texture bytes as real texture assets (with importer settings restored from the
		// payload) and hands material construction to the registered pipeline importer.
		private void ImportMaterials()
		{
			if (!VpeMaterialReader.TryLoad(_tableFolder, loadSourceBytes: true, out var payload, out var sources)) {
				return;
			}

			var importer = VpeMaterialEditorImport.Active;
			if (importer == null) {
				Logger.Warn("No IVpeMaterialEditorImporter registered; imported table keeps glTF materials without textures.");
				return;
			}

			Logger.Info($"Editor import: writing {sources.Entries.Length} source texture(s) as assets...");
			var texturesById = ImportTextureAssets(payload, sources);

			var materialFolder = Path.Combine(_assetPath, "Materials");
			Directory.CreateDirectory(materialFolder);
			var materialAssetFolder = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), materialFolder).Replace('\\', '/');

			Logger.Info($"Editor import: rebuilding materials for {payload.Profiles.Length} profile(s)...");
			var applied = importer.Apply(_tableRoot, payload, texturesById, materialAssetFolder,
				id => _transformByNodeId.GetValueOrDefault(id));
			Logger.Info($"Editor import rebuilt materials for {applied} slot(s) from the materials payload.");
		}

		private Dictionary<string, Texture2D> ImportTextureAssets(VpeMaterialsPayload payload, VpeTextureSources.Result sources)
		{
			var texturesById = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
			if (sources.Blob == null || sources.Entries.Length == 0) {
				return texturesById;
			}

			var normalIds = VpeTextureCook.CollectNormalTextureIds(payload);
			var textureFolder = Path.Combine(_assetPath, "Textures");
			Directory.CreateDirectory(textureFolder);
			var projectRoot = Path.Combine(Application.dataPath, "..");

			// The preprocessor applies the payload's importer settings during the initial import,
			// so each texture only runs through the (expensive) asset pipeline once.
			var assetPathsById = new Dictionary<string, string>(StringComparer.Ordinal);
			try {
				AssetDatabase.StartAssetEditing();
				foreach (var asset in sources.Entries) {
					if (asset == null || string.IsNullOrEmpty(asset.Id)
						|| asset.ByteOffset < 0 || asset.ByteLength <= 0
						|| asset.ByteOffset + (long)asset.ByteLength > sources.Blob.Length) {
						continue;
					}

					var extension = string.Equals(asset.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
					var fileName = SanitizeFileName(asset.Id) + extension;
					var fullPath = Path.Combine(textureFolder, fileName);
					using (var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write)) {
						file.Write(sources.Blob, asset.ByteOffset, asset.ByteLength);
					}
					var assetPath = Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/');
					assetPathsById[asset.Id] = assetPath;
					VpeTextureImportPreprocessor.Register(assetPath, asset, normalIds.Contains(asset.Id));
				}
			} finally {
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
				VpeTextureImportPreprocessor.Clear();
			}

			foreach (var entry in assetPathsById) {
				var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Value);
				if (texture) {
					texturesById[entry.Key] = texture;
				}
			}
			Logger.Info($"Imported {texturesById.Count} texture asset(s) from the package's source layer.");
			return texturesById;
		}

		private static string SanitizeFileName(string name)
		{
			var invalid = Path.GetInvalidFileNameChars();
			var chars = name.ToCharArray();
			for (var i = 0; i < chars.Length; i++) {
				if (Array.IndexOf(invalid, chars[i]) >= 0) {
					chars[i] = '_';
				}
			}
			return new string(chars);
		}

		private void Setup(IPackageStorage storage)
		{
			_manifest = VpePackageManifestIo.TryRead(storage);
			if (_manifest == null) {
				throw new Exception(
					"This file has no package manifest — it is not a .vpe table package (or was written by an " +
					"incompatible pre-release version of VPE).");
			}
			if (_manifest.FormatVersion > PackageApi.FormatVersion) {
				throw new Exception(
					$"This package uses format version {_manifest.FormatVersion}, but this VPE version only supports " +
					$"up to version {PackageApi.FormatVersion}. Update VPE to import it.");
			}

			// open storages
			_tableFolder = storage.GetFolder(PackageApi.TableFolder);
			_assetPath = Path.Combine(Application.dataPath, "Resources", _tableName);
			var n = 0;
			while (Directory.Exists(_assetPath)) {
				_assetPath = Path.Combine(Application.dataPath, "Resources", $"{_tableName} ({++n})");
			}
			Directory.CreateDirectory(_assetPath);
		}

		private async Task ImportModels()
		{
			var sceneFile = _tableFolder.GetFile(PackageApi.SceneFile);
			var sceneData = sceneFile.GetData();
			if (sceneData == null || sceneData.Length == 0) {
				throw new Exception($"Scene data file '{PackageApi.SceneFile}' is missing or empty.");
			}

			var glbPath = Path.Combine(_assetPath, $"{_tableName}.glb");
			try {
				AssetDatabase.StartAssetEditing();

				// dump glb
				await using var glbFileFile = new FileStream(glbPath, FileMode.Create, FileAccess.Write);
				await glbFileFile.WriteAsync(sceneData, 0, sceneData.Length);

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// add glb to scene
			var glbRelativePath = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), glbPath);
			var glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbRelativePath);
			if (glbPrefab == null) {
				throw new Exception($"Could not load {_tableName}.glb at path: {glbRelativePath}");
			}
			_table = PrefabUtility.InstantiatePrefab(glbPrefab) as GameObject;
			if (_table == null) {
				_table = Object.Instantiate(glbPrefab); // fallback instantiation in case the above method fails.
			}
			_table.transform.SetParent(null);
			PrefabUtility.UnpackPrefabInstance(_table, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); // mark the active scene as dirty so that the changes are saved.

			BindNodeIds(sceneData);
		}

		// Binds the package's stable node ids (glTF extras) to the instantiated prefab hierarchy.
		// The asset pipeline gives no node-index hook, so the binding walks both trees by
		// structure and (de-duplicated) name — see VpeNodeIds.BindInstantiated.
		private void BindNodeIds(byte[] sceneData)
		{
			var nodeTree = VpeNodeIds.TryParse(sceneData);
			if (nodeTree?.Root == null || nodeTree.Nodes.All(node => string.IsNullOrEmpty(node.VpeId))) {
				throw new Exception("This package carries no node ids in its scene GLB; cannot import.");
			}

			_transformByNodeId = VpeNodeIds.BindInstantiated(nodeTree, _table.transform);
			if (string.IsNullOrEmpty(_manifest.RootNodeId)
				|| !_transformByNodeId.TryGetValue(_manifest.RootNodeId, out _tableRoot)
				|| !_tableRoot) {
				throw new Exception("Could not resolve the manifest's root node id in the imported scene.");
			}
			Logger.Info($"Bound {_transformByNodeId.Count} node id(s) to the imported hierarchy.");
		}

		private async Task ReadAssets()
		{
			_files.UnpackAssets(_assetPath);
			await _files.UnpackMeshes(_assetPath);
			_files.UnpackSounds(_assetPath);
		}

		private Transform ResolveItemNode(string itemFolderName)
		{
			return _refs.GetNode(itemFolderName);
		}

		/// <summary>
		/// Loops through all items and components in the package, and applies the given action.
		/// The item action is executed before the component action.
		/// </summary>
		/// <param name="rootFolder">Which root folder of the package to start with</param>
		/// <param name="itemAction">Action to execute on the item</param>
		/// <param name="componentAction">Action to execute on the component</param>
		/// <exception cref="Exception">When there are item nodes in the package that don't correspond to the instantiated scene.</exception>
		private void ReadPackables(string rootFolder, Action<GameObject, IPackageFile> itemAction, Action<Transform, Type, IPackageFile, int> componentAction) {

			if (!_tableFolder.TryGetFolder(rootFolder, out var itemsFolder)) {
				return;
			}

			// -> rootFolder <- / nodeId / CompType / 0
			itemsFolder.VisitFolders(itemFolder => {

				// rootFolder / -> nodeId <- / CompType / 0
				var item = ResolveItemNode(itemFolder.Name);
				if (item == null) {
					throw new Exception($"Cannot find item '{itemFolder.Name}' on node {_table.name}.");
				}
				if (itemAction != null && itemFolder.TryGetFile(PackageApi.ItemFile, out var itemFile, PackageApi.Packer.FileExtension)) {
					itemAction(item.gameObject, itemFile);
				}
				itemFolder.VisitFolders(typeFolder => {

					// rootFolder / nodeId / -> CompType <- / 0
					if (!_refs.TryGetType(typeFolder.Name, out var t)) {
						// Forward compatibility: data of unknown component types (newer VPE,
						// missing plugin) is skipped instead of failing the whole import.
						if (_unknownTypeNames.Add(typeFolder.Name)) {
							Logger.Error($"Skipping unknown component type '{typeFolder.Name}' while reading the package. A plugin may be missing.");
						}
						return;
					}
					var index = 0;
					typeFolder.VisitFiles(compFile => {

						// rootFolder / nodeId / CompType / -> 0 <- (there might be multiple components of the same type)
						componentAction(item, t, compFile, index++);
					});
				});
			});
		}

		private void ReadGlobals()
		{
			var tableComponent = _tableRoot.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}
			var globalStorage = _tableFolder.GetFolder(PackageApi.GlobalFolder);
			tableComponent.MappingConfig = new MappingConfig {
				Switches = PackageApi.Packer.Unpack<List<SwitchMapping>>(globalStorage.GetFile(PackageApi.SwitchesFile, PackageApi.Packer.FileExtension).GetData()),
				Coils = PackageApi.Packer.Unpack<List<CoilMapping>>(globalStorage.GetFile(PackageApi.CoilsFile, PackageApi.Packer.FileExtension).GetData()),
				Lamps = PackageApi.Packer.Unpack<List<LampMapping>>(globalStorage.GetFile(PackageApi.LampsFile, PackageApi.Packer.FileExtension).GetData()),
				Wires = PackageApi.Packer.Unpack<List<WireMapping>>(globalStorage.GetFile(PackageApi.WiresFile, PackageApi.Packer.FileExtension).GetData()),
			};
			foreach (var sw in tableComponent.MappingConfig.Switches) {
				sw.RestoreReference(_refs);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.RestoreReference(_refs);
			}
			foreach (var lamp in tableComponent.MappingConfig.Lamps) {
				lamp.RestoreReference(_refs);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.RestoreReferences(_refs);
			}
		}

		private void ReadTableMetadata()
		{
			var tableComponent = _tableRoot.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}

			if (_tableFolder.TryGetFile(PackageApi.TableMetadataFile, out var tableMetadataFile, PackageApi.Packer.FileExtension)) {
				tableComponent.Metadata = PackageApi.Packer.Unpack<TableMetadata>(tableMetadataFile.GetData()) ?? new TableMetadata();
			}
		}
	}
}
