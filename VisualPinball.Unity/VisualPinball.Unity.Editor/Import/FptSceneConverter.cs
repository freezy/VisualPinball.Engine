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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using UnityEditor;
using UnityEngine;

using VisualPinball.Engine.IO.FuturePinball;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.MetalWireGuide;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Simulation;

using EngineMesh = VisualPinball.Engine.VPT.Mesh;
using EngineMaterial = VisualPinball.Engine.VPT.Material;
using EngineSound = VisualPinball.Engine.VPT.Sound.Sound;
using EngineTexture = VisualPinball.Engine.VPT.Texture;
using EngineLight = VisualPinball.Engine.VPT.Light.Light;
using UnityMaterial = UnityEngine.Material;
using UnityMesh = UnityEngine.Mesh;

namespace VisualPinball.Unity.Editor
{
	internal sealed class FptSceneConverter : IMaterialProvider, ITextureProvider
	{
		private const uint NameTag = 0xA4F4D1D7;
		private const uint ModelTag = 0x9DFDC3D8;
		private const uint SurfaceTag = 0xA3EFBDD2;
		private const uint TextureTag = 0xA300C5DC;
		private const uint ColorTag = 0x97F5C3E2;
		private const uint RotationTag = 0xA8EDC3D3;
		private const uint HeightTag = 0xA2F8CDDD;
		private const uint SurfaceTopHeightTag = 0x99F2BEDD;
		private const uint TableWidthTag = 0xA5F8BBD1;
		private const uint TableLengthTag = 0x9BFCC6D1;
		private const uint FrontGlassHeightTag = 0xA1FACCD1;
		private const uint RearGlassHeightTag = 0xA1FAC0D1;
		private const uint SlopeTag = 0x9AF5BFD1;

		private readonly string _sourcePath;
		private readonly string _tableName;
		private readonly FptImportOptions _options;
		private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, Texture2D> _textureAliases = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, string> _textureAliasSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<string> _ambiguousTextureAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, UnityMesh> _meshes = new Dictionary<string, UnityMesh>(StringComparer.Ordinal);
		private readonly Dictionary<string, UnityMaterial> _materials = new Dictionary<string, UnityMaterial>();
		private readonly Dictionary<string, UnityMaterial> _nativeMaterials = new Dictionary<string, UnityMaterial>(StringComparer.Ordinal);
		private readonly HashSet<FuturePinballColliderKind> _reportedTessellatedColliderKinds = new HashSet<FuturePinballColliderKind>();
		private readonly HashSet<FuturePinballElementType> _reportedNativeDefaults = new HashSet<FuturePinballElementType>();
		private readonly HashSet<string> _reportedSurfacePlacementIssues = new HashSet<string>(StringComparer.Ordinal);
		private readonly Dictionary<FuturePinballSourceStream, GameObject> _nativeObjects = new Dictionary<FuturePinballSourceStream, GameObject>();
		private readonly Dictionary<FuturePinballSourceStream, IVpxPrefab> _nativePrefabs = new Dictionary<FuturePinballSourceStream, IVpxPrefab>();
		private readonly Dictionary<FuturePinballSourceStream, TurntableComponent> _spinningDisks = new Dictionary<FuturePinballSourceStream, TurntableComponent>();
		private readonly FptImportReport _report = new FptImportReport();
		private string _tableAssetRoot;
		private string _bundleAssetRoot;
		private string _meshAssetRoot;
		private string _materialAssetRoot;
		private FuturePinballTable _table;
		private FuturePinballExtractionManifest _manifest;
		private Table _nativeTable;
		private Dictionary<string, FuturePinballSourceStream[]> _surfacesByName;

		public FptSceneConverter(string sourcePath, string tableName, FptImportOptions options)
		{
			_sourcePath = Path.GetFullPath(sourcePath ?? throw new ArgumentNullException(nameof(sourcePath)));
			_tableName = string.IsNullOrWhiteSpace(tableName) ? Path.GetFileNameWithoutExtension(sourcePath) : tableName;
			_options = options ?? new FptImportOptions();
		}

		public FptImportResult Convert()
		{
			var timer = Stopwatch.StartNew();
			GameObject root = null;
			ValidateAssetRoot();
			_tableAssetRoot = $"{_options.AssetRoot.TrimEnd('/', '\\')}/{SafeName(_tableName)}";
			_bundleAssetRoot = $"{_tableAssetRoot}/Source/FuturePinball";
			_meshAssetRoot = $"{_tableAssetRoot}/Meshes/FuturePinball";
			_materialAssetRoot = $"{_tableAssetRoot}/Materials/FuturePinball";
			EnsureAssetFolder(_bundleAssetRoot);
			EnsureAssetFolder(_meshAssetRoot);
			EnsureAssetFolder(_materialAssetRoot);

			_table = FuturePinballTableReader.Load(_sourcePath);
			_manifest = FuturePinballExtractor.Extract(_sourcePath, PhysicalPath(_bundleAssetRoot), new FuturePinballExtractionOptions {
				CopyOriginalTable = _options.CopyOriginalTable,
				OverwriteChangedFiles = _options.OverwriteChangedSourceFiles,
				LibrarySearchRoots = _options.LibrarySearchRoots ?? Array.Empty<string>()
			});
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			LoadTextures();

			var rootName = _tableName + " (Future Pinball)";
			var existing = _options.ReplaceExistingSceneRoot ? GameObject.Find(rootName) : null;
			try {
				root = new GameObject(rootName);
				var playfield = CreateVpeHierarchy(root);
				var rootSource = root.AddComponent<FuturePinballSourceComponent>();
				rootSource.SourceFile = Path.GetFileName(_sourcePath);
				rootSource.SourceHash = _manifest.SourceSha256;
				rootSource.ImportOutcome = "lossless-source-bundle-and-vpe-native-scene";

				var proceduralRoot = Child(playfield, "Procedural Geometry");
				var nativeRoot = Child(playfield, "Native Elements");
				var modelRoot = Child(playfield, "Model Instances");
				var placeholderRoot = Child(playfield, "Preserved Placeholders");
				var handled = new HashSet<FuturePinballSourceStream>();
				CreateNativeElements(nativeRoot, handled);
				CreateProceduralElements(proceduralRoot, handled);
				CreateModelInstances(modelRoot, handled);
				CreatePlaceholders(placeholderRoot, handled);
				PersistNativeElements();

				_report.SourceFile = Path.GetFileName(_sourcePath);
				_report.SourceSha256 = _manifest.SourceSha256;
				_report.FileVersion = _manifest.FileVersion;
				_report.Elements = _table.Elements.Count;
				_report.UnresolvedResources = _manifest.Counts.UnresolvedLinkedResources;
				_report.Warnings.AddRange(_manifest.Issues);
				if (_options.GenerateColliders) {
					_report.Warnings.Add("Generated collision uses native VPE collider components; no Unity/PhysX colliders are created.");
				}
				_report.ElapsedMilliseconds = timer.ElapsedMilliseconds;
				AssetDatabase.SaveAssets();
				_report.Write(PhysicalPath(_bundleAssetRoot), _manifest);
				AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
				if (existing != null) Undo.DestroyObjectImmediate(existing);
				return new FptImportResult(root, _manifest, _report, _bundleAssetRoot);
			} catch {
				if (root != null) UnityEngine.Object.DestroyImmediate(root);
				throw;
			}
		}

