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
using VisualPinball.Unity.Simulation;
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
		private TableProvenance _sourceProvenance;
		private TableMetadata _sourceMetadata;
		private VpeIntegrity.Manifest _integrity;
		private Dictionary<string, Transform> _transformByNodeId;
		private PackagedRefs _refs;
		private PackagedFiles _files;
		private IReadOnlyDictionary<string, Material> _importedMaterialsById;
		private IReadOnlyDictionary<string, Texture2D> _importedTexturesById;
		private readonly HashSet<string> _unknownTypeNames = new(StringComparer.Ordinal);
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PackageReader(string vpePath)
		{
			_vpePath = vpePath;
			ContentResolver = new PackagedContentResolver(vpePath);
		}

		/// <summary>Resolves packaged content through the same cache path used by Player builds.</summary>
		public IPackagedContentResolver ContentResolver { get; }

		public async Task ImportIntoScene(string tableName)
		{
			var sw = new Stopwatch();
			sw.Start();
			if (string.IsNullOrWhiteSpace(tableName) || SanitizeFileName(tableName) != tableName || tableName == "." || tableName == "..") throw new ArgumentException("Table name must be a file name.", nameof(tableName));
			_tableName = tableName;
			_assetPath = null; _table = null;
			var restoreActive = false;
			var importSucceeded = false;

			using var storage = await Task.Run(() => PackageApi.StorageManager.OpenStorage(_vpePath));
			try {
				Setup(storage);
				await ImportModels();
				restoreActive = _table.activeSelf;
				_table.SetActive(false);

				_refs = new PackagedRefs(_tableRoot);
				_refs.SetNodeIdsForRead(_transformByNodeId);
				_files = new PackagedFiles(_tableFolder, _refs);
				WarnMissingComponentTypes();

				await VpeAudioAssetIndex.PrepareAsync();
				await ReadAssets();
				using var insertGenerationScope = InsertComponent.DeferGenerationForPackageImport();

				// create components and update game objects, then add the references between them
				ReadReferences(ReadItems());
				insertGenerationScope.Dispose();
				var slingshots = _tableRoot.GetComponentsInChildren<RubberSlingshotComponent>(true);
				foreach (var slingshot in slingshots) {
					slingshot.ValidateAfterUnpack();
				}

				foreach (var consumer in _table.GetComponentsInChildren<MonoBehaviour>(true).OfType<IPackagedContentConsumer>()) {
					consumer.SetPackagedContentResolver(ContentResolver);
				}

				ReadGlobals();
				ReadTableMetadata();
				SimulationThreadComponent.EnsureForTable(_tableRoot.gameObject);
				RestoreLights();
				ImportMaterials();
				RestoreInsertAuthoring();
				Undo.RegisterCreatedObjectUndo(_table, "Import VPE table");
				UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_table.scene);
				importSucceeded = true;

			} finally {
				if (importSucceeded && _table) { _table.SetActive(restoreActive); }
				if (!importSucceeded) {
					if (_table) UnityEngine.Object.DestroyImmediate(_table);
					CleanupImportAssets();
				}
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
			var succeeded = false;
			if (string.IsNullOrWhiteSpace(tableName) || SanitizeFileName(tableName) != tableName || tableName == "." || tableName == "..") throw new ArgumentException("Table name must be a file name.", nameof(tableName));
			_tableName = tableName;
			_assetPath = null; _table = null;

			using var storage = await Task.Run(() => PackageApi.StorageManager.OpenStorage(_vpePath));
			try {
				Setup(storage);
				await ImportModels();
				if (!_table.TryGetComponent<TableComponent>(out var diagnostic)) diagnostic = _table.AddComponent<TableComponent>();
				var source = TableProvenance.Derive(_sourceProvenance, _sourceMetadata);
				source.Kind = "diagnostic";
				diagnostic.RestoreProvenance(source);
				RestoreLights();
				ImportMaterials();
				Undo.RegisterCreatedObjectUndo(_table, "Import VPE materials");
				EditorSceneManager.MarkSceneDirty(_table.scene);
				succeeded = true;
			} finally {
				if (!succeeded) { if (_table) Object.DestroyImmediate(_table); CleanupImportAssets(); }
				storage.Close();
				Logger.Info($"Materials-only import took {sw.ElapsedMilliseconds}ms.");
			}
		}

		private void CleanupImportAssets()
		{
			if (string.IsNullOrEmpty(_assetPath)) return;
			var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources")) + Path.DirectorySeparatorChar;
			var path = Path.GetFullPath(_assetPath);
			if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Import cleanup path escaped the asset root.");
			var assetPath = Path.GetRelativePath(Path.GetDirectoryName(Application.dataPath), path).Replace('\\', '/');
			if (AssetDatabase.IsValidFolder(assetPath)) AssetDatabase.DeleteAsset(assetPath);
			else if (Directory.Exists(path)) Directory.Delete(path, true);
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
			if (!VpeMaterialReader.TryLoad(_tableFolder, loadSourceBytes: true, out var payload, out var sources, stripBakedOcclusion: true)) {
				return;
			}

			var importer = VpeMaterialEditorImport.Active;
			if (importer == null) {
				Logger.Warn("No IVpeMaterialEditorImporter registered; imported table keeps glTF materials without textures.");
				return;
			}

			Logger.Info($"Editor import: writing {sources.Entries.Length} source texture(s) as assets...");
			var texturesById = ImportTextureAssets(payload, sources);
			_importedTexturesById = texturesById;

			var materialFolder = Path.Combine(_assetPath, "Materials");
			Directory.CreateDirectory(materialFolder);
			var materialAssetFolder = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), materialFolder).Replace('\\', '/');

			Logger.Info($"Editor import: rebuilding materials for {payload.Profiles.Length} profile(s)...");
			var result = importer.Apply(_tableRoot, payload, texturesById, materialAssetFolder,
				id => _transformByNodeId.GetValueOrDefault(id));
			_importedMaterialsById = result.MaterialsById;
			Logger.Info($"Editor import rebuilt materials for {result.AppliedSlots} slot(s) from the materials payload.");
		}

		private void RestoreInsertAuthoring()
		{
			if (!_tableFolder.TryGetFolder(PackageApi.MetaFolder, out var meta)
				|| !meta.TryGetFile(PackageApi.InsertAuthoringFile, out var file, PackageApi.Packer.FileExtension)) {
				return;
			}
			var payload = InsertAuthoringPayload.Unpack(file.GetData(),
				meta.TryGetFile(PackageApi.InsertAuthoringBufferFile, out var buffer) ? buffer.GetData() : null);
			payload.Restore(id => _transformByNodeId.GetValueOrDefault(id),
				_importedMaterialsById, _importedTexturesById, _assetPath);
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
			// what an older table stored where this build no longer reads it would be lost without a word
			var incompatibility = VpePackageSchemas.Incompatibility(_manifest);
			if (incompatibility != null) {
				throw new Exception(incompatibility);
			}

			_integrity = VpeIntegrity.ReadManifest(storage);

			// open storages
			_tableFolder = storage.GetFolder(PackageApi.TableFolder);
			_sourceProvenance = TableProvenance.Read(_tableFolder, out _sourceMetadata);
			TableProvenance.Derive(_sourceProvenance, _sourceMetadata);
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
			if (_tableRoot == _table.transform && !string.IsNullOrWhiteSpace(nodeTree.Root.Name)) {
				_table.name = nodeTree.Root.Name;
			}
			Logger.Info($"Bound {_transformByNodeId.Count} node id(s) to the imported hierarchy.");
		}

		private async Task ReadAssets()
		{
			_files.UnpackSounds(_assetPath, _integrity);
			_files.UnpackAssets(_assetPath);
			await _files.UnpackMeshes(_assetPath);
			_files.Rolling.Read(_assetPath, _manifest.Schemas.TryGetValue(VpePackageSchemas.Rolling, out var rollingSchema) ? rollingSchema : RollingSoundGraph.Version);
		}

		private Transform ResolveItemNode(string itemFolderName)
		{
			return _refs.GetNode(itemFolderName);
		}

		/// <summary>
		/// First pass over the package's nodes: applies the state of every game object and creates its components
		/// with their data. Returns the components that carry references, for <see cref="ReadReferences"/>.
		/// </summary>
		/// <exception cref="Exception">When there are nodes in the package that don't correspond to the instantiated scene.</exception>
		private List<(string NodeId, Type Type, int Index, Component Component, PackagedItems.Entry Entry)> ReadItems()
		{
			var references = new List<(string, Type, int, Component, PackagedItems.Entry)>();
			var empty = PackageApi.Packer.Empty;
			// how many components of a type the current node has been given so far
			var created = new Dictionary<Type, int>();

			var nodes = PackagedItems.Read(_tableFolder);
			// linking a game object to its prefab needs the state of the nodes below it
			var items = nodes.Where(node => node.Item != null).ToDictionary(node => node.Id, node => ItemPackable.Unpack(node.Item), StringComparer.Ordinal);

			foreach (var node in nodes) {
				var item = ResolveItemNode(node.Id);
				if (item == null) {
					throw new Exception($"Cannot find item '{node.Id}' on node {_table.name}.");
				}
				var go = item.gameObject;
				if (items.TryGetValue(node.Id, out var state)) {
					ApplyItem(state, go, items);
					// The table node can itself carry an ItemPackable that reactivates it.
					if (go == _table) { _table.SetActive(false); }
				}

				created.Clear();
				foreach (var entry in node.Components) {
					if (!_refs.TryGetType(entry.Type, out var type)) {
						// Forward compatibility: data of unknown component types (newer VPE,
						// missing plugin) is skipped instead of failing the whole import.
						if (_unknownTypeNames.Add(entry.Type)) {
							Logger.Error($"Skipping unknown component type '{entry.Type}' while reading the package. A plugin may be missing.");
						}
						continue;
					}

					// add or update the component: the n-th entry of a type is the n-th component of that type
					created.TryGetValue(type, out var index);
					created[type] = index + 1;
					var comp = PackagedItems.FindComponent(go, type, index);
					if (!comp) {
						comp = go.AddComponent(type);
					}
					if (!comp) {
						throw new Exception($"Cannot create component {entry.Type} on '{go.name}'.");
					}
					var data = entry.Data ?? empty;
					if (comp is IPackable packable) {
						packable.Unpack(data);
					} else {
						PackageApi.Packer.Unpack(data, comp);
					}
					if (entry.Refs != null) {
						references.Add((node.Id, type, index, comp, entry));
					}
				}
			}
			return references;
		}

		/// <summary>
		/// Applies a node's game object state, which can link it to its prefab again.
		/// </summary>
		///
		/// <remarks>
		/// The link matches the game objects below with the prefab's by their path of names, but the imported names
		/// can differ from the prefab's: the instance can override them, and the glTF import makes the names of
		/// siblings unique. So the game objects of this instance (their node says which of the prefab's they are, see
		/// <see cref="ItemPackable.PrefabObject"/>) get the names and order of the prefab's before, and all others a
		/// name nothing in the prefab has. The link replaces the game objects below, so after it, the node ids are
		/// bound again: to the instance of their prefab's game object, or to the game object with their temporary
		/// name. The game objects then get their names back, and the poses the link took from the prefab.
		/// </remarks>
		private void ApplyItem(ItemPackable item, GameObject go, IReadOnlyDictionary<string, ItemPackable> items)
		{
			var prefabPath = !string.IsNullOrEmpty(item.PrefabGuid) && !PrefabUtility.IsPartOfAnyPrefab(go)
				? AssetDatabase.GUIDToAssetPath(item.PrefabGuid)
				: null;
			var prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			if (!prefab) {
				// Apply says why when it can't link
				item.Apply(go);
				return;
			}

			// the game objects of the prefab, by their local file id
			var prefabObjects = new Dictionary<long, Transform>();
			foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) {
				if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(t.gameObject, out _, out long fileId)) {
					prefabObjects[fileId] = t;
				}
			}
			var nodeIds = new Dictionary<Transform, string>();
			foreach (var (id, transform) in _transformByNodeId) {
				if (transform && transform != go.transform && transform.IsChildOf(go.transform)) {
					nodeIds[transform] = id;
				}
			}

			// the game objects of this instance get the names of the prefab's, all others a temporary one
			var matched = new Dictionary<string, Transform>(StringComparer.Ordinal);
			var names = new Dictionary<string, string>(StringComparer.Ordinal);
			var poses = new Dictionary<string, (Vector3 Position, Quaternion Rotation, Vector3 Scale)>(StringComparer.Ordinal);
			var others = new List<(Transform Transform, string Name)>();
			foreach (var transform in go.GetComponentsInChildren<Transform>(true)) {
				if (transform == go.transform) {
					continue;
				}
				if (nodeIds.TryGetValue(transform, out var id) && IsPartOfLinkedInstance(transform, go.transform, nodeIds, items)
					&& items.TryGetValue(id, out var state) && state.PrefabObject != 0
					&& prefabObjects.TryGetValue(state.PrefabObject, out var prefabObject)) {
					matched[id] = prefabObject;
					// the name it was exported with, not the one the glTF import made unique
					names[id] = string.IsNullOrEmpty(state.Name) ? transform.name : state.Name;
					poses[id] = (transform.localPosition, transform.localRotation, transform.localScale);
					transform.name = prefabObject.name;
				} else {
					others.Add((transform, transform.name));
				}
			}
			// The glTF import puts a light on a child without a node of its own, rotated for glTF. Below a game object of
			// this instance whose prefab has a light, that child goes after the link: the instance would light twice.
			var lightHelpers = new List<int>();
			for (var i = 0; i < others.Count; i++) {
				var parent = others[i].Transform.parent;
				if ((parent == go.transform || nodeIds.TryGetValue(parent, out var parentId) && matched.ContainsKey(parentId))
					&& IsLightHelper(others[i].Transform, nodeIds)) {
					lightHelpers.Add(i);
				}
			}
			for (var i = 0; i < others.Count; i++) {
				others[i].Transform.name = UnlinkedName + i;
			}
			foreach (var (id, _) in matched.OrderBy(pair => pair.Value.GetSiblingIndex())) {
				_transformByNodeId[id].SetAsLastSibling();
			}

			// The link drops the components the imported game objects have and the prefab's don't, like the light
			// glTF puts on the node itself. The package's own components are only created after the link.
			var extras = matched.ToDictionary(pair => pair.Key, pair => ExtraComponents(_transformByNodeId[pair.Key], pair.Value), StringComparer.Ordinal);
			var rootExtras = ExtraComponents(go.transform, prefab.transform);

			item.Apply(go);

			// bind the node ids again, and give the game objects their names back
			var instances = new Dictionary<Transform, Transform>();
			if (PrefabUtility.IsPartOfPrefabInstance(go)) {
				foreach (var t in go.GetComponentsInChildren<Transform>(true)) {
					var source = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(t, prefabPath);
					if (source) {
						instances[source] = t;
					}
				}
			}
			var unlinked = new Transform[others.Count];
			foreach (var t in go.GetComponentsInChildren<Transform>(true)) {
				if (t.name.StartsWith(UnlinkedName, StringComparison.Ordinal)
					&& int.TryParse(t.name.Substring(UnlinkedName.Length), out var i) && i >= 0 && i < others.Count) {
					unlinked[i] = t;
					// the dictionary finds the game object the link replaced by its instance id
					if (nodeIds.TryGetValue(others[i].Transform, out var id)) {
						_transformByNodeId[id] = t;
					}
					t.name = others[i].Name;
				}
			}
			foreach (var (id, prefabObject) in matched) {
				if (!instances.TryGetValue(prefabObject, out var transform)) {
					Logger.Warn($"Node {id} under '{go.name}' is gone after linking it to its prefab.");
					continue;
				}
				_transformByNodeId[id] = transform;
				var pose = poses[id];
				transform.name = names[id];
				transform.localPosition = pose.Position;
				transform.localRotation = pose.Rotation;
				transform.localScale = pose.Scale;
				PrefabUtility.RecordPrefabInstancePropertyModifications(transform.gameObject);
				PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
				RestoreComponents(transform, extras[id]);
			}
			RestoreComponents(go.transform, rootExtras);

			var dropped = 0;
			foreach (var i in lightHelpers) {
				var helper = unlinked[i];
				if (helper && helper.parent && helper.parent.GetComponent<UnityEngine.Light>()) {
					Object.DestroyImmediate(helper.gameObject);
					dropped++;
				}
			}
			if (dropped > 0) {
				Logger.Info($"Dropped {dropped} imported light helper(s) below '{go.name}' whose lights its prefab has.");
			}
		}

		// The components of an imported game object beyond the ones of the prefab's game object it is an instance of,
		// by type and index among the components of that type, with their serialized values. Only the ones the glTF
		// import creates whose references are assets: the JSON can't keep references to game objects of the scene.
		private static List<(Type Type, int Index, string Json)> ExtraComponents(Transform imported, Transform prefabObject)
		{
			var extras = new List<(Type, int, string)>();
			var seen = new Dictionary<Type, int>();
			foreach (var component in imported.GetComponents<Component>()) {
				if (!component || component is Transform) {
					continue;
				}
				var type = component.GetType();
				seen.TryGetValue(type, out var index);
				seen[type] = index + 1;
				if (index < prefabObject.GetComponents(type).Count(c => c.GetType() == type)) {
					continue;
				}
				if (component is UnityEngine.Light or MeshFilter or MeshRenderer || RequiresLight(component)) {
					extras.Add((type, index, EditorJsonUtility.ToJson(component)));
				} else {
					Logger.Warn($"Linking '{imported.name}' to its prefab drops its {type.Name}, which the prefab's game object doesn't have.");
				}
			}
			return extras;
		}

		private static void RestoreComponents(Transform transform, List<(Type Type, int Index, string Json)> extras)
		{
			foreach (var (type, index, json) in extras) {
				// adding one can have added another, like the render pipeline's light data with a light
				var existing = transform.GetComponents(type).Where(c => c.GetType() == type).ToArray();
				var component = existing.Length > index ? existing[index] : transform.gameObject.AddComponent(type);
				if (component) {
					EditorJsonUtility.FromJsonOverwrite(json, component);
				}
			}
		}

		// Whether an imported game object is a light helper: no node in it, and nothing but lights, one at least, with
		// what they require (like the render pipeline's light data).
		private static bool IsLightHelper(Transform helper, IReadOnlyDictionary<Transform, string> nodeIds)
		{
			var lights = 0;
			foreach (var t in helper.GetComponentsInChildren<Transform>(true)) {
				if (nodeIds.ContainsKey(t)) {
					return false;
				}
				foreach (var component in t.GetComponents<Component>()) {
					switch (component) {
						case Transform:
							break;
						case UnityEngine.Light:
							lights++;
							break;
						default:
							if (!RequiresLight(component)) {
								return false;
							}
							break;
					}
				}
			}
			return lights > 0;
		}

		// what comes with a light, like the render pipeline's light data
		private static bool RequiresLight(Component component)
		{
			return component && component.GetType().GetCustomAttributes(typeof(RequireComponent), true)
				.Cast<RequireComponent>()
				.Any(required => required.m_Type0 == typeof(UnityEngine.Light) || required.m_Type1 == typeof(UnityEngine.Light) || required.m_Type2 == typeof(UnityEngine.Light));
		}

		// a name no prefab has, followed by an index into the game objects it was given to
		private const string UnlinkedName = "\u001Fvpe-unlinked-";

		// Whether a game object below a root that is linked to its prefab is part of that instance, and not of an
		// instance below it that is linked on its own.
		private static bool IsPartOfLinkedInstance(Transform transform, Transform root, IReadOnlyDictionary<Transform, string> nodeIds,
			IReadOnlyDictionary<string, ItemPackable> items)
		{
			for (var t = transform; t && t != root; t = t.parent) {
				if (nodeIds.TryGetValue(t, out var id) && items.TryGetValue(id, out var state) && !string.IsNullOrEmpty(state.PrefabGuid)) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Second pass: hands each component the references it was written with, now that every component exists.
		/// </summary>
		private void ReadReferences(List<(string NodeId, Type Type, int Index, Component Component, PackagedItems.Entry Entry)> references)
		{
			foreach (var (nodeId, type, index, created, entry) in references) {
				// relinking a later node to its prefab can have replaced the component, or its game object
				var go = created ? created.gameObject : ResolveItemNode(nodeId)?.gameObject;
				var comp = created ? created : go ? PackagedItems.FindComponent(go, type, index) : null;
				if (comp is IPackable packable) {
					packable.UnpackReferences(entry.Refs, _tableRoot, _refs, _files);
				} else {
					Logger.Warn($"Cannot restore the references of {entry.Type} on node {nodeId}: the component is gone or can't be packaged.");
				}
			}
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
				tableComponent.Metadata = TableProvenance.CopyMetadata(_sourceMetadata);
				tableComponent.RestoreProvenance(TableProvenance.Derive(_sourceProvenance, _sourceMetadata));
				tableComponent.Metadata.Version = "1.0.0";
			}
		}
	}
}
