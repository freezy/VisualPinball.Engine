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
using System.Text;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Export;
using GLTFast.Logging;
using Newtonsoft.Json;
using NLog;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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
		private VpeMaterialsPayloadV1 _capturedMaterialPayload;
		private IReadOnlyDictionary<string, byte[]> _capturedMaterialTextureBlobs;
		private bool _embeddedMaterialPayloadSuccessfully;

		private const bool ExportActivesOnly = true;
		private const bool EmbedVpeMaterialsIntoGlb = false;

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

			// prepare scene data
			var sw1 = Stopwatch.StartNew();
			var saveScene = PrepareScene();
			Logger.Info($"Scene prepared in {sw1.ElapsedMilliseconds}ms.");

			// prepare non-scene meshes
			sw1 = Stopwatch.StartNew();
			var saveColliderMeshes = PrepareColliderMeshes();
			Logger.Info($"Collider meshes prepared in {sw1.ElapsedMilliseconds}ms.");

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
			WriteTableMetadata();
			WriteGlobals();
			WriteMaterialProfiles();
			Logger.Info($"Globals written in {sw1.ElapsedMilliseconds}ms.");

			// write assets & co
			sw1 = Stopwatch.StartNew();
			_files.PackAssets();
			_files.PackSoundMetas();
			Logger.Info($"Assets and files written in {sw1.ElapsedMilliseconds}ms.");

			// glTFast still reads Unity mesh data at the start of SaveToStreamAndDispose. Start both saves while
			// we're still on the main thread, but write into memory first because the package zip stream only
			// supports one active file entry at a time.
			sw1 = Stopwatch.StartNew();
			var saveSceneTask = saveScene();
			var saveColliderMeshesTask = saveColliderMeshes?.Invoke();

			var sceneData = await saveSceneTask;
			WritePackageFile(_tableFolder, PackageApi.SceneFile, sceneData);
			Logger.Info($"Scene written in {sw1.ElapsedMilliseconds}ms ({sceneData.Length} bytes).");
			WriteMaterialPayloadFallbackIfNeeded();

			if (saveColliderMeshesTask != null) {
				sw1 = Stopwatch.StartNew();
				var colliderMeshesData = await saveColliderMeshesTask;
				WritePackageFile(_tableFolder, PackageApi.ColliderMeshesFile, colliderMeshesData);
				Logger.Info($"Collider meshes written in {sw1.ElapsedMilliseconds}ms ({colliderMeshesData.Length} bytes).");
			}

			storage.Close();
			sw.Stop();
			Debug.Log($"Done! File saved to {path} in {sw.ElapsedMilliseconds}ms.");
		}

		private Func<Task<byte[]>> PrepareScene()
		{
			// make table meshes readable
			var meshFilters = _table.GetComponentsInChildren<MeshFilter>(!ExportActivesOnly);
			var skinnedMeshRenderers = _table.GetComponentsInChildren<SkinnedMeshRenderer>(!ExportActivesOnly);
			SetMeshesReadable(meshFilters, skinnedMeshRenderers);

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

				// Keep disabled components out of the export. Insert/lamp Light components are
				// typically disabled at author time (LightComponent.Awake flips them on when a
				// lamp fires); we temporarily re-enable them below so they flow into the glb,
				// then restore. Flipping DisabledComponents=true wholesale would also drag in
				// disabled MeshRenderers with degenerate meshes that crash gltFast.
				DisabledComponents = false

				// Only export GameObjects on certain layers
				//LayerMask = LayerMask.GetMask("Default", "MyCustomLayer"),
			};

			#endregion

			var export = new GameObjectExport(exportSettings, gameObjectExportSettings, logger: logger);
			var disabledRenderers = DisableInvalidMeshRenderers(meshFilters, skinnedMeshRenderers, _table.transform);
			var reenabledLights = EnableDisabledLights();
			var renderers = _table.GetComponentsInChildren<Renderer>(!ExportActivesOnly);
			var gltfExportScope = VpeMaterialV1Translator.PrepareGltfExport(renderers);
			try {
				export.AddScene(new [] { _table }, _table.transform.worldToLocalMatrix, "VPE Table");

			} finally {
				gltfExportScope?.Dispose();
				RestoreDisabledLights(reenabledLights);
				RestoreDisabledRenderers(disabledRenderers);
			}

			return () => SaveGltfToBytes(export, embedMaterialPayload: EmbedVpeMaterialsIntoGlb);
		}

		// Temporarily re-enables Light components that are disabled at author time so gltFast
		// emits them into the KHR_lights_punctual extension. Inserts and flasher bulbs are the
		// main targets: VPE's LightComponent disables the Unity Light by default and only
		// enables it while a lamp is actively firing at runtime.
		private List<Light> EnableDisabledLights()
		{
			var toRestore = new List<Light>();
			foreach (var light in _table.GetComponentsInChildren<Light>(!ExportActivesOnly)) {
				if (!light.enabled) {
					light.enabled = true;
					toRestore.Add(light);
				}
			}
			return toRestore;
		}

		private static void RestoreDisabledLights(List<Light> reenabledLights)
		{
			foreach (var light in reenabledLights) {
				if (light) {
					light.enabled = false;
				}
			}
		}

		private Func<Task<byte[]>> PrepareColliderMeshes()
		{
			var meshGos = new List<GameObject>();
			var colliderMeshesMeta = new Dictionary<string, ColliderMeshMetaPackable>();
			GameObjectExport export = null;
			try {
				foreach (var colMesh in _table.GetComponentsInChildren<IColliderMesh>(!ExportActivesOnly)) {
					for (var index = 0; index < colMesh.NumColliderMeshes; index++) {
						var mesh = colMesh.GetColliderMesh(index);
						if (!mesh) {
							continue;
						}
						if (IsInvalidMeshForGltfExport(mesh, out var reason)) {
							var path = ((Component)colMesh).transform.GetPath(_table.transform);
							Logger.Warn($"Skipping collider mesh '{mesh.name}' for '{path}' during package export because {reason}.");
							Debug.LogWarning($"Skipping collider mesh '{mesh.name}' for '{path}' during package export because {reason}.", (Object)colMesh);
							continue;
						}
						var guid = Guid.NewGuid().ToString();
						var meshGo = new GameObject($"{guid}-{index}");
						var meshFilter = meshGo.AddComponent<MeshFilter>();
						meshGo.AddComponent<MeshRenderer>();
						meshFilter.sharedMesh = mesh;
						meshGos.Add(meshGo);
						colliderMeshesMeta.Add(guid, ColliderMeshMetaPackable.Instantiate(colMesh, index));
						_files.AddColliderMeshGuid(colMesh, guid, index);
					}
				}

				if (meshGos.Count > 0) {
					Logger.Info($"Found {meshGos.Count} collider meshes.");
					var logger = new ConsoleLogger();
					var exportSettings = new ExportSettings {
						Format = GltfFormat.Binary,
					};
					export = new GameObjectExport(exportSettings, logger: logger);
					export.AddScene(meshGos.ToArray(), _table.transform.worldToLocalMatrix, "Colliders");

					var glbMeta = _metaFolder.AddFile(PackageApi.ColliderMeshesMeta, PackageApi.Packer.FileExtension);
					glbMeta.SetData(PackageApi.Packer.Pack(colliderMeshesMeta));
				}

			} finally {

				// cleanup scene
				foreach (var meshGo in meshGos) {
					Object.DestroyImmediate(meshGo);
				}
			}

			return export == null ? null : () => SaveGltfToBytes(export);
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
				var key = GetPackagePath(t, _table.transform, ExportActivesOnly);
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
						case null:
							Debug.LogWarning($"Skipping missing component on {key} during package export.", t.gameObject);
							break;

						case IPackable packageable: {

							var packName = _refs.GetName(packageable.GetType());
							counters.TryAdd(packName, 0);

							var packableData = getPackableData(packageable);
							var shouldWriteEmptyMarker = folderName == PackageApi.ItemFolder && (packableData == null || packableData.Length == 0);
							if (packableData?.Length > 0 || shouldWriteEmptyMarker) {

								// rootName / -> 0.0.0 <- / CompType / 0
								itemPathFolder ??= folder.AddFolder(key);

								// rootName / 0.0.0 / -> CompType <- / 0
								if (!itemPathFolder.TryGetFolder(packName, out var itemComponentFolder)) {
									itemComponentFolder = itemPathFolder.AddFolder(packName);
								}

								// rootName / 0.0.0 / CompType / -> 0 <-
								var itemComponentFile = itemComponentFolder.AddFile($"{counters[packName]++}", PackageApi.Packer.FileExtension);
								itemComponentFile.SetData(packableData?.Length > 0 ? packableData : PackageApi.Packer.Empty);
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
			if (!comp) {
				return null;
			}

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

		private static string GetPackagePath(Transform transform, Transform root, bool activeOnly)
		{
			if (!transform) {
				return "0";
			}

			if (transform == root) {
				return "0";
			}

			var path = string.Empty;
			var current = transform;
			while (current != null && current != root) {
				var siblingIndex = GetPackageSiblingIndex(current, activeOnly);
				path = string.IsNullOrEmpty(path) ? $"{siblingIndex}" : $"{siblingIndex}.{path}";
				current = current.parent;
			}

			return string.IsNullOrEmpty(path) ? "0" : $"0.{path}";
		}

		private static int GetPackageSiblingIndex(Transform transform, bool activeOnly)
		{
			if (!activeOnly || transform.parent == null) {
				return transform.GetSiblingIndex();
			}

			var parent = transform.parent;
			var activeSiblingIndex = 0;
			for (var childIndex = 0; childIndex < parent.childCount; childIndex++) {
				var sibling = parent.GetChild(childIndex);
				if (!sibling.gameObject.activeInHierarchy) {
					continue;
				}

				if (sibling == transform) {
					return activeSiblingIndex;
				}

				activeSiblingIndex++;
			}

			return transform.GetSiblingIndex();
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

		private void WriteTableMetadata()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}

			tableComponent.Metadata ??= new TableMetadata();
			_tableFolder.AddFile(PackageApi.TableMetadataFile, PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(tableComponent.Metadata));
		}

		private void WriteMaterialProfiles()
		{
			var renderers = _table.GetComponentsInChildren<Renderer>(!ExportActivesOnly);

			var capture = VpeMaterialV1Translator.Capture(_table.transform, renderers);
			var payload = capture.Payload;
			_capturedMaterialPayload = null;
			_capturedMaterialTextureBlobs = null;
			_embeddedMaterialPayloadSuccessfully = false;
			if (payload?.Profiles == null || payload.Profiles.Length == 0) {
				if (VpeMaterialV1Translator.Active == null) {
					Logger.Info("Skipping materials.v1 export: no IVpeMaterialV1Translator is registered.");
				}
				return;
			}
			_capturedMaterialPayload = payload;
			_capturedMaterialTextureBlobs = capture.TextureBlobs;

			var textureCount = 0;
			var textureBytes = 0L;
			if (capture.TextureBlobs != null && capture.TextureBlobs.Count > 0) {
				foreach (var entry in capture.TextureBlobs) {
					if (entry.Value == null || entry.Value.Length == 0) {
						continue;
					}
					textureCount++;
					textureBytes += entry.Value.Length;
				}
			}
			Logger.Info(
				$"Captured vpe.material v1 payload: {payload.Profiles.Length} profile(s), " +
				$"{textureCount} VPE-only texture(s) ({textureBytes / 1024f / 1024f:F2} MB).");
		}

		private async Task<byte[]> SaveGltfToBytes(GameObjectExport export, bool embedMaterialPayload = false)
		{
			using var stream = new MemoryStream();
			await export.SaveToStreamAndDispose(stream);
			var originalGlbData = stream.ToArray();
			var glbData = originalGlbData;
			if (!embedMaterialPayload || _capturedMaterialPayload == null) {
				return glbData;
			}

			var payload = _capturedMaterialPayload;
			try {
				var candidateGlbData = VpeMaterialsGltfExtension.WritePayload(glbData, payload, _capturedMaterialTextureBlobs);
				if (!VpeMaterialsGltfExtension.TryReadPayload(candidateGlbData, out var roundTrippedPayload)
					|| roundTrippedPayload?.Profiles == null
					|| roundTrippedPayload.Profiles.Length != (payload.Profiles?.Length ?? 0)) {
					throw new InvalidOperationException(
						$"Failed validating embedded {VpeMaterialsGltfExtension.ExtensionName} payload after GLB rewrite.");
				}

				var expectedEmbeddedTextureCount = payload.Textures?.Count(asset => asset != null && asset.GlbBufferView >= 0) ?? 0;
				if (expectedEmbeddedTextureCount > 0
					&& !VpeMaterialsGltfExtension.TryReadEmbeddedTextureBlobs(
						candidateGlbData,
						roundTrippedPayload,
						out var roundTrippedTextureBlobsById)) {
					throw new InvalidOperationException(
						$"Failed validating embedded {VpeMaterialsGltfExtension.ExtensionName} texture blobs after GLB rewrite.");
				}

				glbData = candidateGlbData;
				_embeddedMaterialPayloadSuccessfully = true;
				Logger.Info(
					$"Embedded {VpeMaterialsGltfExtension.ExtensionName} in {PackageApi.SceneFile}: " +
					$"{payload.Profiles?.Length ?? 0} profile(s), {payload.Textures?.Length ?? 0} texture reference(s).");
			} catch (Exception ex) {
				_embeddedMaterialPayloadSuccessfully = false;
				glbData = originalGlbData;
				Logger.Warn(
					$"Failed embedding {VpeMaterialsGltfExtension.ExtensionName} in {PackageApi.SceneFile} " +
					$"({ex.Message}). Writing plain GLB and keeping sidecar material payload.");
			}
			return glbData;
		}

		private void WriteMaterialPayloadFallbackIfNeeded()
		{
			if (_capturedMaterialPayload == null || _embeddedMaterialPayloadSuccessfully) {
				return;
			}

			var packedTextureData = BuildPackedMaterialTextureData(
				_capturedMaterialPayload,
				_capturedMaterialTextureBlobs,
				out var textureCount,
				out var textureBytes);
			if (packedTextureData != null && packedTextureData.Length > 0) {
				_metaFolder.AddFile(PackageApi.TexturesV1PackFile).SetData(packedTextureData);
			}

			_metaFolder
				.AddFile(PackageApi.MaterialsV1File, PackageApi.Packer.FileExtension)
				.SetData(PackageApi.Packer.Pack(_capturedMaterialPayload));

			Logger.Warn(
				$"Fell back to sidecar vpe.material export: profiles={_capturedMaterialPayload.Profiles?.Length ?? 0}, " +
				$"textures={textureCount} ({textureBytes / 1024f / 1024f:F2} MB) at " +
				$"{PackageApi.MetaFolder}/{PackageApi.TexturesV1PackFile}.");
		}

		private static byte[] BuildPackedMaterialTextureData(
			VpeMaterialsPayloadV1 payload,
			IReadOnlyDictionary<string, byte[]> textureBlobsByFileName,
			out int textureCount,
			out long textureBytes)
		{
			textureCount = 0;
			textureBytes = 0L;
			if (payload?.Textures == null || payload.Textures.Length == 0) {
				return null;
			}

			foreach (var asset in payload.Textures) {
				if (asset == null) {
					continue;
				}
				asset.ByteOffset = -1;
				asset.ByteLength = 0;
			}

			if (textureBlobsByFileName == null || textureBlobsByFileName.Count == 0) {
				return null;
			}

			using var stream = new MemoryStream();
			foreach (var asset in payload.Textures) {
				if (asset == null
					|| string.IsNullOrWhiteSpace(asset.FileName)
					|| !textureBlobsByFileName.TryGetValue(asset.FileName, out var blobData)
					|| blobData == null
					|| blobData.Length == 0) {
					continue;
				}

				asset.ByteOffset = checked((int)stream.Position);
				asset.ByteLength = blobData.Length;
				stream.Write(blobData, 0, blobData.Length);
				textureCount++;
				textureBytes += blobData.LongLength;
			}

			return stream.Length > 0 ? stream.ToArray() : null;
		}

		private static void WritePackageFile(IPackageFolder folder, string fileName, byte[] data)
		{
			if (data == null || data.Length == 0) {
				throw new InvalidOperationException($"Cannot write empty package file '{fileName}'.");
			}

			folder.AddFile(fileName).SetData(data);
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

		private static List<Renderer> DisableInvalidMeshRenderers(MeshFilter[] meshFilters, SkinnedMeshRenderer[] skinnedMeshRenderers, Transform root)
		{
			var disabledRenderers = new List<Renderer>();

			foreach (var mf in meshFilters) {
				var renderer = mf.GetComponent<Renderer>();
				if (renderer && renderer.enabled && IsInvalidMeshForGltfExport(mf.sharedMesh, out var reason)) {
					DisableRendererForExport(renderer, mf.sharedMesh, root, reason, disabledRenderers);
				}
			}

			foreach (var smr in skinnedMeshRenderers) {
				if (smr.enabled && IsInvalidMeshForGltfExport(smr.sharedMesh, out var reason)) {
					DisableRendererForExport(smr, smr.sharedMesh, root, reason, disabledRenderers);
				}
			}

			return disabledRenderers;
		}

		private static void DisableRendererForExport(Renderer renderer, Mesh mesh, Transform root, string reason, List<Renderer> disabledRenderers)
		{
			if (disabledRenderers.Contains(renderer)) {
				return;
			}

			renderer.enabled = false;
			disabledRenderers.Add(renderer);

			var path = renderer.transform.GetPath(root);
			var meshName = mesh ? mesh.name : "<none>";
			Logger.Warn($"Skipping mesh '{meshName}' on '{path}' during package export because {reason}.");
			Debug.LogWarning($"Skipping mesh '{meshName}' on '{path}' during package export because {reason}.", renderer);
		}

		private static void RestoreDisabledRenderers(List<Renderer> disabledRenderers)
		{
			foreach (var renderer in disabledRenderers) {
				if (renderer) {
					renderer.enabled = true;
				}
			}
		}

		private static bool IsInvalidMeshForGltfExport(Mesh mesh, out string reason)
		{
			if (!mesh) {
				reason = "no mesh is assigned";
				return true;
			}
			if (mesh.vertexCount == 0) {
				reason = "it has no vertices";
				return true;
			}
			if (mesh.subMeshCount == 0) {
				reason = "it has no submeshes";
				return true;
			}
			if (mesh.GetVertexAttributes().Length == 0) {
				reason = "it has no vertex attributes";
				return true;
			}
			if (!mesh.HasVertexAttribute(VertexAttribute.Position)) {
				reason = "it has no position vertex attribute";
				return true;
			}

			reason = null;
			return false;
		}
	}
}