		private void CreateNativeElements(GameObject parent, ISet<FuturePinballSourceStream> handled)
		{
			foreach (var element in _table.Elements) {
				if (!FuturePinballNativeItemConverter.TryConvert(element, out var converted)) {
					if (element.ElementType == FuturePinballElementType.SpinningDisk) CreateSpinningDisk(parent, element, handled);
					continue;
				}
				var sourcePosition = FuturePinballElementGeometry.SurfaceProbePosition(element);
				if (!TryResolveSurfaceHeight(element, sourcePosition, out var supportHeight)) {
					continue;
				}
				var prefab = InstantiateNativePrefab(converted.Item);
				prefab.GameObject.transform.SetParent(parent.transform, false);
				prefab.SetData();
				SetReferencedData(prefab);
				if (supportHeight != 0f) {
					prefab.GameObject.transform.localPosition += Vector3.up * FuturePinballCoordinateConverter.ToWorld(0f, 0f, supportHeight).Y;
				}
				var nativeColliders = prefab.GameObject.GetComponentsInChildren<MonoBehaviour>(true)
					.Where(component => component is ICollidableComponent).ToArray();
				foreach (var nativeCollider in nativeColliders) {
					if (!_options.GenerateColliders) nativeCollider.enabled = false;
					else if (nativeCollider.enabled) _report.Colliders++;
				}
				AddSource(prefab.GameObject, element, "native-vpe-counterpart");
				_nativeObjects[element] = prefab.GameObject;
				_nativePrefabs[element] = prefab;
				handled.Add(element);
				_report.NativeElements++;
				if (converted.DefaultedParameters.Count > 0 && element.ElementType.HasValue
					&& _reportedNativeDefaults.Add(element.ElementType.Value)) {
					_report.Warnings.Add($"{element.ElementType.Value} maps to its native VPE counterpart; {string.Join(", ", converted.DefaultedParameters)} retain VPE defaults or remain unrecreated because FP units or behavior are not equivalent.");
				}
			}
		}

		private void CreateSpinningDisk(GameObject parent, FuturePinballSourceStream element, ISet<FuturePinballSourceStream> handled)
		{
			if (!TryGetPlacement(element, out var position, out var rotation)) return;
			var go = Child(parent, ElementName(element));
			go.transform.localPosition = position;
			go.transform.localRotation = rotation;
			var component = go.AddComponent<TurntableComponent>();
			AddSource(go, element, "native-vpe-counterpart");
			_spinningDisks[element] = component;
			handled.Add(element);
			_report.NativeElements++;
			if (_reportedNativeDefaults.Add(FuturePinballElementType.SpinningDisk)) {
				_report.Warnings.Add("SpinningDisk maps to VPE's turntable; motor power and damping retain VPE defaults because the Future Pinball scales are not equivalent.");
			}
		}

		private void SetReferencedData(IVpxPrefab prefab)
		{
			var previousSkipSurfaceParenting = ImportContext.SkipSurfaceParenting;
			try {
				// FP support heights are resolved explicitly, including duplicate names and nested guide walls.
				ImportContext.SkipSurfaceParenting = true;
				prefab.SetReferencedData(_nativeTable, this, this, null);
			} finally {
				ImportContext.SkipSurfaceParenting = previousSkipSurfaceParenting;
			}
		}

		private void PersistNativeElements()
		{
			foreach (var prefab in _nativePrefabs.Values) {
				prefab.PersistData();
				foreach (var transform in prefab.GameObject.GetComponentsInChildren<Transform>(true)) {
					EditorUtility.SetDirty(transform.gameObject);
					PrefabUtility.RecordPrefabInstancePropertyModifications(transform.gameObject);
					foreach (var component in transform.GetComponents<Component>()) {
						if (!component) continue;
						EditorUtility.SetDirty(component);
						PrefabUtility.RecordPrefabInstancePropertyModifications(component);
					}
				}
				prefab.FreeBinaryData();
			}
		}

