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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using Newtonsoft.Json.Linq;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class RuntimePackageReader
	{
		// While restoring many small items we stay on the main thread and only yield once the
		// frame budget is used up, so the loading UI keeps animating without paying a yield's
		// frame latency every few items.
		private const long RuntimeYieldBudgetMilliseconds = 25;

		private readonly string _vpePath;
		private readonly Stopwatch _yieldStopwatch = new();
		private string _textureCacheRoot;
		private long _cookSettingsHash;
		private GameObject _table;
		private IPackageFolder _tableFolder;
		private PackagedRefs _refs;
		private PackagedFiles _files;
		private VpePackageManifest _manifest;
		private Dictionary<string, Transform> _transformByNodeId;
		private readonly HashSet<string> _unknownTypeNames = new(StringComparer.Ordinal);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public RuntimePackageReader(string vpePath)
		{
			_vpePath = vpePath;
		}

		public async Task<GameObject> ImportIntoScene(Transform parent = null, CancellationToken cancellationToken = default)
		{
			return await ImportIntoScene(parent, null, cancellationToken);
		}

		public async Task<GameObject> ImportIntoScene(
			Transform parent,
			IProgress<RuntimePackageLoadProgress> progress,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(_vpePath)) {
				throw new ArgumentException("No .vpe path was provided.");
			}
			if (!File.Exists(_vpePath)) {
				throw new FileNotFoundException($"Cannot find .vpe package at {_vpePath}");
			}

			var importStopwatch = Stopwatch.StartNew();
			_yieldStopwatch.Restart();
			ReportProgress(progress, RuntimePackageLoadStage.OpeningPackage, 0f, $"Opening {Path.GetFileName(_vpePath)}...");

			// The material payload and the source textures are the biggest reads besides the
			// scene itself; pull them off disk (and parse the JSON payload) on a worker thread while
			// the main thread imports the scene and restores items. When a valid cooked-texture
			// cache exists for this package, the worker loads it instead of the source textures. A
			// second worker bulk-reads the thousands of tiny item/ref entries so the restore loops
			// don't pay per-entry zip cost on the main thread. Each worker opens its own storage;
			// SharpZipLib readers are not safe to share across threads.
			_textureCacheRoot = Application.persistentDataPath;
			_cookSettingsHash = VpeTextureCookSettings.ComputeHash();
			var materialPrefetchTask = Task.Run(() => PrefetchMaterialData(_vpePath, _textureCacheRoot, _cookSettingsHash), cancellationToken);
			var packablePrefetchTask = Task.Run(() => PrefetchPackableData(_vpePath), cancellationToken);
			// The PackAsAttribute type scan reflects over every loaded assembly; warm it on a worker
			// so building PackagedRefs after the scene import is effectively free.
			var typeScanTask = Task.Run(PackagedRefs.WarmUpTypeScan, cancellationToken);

			var openStopwatch = Stopwatch.StartNew();
			using var storage = PackageApi.StorageManager.OpenStorage(_vpePath);
			_manifest = VpePackageManifestIo.TryRead(storage);
			if (_manifest == null) {
				throw new Exception(
					"This file has no package manifest — it is not a .vpe table package (or was written by an " +
					"incompatible pre-release version of VPE).");
			}
			if (_manifest.FormatVersion > PackageApi.FormatVersion) {
				throw new Exception(
					$"This package uses format version {_manifest.FormatVersion}, but this build only supports up " +
					$"to version {PackageApi.FormatVersion}. Update VPE to load it.");
			}
			_tableFolder = storage.GetFolder(PackageApi.TableFolder);
			openStopwatch.Stop();
			Logger.Info($"RuntimePackageReader: Opened package (format v{_manifest.FormatVersion}) in {openStopwatch.ElapsedMilliseconds}ms.");
			ReportProgress(progress, RuntimePackageLoadStage.OpeningPackage, 1f, "Package opened.");

			try {
				var importModelsStopwatch = Stopwatch.StartNew();
				_table = await ImportModels(parent, progress, cancellationToken);
				importModelsStopwatch.Stop();
				Logger.Info(
					$"RuntimePackageReader: Imported {PackageApi.SceneFile} in {importModelsStopwatch.ElapsedMilliseconds}ms " +
					$"from '{Path.GetFileName(_vpePath)}'.");
				cancellationToken.ThrowIfCancellationRequested();
				var restoreActive = _table.activeSelf;
				var loadSucceeded = false;
				_table.SetActive(false);

				try {
					var refsStopwatch = Stopwatch.StartNew();
					try {
						await typeScanTask;
					} catch (Exception ex) {
						Logger.Warn(ex, "RuntimePackageReader: background type scan failed; scanning on the main thread.");
					}
					_refs = new PackagedRefs(_table.transform);
					_refs.SetNodeIdsForRead(_transformByNodeId);
					_files = new PackagedFiles(_tableFolder, _refs);
					WarnMissingComponentTypes();
					refsStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Built refs/files in {refsStopwatch.ElapsedMilliseconds}ms.");

					var unpackSoundsStopwatch = Stopwatch.StartNew();
					ReportProgress(progress, RuntimePackageLoadStage.LoadingSounds, 0f, "Loading table audio...");
					await _files.UnpackSoundsRuntime(cancellationToken, (processed, total) => {
						ReportProgress(progress, RuntimePackageLoadStage.LoadingSounds, GetProgress01(processed, total),
							FormatStageMessage("Loading table audio", processed, total));
					});
					ReportProgress(progress, RuntimePackageLoadStage.LoadingSounds, 1f, "Audio ready.");
					unpackSoundsStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Unpacked sounds in {unpackSoundsStopwatch.ElapsedMilliseconds}ms.");

					var unpackAssetsStopwatch = Stopwatch.StartNew();
					ReportProgress(progress, RuntimePackageLoadStage.LoadingAssets, 0f, "Loading runtime assets...");
					await _files.UnpackAssetsRuntime(cancellationToken, (processed, total) => {
						ReportProgress(progress, RuntimePackageLoadStage.LoadingAssets, GetProgress01(processed, total),
							FormatStageMessage("Loading runtime assets", processed, total));
					});
					ReportProgress(progress, RuntimePackageLoadStage.LoadingAssets, 1f, "Runtime assets ready.");
					unpackAssetsStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Unpacked assets in {unpackAssetsStopwatch.ElapsedMilliseconds}ms.");

					var unpackMeshesStopwatch = Stopwatch.StartNew();
					ReportProgress(progress, RuntimePackageLoadStage.LoadingColliderMeshes, 0f, "Loading collider meshes...");
					await _files.UnpackMeshesRuntime(cancellationToken);
					ReportProgress(progress, RuntimePackageLoadStage.LoadingColliderMeshes, 1f, "Collider meshes ready.");
					unpackMeshesStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Unpacked meshes in {unpackMeshesStopwatch.ElapsedMilliseconds}ms.");

					Dictionary<string, byte[]> packableBytes = null;
					try {
						packableBytes = await packablePrefetchTask;
					} catch (Exception ex) {
						Logger.Warn(ex, "RuntimePackageReader: packable data prefetch failed; reading from package directly.");
					}

					var readItemsStopwatch = Stopwatch.StartNew();
					await ReadPackablesAsync(
						PackageApi.ItemFolder,
						RuntimePackageLoadStage.RestoringPackables,
						"Restoring table objects",
						ApplyItemData,
						(item, type, file, index) => {
							var comps = item.gameObject.GetComponents(type);
							var comp = comps.Length > index
								? comps[index]
								: item.gameObject.AddComponent(type);
							if (comp is IPackable packable) {
								packable.Unpack(file.GetData());
							} else {
								PackageApi.Packer.Unpack(file.GetData(), comp);
							}
						},
						packableBytes,
						progress,
						cancellationToken);
					readItemsStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Restored packables in {readItemsStopwatch.ElapsedMilliseconds}ms.");

					var readRefsStopwatch = Stopwatch.StartNew();
					await ReadPackablesAsync(
						PackageApi.ItemReferencesFolder,
						RuntimePackageLoadStage.RestoringReferences,
						"Restoring gameplay references",
						null,
						(item, type, file, index) => {
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
						},
						packableBytes,
						progress,
						cancellationToken);
					readRefsStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Restored references in {readRefsStopwatch.ElapsedMilliseconds}ms.");

					var globalsStopwatch = Stopwatch.StartNew();
					await ReadGlobalsAsync(progress, cancellationToken);
					globalsStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Read globals in {globalsStopwatch.ElapsedMilliseconds}ms.");

					var tableMetadataStopwatch = Stopwatch.StartNew();
					ReportProgress(progress, RuntimePackageLoadStage.RestoringTableMetadata, 0f, "Reading table metadata...");
					ReadTableMetadata();
					ReportProgress(progress, RuntimePackageLoadStage.RestoringTableMetadata, 1f, "Table metadata ready.");
					tableMetadataStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Read table metadata in {tableMetadataStopwatch.ElapsedMilliseconds}ms.");

					var materialsStopwatch = Stopwatch.StartNew();
					ReportProgress(progress, RuntimePackageLoadStage.RestoringMaterials, 0f, "Applying material profiles...");
					MaterialPrefetchResult materialPrefetch = null;
					try {
						materialPrefetch = await materialPrefetchTask;
					} catch (Exception ex) {
						Logger.Warn(ex, "RuntimePackageReader: material data prefetch failed; reading synchronously.");
					}
					await RestoreMaterialProfilesAsync(materialPrefetch, progress, cancellationToken);
					ReportProgress(progress, RuntimePackageLoadStage.RestoringMaterials, 1f, "Material profiles applied.");
					materialsStopwatch.Stop();
					Logger.Info($"RuntimePackageReader: Restored material profiles in {materialsStopwatch.ElapsedMilliseconds}ms.");
					loadSucceeded = true;

				} finally {
					if (loadSucceeded && _table) {
						var activateStopwatch = Stopwatch.StartNew();
						_table.SetActive(restoreActive);
						activateStopwatch.Stop();
						Logger.Info($"RuntimePackageReader: Activated table in {activateStopwatch.ElapsedMilliseconds}ms.");
					}
				}

				importStopwatch.Stop();
				ReportProgress(progress, RuntimePackageLoadStage.Finalizing, 1f, "Table ready.");
				Logger.Info(
					$"RuntimePackageReader: Imported '{Path.GetFileName(_vpePath)}' in {importStopwatch.ElapsedMilliseconds}ms total.");
				return _table;

			} catch {
				importStopwatch.Stop();
				DestroyLoadedTable();
				throw;
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
					$"This package uses component types that are not registered in this build: {string.Join(", ", missing)}. " +
					"Their data will be skipped — a plugin may be missing.");
			}
		}

		private async Task<GameObject> ImportModels(
			Transform parent,
			IProgress<RuntimePackageLoadProgress> progress,
			CancellationToken cancellationToken)
		{
			var sceneFile = _tableFolder.GetFile(PackageApi.SceneFile);
			var sceneData = sceneFile.GetData();
			if (sceneData == null || sceneData.Length == 0) {
				throw new Exception($"Scene data file '{PackageApi.SceneFile}' is missing or empty.");
			}

			_transformByNodeId = null;
			// These scans only touch the raw GLB bytes (chunk walking + JSON), so they can run on a
			// worker thread while glTFast parses the same buffer on the main path.
			var sceneScanTask = Task.Run(() => {
				var nodeTree = VpeNodeIds.TryParse(sceneData);
				var noTangents = CollectMeshesWithoutGltfTangents(sceneData);
				return (nodeTree, noTangents);
			}, cancellationToken);

			var importRoot = new GameObject("__vpe_runtime_import");
			importRoot.hideFlags = HideFlags.HideAndDontSave;
			if (parent != null) {
				importRoot.transform.SetParent(parent, false);
			}

			try {
				// The player shows a loading screen during import, so trade per-frame smoothness for
				// total wall time: without time-slicing, glTFast finishes mesh/scene setup in one go.
				var gltf = new GltfImport(
					deferAgent: new UninterruptedDeferAgent(),
					materialGenerator: RuntimeGltfMaterialGenerator.Create(),
					logger: new ConsoleLogger());
				var uri = new Uri(Path.GetFullPath(_vpePath));
				ReportProgress(progress, RuntimePackageLoadStage.ImportingScene, 0f, "Reading table scene...");
				var loadStopwatch = Stopwatch.StartNew();
				var loaded = await gltf.Load(sceneData, uri, cancellationToken: cancellationToken);
				loadStopwatch.Stop();
				ReportProgress(progress, RuntimePackageLoadStage.ImportingScene, 1f, "Scene data imported.");
				cancellationToken.ThrowIfCancellationRequested();
				if (!loaded) {
					throw new Exception("Failed loading table.glb from package.");
				}

				ReportProgress(progress, RuntimePackageLoadStage.InstantiatingScene, 0f, "Building scene hierarchy...");
				var instantiateStopwatch = Stopwatch.StartNew();
				// The instantiator's NodeCreated hook gives the exact glTF-node-index → GameObject
				// mapping, which (combined with the node ids in the glTF extras) binds the package's
				// stable node ids to the instantiated transforms without any structural guessing.
				var gameObjectsByNodeIndex = new Dictionary<uint, GameObject>();
				var instantiator = new GameObjectInstantiator(gltf, importRoot.transform);
				instantiator.NodeCreated += (nodeIndex, go) => gameObjectsByNodeIndex[nodeIndex] = go;
				var instantiated = await gltf.InstantiateMainSceneAsync(instantiator, cancellationToken);
				instantiateStopwatch.Stop();
				ReportProgress(progress, RuntimePackageLoadStage.InstantiatingScene, 1f, "Scene hierarchy ready.");
				cancellationToken.ThrowIfCancellationRequested();
				if (!instantiated) {
					throw new Exception("Failed instantiating table.glb scene.");
				}

				if (importRoot.transform.childCount == 0) {
					throw new Exception("The .vpe scene did not instantiate any root object.");
				}

				var (nodeTree, meshesWithoutTangents) = await sceneScanTask;
				if (nodeTree == null) {
					throw new Exception("table.glb carries no node tree.");
				}
				_transformByNodeId = VpeNodeIds.MapInstantiatedNodes(nodeTree, gameObjectsByNodeIndex);

				var rootSelectStopwatch = Stopwatch.StartNew();
				if (string.IsNullOrEmpty(_manifest.RootNodeId)
					|| !_transformByNodeId.TryGetValue(_manifest.RootNodeId, out var tableRoot)
					|| !tableRoot) {
					throw new Exception("Could not resolve the manifest's root node id in the imported scene.");
				}
				rootSelectStopwatch.Stop();

				var table = tableRoot.gameObject;
				table.transform.SetParent(parent, false);
				var fixupStopwatch = Stopwatch.StartNew();
				ClearGeneratedTangents(table, meshesWithoutTangents);
				VpeLightRestore.NormalizeImportedLightIntensities(table);
				VpeLightRestore.RestoreLightProfiles(table, _tableFolder, id => _transformByNodeId.GetValueOrDefault(id));
				fixupStopwatch.Stop();
				Logger.Info(
					$"RuntimePackageReader: glb stages: load={loadStopwatch.ElapsedMilliseconds}ms, " +
					$"instantiate={instantiateStopwatch.ElapsedMilliseconds}ms, rootSelect={rootSelectStopwatch.ElapsedMilliseconds}ms, " +
					$"fixups={fixupStopwatch.ElapsedMilliseconds}ms, nodeIds={_transformByNodeId.Count}.");

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

		private static HashSet<string> CollectMeshesWithoutGltfTangents(byte[] glbData)
		{
			var result = new HashSet<string>(StringComparer.Ordinal);
			try {
				if (glbData == null || glbData.Length < 20) {
					return result;
				}

				if (BinaryPrimitives.ReadUInt32LittleEndian(glbData.AsSpan(0, 4)) != 0x46546C67 /* glTF */) {
					return result;
				}

				var offset = 12;
				while (offset + 8 <= glbData.Length) {
					var length = BinaryPrimitives.ReadInt32LittleEndian(glbData.AsSpan(offset, 4));
					var type = BinaryPrimitives.ReadUInt32LittleEndian(glbData.AsSpan(offset + 4, 4));
					offset += 8;
					if (length < 0 || offset + length > glbData.Length) {
						return result;
					}

					if (type == 0x4E4F534A /* JSON */) {
						var json = Encoding.UTF8.GetString(glbData, offset, length).TrimEnd('\0', ' ');
						var root = JObject.Parse(json);
						foreach (var mesh in root["meshes"] as JArray ?? new JArray()) {
							var name = mesh.Value<string>("name");
							if (string.IsNullOrWhiteSpace(name)) {
								continue;
							}

							var primitives = mesh["primitives"] as JArray;
							if (primitives == null || primitives.Count == 0) {
								continue;
							}

							var hasTangents = false;
							foreach (var primitive in primitives) {
								if (primitive["attributes"]?["TANGENT"] != null) {
									hasTangents = true;
									break;
								}
							}

							if (!hasTangents) {
								result.Add(name);
							}
						}

						break;
					}

					offset += length;
				}
			} catch (Exception ex) {
				Logger.Warn(ex, "RuntimePackageReader: failed to inspect GLB mesh tangent metadata.");
			}

			return result;
		}

		private static void ClearGeneratedTangents(GameObject table, HashSet<string> meshesWithoutTangents)
		{
			if (!table || meshesWithoutTangents == null || meshesWithoutTangents.Count == 0) {
				return;
			}

			var cleared = 0;
			var visited = new HashSet<int>();
			foreach (var meshFilter in table.GetComponentsInChildren<MeshFilter>(true)) {
				var mesh = meshFilter.sharedMesh;
				if (!mesh || string.IsNullOrWhiteSpace(mesh.name) || !meshesWithoutTangents.Contains(mesh.name)) {
					continue;
				}
				if (!visited.Add(mesh.GetInstanceID())) {
					continue;
				}
				if (mesh.tangents == null || mesh.tangents.Length == 0) {
					continue;
				}

				mesh.tangents = Array.Empty<Vector4>();
				cleared++;
			}

			if (cleared > 0) {
				Logger.Info($"RuntimePackageReader: cleared generated tangents on {cleared} imported meshes that had no GLB TANGENT attribute.");
			}
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
		private async Task ReadPackablesAsync(
			string rootFolder,
			RuntimePackageLoadStage stage,
			string statusPrefix,
			Action<GameObject, IPackageFile> itemAction,
			Action<Transform, Type, IPackageFile, int> componentAction,
			IReadOnlyDictionary<string, byte[]> prefetchedBytes,
			IProgress<RuntimePackageLoadProgress> progress,
			CancellationToken cancellationToken)
		{
			if (!_tableFolder.TryGetFolder(rootFolder, out var itemsFolder)) {
				ReportProgress(progress, stage, 1f, $"{statusPrefix} complete.");
				return;
			}

			var itemFolders = GetFolders(itemsFolder);
			var totalOperations = itemAction != null ? itemFolders.Count : 0;
			foreach (var itemFolder in itemFolders) {
				foreach (var typeFolder in GetFolders(itemFolder)) {
					totalOperations += GetFiles(typeFolder).Count;
				}
			}

			if (totalOperations == 0) {
				ReportProgress(progress, stage, 1f, $"{statusPrefix} complete.");
				return;
			}

			ReportProgress(progress, stage, 0f, statusPrefix + "...");
			var processedOperations = 0;

			foreach (var itemFolder in itemFolders) {
				cancellationToken.ThrowIfCancellationRequested();

				var item = _refs.GetNode(itemFolder.Name);
				if (item == null) {
					throw new Exception($"Cannot find node '{itemFolder.Name}' in the imported scene. The package does not match its scene GLB.");
				}

				if (itemAction != null && itemFolder.TryGetFile(PackageApi.ItemFile, out var itemFile, PackageApi.Packer.FileExtension)) {
					itemAction(item.gameObject, ResolvePackableFile(prefetchedBytes, rootFolder, itemFolder.Name, null, itemFile));
					processedOperations++;
					await ReportProgressAndYieldAsync(progress, stage, statusPrefix, processedOperations, totalOperations, cancellationToken);
				}

				foreach (var typeFolder in GetFolders(itemFolder)) {
					if (!_refs.TryGetType(typeFolder.Name, out var type)) {
						// Forward compatibility: data of unknown component types (newer VPE,
						// missing plugin) is skipped instead of failing the whole import.
						if (_unknownTypeNames.Add(typeFolder.Name)) {
							Logger.Error($"Skipping unknown component type '{typeFolder.Name}' while reading the package. A plugin may be missing.");
						}
						processedOperations += GetFiles(typeFolder).Count;
						continue;
					}

					var index = 0;
					foreach (var compFile in GetFiles(typeFolder)) {
						componentAction(item, type, ResolvePackableFile(prefetchedBytes, rootFolder, itemFolder.Name, typeFolder.Name, compFile), index++);
						processedOperations++;
						await ReportProgressAndYieldAsync(progress, stage, statusPrefix, processedOperations, totalOperations, cancellationToken);
					}
				}
			}

			ReportProgress(progress, stage, 1f, $"{statusPrefix} complete.");
		}

		private static IPackageFile ResolvePackableFile(
			IReadOnlyDictionary<string, byte[]> prefetchedBytes,
			string rootFolder,
			string itemName,
			string typeName,
			IPackageFile file)
		{
			if (prefetchedBytes != null
				&& prefetchedBytes.TryGetValue(PackableKey(rootFolder, itemName, typeName, file.Name), out var bytes)
				&& bytes != null) {
				return new PrefetchedPackageFile(file.Name, bytes);
			}
			return file;
		}

		private static string PackableKey(string rootFolder, string itemName, string typeName, string fileName)
		{
			return typeName == null
				? $"{rootFolder}|{itemName}|{fileName}"
				: $"{rootFolder}|{itemName}|{typeName}|{fileName}";
		}

		// Runs on a worker thread with its own storage instance; bulk-reads every item/ref entry so
		// the main-thread restore loops get their bytes from memory.
		private static Dictionary<string, byte[]> PrefetchPackableData(string vpePath)
		{
			try {
				using var storage = PackageApi.StorageManager.OpenStorage(vpePath);
				var tableFolder = storage.GetFolder(PackageApi.TableFolder);
				var result = new Dictionary<string, byte[]>(4096, StringComparer.Ordinal);
				foreach (var rootName in new[] { PackageApi.ItemFolder, PackageApi.ItemReferencesFolder }) {
					if (!tableFolder.TryGetFolder(rootName, out var rootFolder)) {
						continue;
					}
					rootFolder.VisitFolders(itemFolder => {
						if (itemFolder.TryGetFile(PackageApi.ItemFile, out var itemFile, PackageApi.Packer.FileExtension)) {
							result[PackableKey(rootName, itemFolder.Name, null, itemFile.Name)] = itemFile.GetData();
						}
						itemFolder.VisitFolders(typeFolder => {
							typeFolder.VisitFiles(file => {
								result[PackableKey(rootName, itemFolder.Name, typeFolder.Name, file.Name)] = file.GetData();
							});
						});
					});
				}
				return result;

			} catch (Exception ex) {
				Logger.Warn(ex, "RuntimePackageReader: failed prefetching packable data; falling back to direct package reads.");
				return null;
			}
		}

		private sealed class PrefetchedPackageFile : IPackageFile
		{
			private readonly byte[] _data;

			public PrefetchedPackageFile(string name, byte[] data)
			{
				Name = name;
				_data = data;
			}

			public string Name { get; }
			public Stream AsStream() => new MemoryStream(_data, false);
			public byte[] GetData() => _data;
			public void SetData(byte[] data, PackageCompression compression = PackageCompression.Default)
				=> throw new InvalidOperationException("Prefetched package files are read-only.");
		}

		private async Task ReadGlobalsAsync(IProgress<RuntimePackageLoadProgress> progress, CancellationToken cancellationToken)
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}
			if (!_tableFolder.TryGetFolder(PackageApi.GlobalFolder, out var globalStorage)) {
				ReportProgress(progress, RuntimePackageLoadStage.RestoringGlobals, 1f, "Table wiring ready.");
				return;
			}

			tableComponent.MappingConfig = new MappingConfig {
				Switches = ReadGlobalList<SwitchMapping>(globalStorage, PackageApi.SwitchesFile),
				Coils = ReadGlobalList<CoilMapping>(globalStorage, PackageApi.CoilsFile),
				Lamps = ReadGlobalList<LampMapping>(globalStorage, PackageApi.LampsFile),
				Wires = ReadGlobalList<WireMapping>(globalStorage, PackageApi.WiresFile),
			};

			var totalMappings = tableComponent.MappingConfig.Switches.Count +
				tableComponent.MappingConfig.Coils.Count +
				tableComponent.MappingConfig.Lamps.Count +
				tableComponent.MappingConfig.Wires.Count;

			if (totalMappings == 0) {
				ReportProgress(progress, RuntimePackageLoadStage.RestoringGlobals, 1f, "Table wiring ready.");
				return;
			}

			ReportProgress(progress, RuntimePackageLoadStage.RestoringGlobals, 0f, "Restoring table wiring...");
			var processedMappings = 0;

			foreach (var sw in tableComponent.MappingConfig.Switches) {
				sw.RestoreReference(_refs);
				processedMappings++;
				await ReportProgressAndYieldAsync(progress, RuntimePackageLoadStage.RestoringGlobals, "Restoring table wiring",
					processedMappings, totalMappings, cancellationToken);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.RestoreReference(_refs);
				processedMappings++;
				await ReportProgressAndYieldAsync(progress, RuntimePackageLoadStage.RestoringGlobals, "Restoring table wiring",
					processedMappings, totalMappings, cancellationToken);
			}
			foreach (var lamp in tableComponent.MappingConfig.Lamps) {
				lamp.RestoreReference(_refs);
				processedMappings++;
				await ReportProgressAndYieldAsync(progress, RuntimePackageLoadStage.RestoringGlobals, "Restoring table wiring",
					processedMappings, totalMappings, cancellationToken);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.RestoreReferences(_refs);
				processedMappings++;
				await ReportProgressAndYieldAsync(progress, RuntimePackageLoadStage.RestoringGlobals, "Restoring table wiring",
					processedMappings, totalMappings, cancellationToken);
			}

			ReportProgress(progress, RuntimePackageLoadStage.RestoringGlobals, 1f, "Table wiring ready.");
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

		private async Task RestoreMaterialProfilesAsync(
			MaterialPrefetchResult prefetch,
			IProgress<RuntimePackageLoadProgress> progress,
			CancellationToken cancellationToken)
		{
			var payload = prefetch?.Payload;
			var entries = prefetch?.Sources?.Entries;
			var blob = prefetch?.Sources?.Blob;

			if (payload == null) {
				// Prefetch failed or carried no payload; read synchronously.
				if (VpeMaterialReader.TryLoad(_tableFolder, loadSourceBytes: true, out payload, out var sources)) {
					entries = sources.Entries;
					blob = sources.Blob;
				}
			}

			if (payload == null) {
				return;
			}

			if (prefetch?.CachedTextures != null) {
				// Cached cook: swap in the GPU-ready entries; the source textures were never read.
				entries = VpeTextureCook.ReplaceEntries(entries, prefetch.CachedTextures.Manifest);
				VpeTextureCook.RewriteNormalRefs(payload, prefetch.CachedTextures.Manifest);
				blob = prefetch.CachedTextures.CookedData;
				Logger.Info("RuntimePackageReader: using cooked texture cache.");

			} else if (blob != null && VpeTextureCook.IsSupported) {
				// First load on this machine (or settings changed): cook the source textures into
				// GPU-ready payloads now and persist them for the next load.
				ReportProgress(progress, RuntimePackageLoadStage.RestoringMaterials, 0f, "Optimizing textures (first load)...");
				VpeTextureCook.Result cookResult = null;
				try {
					cookResult = await VpeTextureCook.CookAsync(
						entries,
						VpeTextureCook.CollectNormalTextureIds(payload),
						blob,
						(done, total) => ReportProgress(progress, RuntimePackageLoadStage.RestoringMaterials,
							total > 0 ? done * 0.9f / total : 0f, $"Optimizing textures (first load, {done}/{total})"),
						cancellationToken);
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					Logger.Warn(ex, "RuntimePackageReader: texture cook failed; decoding textures directly.");
				}

				if (cookResult != null) {
					entries = VpeTextureCook.ReplaceEntries(entries, cookResult.Manifest);
					VpeTextureCook.RewriteNormalRefs(payload, cookResult.Manifest);
					blob = cookResult.CookedData;

					// Persisting ~hundreds of MB takes a moment; do it off-thread, the in-memory
					// result is used for this load either way.
					var vpePath = _vpePath;
					var cacheRoot = _textureCacheRoot;
					var settingsHash = _cookSettingsHash;
					var manifest = cookResult.Manifest;
					var cookedData = cookResult.CookedData;
					_ = Task.Run(() => VpeTextureCache.Write(cacheRoot, vpePath, settingsHash, manifest, cookedData));
				}
			}

			await VpeMaterialReader.TryApplyAsync(
				payload, entries, blob, _table.transform,
				id => _transformByNodeId.GetValueOrDefault(id), "materials");
		}

		// Runs on a worker thread. Opens its own read-only view on the package so it doesn't race
		// the main import's storage, and pre-parses the material payload (Newtonsoft is thread-safe).
		// When a valid texture cache exists, the cooked blob is loaded instead of the source
		// textures — the source bytes are only needed for cooking.
		private static MaterialPrefetchResult PrefetchMaterialData(string vpePath, string cacheRoot, long cookSettingsHash)
		{
			try {
				using var storage = PackageApi.StorageManager.OpenStorage(vpePath);
				var tableFolder = storage.GetFolder(PackageApi.TableFolder);

				if (!VpeMaterialReader.TryLoad(tableFolder, loadSourceBytes: false, out var payload, out var entriesOnly)) {
					return null;
				}

				var result = new MaterialPrefetchResult { Payload = payload, Sources = entriesOnly };
				if (VpeTextureCache.TryLoad(cacheRoot, vpePath, cookSettingsHash, out var cached)) {
					result.CachedTextures = cached;
					return result;
				}

				// No cache: load the source bytes for the cook (re-parses the payload; only the
				// first load pays this, and the cook dwarfs it).
				if (VpeMaterialReader.TryLoad(tableFolder, loadSourceBytes: true, out payload, out var sources)) {
					result.Payload = payload;
					result.Sources = sources;
				}
				return result;

			} catch (Exception ex) {
				Logger.Warn(ex, "RuntimePackageReader: failed prefetching material data; falling back to synchronous read.");
				return null;
			}
		}

		private sealed class MaterialPrefetchResult
		{
			public VpeMaterialsPayload Payload;
			public VpeTextureSources.Result Sources;
			public VpeTextureCache.CacheData CachedTextures;
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

		private async Task ReportProgressAndYieldAsync(
			IProgress<RuntimePackageLoadProgress> progress,
			RuntimePackageLoadStage stage,
			string statusPrefix,
			int processed,
			int total,
			CancellationToken cancellationToken)
		{
			ReportProgress(progress, stage, GetProgress01(processed, total), FormatStageMessage(statusPrefix, processed, total));

			if (_yieldStopwatch.ElapsedMilliseconds < RuntimeYieldBudgetMilliseconds) {
				return;
			}

			cancellationToken.ThrowIfCancellationRequested();
			await Task.Yield();
			_yieldStopwatch.Restart();
		}

		private static void ReportProgress(IProgress<RuntimePackageLoadProgress> progress, RuntimePackageLoadStage stage, float stageProgress01, string message)
		{
			if (progress == null) {
				return;
			}

			var (stageStart, stageEnd) = GetStageRange(stage);
			var value01 = Mathf.Lerp(stageStart, stageEnd, Mathf.Clamp01(stageProgress01));
			progress.Report(new RuntimePackageLoadProgress(stage, value01, message));
		}

		private static float GetProgress01(int processed, int total)
		{
			if (total <= 0) {
				return 1f;
			}

			return Mathf.Clamp01(processed / (float)total);
		}

		private static string FormatStageMessage(string statusPrefix, int processed, int total)
		{
			if (total <= 0) {
				return $"{statusPrefix} complete.";
			}

			return $"{statusPrefix} ({processed}/{total})";
		}

		// Tuned to the post-cook load profile: the scene import and the material/texture stage
		// dominate, and on a first load the texture cook reports fine-grained progress within the
		// RestoringMaterials range.
		private static (float Start, float End) GetStageRange(RuntimePackageLoadStage stage)
		{
			return stage switch {
				RuntimePackageLoadStage.OpeningPackage => (0f, 0.04f),
				RuntimePackageLoadStage.ImportingScene => (0.04f, 0.22f),
				RuntimePackageLoadStage.InstantiatingScene => (0.22f, 0.30f),
				RuntimePackageLoadStage.LoadingSounds => (0.30f, 0.33f),
				RuntimePackageLoadStage.LoadingAssets => (0.33f, 0.36f),
				RuntimePackageLoadStage.LoadingColliderMeshes => (0.36f, 0.40f),
				RuntimePackageLoadStage.RestoringPackables => (0.40f, 0.58f),
				RuntimePackageLoadStage.RestoringReferences => (0.58f, 0.63f),
				RuntimePackageLoadStage.RestoringGlobals => (0.63f, 0.66f),
				RuntimePackageLoadStage.RestoringTableMetadata => (0.66f, 0.67f),
				RuntimePackageLoadStage.RestoringMaterials => (0.67f, 0.97f),
				RuntimePackageLoadStage.Finalizing => (0.97f, 1f),
				_ => (0f, 1f),
			};
		}

		private static List<IPackageFolder> GetFolders(IPackageFolder folder)
		{
			var folders = new List<IPackageFolder>();
			folder.VisitFolders(folders.Add);
			return folders;
		}

		private static List<IPackageFile> GetFiles(IPackageFolder folder)
		{
			var files = new List<IPackageFile>();
			folder.VisitFiles(files.Add);
			return files;
		}
	}
}
