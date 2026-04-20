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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class RuntimePackageReader
	{
		private readonly string _vpePath;
		private GameObject _table;
		private IPackageFolder _tableFolder;
		private PackagedRefs _refs;
		private PackagedFiles _files;
		private Dictionary<string, int[]> _sparsePathIndexMap;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public RuntimePackageReader(string vpePath)
		{
			_vpePath = vpePath;
		}

		public async Task<GameObject> ImportIntoScene(Transform parent = null, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(_vpePath)) {
				throw new ArgumentException("No .vpe path was provided.");
			}
			if (!File.Exists(_vpePath)) {
				throw new FileNotFoundException($"Cannot find .vpe package at {_vpePath}");
			}

			using var storage = PackageApi.StorageManager.OpenStorage(_vpePath);
			_tableFolder = storage.GetFolder(PackageApi.TableFolder);
			_sparsePathIndexMap = BuildSparsePathIndexMap(PackageApi.ItemFolder, PackageApi.ItemReferencesFolder);

			var previousSparsePathIndexMap = TransformExtensions.SparsePathIndexMap;
			TransformExtensions.SparsePathIndexMap = _sparsePathIndexMap;

			try {
				try {
					_table = await ImportModels(parent, cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();
					var restoreActive = _table.activeSelf;
					var loadSucceeded = false;
					_table.SetActive(false);

					try {
						_refs = new PackagedRefs(_table.transform);
						_files = new PackagedFiles(_tableFolder, _refs);

						await _files.UnpackSoundsRuntime(cancellationToken);
						_files.UnpackAssetsRuntime();
						await _files.UnpackMeshesRuntime(cancellationToken);

						ReadPackables(PackageApi.ItemFolder, ApplyItemData, (item, type, file, index) => {
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

						ReadPackables(PackageApi.ItemReferencesFolder, null, (item, type, file, index) => {
							var comps = item.gameObject.GetComponents(type);
							var comp = comps.Length > index
								? comps[index]
								: item.gameObject.AddComponent(type);
							if (comp == null) {
								Logger.Warn($"Cannot create component of type {type.FullName} on {item.name}.");
								return;
							}
							if (comp is not IPackable packable) {
								Logger.Warn($"Cannot unpack references for type {type.FullName} on {item.name} because the component does not implement {nameof(IPackable)}.");
								return;
							}

							// Some components are intentionally refs-only and return null from Pack().
							// Ensure they still get created so UnpackReferences can restore wiring.
							if (comps.Length <= index) {
								Logger.Info($"Created refs-only component {type.FullName} on {item.name} (index {index}).");
							}

							try {
								packable.UnpackReferences(file.GetData(), _table.transform, _refs, _files);
							} catch (Exception ex) {
								Logger.Warn(ex, $"Failed unpacking references for type {type.FullName} on {item.name} (index {index}).");
							}
						});

						ReadGlobals();
						ReadTableMetadata();
						RestoreMaterialProfiles();
						loadSucceeded = true;

					} finally {
						if (loadSucceeded && _table) {
							_table.SetActive(restoreActive);
						}
					}

					return _table;

				} catch {
					DestroyLoadedTable();
					throw;
				}

			} finally {
				TransformExtensions.SparsePathIndexMap = previousSparsePathIndexMap;
			}
		}

		private async Task<GameObject> ImportModels(Transform parent, CancellationToken cancellationToken)
		{
			var sceneFile = _tableFolder.GetFile(PackageApi.SceneFile);
			var sceneData = sceneFile.GetData();
			if (sceneData == null || sceneData.Length == 0) {
				throw new Exception($"Scene data file '{PackageApi.SceneFile}' is missing or empty.");
			}

			var importRoot = new GameObject("__vpe_runtime_import");
			importRoot.hideFlags = HideFlags.HideAndDontSave;
			if (parent != null) {
				importRoot.transform.SetParent(parent, false);
			}

			try {
				var gltf = new GltfImport(logger: new ConsoleLogger());
				var uri = new Uri(Path.GetFullPath(_vpePath));
				var loaded = await gltf.Load(sceneData, uri, cancellationToken: cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!loaded) {
					throw new Exception("Failed loading table.glb from package.");
				}

				var instantiated = await gltf.InstantiateMainSceneAsync(importRoot.transform, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!instantiated) {
					throw new Exception("Failed instantiating table.glb scene.");
				}

				if (importRoot.transform.childCount == 0) {
					throw new Exception("The .vpe scene did not instantiate any root object.");
				}

				var tableRoot = SelectTableRoot(importRoot.transform);
				if (!tableRoot) {
					throw new Exception("Could not determine table root from imported .vpe scene.");
				}

				var table = tableRoot.gameObject;
				table.transform.SetParent(parent, false);

				if (Application.isPlaying) {
					UnityEngine.Object.Destroy(importRoot);
				} else {
					UnityEngine.Object.DestroyImmediate(importRoot);
				}
				return table;

			} catch {
				if (Application.isPlaying) {
					UnityEngine.Object.Destroy(importRoot);
				} else {
					UnityEngine.Object.DestroyImmediate(importRoot);
				}
				throw;
			}
		}

		private Transform SelectTableRoot(Transform importRoot)
		{
			var candidates = CollectRootCandidates(importRoot);
			if (candidates.Count == 0) {
				return null;
			}

			if (!_tableFolder.TryGetFolder(PackageApi.ItemFolder, out var itemsFolder)) {
				return candidates[0];
			}

			var paths = new List<string>();
			itemsFolder.VisitFolders(itemFolder => paths.Add(itemFolder.Name));
			if (paths.Count == 0) {
				return candidates[0];
			}

			Transform bestCandidate = candidates[0];
			var bestScore = ScoreCandidate(candidates[0], paths);
			foreach (var candidate in candidates.Skip(1)) {
				var score = ScoreCandidate(candidate, paths);
				if (score <= bestScore) {
					continue;
				}

				bestCandidate = candidate;
				bestScore = score;
				if (bestScore == paths.Count) {
					break;
				}
			}

			if (bestScore == 0) {
				Logger.Warn("Runtime loader could not match any packaged paths on imported scene roots. Falling back to first root candidate.");
				return candidates[0];
			}

			return bestCandidate;
		}

		private static List<Transform> CollectRootCandidates(Transform importRoot)
		{
			var candidates = new List<Transform>();
			var visited = new HashSet<int>();

			void Add(Transform transform)
			{
				if (!transform) {
					return;
				}

				var instanceId = transform.GetInstanceID();
				if (visited.Add(instanceId)) {
					candidates.Add(transform);
				}
			}

			for (var i = 0; i < importRoot.childCount; i++) {
				var child = importRoot.GetChild(i);
				Add(child);
				for (var j = 0; j < child.childCount; j++) {
					var grandChild = child.GetChild(j);
					Add(grandChild);
				}
			}

			return candidates;
		}

		private static int ScoreCandidate(Transform candidate, IReadOnlyList<string> paths)
		{
			var score = 0;
			foreach (var path in paths) {
				if (candidate.FindByPath(path) != null) {
					score++;
				}
			}

			return score;
		}

		private void ApplyItemData(GameObject gameObject, IPackageFile itemFile)
		{
			if (itemFile == null) {
				return;
			}
			var itemData = ItemPackable.Unpack(itemFile.GetData());
			itemData.ApplyRuntime(gameObject);
		}

		/// <summary>
		/// Loops through all items and components in the package, and applies the given action.
		/// The item action is executed before the component action.
		/// </summary>
		private void ReadPackables(string rootFolder, Action<GameObject, IPackageFile> itemAction, Action<Transform, Type, IPackageFile, int> componentAction)
		{
			if (!_tableFolder.TryGetFolder(rootFolder, out var itemsFolder)) {
				return;
			}

			// -> rootFolder <- / 0.0.0 / CompType / 0
			itemsFolder.VisitFolders(itemFolder => {
				// rootFolder / -> 0.0.0 <- / CompType / 0
				var item = _table.transform.FindByPath(itemFolder.Name);
				if (item == null) {
					throw new Exception($"Cannot find item at path {itemFolder.Name} on node {_table.name}. Imported hierarchy does not match packaged item paths.");
				}

				if (itemAction != null && itemFolder.TryGetFile(PackageApi.ItemFile, out var itemFile, PackageApi.Packer.FileExtension)) {
					itemAction(item.gameObject, itemFile);
				}

				itemFolder.VisitFolders(typeFolder => {
					// rootFolder / 0.0.0 / -> CompType <- / 0
					var type = _refs.GetType(typeFolder.Name);
					if (type == null) {
						throw new Exception($"Unknown component type '{typeFolder.Name}' while reading package.");
					}

					var index = 0;
					typeFolder.VisitFiles(compFile => {
						// rootFolder / 0.0.0 / CompType / -> 0 <-
						componentAction(item, type, compFile, index++);
					});
				});
			});
		}

		private Dictionary<string, int[]> BuildSparsePathIndexMap(params string[] rootFolders)
		{
			var sparseChildrenByParent = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
			foreach (var rootFolder in rootFolders) {
				if (!_tableFolder.TryGetFolder(rootFolder, out var pathFolder)) {
					continue;
				}

				pathFolder.VisitFolders(itemFolder => RegisterPath(itemFolder.Name, sparseChildrenByParent));
			}

			var sparsePathIndexMap = new Dictionary<string, int[]>(sparseChildrenByParent.Count, StringComparer.Ordinal);
			foreach (var entry in sparseChildrenByParent) {
				var parentPath = entry.Key;
				var sparseChildren = entry.Value;
				var indices = new int[sparseChildren.Count];
				sparseChildren.CopyTo(indices);
				sparsePathIndexMap[parentPath] = indices;
			}

			return sparsePathIndexMap;
		}

		private static void RegisterPath(string path, IDictionary<string, SortedSet<int>> sparseChildrenByParent)
		{
			if (string.IsNullOrWhiteSpace(path)) {
				return;
			}

			var segments = path.Split('.');
			if (segments.Length == 0 || segments[0] != "0") {
				return;
			}

			var parentPath = "0";
			for (var segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++) {
				if (!int.TryParse(segments[segmentIndex], out var sparseChildIndex)) {
					return;
				}

				if (!sparseChildrenByParent.TryGetValue(parentPath, out var sparseChildren)) {
					sparseChildren = new SortedSet<int>();
					sparseChildrenByParent[parentPath] = sparseChildren;
				}
				sparseChildren.Add(sparseChildIndex);
				parentPath = $"{parentPath}.{segments[segmentIndex]}";
			}
		}

		private void ReadGlobals()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}
			if (!_tableFolder.TryGetFolder(PackageApi.GlobalFolder, out var globalStorage)) {
				return;
			}

			tableComponent.MappingConfig = new MappingConfig {
				Switches = ReadGlobalList<SwitchMapping>(globalStorage, PackageApi.SwitchesFile),
				Coils = ReadGlobalList<CoilMapping>(globalStorage, PackageApi.CoilsFile),
				Lamps = ReadGlobalList<LampMapping>(globalStorage, PackageApi.LampsFile),
				Wires = ReadGlobalList<WireMapping>(globalStorage, PackageApi.WiresFile),
			};

			foreach (var sw in tableComponent.MappingConfig.Switches) {
				sw.RestoreReference(_table.transform);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.RestoreReference(_table.transform);
			}
			foreach (var lamp in tableComponent.MappingConfig.Lamps) {
				lamp.RestoreReference(_table.transform);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.RestoreReferences(_table.transform);
			}
		}

		private static List<T> ReadGlobalList<T>(IPackageFolder folder, string fileName)
		{
			if (!folder.TryGetFile(fileName, out var file, PackageApi.Packer.FileExtension)) {
				return new List<T>();
			}

			return PackageApi.Packer.Unpack<List<T>>(file.GetData()) ?? new List<T>();
		}

		private void ReadTableMetadata()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}

			if (_tableFolder.TryGetFile(PackageApi.TableMetadataFile, out var tableMetadataFile, PackageApi.Packer.FileExtension)) {
				tableComponent.Metadata = PackageApi.Packer.Unpack<TableMetadata>(tableMetadataFile.GetData()) ?? new TableMetadata();
			}
		}

		private void RestoreMaterialProfiles()
		{
			if (!_tableFolder.TryGetFolder(PackageApi.MetaFolder, out var metaFolder)) {
				return;
			}

			VpeMaterialV1Reader.TryApply(metaFolder, _table.transform);
		}

		private void DestroyLoadedTable()
		{
			if (!_table) {
				return;
			}

			if (Application.isPlaying) {
				UnityEngine.Object.Destroy(_table);
			} else {
				UnityEngine.Object.DestroyImmediate(_table);
			}
			_table = null;
		}
	}
}