		private IVpxPrefab InstantiateNativePrefab(IItem item)
		{
			switch (item) {
				case Bumper bumper: return bumper.InstantiatePrefab();
				case Flipper flipper: return flipper.InstantiatePrefab();
				case Gate gate: return gate.InstantiatePrefab();
				case HitTarget target: return target.InstantiatePrefab();
				case Kicker kicker: return kicker.InstantiatePrefab();
				case EngineLight light: return light.InstantiatePrefab(_nativeTable);
				case MetalWireGuide wireGuide: return wireGuide.InstantiatePrefab();
				case Plunger plunger: return plunger.InstantiatePrefab();
				case Ramp ramp: return ramp.InstantiatePrefab();
				case Rubber rubber: return rubber.InstantiatePrefab();
				case Spinner spinner: return spinner.InstantiatePrefab();
				case Surface surface: return surface.InstantiatePrefab();
				case Trigger trigger: return trigger.InstantiatePrefab();
				default: throw new InvalidOperationException($"No VPE prefab is registered for {item.GetType().Name}.");
			}
		}

		private void CreateProceduralElements(GameObject parent, ISet<FuturePinballSourceStream> handled)
		{
			var sources = _table.Elements.Where(item => item.SourceIndex.HasValue)
				.GroupBy(item => item.SourceIndex.Value)
				.ToDictionary(group => group.Key, group => group.First());
			foreach (var element in FuturePinballProceduralMeshBuilder.Build(_table)) {
				if (!sources.TryGetValue(element.SourceIndex, out var source)) {
					_report.Warnings.Add($"Procedural element '{element.Name}' has no matching source stream and was skipped.");
					continue;
				}
				var sourcePosition = FuturePinballElementGeometry.SurfaceProbePosition(source);
				if (!TryResolveSurfaceHeight(source, sourcePosition, out var supportHeight)) {
					supportHeight = 0f;
					_report.Warnings.Add($"{ElementName(source)} procedural geometry uses base height because its named support height is unresolved.");
				}
				var go = Child(parent, element.Name);
				if (supportHeight != 0f) {
					go.transform.localPosition += Vector3.up * FuturePinballCoordinateConverter.ToWorld(0f, 0f, supportHeight).Y;
				}
				var hasNativeCounterpart = _nativeObjects.TryGetValue(source, out var nativeObject);
				AddSource(go, source, hasNativeCounterpart ? "native-procedural-visual" : "native-procedural-static");
				if (hasNativeCounterpart) DisableRenderers(nativeObject);
				for (var i = 0; i < element.Meshes.Count; i++) {
					var worldMesh = element.Meshes[i].Clone().TransformToWorld();
					CreateRenderer(go, source, worldMesh, element.Material, i, true, !hasNativeCounterpart && element.IsCollidable);
				}
				handled.Add(source);
				_report.ProceduralElements++;
			}
		}

		private void CreateModelInstances(GameObject parent, ISet<FuturePinballSourceStream> handled)
		{
			var configuredSpinningDisks = new HashSet<FuturePinballSourceStream>();
			if (_options.ImportPrimaryModels) {
				var referenced = new HashSet<string>(_table.Elements.Select(element => FuturePinballElementGeometry.Text(element, ModelTag))
					.Where(name => !string.IsNullOrWhiteSpace(name)), StringComparer.OrdinalIgnoreCase);
				var models = LoadModels(referenced);
				foreach (var element in _table.Elements) {
					var modelName = FuturePinballElementGeometry.Text(element, ModelTag);
					if (string.IsNullOrWhiteSpace(modelName) || !models.TryGetValue(modelName, out var model)) continue;
					if (!TryGetPlacement(element, out var position, out var rotation)) {
						handled.Add(element);
						continue;
					}
					var name = ElementName(element);
					var hasNativeCounterpart = _nativeObjects.ContainsKey(element);
					var isSpinningDisk = _spinningDisks.TryGetValue(element, out var turntable);
					var go = Child(parent, hasNativeCounterpart || isSpinningDisk ? name + " (Source Model)" : name);
					AddSource(go, element, isSpinningDisk
						? "source-model-drives-native-turntable-visual"
						: hasNativeCounterpart ? "source-model-preserved-for-native-counterpart" : "static-model");
					GameObject visualRoot;
					if (isSpinningDisk) {
						visualRoot = CreateSpinningDiskVisualRoot(turntable, go);
					} else {
						go.transform.localPosition = position;
						go.transform.localRotation = rotation;
						visualRoot = go;
					}
					var elementMaterial = FuturePinballMaterialConverter.FromElement(element, TextureTag, ColorTag);
					var groups = model.Primary.CreateMeshes();
					for (var i = 0; i < groups.Count; i++) {
						var worldMesh = FuturePinballCoordinateConverter.ModelMeshToWorld(groups[i].Mesh);
						var material = groups[i].MaterialIndex >= 0 && groups[i].MaterialIndex < model.Primary.Materials.Count
							? FuturePinballMaterialConverter.FromMilkShape(model.Primary.Materials[groups[i].MaterialIndex])
							: elementMaterial;
						CreateRenderer(visualRoot, element, worldMesh, material, i, false, false, $"model-{model.Primary.SourceSha256}-{i}");
					}
					if (isSpinningDisk && ConfigureSpinningDiskVisual(turntable, visualRoot)) configuredSpinningDisks.Add(element);
					if (_options.GenerateColliders && !hasNativeCounterpart) CreateModelColliders(go, element, model);
					if (hasNativeCounterpart) go.SetActive(false);
					handled.Add(element);
					_report.ModelInstances++;
				}
			}
			foreach (var element in _spinningDisks.Keys.Where(element => !configuredSpinningDisks.Contains(element))) {
				AddSpinningDiskBacklog(element);
			}
		}

		internal static GameObject CreateSpinningDiskVisualRoot(TurntableComponent component, GameObject modelRoot)
		{
			modelRoot.transform.SetParent(component.transform, false);
			modelRoot.transform.localPosition = Vector3.zero;
			modelRoot.transform.localRotation = Quaternion.identity;
			return Child(modelRoot, "Rotating Visual");
		}

