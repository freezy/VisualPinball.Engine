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
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Simulation;

using EngineMesh = VisualPinball.Engine.VPT.Mesh;
using UnityMaterial = UnityEngine.Material;
using UnityMesh = UnityEngine.Mesh;

namespace VisualPinball.Unity.Editor
{
	internal sealed class FptSceneConverter
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
		private readonly Dictionary<string, UnityMesh> _meshes = new Dictionary<string, UnityMesh>(StringComparer.Ordinal);
		private readonly Dictionary<string, UnityMaterial> _materials = new Dictionary<string, UnityMaterial>();
		private readonly HashSet<FuturePinballColliderKind> _reportedTessellatedColliderKinds = new HashSet<FuturePinballColliderKind>();
		private readonly FptImportReport _report = new FptImportReport();
		private string _tableAssetRoot;
		private string _bundleAssetRoot;
		private string _meshAssetRoot;
		private string _materialAssetRoot;
		private FuturePinballTable _table;
		private FuturePinballExtractionManifest _manifest;

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
				rootSource.ImportOutcome = "lossless-source-bundle-and-vpe-static-scene";

				var proceduralRoot = Child(playfield, "Procedural Geometry");
				var modelRoot = Child(playfield, "Model Instances");
				var placeholderRoot = Child(playfield, "Preserved Placeholders");
				var handled = new HashSet<int>();
				CreateProceduralElements(proceduralRoot, handled);
				CreateModelInstances(modelRoot, handled);
				CreatePlaceholders(placeholderRoot, handled);

				_report.SourceFile = Path.GetFileName(_sourcePath);
				_report.SourceSha256 = _manifest.SourceSha256;
				_report.FileVersion = _manifest.FileVersion;
				_report.Elements = _table.Elements.Count;
				_report.UnresolvedResources = _manifest.Counts.UnresolvedLinkedResources;
				_report.Warnings.AddRange(_manifest.Issues);
				if (_options.GenerateColliders) {
					_report.Warnings.Add("Generated collision uses VPE PlayfieldColliderComponent and PrimitiveColliderComponent; no Unity/PhysX colliders are created.");
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

		private void CreateProceduralElements(GameObject parent, ISet<int> handled)
		{
			var sources = _table.Elements.Where(item => item.SourceIndex.HasValue)
				.GroupBy(item => item.SourceIndex.Value)
				.ToDictionary(group => group.Key, group => group.First());
			foreach (var element in FuturePinballProceduralMeshBuilder.Build(_table)) {
				if (!sources.TryGetValue(element.SourceIndex, out var source)) {
					_report.Warnings.Add($"Procedural element '{element.Name}' has no matching source stream and was skipped.");
					continue;
				}
				var go = Child(parent, element.Name);
				AddSource(go, source, "native-procedural-static");
				for (var i = 0; i < element.Meshes.Count; i++) {
					var worldMesh = element.Meshes[i].Clone().TransformToWorld();
					CreateRenderer(go, source, worldMesh, element.Material, i, true, element.IsCollidable);
				}
				handled.Add(element.SourceIndex);
				_report.ProceduralElements++;
			}
		}

		private void CreateModelInstances(GameObject parent, ISet<int> handled)
		{
			if (!_options.ImportPrimaryModels) return;
			var referenced = new HashSet<string>(_table.Elements.Select(element => FuturePinballElementGeometry.Text(element, ModelTag))
				.Where(name => !string.IsNullOrWhiteSpace(name)), StringComparer.OrdinalIgnoreCase);
			var models = LoadModels(referenced);
			var surfaceHeights = SurfaceHeights();
			foreach (var element in _table.Elements) {
				var modelName = FuturePinballElementGeometry.Text(element, ModelTag);
				if (string.IsNullOrWhiteSpace(modelName) || !models.TryGetValue(modelName, out var model)) continue;
				var name = ElementName(element);
				var go = Child(parent, name);
				AddSource(go, element, "static-model");
				Place(go.transform, element, surfaceHeights);
				var elementMaterial = FuturePinballMaterialConverter.FromElement(element, TextureTag, ColorTag);
				var groups = model.Primary.CreateMeshes();
				for (var i = 0; i < groups.Count; i++) {
					var worldMesh = FuturePinballCoordinateConverter.ModelMeshToWorld(groups[i].Mesh);
					var material = groups[i].MaterialIndex >= 0 && groups[i].MaterialIndex < model.Primary.Materials.Count
						? FuturePinballMaterialConverter.FromMilkShape(model.Primary.Materials[groups[i].MaterialIndex])
						: elementMaterial;
					CreateRenderer(go, element, worldMesh, material, i, false, false, $"model-{model.Primary.SourceSha256}-{i}");
				}
				if (_options.GenerateColliders) CreateModelColliders(go, element, model);
				handled.Add(element.SourceIndex ?? -1);
				_report.ModelInstances++;
			}
		}

		private void CreatePlaceholders(GameObject parent, ISet<int> handled)
		{
			var surfaceHeights = SurfaceHeights();
			foreach (var element in _table.Elements.Where(element => !handled.Contains(element.SourceIndex ?? -1))) {
				var go = Child(parent, ElementName(element));
				AddSource(go, element, "preserved-placeholder");
				Place(go.transform, element, surfaceHeights);
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
			if (!string.IsNullOrWhiteSpace(source.Texture) && _textures.TryGetValue(source.Texture, out var texture)) {
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

		private void LoadTextures()
		{
			foreach (var resource in _manifest.Resources.Where(resource => resource.Category == "images")) {
				var file = resource.Files.FirstOrDefault(item => item.Role == "original");
				if (file == null) continue;
				var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{_bundleAssetRoot}/{file.Path}");
				if (texture == null) continue;
				AddTextureAlias(resource.LogicalName, texture);
				AddTextureAlias(Path.GetFileName(resource.LogicalName), texture);
				AddTextureAlias(Path.GetFileNameWithoutExtension(resource.LogicalName), texture);
			}
		}

		private void AddTextureAlias(string name, Texture2D texture)
		{
			if (!string.IsNullOrWhiteSpace(name)) _textures[name] = texture;
		}

		private Dictionary<string, float> SurfaceHeights()
		{
			return _table.Elements.Where(element => element.ElementType == FuturePinballElementType.Surface)
				.GroupBy(ElementName, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => FuturePinballElementGeometry.Float(group.First(), SurfaceTopHeightTag), StringComparer.OrdinalIgnoreCase);
		}

		private static void Place(Transform transform, FuturePinballSourceStream element, IReadOnlyDictionary<string, float> surfaceHeights)
		{
			var position = FuturePinballElementGeometry.Position(element);
			var surface = FuturePinballElementGeometry.Text(element, SurfaceTag);
			var height = !string.IsNullOrWhiteSpace(surface) && surfaceHeights.TryGetValue(surface, out var surfaceHeight) ? surfaceHeight : 0f;
			height += FuturePinballElementGeometry.Integer(element, HeightTag);
			var world = FuturePinballCoordinateConverter.ToWorld(position.X, position.Y, height);
			transform.localPosition = new Vector3(world.X, world.Y, world.Z);
			transform.localRotation = Quaternion.Euler(0f, -90f - FuturePinballElementGeometry.Integer(element, RotationTag), 0f);
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
