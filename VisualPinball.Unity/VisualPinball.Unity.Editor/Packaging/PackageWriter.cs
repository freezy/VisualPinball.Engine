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
using System.Threading;
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
		private VpeMaterialsPayload _capturedMaterialPayload;
		private IReadOnlyList<VpeTextureBlobSource> _capturedBlobSources;
		private IReadOnlyDictionary<string, byte[]> _capturedMaterialTextureBlobs;
		private readonly bool _runtimeCompressSideChannelTextures;
		private readonly bool _runtimeCompressNormalMaps;
		private readonly string _screenshotFolder;
		private IReadOnlyDictionary<string, GlbImageSwap.ImageReplacement> _originalGltfImageReplacements;
		private Dictionary<Transform, string> _nodeIdByTransform;
		private readonly SortedSet<string> _usedTypeNames = new(StringComparer.Ordinal);

		private const bool ExportActivesOnly = true;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PackageWriter(
			GameObject table,
			bool runtimeCompressSideChannelTextures = true,
			bool runtimeCompressNormalMaps = true,
			string screenshotFolder = null)
		{
			_table = table;
			_runtimeCompressSideChannelTextures = runtimeCompressSideChannelTextures;
			_runtimeCompressNormalMaps = runtimeCompressNormalMaps;
			_screenshotFolder = screenshotFolder;
			_refs = new PackagedRefs(table.transform);
		}

		/// <summary>
		/// Synchronous export (blocks the calling thread). Prefer <see cref="WritePackageAsync"/> from
		/// the editor so the UI stays responsive.
		/// </summary>
		public void WritePackageSync(string path)
		{
			AsyncHelper.RunSync(() => WritePackage(path, null, CancellationToken.None));
		}

		/// <summary>
		/// Exports the package without freezing Unity: the editor keeps repainting at the yield points
		/// between stages, the heavy texture byte-load runs on worker threads, and the whole run is
		/// cancelable. <paramref name="progress"/> receives a stage-weighted fraction plus a label.
		/// </summary>
		public Task WritePackageAsync(string path, IProgress<ExportProgress> progress = null, CancellationToken cancellationToken = default)
		{
			return WritePackage(path, progress, cancellationToken);
		}

		private async Task WritePackage(string path, IProgress<ExportProgress> progress, CancellationToken ct)
		{
			var sw = new Stopwatch();
			sw.Start();
			if (File.Exists(path)) {
				File.Delete(path);
			}

			Logger.Info($"Writing table to {path}...");
			IPackageStorage storage = null;
			List<GameObject> reactivatedObjects = null;
			try {
				Report(progress, 0f, "Starting export…");
				storage = PackageApi.StorageManager.CreateStorage(path);

				_tableFolder = storage.AddFolder(PackageApi.TableFolder);
				_globalFolder = _tableFolder.AddFolder(PackageApi.GlobalFolder);
				_metaFolder = _tableFolder.AddFolder(PackageApi.MetaFolder);
				_files = new PackagedFiles(_tableFolder, _refs);

				// Cabinet and backbox are authored inactive; activate them (located via their
				// marker components) so they flow into the active-only export. Restored in finally.
				reactivatedObjects = ActivateMarkedObjects();

				// Every exported node gets a stable id; items, refs, mappings, renderer states and
				// light profiles all reference nodes by these ids. The ids are written into the glTF
				// node extras after the scene export (see SaveGltfToBytes).
				_nodeIdByTransform = VpeNodeIds.AssignIds(_table.transform);
				_refs.SetNodeIdsForWrite(_nodeIdByTransform);

				// prepare scene data
				ct.ThrowIfCancellationRequested();
				Report(progress, 0.03f, "Preparing scene…");
				await Yield();
				var sw1 = Stopwatch.StartNew();
				var saveScene = PrepareScene();
				Logger.Info($"Scene prepared in {sw1.ElapsedMilliseconds}ms.");

				// prepare non-scene meshes
				sw1 = Stopwatch.StartNew();
				var saveColliderMeshes = PrepareColliderMeshes();
				Logger.Info($"Collider meshes prepared in {sw1.ElapsedMilliseconds}ms.");

				// write component data
				ct.ThrowIfCancellationRequested();
				Report(progress, 0.06f, "Writing components…");
				await Yield();
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

				// write globals (this captures the material metadata + the deferred texture sources)
				ct.ThrowIfCancellationRequested();
				Report(progress, 0.14f, "Translating materials…");
				await Yield();
				sw1 = Stopwatch.StartNew();
				WriteTableMetadata();
				WriteGlobals();
				WriteMaterialProfiles();
				WriteLightProfiles();
				Logger.Info($"Globals written in {sw1.ElapsedMilliseconds}ms.");

				// Load the texture bytes (disk read + PNG16→8) on worker threads — the heaviest part of
				// the export, kept off the main thread so the editor stays responsive.
				ct.ThrowIfCancellationRequested();
				sw1 = Stopwatch.StartNew();
				await MaterializeTextureBlobsAsync(progress, 0.25f, 0.70f, ct);
				Logger.Info($"Textures loaded in {sw1.ElapsedMilliseconds}ms.");

				// write assets & co
				ct.ThrowIfCancellationRequested();
				Report(progress, 0.71f, "Writing assets…");
				sw1 = Stopwatch.StartNew();
				_files.PackAssets();
				_files.PackSoundMetas();
				Logger.Info($"Assets and files written in {sw1.ElapsedMilliseconds}ms.");

				// glTFast still reads Unity mesh data at the start of SaveToStreamAndDispose. Start both saves while
				// we're still on the main thread, but write into memory first because the package zip stream only
				// supports one active file entry at a time.
				Report(progress, 0.74f, "Building meshes…");
				await Yield();
				sw1 = Stopwatch.StartNew();
				var saveSceneTask = saveScene();
				var saveColliderMeshesTask = saveColliderMeshes?.Invoke();

				var sceneData = await saveSceneTask;
				ct.ThrowIfCancellationRequested();
				Report(progress, 0.85f, "Writing package…");
				// The GLB payload is mostly already-compressed image data and packed buffers; deflate
				// barely shrinks it but costs real time on both export and load.
				WritePackageFile(_tableFolder, PackageApi.SceneFile, sceneData, PackageCompression.Stored);
				Logger.Info($"Scene written in {sw1.ElapsedMilliseconds}ms ({sceneData.Length} bytes).");
				WriteMaterialPayload();

				if (saveColliderMeshesTask != null) {
					sw1 = Stopwatch.StartNew();
					var colliderMeshesData = await saveColliderMeshesTask;
					WritePackageFile(_tableFolder, PackageApi.ColliderMeshesFile, colliderMeshesData, PackageCompression.Stored);
					Logger.Info($"Collider meshes written in {sw1.ElapsedMilliseconds}ms ({colliderMeshesData.Length} bytes).");
				}

				// screenshots (encoded to jpg, stored under a top-level screenshots/ folder)
				ct.ThrowIfCancellationRequested();
				Report(progress, 0.95f, "Writing screenshots…");
				await Yield();
				sw1 = Stopwatch.StartNew();
				WriteScreenshots(storage);
				Logger.Info($"Screenshots written in {sw1.ElapsedMilliseconds}ms.");

				// the manifest is written last so it can list everything the package contains
				WriteManifest(storage);

				storage.Close();
				storage = null;
				Report(progress, 1f, "Done.");

				sw.Stop();
				Debug.Log($"Done! File saved to {path} in {sw.ElapsedMilliseconds}ms.");

			} catch (OperationCanceledException) {
				storage?.Close();
				storage = null;
				TryDeletePartial(path);
				Logger.Info("Export canceled.");
				throw;
			} catch (Exception) {
				storage?.Close();
				storage = null;
				TryDeletePartial(path);
				throw;
			} finally {
				storage?.Close();
				RestoreMarkedObjects(reactivatedObjects);
			}
		}

		private static void Report(IProgress<ExportProgress> progress, float fraction, string message)
		{
			progress?.Report(new ExportProgress(fraction, message));
		}

		// Yields control back to Unity so the editor repaints and pending progress callbacks run.
		private static async Task Yield()
		{
			await Task.Yield();
		}

		private static void TryDeletePartial(string path)
		{
			try {
				if (File.Exists(path)) {
					File.Delete(path);
				}
			} catch (Exception ex) {
				Logger.Warn(ex, $"Failed deleting partial export '{path}'.");
			}
		}

		// Loads the deferred texture byte sources captured by WriteMaterialProfiles into final blobs,
		// in parallel on worker threads, reporting per-texture progress within [startFraction, endFraction].
		private async Task MaterializeTextureBlobsAsync(IProgress<ExportProgress> progress, float startFraction, float endFraction, CancellationToken ct)
		{
			if (_capturedBlobSources == null || _capturedBlobSources.Count == 0) {
				_capturedMaterialTextureBlobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
				return;
			}

			// Initialize libvips on the main thread BEFORE fanning out, so the worker threads don't race
			// its lazy native init (which hard-crashes Unity — see EnsureVipsInitialized).
			VpeTextureBlobLoader.EnsureVipsInitialized();

			var total = _capturedBlobSources.Count;
			Report(progress, startFraction, $"Loading textures (0/{total})…");
			var sources = _capturedBlobSources;
			_capturedMaterialTextureBlobs = await Task.Run(() => VpeTextureBlobLoader.LoadAll(
				sources,
				done => Report(progress, Mathf.Lerp(startFraction, endFraction, (float)done / total), $"Loading textures ({done}/{total})…"),
				ct), ct);
		}

		private void WriteManifest(IPackageStorage storage)
		{
			foreach (var assetTypeName in _files.UsedAssetTypeNames) {
				_usedTypeNames.Add(assetTypeName);
			}
			var manifest = new VpePackageManifest {
				FormatVersion = PackageApi.FormatVersion,
				WrittenBy = GetWriterId(),
				RootNodeId = _nodeIdByTransform[_table.transform],
				Schemas = new Dictionary<string, int> {
					[VpePackageSchemas.Items] = 1,
				},
				ComponentTypes = _usedTypeNames.ToList(),
			};
			if (_capturedMaterialPayload != null) {
				manifest.Schemas[VpePackageSchemas.Materials] = VpeMaterialSchema.Version;
			}
			if (_wroteLightProfiles) {
				manifest.Schemas[VpePackageSchemas.Lights] = 1;
			}
			if (_files.HasSounds) {
				manifest.Schemas[VpePackageSchemas.Sounds] = 1;
			}
			VpePackageManifestIo.Write(storage, manifest);
		}

		private static string GetWriterId()
		{
			try {
				var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PackageWriter).Assembly);
				var version = packageInfo?.version ?? "unknown";
				return $"VPE {version} (Unity {Application.unityVersion})";
			} catch (Exception) {
				return "VPE";
			}
		}

		// Cabinet/backbox are authored inactive but must be exported; activate the GameObjects
		// carrying the marker components (CabinetComponent/BackboxComponent) and return the ones
		// we flipped so they can be restored afterwards.
		private List<GameObject> ActivateMarkedObjects()
		{
			var reactivated = new List<GameObject>();
			foreach (var go in CollectMarkedObjects()) {
				if (go && !go.activeSelf) {
					go.SetActive(true);
					reactivated.Add(go);
				}
			}
			return reactivated;
		}

		private static void RestoreMarkedObjects(List<GameObject> reactivated)
		{
			if (reactivated == null) {
				return;
			}
			foreach (var go in reactivated) {
				if (go) {
					go.SetActive(false);
				}
			}
		}

		private List<GameObject> CollectMarkedObjects()
		{
			var objects = new List<GameObject>();
			foreach (var cabinet in _table.GetComponentsInChildren<CabinetComponent>(true)) {
				if (cabinet && !objects.Contains(cabinet.gameObject)) {
					objects.Add(cabinet.gameObject);
				}
			}
			foreach (var backbox in _table.GetComponentsInChildren<BackboxComponent>(true)) {
				if (backbox && !objects.Contains(backbox.gameObject)) {
					objects.Add(backbox.gameObject);
				}
			}
			return objects;
		}

		// Encodes the generated screenshots to jpg (libvips via NetVips) and stores them under a
		// top-level "screenshots/" folder in the package, alongside the user-supplied backglass
		// image. Missing screenshots are skipped.
		private void WriteScreenshots(IPackageStorage storage)
		{
			IPackageFolder screenshotsFolder = null;

			if (!string.IsNullOrEmpty(_screenshotFolder)) {
				foreach (var fileName in PackageScreenshotGenerator.ScreenshotFileNames) {
					var pngPath = Path.GetFullPath(Path.Combine(_screenshotFolder, fileName));
					if (!File.Exists(pngPath)) {
						Logger.Warn($"Screenshot '{pngPath}' not found; skipping.");
						continue;
					}

					byte[] jpegData;
					try {
						using var image = NetVips.Image.NewFromFile(pngPath);
						jpegData = image.JpegsaveBuffer(q: 90);
					} catch (Exception ex) {
						Logger.Warn(ex, $"Failed to encode screenshot '{pngPath}' to jpg; skipping.");
						continue;
					}

					screenshotsFolder ??= storage.AddFolder(PackageApi.ScreenshotsFolder);
					var name = Path.GetFileNameWithoutExtension(fileName);
					screenshotsFolder.AddFile(name, ".jpg").SetData(jpegData);
					Logger.Info($"Packaged screenshot '{name}.jpg' ({jpegData.Length / 1024f:F1} KB).");
				}

				// The table crop-bounds sidecar (json), packaged next to the screenshots.
				var boundsPath = Path.GetFullPath(Path.Combine(_screenshotFolder, PackageScreenshotGenerator.FilenameBounds));
				if (File.Exists(boundsPath)) {
					screenshotsFolder ??= storage.AddFolder(PackageApi.ScreenshotsFolder);
					var boundsName = Path.GetFileNameWithoutExtension(PackageScreenshotGenerator.FilenameBounds);
					screenshotsFolder.AddFile(boundsName, ".json").SetData(File.ReadAllBytes(boundsPath));
					Logger.Info($"Packaged screenshot bounds '{boundsName}.json'.");
				}
			}

			WriteBackglass(storage, ref screenshotsFolder);
		}

		// Encodes the user-supplied backglass image (linked in the table metadata) to jpg and
		// stores it as screenshots/backglass.jpg. No-op when no backglass image is linked.
		private void WriteBackglass(IPackageStorage storage, ref IPackageFolder screenshotsFolder)
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent || tableComponent.Metadata == null || !tableComponent.Metadata.BackglassImage) {
				return;
			}

			var assetPath = UnityEditor.AssetDatabase.GetAssetPath(tableComponent.Metadata.BackglassImage);
			if (string.IsNullOrEmpty(assetPath)) {
				Logger.Warn("Backglass image is not a saved asset; skipping.");
				return;
			}

			var fullPath = Path.GetFullPath(assetPath);
			if (!File.Exists(fullPath)) {
				Logger.Warn($"Backglass image '{fullPath}' not found; skipping.");
				return;
			}

			byte[] jpegData;
			try {
				using var image = NetVips.Image.NewFromFile(fullPath);
				jpegData = image.JpegsaveBuffer(q: 90);
			} catch (Exception ex) {
				Logger.Warn(ex, $"Failed to encode backglass '{fullPath}' to jpg; skipping.");
				return;
			}

			screenshotsFolder ??= storage.AddFolder(PackageApi.ScreenshotsFolder);
			screenshotsFolder.AddFile("backglass", ".jpg").SetData(jpegData);
			Logger.Info($"Packaged backglass 'backglass.jpg' ({jpegData.Length / 1024f:F1} KB).");
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
				LightIntensityFactor = PackageApi.LightIntensityFactor,

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
			// glTFast re-encodes images (JPEG for opaque base color). The package format is
			// lossless, so the original asset bytes are swapped back in after export — for
			// captured materials the textures are stripped anyway, this covers the
			// unsupported-shader materials that keep their textures in the GLB.
			_originalGltfImageReplacements = BuildOriginalGltfImageReplacements(renderers);
			var gltfExportScope = VpeMaterialTranslator.PrepareGltfExport(renderers);
			try {
				export.AddScene(new [] { _table }, _table.transform.worldToLocalMatrix, "VPE Table");

			} finally {
				gltfExportScope?.Dispose();
				RestoreDisabledLights(reenabledLights);
				RestoreDisabledRenderers(disabledRenderers);
			}

			return () => SaveGltfToBytes(export, isScene: true);
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

			return export == null ? null : () => SaveGltfToBytes(export, isScene: false);
		}

		/// <summary>
		/// Walks through the entire game object tree and creates the same structure for
		/// a given storage name, for each IPackageable component. Folders are keyed by the
		/// node's stable id.
		/// </summary>
		/// <param name="folderName">Name of the storage within table storage</param>
		/// <param name="getPackableData">Retrieves component-specific data.</param>
		/// <param name="getItemData">Retrieves item-specific data.</param>
		private void WritePackables(string folderName, Func<IPackable, byte[]> getPackableData, Func<GameObject, byte[]> getItemData = null, Func<Component, byte[]> getNativeData = null)
		{
			// -> rootName <- / nodeId / CompType / 0
			var folder = _tableFolder.AddFolder(folderName);

			// walk the entire tree
			foreach (var t in _table.transform.GetComponentsInChildren<Transform>(!ExportActivesOnly)) {

				// for each game object, loop through all components
				if (!_nodeIdByTransform.TryGetValue(t, out var key)) {
					throw new InvalidOperationException(
						$"No node id assigned for '{t.name}' — the hierarchy changed during export.");
				}
				var counters = new Dictionary<string, int>();
				var itemData = getItemData?.Invoke(t.gameObject);

				// rootName / -> nodeId <- / CompType / 0
				IPackageFolder itemPathFolder = null;
				if (itemData?.Length > 0) {
					itemPathFolder = folder.AddFolder(key);

					var itemFile = itemPathFolder.AddFile(PackageApi.ItemFile, PackageApi.Packer.FileExtension);
					itemFile.SetData(itemData);
				}

				foreach (var component in t.gameObject.GetComponents<Component>()) {
					switch (component) {
						case null:
							Debug.LogWarning($"Skipping missing component on {t.name} during package export.", t.gameObject);
							break;

						case IPackable packageable: {

							var packName = _refs.GetName(packageable.GetType());
							counters.TryAdd(packName, 0);

							var packableData = getPackableData(packageable);
							var shouldWriteEmptyMarker = folderName == PackageApi.ItemFolder && (packableData == null || packableData.Length == 0);
							if (packableData?.Length > 0 || shouldWriteEmptyMarker) {

								// rootName / -> nodeId <- / CompType / 0
								itemPathFolder ??= folder.AddFolder(key);

								// rootName / nodeId / -> CompType <- / 0
								if (!itemPathFolder.TryGetFolder(packName, out var itemComponentFolder)) {
									itemComponentFolder = itemPathFolder.AddFolder(packName);
								}

								// rootName / nodeId / CompType / -> 0 <-
								var itemComponentFile = itemComponentFolder.AddFile($"{counters[packName]++}", PackageApi.Packer.FileExtension);
								itemComponentFile.SetData(packableData?.Length > 0 ? packableData : PackageApi.Packer.Empty);
								_usedTypeNames.Add(packName);
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

			// Cabinet/backbox markers are authoring-only and intentionally not packaged.
			if (comp is CabinetComponent or BackboxComponent) {
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

		private void WriteGlobals()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}

			foreach (var sw in tableComponent.MappingConfig.Switches) {
				sw.SaveReference(_refs);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.SaveReference(_refs);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.SaveReferences(_refs);
			}
			foreach (var lp in tableComponent.MappingConfig.Lamps) {
				lp.SaveReference(_refs);
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

			var capture = VpeMaterialTranslator.Capture(_table.transform, renderers, t => _nodeIdByTransform.GetValueOrDefault(t));
			var payload = capture.Payload;
			_capturedMaterialPayload = null;
			_capturedBlobSources = null;
			_capturedMaterialTextureBlobs = null;
			if (payload?.Profiles == null || payload.Profiles.Length == 0) {
				if (VpeMaterialTranslator.Active == null) {
					Logger.Info("Skipping materials export: no IVpeMaterialTranslator is registered.");
				}
				return;
			}
			ApplyMaterialTextureCompressionMode(payload, _runtimeCompressSideChannelTextures);
			ApplyMaterialNormalCompressionMode(payload, _runtimeCompressNormalMaps);
			_capturedMaterialPayload = payload;
			// Deferred: the actual texture bytes are loaded later off the main thread by
			// MaterializeTextureBlobsAsync. Here we only record the sources (file paths / inline bytes).
			_capturedBlobSources = capture.TextureBlobSources;

			var sourceCount = capture.TextureBlobSources?.Count ?? 0;
			Logger.Info(
				$"Captured material payload: {payload.Profiles.Length} profile(s), " +
				$"{sourceCount} source texture(s) (bytes loaded later off-thread).");
		}

		private bool _wroteLightProfiles;

		private void WriteLightProfiles()
		{
			var payload = new VpeLightsPayload {
				Version = 1,
				Lights = _table.GetComponentsInChildren<Light>(!ExportActivesOnly)
					.Select(light => LightSourcePackable.From(
						_table.transform, light, _nodeIdByTransform.GetValueOrDefault(light.transform)))
					.ToList(),
			};

			_wroteLightProfiles = payload.Lights.Count > 0;
			if (!_wroteLightProfiles) {
				return;
			}

			_metaFolder
				.AddFile(PackageApi.LightsFile, PackageApi.Packer.FileExtension)
				.SetData(PackageApi.Packer.Pack(payload));

			Logger.Info($"Captured lights payload: {payload.Lights.Count} light profile(s).");
		}

		// Writes the materials payload plus the lossless source texture layer: one plain image
		// file per texture under table/textures/. Stored, not deflated — PNG/JPEG bytes don't
		// deflate meaningfully and the inflate cost is pure waste at load time.
		private void WriteMaterialPayload()
		{
			if (_capturedMaterialPayload == null) {
				return;
			}

			var textureCount = 0;
			var textureBytes = 0L;
			if (_capturedMaterialPayload.Textures != null
				&& _capturedMaterialPayload.Textures.Length > 0
				&& _capturedMaterialTextureBlobs != null) {
				IPackageFolder texturesFolder = null;
				foreach (var texture in _capturedMaterialPayload.Textures) {
					if (texture == null
						|| string.IsNullOrWhiteSpace(texture.FileName)
						|| !_capturedMaterialTextureBlobs.TryGetValue(texture.FileName, out var data)
						|| data == null
						|| data.Length == 0) {
						continue;
					}
					texturesFolder ??= _tableFolder.AddFolder(PackageApi.TexturesFolder);
					texturesFolder.AddFile(texture.FileName).SetData(data, PackageCompression.Stored);
					textureCount++;
					textureBytes += data.LongLength;
				}
			}

			_metaFolder
				.AddFile(PackageApi.MaterialsFile, PackageApi.Packer.FileExtension)
				.SetData(PackageApi.Packer.Pack(_capturedMaterialPayload));

			Logger.Info(
				$"Packaged materials: profiles={_capturedMaterialPayload.Profiles?.Length ?? 0}, " +
				$"textures={textureCount} ({textureBytes / 1024f / 1024f:F2} MB) at " +
				$"{PackageApi.TableFolder}/{PackageApi.TexturesFolder}/.");
		}

		private static void ApplyMaterialTextureCompressionMode(VpeMaterialsPayload payload, bool runtimeCompress)
		{
			if (payload?.Textures == null) {
				return;
			}
			foreach (var asset in payload.Textures) {
				if (asset != null) {
					asset.RuntimeCompress = runtimeCompress;
				}
			}
		}

		private static void ApplyMaterialNormalCompressionMode(VpeMaterialsPayload payload, bool runtimeCompress)
		{
			if (payload?.Profiles == null) {
				return;
			}
			foreach (var profile in payload.Profiles) {
				if (profile == null) {
					continue;
				}
				ApplyNormalCompressionMode(profile.Lit?.NormalMap, runtimeCompress);
				ApplyNormalCompressionMode(profile.Decal?.NormalMap, runtimeCompress);
			}
		}

		private static void ApplyNormalCompressionMode(VpeNormalMapRef normalMap, bool runtimeCompress)
		{
			// Cooked normals (non-RGB packing) skip the runtime repack entirely, so the
			// runtime-compression toggle has no business overriding them.
			if (normalMap != null && normalMap.Packing == VpeNormalPackings.Rgb) {
				normalMap.RuntimeCompress = runtimeCompress;
			}
		}

		private async Task<byte[]> SaveGltfToBytes(GameObjectExport export, bool isScene)
		{
			using var stream = new MemoryStream();
			await export.SaveToStreamAndDispose(stream);
			var glbData = stream.ToArray();
			if (!isScene) {
				return glbData;
			}

			// Lossless source guarantee: swap glTFast's re-encoded images back to the original
			// asset bytes.
			glbData = ReplaceCompressedGltfImagesWithOriginals(glbData);

			// Stamp the stable node ids into the glTF node extras. This must succeed — a package
			// whose ids don't bind would mis-wire components on import.
			glbData = VpeNodeIds.InjectIds(glbData, _table.transform, _nodeIdByTransform);
			return glbData;
		}

		private byte[] ReplaceCompressedGltfImagesWithOriginals(byte[] glbData)
		{
			if (_originalGltfImageReplacements == null || _originalGltfImageReplacements.Count == 0) {
				return glbData;
			}

			try {
				var rewritten = GlbImageSwap.ReplaceImages(
					glbData,
					_originalGltfImageReplacements,
					out var replacementCount,
					out var originalBytes,
					out var replacementBytes);
				if (replacementCount > 0) {
					Logger.Info(
						$"Replaced {replacementCount} compressed GLB image(s) with original asset bytes " +
						$"({originalBytes / 1024f / 1024f:F2} MB -> {replacementBytes / 1024f / 1024f:F2} MB).");
				}
				return rewritten;
			} catch (Exception ex) {
				Logger.Warn(ex, "Failed replacing compressed GLB images with original asset bytes. Keeping glTFast image payloads.");
				return glbData;
			}
		}

		private static IReadOnlyDictionary<string, GlbImageSwap.ImageReplacement> BuildOriginalGltfImageReplacements(
			IEnumerable<Renderer> renderers)
		{
			var replacements = new Dictionary<string, GlbImageSwap.ImageReplacement>(StringComparer.Ordinal);
			if (renderers == null) {
				return replacements;
			}

			// Image names in the GLB are derived from the source file stem. Two different source
			// files with the same stem would make the name-based replacement ambiguous — those
			// stems are excluded (their GLB images keep glTFast's encoding) and reported.
			var sourcesByStem = new Dictionary<string, string>(StringComparer.Ordinal);
			var ambiguousStems = new HashSet<string>(StringComparer.Ordinal);

			foreach (var renderer in renderers) {
				if (!renderer) {
					continue;
				}
				var materials = renderer.sharedMaterials;
				if (materials == null) {
					continue;
				}
				foreach (var material in materials) {
					CollectOriginalGltfImageReplacements(material, replacements, sourcesByStem, ambiguousStems);
				}
			}

			foreach (var stem in ambiguousStems) {
				replacements.Remove($"{stem}.jpg");
				replacements.Remove($"{stem}.jpeg");
				replacements.Remove($"{stem}.png");
				Logger.Warn(
					$"Multiple source textures share the file stem '{stem}'; their GLB images keep " +
					"glTFast's encoding to avoid swapping in the wrong bytes. Rename the source files for a lossless GLB.");
			}
			return replacements;
		}

		private static void CollectOriginalGltfImageReplacements(
			Material material,
			IDictionary<string, GlbImageSwap.ImageReplacement> replacements,
			IDictionary<string, string> sourcesByStem,
			ISet<string> ambiguousStems)
		{
			if (!material || replacements == null) {
				return;
			}

			var propertyNames = material.GetTexturePropertyNames();
			foreach (var propertyName in propertyNames) {
				if (string.IsNullOrWhiteSpace(propertyName) || material.GetTexture(propertyName) is not Texture2D texture) {
					continue;
				}

				var assetPath = AssetDatabase.GetAssetPath(texture);
				if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath)) {
					continue;
				}

				var extension = Path.GetExtension(assetPath);
				var mimeType = GetImageMimeType(extension);
				if (mimeType == null) {
					continue;
				}

				var stem = Path.GetFileNameWithoutExtension(assetPath);
				if (sourcesByStem.TryGetValue(stem, out var existingPath)) {
					if (!string.Equals(existingPath, assetPath, StringComparison.OrdinalIgnoreCase)) {
						ambiguousStems.Add(stem);
					}
					continue;
				}
				sourcesByStem[stem] = assetPath;

				var replacement = new GlbImageSwap.ImageReplacement {
					Name = Path.GetFileName(assetPath),
					MimeType = mimeType,
					Data = File.ReadAllBytes(assetPath)
				};
				replacements[$"{stem}.jpg"] = replacement;
				replacements[$"{stem}.jpeg"] = replacement;
				replacements[$"{stem}.png"] = replacement;
			}
		}

		private static string GetImageMimeType(string extension)
		{
			if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)) {
				return "image/png";
			}
			if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)) {
				return "image/jpeg";
			}
			return null;
		}

		private static void WritePackageFile(IPackageFolder folder, string fileName, byte[] data, PackageCompression compression = PackageCompression.Default)
		{
			if (data == null || data.Length == 0) {
				throw new InvalidOperationException($"Cannot write empty package file '{fileName}'.");
			}

			folder.AddFile(fileName).SetData(data, compression);
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
