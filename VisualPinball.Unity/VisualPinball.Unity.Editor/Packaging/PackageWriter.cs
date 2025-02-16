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
using System.Text;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Export;
using GLTFast.Logging;
using Newtonsoft.Json;
using NLog;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class PackageWriter
	{
		private readonly GameObject _table;
		private readonly PackagedRefs _refs;
		private IPackageFolder _tableFolder;
		private PackagedFiles _files;
		private IPackageFolder _globalFolder;
		private IPackageFolder _metaFolder;

		private const bool ExportActivesOnly = true;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PackageWriter(GameObject table)
		{
			_table = table;
			_refs = new PackagedRefs(table.transform);
		}

		public void WritePackageSync(string path)
		{
			AsyncHelper.RunSync(() => WritePackage(path));
		}

		private async Task WritePackage(string path)
		{
			var sw = new Stopwatch();
			sw.Start();
			if (File.Exists(path)) {
				File.Delete(path);
			}

			Logger.Info($"Writing table to {path}...");
			using var storage = PackageApi.StorageManager.CreateStorage(path);

			_tableFolder = storage.AddFolder(PackageApi.TableFolder);
			_globalFolder = _tableFolder.AddFolder(PackageApi.GlobalFolder);
			_metaFolder = _tableFolder.AddFolder(PackageApi.MetaFolder);
			_files = new PackagedFiles(_tableFolder, _refs);

			// write scene data
			var sw1 = Stopwatch.StartNew();
			await WriteScene();
			Logger.Info($"Scene written in {sw1.ElapsedMilliseconds}ms.");

			// write non-scene meshes
			sw1 = Stopwatch.StartNew();
			await WriteColliderMeshes();
			Logger.Info($"Collider meshes written in {sw1.ElapsedMilliseconds}ms.");

			// write component data
			sw1 = Stopwatch.StartNew();
			WritePackables(
				PackageApi.ItemFolder,
				packageable => packageable.Pack(),
				go => ItemPackable.Instantiate(go).Pack(),
				PackNativeComponent);
			Logger.Info($"Component data written in {sw1.ElapsedMilliseconds}ms.");

			// write reference data
			sw1 = Stopwatch.StartNew();
			WritePackables(PackageApi.ItemReferencesFolder, packageable => packageable.PackReferences(_table.transform, _refs, _files));
			Logger.Info($"References written in {sw1.ElapsedMilliseconds}ms.");

			// write globals
			sw1 = Stopwatch.StartNew();
			WriteGlobals();
			Logger.Info($"Globals written in {sw1.ElapsedMilliseconds}ms.");

			// write assets & co
			sw1 = Stopwatch.StartNew();
			_files.PackAssets();
			_files.PackSoundMetas();
			Logger.Info($"Assets and files written in {sw1.ElapsedMilliseconds}ms.");

			storage.Close();
			sw.Stop();
			Debug.Log($"Done! File saved to {path} in {sw.ElapsedMilliseconds}ms.");
		}

		private async Task WriteScene()
		{
			// make table meshes readable
			var meshFilters = _table.GetComponentsInChildren<MeshFilter>(!ExportActivesOnly);
			var skinnedMeshRenderers = _table.GetComponentsInChildren<SkinnedMeshRenderer>(!ExportActivesOnly);
			SetMeshesReadable(meshFilters, skinnedMeshRenderers);

			var glbFile = _tableFolder.AddFile(PackageApi.SceneFile);
			var logger = new ConsoleLogger();

			#region glTF Settings

			var exportSettings = new ExportSettings {

				// Format = GltfFormat.Json,
				Format = GltfFormat.Binary,
				FileConflictResolution = FileConflictResolution.Abort,
				Deterministic = false,

				// Export everything except cameras or animation
				ComponentMask = ~(ComponentType.Camera | ComponentType.Animation),

				// Boost light intensities
				LightIntensityFactor = 100f,

				// Ensure mesh vertex attributes colors and texture coordinate (channels 1 through 8) are always
				// exported, even if they are not used/referenced.
				PreservedVertexAttributes = VertexAttributeUsage.None,

				// Enable Draco compression
				Compression = Compression.Uncompressed,

				// Optional: Tweak the Draco compression settings
				// DracoSettings = new DracoExportSettings {
				// 	positionQuantization = 12,
				// },

				JpgQuality = 90,

				ImageDestination = ImageDestination.Automatic,
			};

			var gameObjectExportSettings = new GameObjectExportSettings {

				// Include inactive GameObjects in export
				OnlyActiveInHierarchy = ExportActivesOnly,

				// Also export disabled components
				DisabledComponents = false

				// Only export GameObjects on certain layers
				//LayerMask = LayerMask.GetMask("Default", "MyCustomLayer"),
			};

			#endregion

			var export = new GameObjectExport(exportSettings, gameObjectExportSettings, logger: logger);
			export.AddScene(new [] { _table }, _table.transform.worldToLocalMatrix, "VPE Table");

			await export.SaveToStreamAndDispose(glbFile.AsStream());
		}

		private async Task WriteColliderMeshes()
		{
			var meshGos = new List<GameObject>();
			var colliderMeshesMeta = new Dictionary<string, ColliderMeshMetaPackable>();
			try {
				foreach (var colMesh in _table.GetComponentsInChildren<IColliderMesh>(!ExportActivesOnly)) {
					var mesh = colMesh.GetColliderMesh();
					if (!mesh) {
						continue;
					}
					var guid = Guid.NewGuid().ToString();
					var meshGo = new GameObject(guid);
					var meshFilter = meshGo.AddComponent<MeshFilter>();
					meshGo.AddComponent<MeshRenderer>();
					meshFilter.sharedMesh = mesh;
					meshGos.Add(meshGo);
					colliderMeshesMeta.Add(guid, ColliderMeshMetaPackable.Instantiate(colMesh));
					_files.ColliderMeshInstanceIdToGuid.Add((colMesh as Component)!.GetInstanceID(), guid);
				}

				if (meshGos.Count > 0) {
					Logger.Info($"Found {meshGos.Count} collider meshes.");
					var glbFile = _tableFolder.AddFile(PackageApi.ColliderMeshesFile);
					var logger = new ConsoleLogger();
					var exportSettings = new ExportSettings {
						Format = GltfFormat.Binary,
					};
					var export = new GameObjectExport(exportSettings, logger: logger);
					export.AddScene(meshGos.ToArray(), _table.transform.worldToLocalMatrix, "Colliders");
					await export.SaveToStreamAndDispose(glbFile.AsStream());

					var glbMeta = _metaFolder.AddFile(PackageApi.ColliderMeshesMeta, PackageApi.Packer.FileExtension);
					glbMeta.SetData(PackageApi.Packer.Pack(colliderMeshesMeta));
				}

			} finally {

				// cleanup scene
				foreach (var meshGo in meshGos) {
					Object.DestroyImmediate(meshGo);
				}
			}
		}

		/// <summary>
		/// Walks through the entire game object tree and creates the same structure for
		/// a given storage name, for each IPackageable component.
		/// </summary>
		/// <param name="folderName">Name of the storage within table storage</param>
		/// <param name="getPackableData">Retrieves component-specific data.</param>
		/// <param name="getItemData">Retrieves item-specific data.</param>
		private void WritePackables(string folderName, Func<IPackable, byte[]> getPackableData, Func<GameObject, byte[]> getItemData = null, Func<Component, byte[]> getNativeData = null)
		{
			// -> rootName <- / 0.0.0 / CompType / 0
			var folder = _tableFolder.AddFolder(folderName);

			// walk the entire tree
			foreach (var t in _table.transform.GetComponentsInChildren<Transform>(!ExportActivesOnly)) {

				// for each game object, loop through all components
				var key = t.GetPath(_table.transform);
				var counters = new Dictionary<string, int>();
				var itemData = getItemData?.Invoke(t.gameObject);

				// rootName / -> 0.0.0 <- / CompType / 0
				IPackageFolder itemPathFolder = null;
				if (itemData?.Length > 0) {
					itemPathFolder = folder.AddFolder(key);

					var itemFile = itemPathFolder.AddFile(PackageApi.ItemFile, PackageApi.Packer.FileExtension);
					itemFile.SetData(itemData);
				}

				foreach (var component in t.gameObject.GetComponents<Component>()) {
					switch (component) {
						case IPackable packageable: {

							var packName = _refs.GetName(packageable.GetType());
							counters.TryAdd(packName, 0);

							var packableData = getPackableData(packageable);
							if (packableData?.Length > 0) {

								// rootName / -> 0.0.0 <- / CompType / 0
								itemPathFolder ??= folder.AddFolder(key);

								// rootName / 0.0.0 / -> CompType <- / 0
								if (!itemPathFolder.TryGetFolder(packName, out var itemComponentFolder)) {
									itemComponentFolder = itemPathFolder.AddFolder(packName);
								}

								// rootName / 0.0.0 / CompType / -> 0 <-
								var itemComponentFile = itemComponentFolder.AddFile($"{counters[packName]++}", PackageApi.Packer.FileExtension);
								itemComponentFile.SetData(packableData);
							}
							break;
						}

						// those are covered by the glTF export
						case Transform:
						case MeshFilter:
						case MeshRenderer:
						case Light:
						case SkinnedMeshRenderer:
							break;

						default:
							getNativeData?.Invoke(component);
							break;
					}
				}
			}
		}

		private byte[] PackNativeComponent(Component comp)
		{
			if (_refs.HasType(comp.GetType())) {

				try {
					// todo abstract this
					return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(comp, Formatting.Indented, new JsonSerializerSettings {
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}));
				} catch (Exception e) {
					Debug.LogError(e);
					return null;
				}
			}

			Debug.LogWarning($"Unknown component {comp.GetType()} ({comp.name})");
			return null;
		}


		private void WriteGlobals()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}

			foreach (var sw in tableComponent.MappingConfig.Switches) {
				sw.SaveReference(_table.transform);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.SaveReference(_table.transform);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.SaveReferences(_table.transform);
			}
			foreach (var lp in tableComponent.MappingConfig.Lamps) {
				lp.SaveReference(_table.transform);
			}

			_globalFolder.AddFile(PackageApi.SwitchesFile, PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(tableComponent.MappingConfig.Switches));
			_globalFolder.AddFile(PackageApi.CoilsFile, PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(tableComponent.MappingConfig.Coils));
			_globalFolder.AddFile(PackageApi.WiresFile, PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(tableComponent.MappingConfig.Wires));
			_globalFolder.AddFile(PackageApi.LampsFile, PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(tableComponent.MappingConfig.Lamps));
		}

		private static void SetMeshesReadable(MeshFilter[] meshFilters, SkinnedMeshRenderer[] skinnedMeshRenderers)
		{
			// Keep track of which assets we've changed to avoid re-importing multiple times
			var changedAssets = new HashSet<string>();

			foreach (var mf in meshFilters) {
				if (mf.sharedMesh != null) {
					MakeModelReadable(mf.sharedMesh, changedAssets);
				}
			}
			foreach (var smr in skinnedMeshRenderers) {
				if (smr.sharedMesh != null) {
					MakeModelReadable(smr.sharedMesh, changedAssets);
				}
			}
		}

		private static void MakeModelReadable(Mesh mesh, HashSet<string> changedAssets)
		{
			var assetPath = AssetDatabase.GetAssetPath(mesh);
			if (string.IsNullOrEmpty(assetPath)) {
				// This can happen if it's a dynamically created mesh at runtime, so just skip it
				return;
			}

			var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
			if (modelImporter == null) {
				// It may not be a model (could be a .asset file or something else)
				// If it's a .asset that you created manually, you might need a different approach.
				// For typical .fbx or .obj models, this cast should succeed.
				return;
			}

			if (!modelImporter.isReadable) {
				Debug.Log($"Enabling Read/Write for: {assetPath}");
				modelImporter.isReadable = true;
				modelImporter.SaveAndReimport(); // Force re-import
				changedAssets.Add(assetPath);
			}
		}
	}
}