		internal static bool ConfigureSpinningDiskVisual(TurntableComponent component, GameObject visual)
		{
			component.RotationTarget = visual.transform;
			var radiusSquared = 0f;
			// FP spinning-disk primary models are treated as the playable disc; auxiliary geometry can overestimate this radius.
			foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true)) {
				if (filter.sharedMesh == null) continue;
				foreach (var vertex in filter.sharedMesh.vertices) {
					var local = visual.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
					radiusSquared = Mathf.Max(radiusSquared, local.x * local.x + local.z * local.z);
				}
			}
			var radius = Mathf.Sqrt(radiusSquared) * 1000f;
			var validRadius = !float.IsNaN(radius) && !float.IsInfinity(radius) && radius > 0f;
			if (validRadius) component.Radius = radius;
			EditorUtility.SetDirty(component);
			return validRadius;
		}

		private void CreatePlaceholders(GameObject parent, ISet<FuturePinballSourceStream> handled)
		{
			foreach (var element in _table.Elements.Where(element => !handled.Contains(element))) {
				if (!TryGetPlacement(element, out var position, out var rotation)) continue;
				var go = Child(parent, ElementName(element));
				AddSource(go, element, "preserved-placeholder");
				go.transform.localPosition = position;
				go.transform.localRotation = rotation;
				_report.Placeholders++;
				_report.Backlog.Add(new FptRecreationBacklogItem {
					SourceIndex = element.SourceIndex ?? -1,
					Name = ElementName(element),
					ElementType = element.ElementType?.ToString() ?? $"Unknown({element.ElementTypeId})",
					CurrentOutcome = "Source data preserved; no recreated visual behavior",
					SuggestedCapability = SuggestedCapability(element.ElementType),
					SourceStream = element.Name
				});
			}
		}

		private Dictionary<string, LoadedModel> LoadModels(ISet<string> referenced)
		{
			var result = new Dictionary<string, LoadedModel>(StringComparer.OrdinalIgnoreCase);
			var cache = new MilkShapeModelCache();
			foreach (var stream in _table.PinModels) {
				var name = stream.Text(NameTag) ?? stream.Name;
				if (!referenced.Contains(name)) continue;
				try {
					var source = stream;
					var assets = FuturePinballModelAssetReader.ReadEmbedded(source, cache);
					if (assets.Variants.Count == 0) {
						var resource = _manifest.Resources.FirstOrDefault(item => item.Category == "models" && item.SourceIndex == stream.SourceIndex);
						var linked = resource?.Files.FirstOrDefault(file => file.Role == "linked-model");
						if (linked != null) {
							var fpm = FuturePinballModelReader.Load(PhysicalPath($"{_bundleAssetRoot}/{linked.Path}"));
							source = fpm.ModelData;
							assets = FuturePinballModelAssetReader.ReadEmbedded(source, cache);
						}
					}
					var primary = assets.Variants.FirstOrDefault(variant => variant.Role == "primary_model_data")?.Model;
					if (primary != null) result[name] = new LoadedModel(primary, source);
					else _report.Warnings.Add($"Model '{name}' has no decoded primary mesh.");
				} catch (Exception exception) {
					_report.Warnings.Add($"Could not decode model '{name}': {exception.Message}");
				}
			}
			return result;
		}

		private void CreateModelColliders(GameObject parent, FuturePinballSourceStream element, LoadedModel model)
		{
			var descriptions = FuturePinballColliderBuilder.FromModel(model.Source, model.Primary, new FuturePinballColliderOptions {
				EnableAnalyticShapes = true,
				EnablePerPolygonCollision = _options.EnablePerPolygonCollision,
				GenerateRenderMeshFallback = _options.GenerateRenderMeshFallbackColliders
			});
			var index = 0;
			foreach (var description in descriptions) {
				if (description.Status != FuturePinballColliderStatus.Generated) {
					AddColliderBacklog(element, description.Reason ?? $"{description.Kind} collider was not generated");
					continue;
				}
				var colliderIndex = index++;
				var colliderMesh = FuturePinballColliderMeshBuilder.Build(description);
				if (colliderMesh?.IsSet != true || colliderMesh.Indices.Length == 0) {
					AddColliderBacklog(element, $"{description.Kind} has no VPE-compatible triangle mesh");
					continue;
				}
				var go = Child(parent, $"VPE Collider {colliderIndex} ({description.Kind})");
				go.transform.localPosition = new Vector3(description.Center.X, description.Center.Y, description.Center.Z);
				var unityMesh = GetOrCreateMesh(colliderMesh,
					$"vpe-collider-{model.Primary.SourceSha256}-{colliderIndex}-{description.Kind}");
				AddVpePrimitiveCollider(go, unityMesh, description.GenerateHitEvent, false);
				if (FuturePinballColliderMeshBuilder.IsTessellatedApproximation(description.Kind)
					&& _reportedTessellatedColliderKinds.Add(description.Kind)) {
					_report.Warnings.Add($"{description.Kind} collision is tessellated into a VPE primitive mesh; curved-source geometry is approximate.");
				}
				if (description.GenerateHitEvent && description.EventId != 0) {
					_report.Warnings.Add($"{ElementName(element)} collider event {description.EventId} maps to a generic VPE primitive hit event; Future Pinball script dispatch remains unimplemented.");
				}
				_report.Colliders++;
			}
		}

		private void CreateRenderer(
			GameObject parent,
			FuturePinballSourceStream source,
			EngineMesh mesh,
			FuturePinballMaterialDescription material,
			int part,
			bool procedural,
			bool collidable,
			string meshAssetKey = null)
		{
			if (mesh?.IsSet != true || mesh.Vertices.Length == 0 || mesh.Indices.Length == 0) return;
			var go = Child(parent, $"{SafeName(mesh.Name ?? "Mesh")} {part}");
			var unityMesh = GetOrCreateMesh(mesh, meshAssetKey ?? $"{source.SourceIndex:D5}-{SafeName(ElementName(source))}-{part}");
			go.AddComponent<MeshFilter>().sharedMesh = unityMesh;
			go.AddComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial(material);
			if (_options.GenerateColliders && procedural && collidable) {
				AddVpePrimitiveCollider(go, unityMesh, false, true);
				_report.Colliders++;
			}
		}

		private static void AddVpePrimitiveCollider(GameObject go, UnityMesh mesh, bool hitEvent, bool visible)
		{
			var meshFilter = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = mesh;
			go.AddComponent<PrimitiveComponent>();
			var meshComponent = go.AddComponent<PrimitiveMeshComponent>();
			meshComponent.UseLegacyMesh = false;
			meshComponent.enabled = visible;
			var collider = go.AddComponent<PrimitiveColliderComponent>();
			collider.HitEvent = hitEvent;
			collider.enabled = true;
		}

		private UnityMesh GetOrCreateMesh(EngineMesh source, string assetName)
		{
			var path = $"{_meshAssetRoot}/{SafeName(assetName)}-{MeshHash(source).Substring(0, 16)}.asset";
			if (_meshes.TryGetValue(path, out var cached)) return cached;
			var existing = AssetDatabase.LoadAssetAtPath<UnityMesh>(path);
			if (existing != null && _options.ReuseGeneratedAssets) {
				_report.ReusedAssets++;
				return _meshes[path] = existing;
			}
			if (existing != null) AssetDatabase.DeleteAsset(path);
			var mesh = new UnityMesh {
				name = assetName,
				indexFormat = source.Vertices.Length > ushort.MaxValue
					? UnityEngine.Rendering.IndexFormat.UInt32
					: UnityEngine.Rendering.IndexFormat.UInt16
			};
			source.ApplyToUnityMesh(mesh);
			AssetDatabase.CreateAsset(mesh, path);
			_report.MeshAssets++;
			return _meshes[path] = mesh;
		}

		private UnityMaterial GetOrCreateMaterial(FuturePinballMaterialDescription source)
		{
			source ??= FuturePinballMaterialConverter.FromValues("default", 2, 0xffffffff);
			var key = $"{source.Category}|{source.SourceColor}|{source.Opacity:R}|{source.Roughness:R}|{source.IsCrystal}|{source.IsSphereMapped}|{source.IsTwoSided}|{source.IsEmissive}|{source.Texture}";
			var hash = Sha256(key).Substring(0, 16);
			if (_materials.TryGetValue(hash, out var cached)) return cached;
			var path = $"{_materialAssetRoot}/{SafeName(source.Name)}-{hash}.mat";
			var existing = AssetDatabase.LoadAssetAtPath<UnityMaterial>(path);
			if (existing != null && _options.ReuseGeneratedAssets) {
				_report.ReusedAssets++;
				return _materials[hash] = existing;
			}
			if (existing != null) AssetDatabase.DeleteAsset(path);

			var pbr = new PbrMaterial(source.ToVpeMaterial(), id: hash);
			var material = RenderPipeline.Current?.MaterialConverter?.CreateMaterial(pbr, null)
				?? new UnityMaterial(Shader.Find("Standard"));
			material.name = source.Name;
			if (!string.IsNullOrWhiteSpace(source.Texture) && TryGetTexture(source.Texture, out var texture)) {
				SetTexture(material, texture);
			}
			if (source.IsTwoSided) {
				SetFloat(material, "_DoubleSidedEnable", 1f);
				SetFloat(material, "_CullMode", 0f);
				SetFloat(material, "_Cull", 0f);
			}
			if (source.IsEmissive) {
				var color = source.ToVpeMaterial().BaseColor.ToUnityColor();
				if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", color);
				if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color);
				material.EnableKeyword("_EMISSION");
			}
			AssetDatabase.CreateAsset(material, path);
			_report.MaterialAssets++;
			return _materials[hash] = material;
		}

		private GameObject CreateVpeHierarchy(GameObject root)
		{
			var tableData = CreateTableData();
			_nativeTable = new NativeTableContainer(tableData).Table;
			var tableComponent = root.AddComponent<TableComponent>();
			tableComponent.SetData(tableData);
			root.AddComponent<DefaultGamelogicEngine>();
			root.AddComponent<Player>();

			var playfield = Child(root, "Playfield");
			var physicsEngine = playfield.AddComponent<PhysicsEngine>();
			SimulationThreadComponent.EnsureFor(physicsEngine);
			var playfieldComponent = playfield.AddComponent<PlayfieldComponent>();
			if (_options.GenerateColliders) {
				playfield.AddComponent<PlayfieldColliderComponent>();
				_report.Colliders++;
			}
			playfieldComponent.SetData(tableData);
			playfieldComponent.RenderSlope = tableData.AngleTiltMin;
			return playfield;
		}

		private TableData CreateTableData()
		{
			const int defaultWidthMillimeters = 514;
			const int defaultLengthMillimeters = 1168;
			const int defaultGlassHeightMillimeters = 216;
			var width = FuturePinballElementGeometry.Integer(_table.TableData, TableWidthTag, defaultWidthMillimeters);
			var length = FuturePinballElementGeometry.Integer(_table.TableData, TableLengthTag, defaultLengthMillimeters);
			var frontGlass = FuturePinballElementGeometry.Integer(_table.TableData, FrontGlassHeightTag, defaultGlassHeightMillimeters);
			var rearGlass = FuturePinballElementGeometry.Integer(_table.TableData, RearGlassHeightTag, defaultGlassHeightMillimeters);
			var slope = FuturePinballElementGeometry.Float(_table.TableData, SlopeTag, 6f);
			if (width <= 0) width = defaultWidthMillimeters;
			if (length <= 0) length = defaultLengthMillimeters;
			if (frontGlass <= 0) frontGlass = defaultGlassHeightMillimeters;
			if (rearGlass <= 0) rearGlass = defaultGlassHeightMillimeters;
			if (float.IsNaN(slope) || float.IsInfinity(slope) || slope < 0f || slope > 20f) slope = 6f;
			return new TableData {
				Name = _tableName,
				Image = FuturePinballNativeItemConverter.PlayfieldImage,
				Left = 0f,
				Right = FuturePinballCoordinateConverter.ToVpx(width),
				Top = 0f,
				Bottom = FuturePinballCoordinateConverter.ToVpx(length),
				GlassHeight = FuturePinballCoordinateConverter.ToVpx(System.Math.Max(frontGlass, rearGlass)),
				AngleTiltMin = slope,
				AngleTiltMax = slope
			};
		}

		private void AddColliderBacklog(FuturePinballSourceStream element, string reason)
		{
			if (reason == "The source shape does not affect the ball") return;
			var message = $"{ElementName(element)} collider: {reason}";
			_report.Warnings.Add(message);
			_report.Backlog.Add(new FptRecreationBacklogItem {
				SourceIndex = element.SourceIndex ?? -1,
				Name = ElementName(element),
				ElementType = element.ElementType?.ToString() ?? $"Unknown({element.ElementTypeId})",
				CurrentOutcome = $"Collision source preserved but not converted: {reason}",
				SuggestedCapability = "map the preserved collision record to a supported VPE collider",
				SourceStream = element.Name
			});
		}

		private void AddSpinningDiskBacklog(FuturePinballSourceStream element)
		{
			var message = $"{ElementName(element)} turntable radius could not be derived because its source model was not imported or had no usable mesh; the VPE default radius is retained.";
			_report.Warnings.Add(message);
			_report.Backlog.Add(new FptRecreationBacklogItem {
				SourceIndex = element.SourceIndex ?? -1,
				Name = ElementName(element),
				ElementType = FuturePinballElementType.SpinningDisk.ToString(),
				CurrentOutcome = "Native VPE turntable created, but its influence radius retains the VPE default and its source visual may be unavailable",
				SuggestedCapability = "import or map a usable spinning-disk source model to recover the visual and influence radius",
				SourceStream = element.Name
			});
		}

		private void LoadTextures()
		{
			foreach (var resource in _manifest.Resources.Where(resource => resource.Category == "images")) {
				var file = resource.Files.FirstOrDefault(item => item.Role == "original");
				if (file == null) continue;
				var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{_bundleAssetRoot}/{file.Path}");
				if (texture == null) continue;
				var logicalName = resource.LogicalName;
				if (string.IsNullOrWhiteSpace(logicalName)) continue;
				if (_textures.ContainsKey(logicalName)) {
					_report.Warnings.Add($"Duplicate image name '{logicalName}'; the later source stream is used for exact-name references.");
				}
				_textures[logicalName] = texture;
				AddTextureAlias(Path.GetFileName(logicalName), logicalName, texture);
				AddTextureAlias(Path.GetFileNameWithoutExtension(logicalName), logicalName, texture);
			}
		}

		private void AddTextureAlias(string alias, string logicalName, Texture2D texture)
		{
			if (string.IsNullOrWhiteSpace(alias) || alias.Equals(logicalName, StringComparison.OrdinalIgnoreCase)
				|| _ambiguousTextureAliases.Contains(alias)) return;
			if (_textureAliasSources.TryGetValue(alias, out var existingSource)
				&& !existingSource.Equals(logicalName, StringComparison.OrdinalIgnoreCase)) {
				_textureAliases.Remove(alias);
				_textureAliasSources.Remove(alias);
				_ambiguousTextureAliases.Add(alias);
				_report.Warnings.Add($"Image alias '{alias}' is ambiguous between '{existingSource}' and '{logicalName}' and will not be used.");
				return;
			}
			_textureAliases[alias] = texture;
			_textureAliasSources[alias] = logicalName;
		}

		private bool TryGetTexture(string name, out Texture2D texture)
		{
			if (_textures.TryGetValue(name, out texture)) return true;
			if (_ambiguousTextureAliases.Contains(name)) {
				texture = null;
				return false;
			}
			return _textureAliases.TryGetValue(name, out texture);
		}

		private bool TryResolveSurfaceHeight(FuturePinballSourceStream element, FuturePinballVector2 position, out float height)
		{
			var surfaceName = FuturePinballElementGeometry.Text(element, SurfaceTag);
			if (string.IsNullOrWhiteSpace(surfaceName)) {
				height = 0f;
				return true;
			}
			return TryResolveSurfaceHeight(surfaceName, position, element,
				new HashSet<string>(StringComparer.OrdinalIgnoreCase), out height);
		}

		private bool TryResolveSurfaceHeight(
			string surfaceName,
			FuturePinballVector2 position,
			FuturePinballSourceStream placedElement,
			ISet<string> resolving,
			out float height)
		{
			height = 0f;
			if (!resolving.Add(surfaceName)) {
				AddSurfacePlacementBacklog(placedElement, surfaceName, "surface reference cycle");
				return false;
			}
			try {
				var candidates = SurfacesByName().TryGetValue(surfaceName, out var named) ? named : Array.Empty<FuturePinballSourceStream>();
				if (candidates.Length == 0) {
					AddSurfacePlacementBacklog(placedElement, surfaceName, "referenced surface was not found");
					return false;
				}
				if (candidates.Length == 1) {
					return TryCandidateSurfaceHeight(candidates[0], position, placedElement, resolving, out height);
				}
				var containing = candidates.Where(candidate => FuturePinballElementGeometry.ContainsPoint(candidate, position)).ToArray();
				var selected = containing.Length > 0 ? containing : candidates;
				var heights = new float[selected.Length];
				for (var i = 0; i < selected.Length; i++) {
					if (!TryCandidateSurfaceHeight(selected[i], position, placedElement, resolving, out heights[i])) return false;
				}
				if (containing.Length == 1 || heights.All(candidateHeight => System.Math.Abs(candidateHeight - heights[0]) < 0.0001f)) {
					height = heights[0];
					return true;
				}
				AddSurfacePlacementBacklog(placedElement, surfaceName,
					containing.Length == 0 ? "duplicate surfaces could not be disambiguated by element position" : "element position lies on multiple surfaces with different heights");
				return false;
			} finally {
				resolving.Remove(surfaceName);
			}
		}

		private bool TryCandidateSurfaceHeight(
			FuturePinballSourceStream candidate,
			FuturePinballVector2 position,
			FuturePinballSourceStream placedElement,
			ISet<string> resolving,
			out float height)
		{
			if (candidate.ElementType == FuturePinballElementType.Surface) {
				height = FuturePinballElementGeometry.Float(candidate, SurfaceTopHeightTag);
				return true;
			}
			height = FuturePinballElementGeometry.Float(candidate, HeightTag);
			var parentSurface = FuturePinballElementGeometry.Text(candidate, SurfaceTag);
			if (string.IsNullOrWhiteSpace(parentSurface)) return true;
			if (!TryResolveSurfaceHeight(parentSurface, position, placedElement, resolving, out var parentHeight)) {
				height = 0f;
				return false;
			}
			height += parentHeight;
			return true;
		}

		private Dictionary<string, FuturePinballSourceStream[]> SurfacesByName()
		{
			return _surfacesByName ??= _table.Elements
				.Where(element => element.ElementType == FuturePinballElementType.Surface
					|| element.ElementType == FuturePinballElementType.GuideWall)
				.GroupBy(ElementName, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
		}

		private void AddSurfacePlacementBacklog(FuturePinballSourceStream element, string surfaceName, string reason)
		{
			var issueKey = $"{element.SourceIndex}:{element.Name}:{surfaceName}:{reason}";
			if (!_reportedSurfacePlacementIssues.Add(issueKey)) return;
			_report.Warnings.Add($"{ElementName(element)} surface '{surfaceName}': {reason}; its named support offset is unresolved.");
			_report.Backlog.Add(new FptRecreationBacklogItem {
				SourceIndex = element.SourceIndex ?? -1,
				Name = ElementName(element),
				ElementType = element.ElementType?.ToString() ?? $"Unknown({element.ElementTypeId})",
				CurrentOutcome = $"Named support offset is unresolved: {reason}",
				SuggestedCapability = "resolve the preserved surface reference manually",
				SourceStream = element.Name
			});
		}

		private bool TryGetPlacement(FuturePinballSourceStream element, out Vector3 worldPosition, out Quaternion worldRotation)
		{
			var position = FuturePinballElementGeometry.Position(element);
			if (!TryResolveSurfaceHeight(element, position, out var height)) {
				worldPosition = default;
				worldRotation = default;
				return false;
			}
			height += FuturePinballElementGeometry.Integer(element, HeightTag);
			var world = FuturePinballCoordinateConverter.ToWorld(position.X, position.Y, height);
			worldPosition = new Vector3(world.X, world.Y, world.Z);
			// FPM meshes use model Y as up, unlike table records where Z is height. This basis correction is
			// model-only; native VPE item data intentionally keeps the FP table yaw unchanged.
			worldRotation = Quaternion.Euler(0f, -90f - FuturePinballElementGeometry.Integer(element, RotationTag), 0f);
			return true;
		}

		private void AddSource(GameObject go, FuturePinballSourceStream source, string outcome)
		{
			var component = go.AddComponent<FuturePinballSourceComponent>();
			component.SourceFile = Path.GetFileName(_sourcePath);
			component.SourceHash = _manifest.SourceSha256;
			component.SourceStream = source.Name;
			component.SourceIndex = source.SourceIndex ?? -1;
			component.ElementType = source.ElementType?.ToString() ?? $"Unknown({source.ElementTypeId})";
			component.ImportOutcome = outcome;
		}

		private static string SuggestedCapability(FuturePinballElementType? type)
		{
			switch (type) {
				case FuturePinballElementType.RoundLight:
				case FuturePinballElementType.ShapeableLight:
				case FuturePinballElementType.Flasher:
				case FuturePinballElementType.Bulb:
					return "recreate lamp state and light-list behavior";
				case FuturePinballElementType.Dmd:
				case FuturePinballElementType.HudDmd:
				case FuturePinballElementType.DispReel:
				case FuturePinballElementType.HudReel:
					return "recreate script-driven display output";
				case FuturePinballElementType.Timer:
				case FuturePinballElementType.LightSequencer:
					return "translate runtime behavior from the preserved table script";
				default:
					return "map to a native VPE component or static fallback";
			}
		}

		private static string ElementName(FuturePinballSourceStream element)
		{
			return FuturePinballElementGeometry.Text(element, NameTag, element.Name);
		}

		private void ValidateAssetRoot()
		{
			_options.AssetRoot = (_options.AssetRoot ?? string.Empty).Replace('\\', '/').TrimEnd('/');
			if (_options.AssetRoot != "Assets" && !_options.AssetRoot.StartsWith("Assets/", StringComparison.Ordinal)) {
				throw new ArgumentException("Future Pinball import asset root must be inside Assets.");
			}
			if (_options.AssetRoot.Split('/').Any(part => part.Length == 0 || part == "." || part == "..")) {
				throw new ArgumentException("Future Pinball import asset root contains an invalid path segment.");
			}
		}

		private static GameObject Child(GameObject parent, string name)
		{
			var child = new GameObject(name);
			child.transform.SetParent(parent.transform, false);
			return child;
		}

		private static void DisableRenderers(GameObject root)
		{
			foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
		}

		public UnityEngine.Texture GetTexture(string name)
		{
			return !string.IsNullOrWhiteSpace(name) && TryGetTexture(name, out var texture) ? texture : null;
		}

		public bool HasMaterial(PbrMaterial material)
		{
			var key = NativeMaterialKey(material);
			if (_nativeMaterials.ContainsKey(key)) return true;
			if (!_options.ReuseGeneratedAssets) return false;
			var existing = AssetDatabase.LoadAssetAtPath<UnityMaterial>(NativeMaterialPath(key));
			if (existing == null) return false;
			_nativeMaterials[key] = existing;
			_report.ReusedAssets++;
			return true;
		}

		public void SaveMaterial(PbrMaterial source, UnityMaterial material)
		{
			var key = NativeMaterialKey(source);
			var path = NativeMaterialPath(key);
			var existing = AssetDatabase.LoadAssetAtPath<UnityMaterial>(path);
			if (existing != null && _options.ReuseGeneratedAssets) {
				_nativeMaterials[key] = existing;
				if (material != null && !AssetDatabase.Contains(material)) UnityEngine.Object.DestroyImmediate(material);
				_report.ReusedAssets++;
				return;
			}
			if (existing != null) AssetDatabase.DeleteAsset(path);
			material ??= new UnityMaterial(Shader.Find("Standard"));
			material.name = $"Future Pinball Native {key.Substring(0, 8)}";
			AssetDatabase.CreateAsset(material, path);
			_nativeMaterials[key] = material;
			_report.MaterialAssets++;
		}

		public UnityMaterial GetMaterial(PbrMaterial material)
		{
			if (HasMaterial(material)) return _nativeMaterials[NativeMaterialKey(material)];
			var unityMaterial = RenderPipeline.Current?.MaterialConverter?.CreateMaterial(material ?? new PbrMaterial(), this)
				?? new UnityMaterial(Shader.Find("Standard"));
			SaveMaterial(material, unityMaterial);
			return _nativeMaterials[NativeMaterialKey(material)];
		}

		public PhysicsMaterialAsset GetPhysicsMaterial(string name) => null;

		public UnityMaterial MergeMaterials(string vpxMaterial, UnityMaterial textureMaterial)
		{
			if (textureMaterial == null) return GetMaterial(new PbrMaterial(id: vpxMaterial));
			var source = new PbrMaterial(id: string.IsNullOrWhiteSpace(vpxMaterial) ? PbrMaterial.NameNoMaterial : vpxMaterial);
			if (HasMaterial(source)) return _nativeMaterials[NativeMaterialKey(source)];
			var merged = RenderPipeline.Current?.MaterialConverter?.MergeMaterials(source, textureMaterial)
				?? new UnityMaterial(textureMaterial);
			SaveMaterial(source, merged);
			return _nativeMaterials[NativeMaterialKey(source)];
		}

		private static string NativeMaterialKey(PbrMaterial material)
		{
			return Sha256(material?.Id ?? PbrMaterial.NameNoMaterial);
		}

		private string NativeMaterialPath(string key) => $"{_materialAssetRoot}/native-{key.Substring(0, 16)}.mat";

		private static void SetTexture(UnityMaterial material, UnityEngine.Texture texture)
		{
			foreach (var property in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" }) {
				if (material.HasProperty(property)) material.SetTexture(property, texture);
			}
		}

		private static void SetFloat(UnityMaterial material, string property, float value)
		{
			if (material.HasProperty(property)) material.SetFloat(property, value);
		}

		private static void EnsureAssetFolder(string assetPath)
		{
			var parts = assetPath.Replace('\\', '/').Split('/');
			var current = parts[0];
			for (var i = 1; i < parts.Length; i++) {
				var next = current + "/" + parts[i];
				if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
				current = next;
			}
		}

		private static string PhysicalPath(string assetPath)
		{
			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("Unity project root not found.");
			return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
		}

		private static string SafeName(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return "unnamed";
			var invalid = new HashSet<char>("<>:\"/\\|?*");
			var result = new string(value.Select(character => character < 0x20 || invalid.Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.', ' ');
			return string.IsNullOrEmpty(result) ? "unnamed" : result.Length > 80 ? result.Substring(0, 80) : result;
		}

		private static string Sha256(string value)
		{
			using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
		}

		private static string MeshHash(EngineMesh mesh)
		{
			using (var sha = SHA256.Create())
			using (var output = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
			using (var writer = new BinaryWriter(output)) {
				foreach (var vertex in mesh.Vertices) {
					writer.Write(vertex.X); writer.Write(vertex.Y); writer.Write(vertex.Z);
					writer.Write(vertex.Nx); writer.Write(vertex.Ny); writer.Write(vertex.Nz);
					writer.Write(vertex.Tu); writer.Write(vertex.Tv);
				}
				foreach (var index in mesh.Indices) writer.Write(index);
				writer.Flush();
				output.FlushFinalBlock();
				return BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
			}
		}

		private sealed class LoadedModel
		{
			public MilkShapeModel Primary { get; }
			public FuturePinballSourceStream Source { get; }

			public LoadedModel(MilkShapeModel primary, FuturePinballSourceStream source)
			{
				Primary = primary;
				Source = source;
			}
		}

		private sealed class NativeTableContainer : TableContainer
		{
			public override Table Table { get; }
			public override Dictionary<string, string> TableInfo { get; } = new Dictionary<string, string>();
			public override List<CollectionData> Collections { get; } = new List<CollectionData>();
			public override CustomInfoTags CustomInfoTags { get; } = new CustomInfoTags();
			public override IEnumerable<EngineTexture> Textures => Array.Empty<EngineTexture>();
			public override IEnumerable<EngineSound> Sounds => Array.Empty<EngineSound>();

			public NativeTableContainer(TableData data)
			{
				Table = new Table(this, data);
			}

			public override EngineMaterial GetMaterial(string name) => null;
			public override EngineTexture GetTexture(string name) => null;
		}
	}

	public sealed class FptImportResult
	{
		public GameObject Root { get; }
		public FuturePinballExtractionManifest Manifest { get; }
		public FptImportReport Report { get; }
		public string BundleAssetPath { get; }

		internal FptImportResult(GameObject root, FuturePinballExtractionManifest manifest, FptImportReport report, string bundleAssetPath)
		{
			Root = root;
			Manifest = manifest;
			Report = report;
			BundleAssetPath = bundleAssetPath;
		}
	}
}
