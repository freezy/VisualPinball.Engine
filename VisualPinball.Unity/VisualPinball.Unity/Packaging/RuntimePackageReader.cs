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
		private const int RuntimeYieldInterval = 16;

		private readonly string _vpePath;
		private GameObject _table;
		private IPackageFolder _tableFolder;
		private PackagedRefs _refs;
		private PackagedFiles _files;
		private Dictionary<string, int[]> _sparsePathIndexMap;
		private VpeMaterialsPayloadV1 _embeddedMaterialPayload;
		private IReadOnlyDictionary<string, byte[]> _embeddedMaterialTextureBlobs;

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
			ReportProgress(progress, RuntimePackageLoadStage.OpeningPackage, 0f, $"Opening {Path.GetFileName(_vpePath)}...");
			using var storage = PackageApi.StorageManager.OpenStorage(_vpePath);
			_tableFolder = storage.GetFolder(PackageApi.TableFolder);
			_sparsePathIndexMap = BuildSparsePathIndexMap(PackageApi.ItemFolder, PackageApi.ItemReferencesFolder);
			ReportProgress(progress, RuntimePackageLoadStage.OpeningPackage, 1f, "Package opened.");

			var previousSparsePathIndexMap = TransformExtensions.SparsePathIndexMap;
			TransformExtensions.SparsePathIndexMap = _sparsePathIndexMap;

			try {
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
						_refs = new PackagedRefs(_table.transform);
						_files = new PackagedFiles(_tableFolder, _refs);

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
						RestoreMaterialProfiles();
						ReportProgress(progress, RuntimePackageLoadStage.RestoringMaterials, 1f, "Material profiles applied.");
						materialsStopwatch.Stop();
						Logger.Info($"RuntimePackageReader: Restored material profiles in {materialsStopwatch.ElapsedMilliseconds}ms.");
						loadSucceeded = true;

					} finally {
						if (loadSucceeded && _table) {
							_table.SetActive(restoreActive);
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

			} finally {
				TransformExtensions.SparsePathIndexMap = previousSparsePathIndexMap;
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

			_embeddedMaterialPayload = null;
			_embeddedMaterialTextureBlobs = null;
			if (VpeMaterialsGltfExtension.TryReadPayload(sceneData, out var embeddedMaterialPayload)) {
				_embeddedMaterialPayload = embeddedMaterialPayload;
				VpeMaterialsGltfExtension.TryReadEmbeddedTextureBlobs(
					sceneData,
					embeddedMaterialPayload,
					out _embeddedMaterialTextureBlobs);
			}

			var importRoot = new GameObject("__vpe_runtime_import");
			importRoot.hideFlags = HideFlags.HideAndDontSave;
			if (parent != null) {
				importRoot.transform.SetParent(parent, false);
			}
			var meshesWithoutTangents = CollectMeshesWithoutGltfTangents(sceneData);

			try {
				var gltf = new GltfImport(logger: new ConsoleLogger());
				var uri = new Uri(Path.GetFullPath(_vpePath));
				ReportProgress(progress, RuntimePackageLoadStage.ImportingScene, 0f, "Reading table scene...");
				var loaded = await gltf.Load(sceneData, uri, cancellationToken: cancellationToken);
				ReportProgress(progress, RuntimePackageLoadStage.ImportingScene, 1f, "Scene data imported.");
				cancellationToken.ThrowIfCancellationRequested();
				if (!loaded) {
					throw new Exception("Failed loading table.glb from package.");
				}

				ReportProgress(progress, RuntimePackageLoadStage.InstantiatingScene, 0f, "Building scene hierarchy...");
				var instantiated = await gltf.InstantiateMainSceneAsync(importRoot.transform, cancellationToken);
				ReportProgress(progress, RuntimePackageLoadStage.InstantiatingScene, 1f, "Scene hierarchy ready.");
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
				ClearGeneratedTangents(table, meshesWithoutTangents);
				NormalizeImportedLightIntensities(table);
				RestoreLightProfiles(table);

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

		private void RestoreLightProfiles(GameObject table)
		{
			if (!table || !_tableFolder.TryGetFolder(PackageApi.MetaFolder, out var metaFolder)) {
				return;
			}
			if (!metaFolder.TryGetFile(PackageApi.LightsV1File, out var payloadFile, PackageApi.Packer.FileExtension)) {
				return;
			}

			var payload = PackageApi.Packer.Unpack<VpeLightsPayloadV1>(payloadFile.GetData());
			if (payload.Lights == null || payload.Lights.Count == 0) {
				return;
			}

			var matchedLights = new HashSet<int>();
			var lights = table.GetComponentsInChildren<Light>(true);
			for (var i = 0; i < payload.Lights.Count; i++) {
				var profile = payload.Lights[i];
				var target = ResolveLightProfileTarget(table.transform, profile.Path, lights, i, matchedLights);
				if (!target) {
					continue;
				}

				profile.Apply(target);
				matchedLights.Add(target.GetInstanceID());
			}
		}

		private static Light ResolveLightProfileTarget(
			Transform tableRoot,
			string path,
			IReadOnlyList<Light> lights,
			int profileIndex,
			ISet<int> matchedLights)
		{
			var transform = tableRoot.FindByPath(path);
			if (transform) {
				var exact = transform.GetComponent<Light>();
				if (exact) {
					return exact;
				}

				var descendant = transform.GetComponentsInChildren<Light>(true)
					.FirstOrDefault(light => !matchedLights.Contains(light.GetInstanceID()));
				if (descendant) {
					return descendant;
				}
			}

			return profileIndex >= 0 && profileIndex < lights.Count ? lights[profileIndex] : null;
		}

		private static void NormalizeImportedLightIntensities(GameObject table)
		{
			if (!table || PackageApi.LightIntensityFactor <= 0f || Mathf.Approximately(PackageApi.LightIntensityFactor, 1f)) {
				return;
			}

			foreach (var light in table.GetComponentsInChildren<Light>(true)) {
				var originalIntensity = light.intensity;
				light.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
				light.intensity = originalIntensity / PackageApi.LightIntensityFactor;
				NormalizeHdrpLightIntensity(light, originalIntensity);
			}
		}

		private static void NormalizeHdrpLightIntensity(Light light, float originalUnityIntensity)
		{
			foreach (var component in light.GetComponents<Component>()) {
				if (component == null || component.GetType().FullName != "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData") {
					continue;
				}

				var intensityProperty = component.GetType().GetProperty("intensity");
				if (intensityProperty == null || !intensityProperty.CanRead || !intensityProperty.CanWrite) {
					continue;
				}

				var value = intensityProperty.GetValue(component);
				if (value is not float hdIntensity) {
					continue;
				}

				// HDRP usually mirrors Light.intensity. Only compensate separately if it still contains the
				// boosted glTF import value, otherwise setting Light.intensity above already normalized it.
				var tolerance = Mathf.Max(0.001f, Mathf.Abs(originalUnityIntensity) * 0.001f);
				if (Mathf.Abs(hdIntensity - originalUnityIntensity) <= tolerance) {
					intensityProperty.SetValue(component, hdIntensity / PackageApi.LightIntensityFactor);
				}
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
		private async Task ReadPackablesAsync(
			string rootFolder,
			RuntimePackageLoadStage stage,
			string statusPrefix,
			Action<GameObject, IPackageFile> itemAction,
			Action<Transform, Type, IPackageFile, int> componentAction,
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

				var item = _table.transform.FindByPath(itemFolder.Name);
				if (item == null) {
					throw new Exception($"Cannot find item at path {itemFolder.Name} on node {_table.name}. Imported hierarchy does not match packaged item paths.");
				}

				if (itemAction != null && itemFolder.TryGetFile(PackageApi.ItemFile, out var itemFile, PackageApi.Packer.FileExtension)) {
					itemAction(item.gameObject, itemFile);
					processedOperations++;
					await ReportProgressAndYieldAsync(progress, stage, statusPrefix, processedOperations, totalOperations, cancellationToken);
				}

				foreach (var typeFolder in GetFolders(itemFolder)) {
					var type = _refs.GetType(typeFolder.Name);
					if (type == null) {
						throw new Exception($"Unknown component type '{typeFolder.Name}' while reading package.");
					}

					var index = 0;
					foreach (var compFile in GetFiles(typeFolder)) {
						componentAction(item, type, compFile, index++);
						processedOperations++;
						await ReportProgressAndYieldAsync(progress, stage, statusPrefix, processedOperations, totalOperations, cancellationToken);
					}
				}
			}

			ReportProgress(progress, stage, 1f, $"{statusPrefix} complete.");
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
				sw.RestoreReference(_table.transform);
				processedMappings++;
				await ReportProgressAndYieldAsync(progress, RuntimePackageLoadStage.RestoringGlobals, "Restoring table wiring",
					processedMappings, totalMappings, cancellationToken);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.RestoreReference(_table.transform);
				processedMappings++;
				await ReportProgressAndYieldAsync(progress, RuntimePackageLoadStage.RestoringGlobals, "Restoring table wiring",
					processedMappings, totalMappings, cancellationToken);
			}
			foreach (var lamp in tableComponent.MappingConfig.Lamps) {
				lamp.RestoreReference(_table.transform);
				processedMappings++;
				await ReportProgressAndYieldAsync(progress, RuntimePackageLoadStage.RestoringGlobals, "Restoring table wiring",
					processedMappings, totalMappings, cancellationToken);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.RestoreReferences(_table.transform);
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

		private void RestoreMaterialProfiles()
		{
			_tableFolder.TryGetFolder(PackageApi.MetaFolder, out var metaFolder);

			if (_embeddedMaterialPayload != null &&
				VpeMaterialV1Reader.TryApply(_embeddedMaterialPayload, _embeddedMaterialTextureBlobs, metaFolder, _table.transform,
					$"{PackageApi.SceneFile}/{VpeMaterialsGltfExtension.ExtensionName}")) {
				return;
			}

			if (metaFolder == null) {
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

		private static async Task ReportProgressAndYieldAsync(
			IProgress<RuntimePackageLoadProgress> progress,
			RuntimePackageLoadStage stage,
			string statusPrefix,
			int processed,
			int total,
			CancellationToken cancellationToken)
		{
			ReportProgress(progress, stage, GetProgress01(processed, total), FormatStageMessage(statusPrefix, processed, total));

			if (processed % RuntimeYieldInterval != 0) {
				return;
			}

			cancellationToken.ThrowIfCancellationRequested();
			await Task.Yield();
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

		private static (float Start, float End) GetStageRange(RuntimePackageLoadStage stage)
		{
			return stage switch {
				RuntimePackageLoadStage.OpeningPackage => (0f, 0.02f),
				RuntimePackageLoadStage.ImportingScene => (0.02f, 0.22f),
				RuntimePackageLoadStage.InstantiatingScene => (0.22f, 0.42f),
				RuntimePackageLoadStage.LoadingSounds => (0.42f, 0.54f),
				RuntimePackageLoadStage.LoadingAssets => (0.54f, 0.62f),
				RuntimePackageLoadStage.LoadingColliderMeshes => (0.62f, 0.72f),
				RuntimePackageLoadStage.RestoringPackables => (0.72f, 0.88f),
				RuntimePackageLoadStage.RestoringReferences => (0.88f, 0.96f),
				RuntimePackageLoadStage.RestoringGlobals => (0.96f, 0.985f),
				RuntimePackageLoadStage.RestoringTableMetadata => (0.985f, 0.992f),
				RuntimePackageLoadStage.RestoringMaterials => (0.992f, 0.998f),
				RuntimePackageLoadStage.Finalizing => (0.998f, 1f),
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
