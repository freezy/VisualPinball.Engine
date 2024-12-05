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
using System.IO;
using System.Linq;
using System.Reflection;
using NLog;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;
using VisualPinball.Engine.VPT.MetalWireGuide;
using VisualPinball.Unity.Playfield;
using Light = VisualPinball.Engine.VPT.Light.Light;
using Logger = NLog.Logger;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using Object = UnityEngine.Object;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity.Editor
{
	public class VpxSceneConverter : ITextureProvider, IMaterialProvider, IMeshProvider
	{
		private readonly FileTableContainer _sourceContainer;
		private readonly Table _sourceTable;
		private readonly ConvertOptions _options;

		private Scene _tableScene;
		private GameObject _tableGo;
		private TableComponent _tableComponent;
		private PlayfieldComponent _playfieldComponent;

		private GameObject _playfieldGo;

		private string _assetsTableRoot;
		private string _assetsTextures;
		private string _assetsMaterials;
		private string _assetsPhysicsMaterials;
		private string _assetsMeshes;
		private string _assetsSounds;

		private readonly Dictionary<string, GameObject> _groupParents = new Dictionary<string, GameObject>();
		private readonly Dictionary<string, Texture> _textures = new Dictionary<string, Texture>();
		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
		private readonly Dictionary<string, PhysicsMaterialAsset> _physicalMaterials = new Dictionary<string, PhysicsMaterialAsset>();

		private readonly IPatcher _patcher;
		private bool _applyPatch = true;

		/// <summary>
		/// Creates a new converter for a new table
		/// </summary>
		/// <param name="sourceContainer">Source table container</param>
		/// <param name="fileName">File name of the file being imported</param>
		/// <param name="options">Optional convert options</param>
		public VpxSceneConverter(FileTableContainer sourceContainer, string fileName = "", ConvertOptions options = null)
		{
			_sourceContainer = sourceContainer;
			_sourceTable = sourceContainer.Table;
			_patcher = PatcherManager.GetPatcher();
			_patcher?.Set(sourceContainer, fileName, this, this);
			_options = options ?? new ConvertOptions();
		}

		/// <summary>
		/// Creates a converter based on an existing table in the scene.
		/// </summary>
		/// <param name="tableComponent">Existing component</param>
		public VpxSceneConverter(TableComponent tableComponent)
		{
			_options = new ConvertOptions();
			_tableGo = tableComponent.gameObject;
			var playfieldComponent = _tableGo.GetComponentInChildren<PlayfieldComponent>();
			if (!playfieldComponent) {
				throw new InvalidOperationException("Cannot find playfield hierarchy.");
			}
			_playfieldGo = playfieldComponent.gameObject;
			_tableComponent = tableComponent;
			_sourceTable = new Table(_tableComponent.TableContainer, new TableData());

			// get materials in scene
			var guids = AssetDatabase.FindAssets("t:Material");
			foreach (var guid in guids) {
				var assetPath = AssetDatabase.GUIDToAssetPath(guid);
				var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
				if (material != null) {
					_materials[material.name] = material;
				}
			}

			// get group parents in scene
			for (var i = 0; i < _playfieldGo.transform.childCount; i++) {
				var child = _playfieldGo.transform.GetChild(i);
				var childGo = child.gameObject;
				_groupParents[childGo.name] = childGo;
			}

			CreateFileHierarchy();
		}

		public GameObject Convert(bool applyPatch = true, string tableName = null)
		{
			_applyPatch = applyPatch;

			CreateRootHierarchy(tableName);
			CreateFileHierarchy();

			_tableComponent.LegacyContainer = ScriptableObject.CreateInstance<LegacyContainer>();

			ExtractPhysicsMaterials();
			ExtractTextures();
			ExtractSounds();
			SaveData();

			var prefabLookup = InstantiateGameItems();
			var componentLookup = UpdateGameItems(prefabLookup);

			SaveLegacyData(); // now we freed the binary data, write the remaining game items.
			FinalizeGameItems(componentLookup);

			FreeTextures();

			ConfigurePlayer(componentLookup);

			// patch
			if (_applyPatch) {
				_patcher?.PostPatch(_tableGo);
			}

			return MakeSubScene();
		}
		private void SaveData()
		{
			foreach (var key in _sourceContainer.TableInfo.Keys) {
				_tableComponent.TableInfo[key] = _sourceContainer.TableInfo[key];
			}
			_tableComponent.CustomInfoTags = _sourceContainer.CustomInfoTags;
			_tableComponent.Collections = _sourceContainer.Collections;
		}

		private void SaveLegacyData()
		{
			_tableComponent.LegacyContainer.TableData = _sourceContainer.Table.Data;
			_sourceContainer.WriteDataToDict<Bumper, BumperData>(_tableComponent.LegacyContainer.Bumpers);
			_sourceContainer.WriteDataToDict<Flipper, FlipperData>(_tableComponent.LegacyContainer.Flippers);
			_sourceContainer.WriteDataToDict<Gate, GateData>(_tableComponent.LegacyContainer.Gates);
			_sourceContainer.WriteDataToDict<HitTarget, HitTargetData>(_tableComponent.LegacyContainer.HitTargets);
			_sourceContainer.WriteDataToDict<Kicker, KickerData>(_tableComponent.LegacyContainer.Kickers);
			_sourceContainer.WriteDataToDict<Light, LightData>(_tableComponent.LegacyContainer.Lights);
			_sourceContainer.WriteDataToDict<Plunger, PlungerData>(_tableComponent.LegacyContainer.Plungers);
			_sourceContainer.WriteDataToDict<Primitive, PrimitiveData>(_tableComponent.LegacyContainer.Primitives);
			_sourceContainer.WriteDataToDict<Ramp, RampData>(_tableComponent.LegacyContainer.Ramps);
			_sourceContainer.WriteDataToDict<Rubber, RubberData>(_tableComponent.LegacyContainer.Rubbers);
			_sourceContainer.WriteDataToDict<Spinner, SpinnerData>(_tableComponent.LegacyContainer.Spinners);
			_sourceContainer.WriteDataToDict<Surface, SurfaceData>(_tableComponent.LegacyContainer.Surfaces);
			_sourceContainer.WriteDataToDict<Trigger, TriggerData>(_tableComponent.LegacyContainer.Triggers);
			_sourceContainer.WriteDataToDict<Bumper, BumperData>(_tableComponent.LegacyContainer.Bumpers);
			_sourceContainer.WriteDataToDict<MetalWireGuide, MetalWireGuideData>(_tableComponent.LegacyContainer.MetalWireGuides);

			_tableComponent.LegacyContainer.Decals = _sourceContainer.GetAllData<Decal, DecalData>();
			_tableComponent.LegacyContainer.DispReels = _sourceContainer.GetAllData<DispReel, DispReelData>();
			_tableComponent.LegacyContainer.Flashers = _sourceContainer.GetAllData<Flasher, FlasherData>();
			_tableComponent.LegacyContainer.LightSeqs = _sourceContainer.GetAllData<LightSeq, LightSeqData>();
			_tableComponent.LegacyContainer.TextBoxes = _sourceContainer.GetAllData<TextBox, TextBoxData>();
			_tableComponent.LegacyContainer.Timers = _sourceContainer.GetAllData<Timer, TimerData>();

			var path = Path.Combine(_assetsTableRoot, "Legacy Data.asset");
			if (File.Exists(path)) {
				File.Delete(path);
			}
			AssetDatabase.CreateAsset(_tableComponent.LegacyContainer, path);
		}

		/// <summary>
		/// This instantiates all games items from prefabs into the scene and copies the data into the
		/// components.
		/// </summary>
		/// <returns>A dictionary with lower-case names as key, and created prefabs as values.</returns>
		private Dictionary<string, IVpxPrefab> InstantiateGameItems()
		{
			var prefabLookup = new Dictionary<string, IVpxPrefab>();
			var renderables = _sourceContainer.Renderables.ToArray();

			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var renderable in renderables) {

					// create object(s)
					var prefab = InstantiateAndParentPrefab(renderable);
					prefab.SetData();
					prefabLookup[renderable.Name.ToLower()] = prefab;
				}

			} finally {
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			return prefabLookup;
		}

		/// <summary>
		/// In a second pass, we update the referenced data. This is so states dependent on other components
		/// is correctly applied.
		/// </summary>
		/// <param name="prefabLookup">A dictionary with lower-case names as key, and created prefabs as values.</param>
		private Dictionary<string, IMainComponent> UpdateGameItems(Dictionary<string, IVpxPrefab> prefabLookup)
		{
			var componentLookup = prefabLookup.ToDictionary(x => x.Key, x => x.Value.MainComponent);
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				var fxbExporter = new FxbExporter();

				// first loop: write fbx files
				foreach (var prefab in prefabLookup.Values) {
					prefab.SetReferencedData(_sourceTable, this, this, componentLookup);
					prefab.FreeBinaryData();

					if (prefab.ExtractMesh) {
						var meshFilename = $"{prefab.GameObject.name.ToFilename()}.fbx";
						var meshPath = Path.Combine(_assetsMeshes, meshFilename);
						if (_options.SkipExistingMeshes && File.Exists(meshPath)) {
							continue;
						}
						if (File.Exists(meshPath)) {
							AssetDatabase.DeleteAsset(meshPath);
						}

						fxbExporter.Export(prefab.GameObject, meshPath, !prefab.SkipParenting);
					}
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// second loop: assign them to the game object.
			foreach (var prefab in prefabLookup.Values) {

				if (prefab.ExtractMesh) {
					var meshFilename = $"{prefab.GameObject.name.ToFilename()}.fbx";
					var meshPath = Path.Combine(_assetsMeshes, meshFilename);
					var mf = prefab.GameObject.GetComponent<MeshFilter>();
					mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
				}

				// patch
				if (_applyPatch) {
					_patcher?.ApplyPatches(prefab.GameObject, _tableGo);
					if (prefab.GameObject) { // only if not destroyed..
						prefab.UpdateTransforms();
					}
				}

				// persist changes
				if (prefab.GameObject) { // only if not destroyed..
					prefab.PersistData();
				}
			}

			return componentLookup;
		}

		private void FinalizeGameItems(Dictionary<string, IMainComponent> componentLookup)
		{
			// convert non-renderables
			foreach (var item in _sourceContainer.NonRenderables) {
				var prefab = InstantiateAndParentPrefab(item);
				prefab.SetData();
				prefab.SetReferencedData(_sourceTable, this, this, componentLookup);
				prefab.FreeBinaryData();
			}

			// the playfield needs separate treatment
			_playfieldComponent.SetReferencedData(_sourceTable.Data, _sourceTable, this, this, null);

			// yes, really, persist changes..
			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
		}

		internal IVpxPrefab InstantiateAndPersistPrefab(IItem item, Dictionary<string, IMainComponent> components = null)
		{
			var prefab = InstantiateAndParentPrefab(item);
			prefab.SetData();
			prefab.SetReferencedData(_sourceTable, this, this, components);
			prefab.PersistData();
			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

			return prefab;
		}

		private IVpxPrefab InstantiateAndParentPrefab(IItem item)
		{
			var prefab = InstantiatePrefab(item);

			if (prefab == null)	{
				throw new Exception($"Could not instantiate prefab for item {item.Name} of type {item.GetType()}.");
			}

			if (prefab.SkipParenting) {
				return prefab;
			}

			// parent to group
			var parentGo = GetGroupParent(item);
			prefab.GameObject.transform.SetParent(parentGo.transform, false);

			return prefab;
		}

		private IVpxPrefab InstantiatePrefab(IItem item)
		{
			switch (item) {
				case Bumper bumper:                  return bumper.InstantiatePrefab();
				case Flipper flipper:                return flipper.InstantiatePrefab();
				case Gate gate:                      return gate.InstantiatePrefab();
				case HitTarget hitTarget:            return hitTarget.InstantiatePrefab();
				case Kicker kicker:                  return kicker.InstantiatePrefab();
				case Light lt:                       return lt.InstantiatePrefab(_sourceTable);
				case Plunger plunger:                return plunger.InstantiatePrefab();
				case Primitive primitive:            return primitive.InstantiatePrefab(_playfieldGo);
				case Ramp ramp:                      return ramp.InstantiatePrefab();
				case Rubber rubber:                  return rubber.InstantiatePrefab();
				case Spinner spinner:                return spinner.InstantiatePrefab();
				case Surface surface:                return surface.InstantiatePrefab();
				case Trigger trigger:                return trigger.InstantiatePrefab();
				case Trough trough:                  return trough.InstantiatePrefab();
				case MetalWireGuide metalWireGuide : return metalWireGuide.InstantiatePrefab();
			}

			throw new InvalidOperationException("Unknown item " + item + " to setup!");
		}

		private void ExtractPhysicsMaterials()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var material in _sourceTable.Data.Materials) {

					// skip material if physics aren't set.
					if (material.Elasticity == 0 && material.ElasticityFalloff == 0 && material.ScatterAngle == 0 && material.Friction == 0) {
						continue;
					}
					SavePhysicsMaterial(material);
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			foreach (var material in _sourceTable.Data.Materials) {
				_physicalMaterials[material.Name] = AssetDatabase.LoadAssetAtPath<PhysicsMaterialAsset>($"{_assetsPhysicsMaterials}/{material.Name}.asset");
			}
		}

		private string SavePhysicsMaterial(Engine.VPT.Material material)
		{
			var path = $"{_assetsPhysicsMaterials}/{material.Name}.asset";
			if (_options.SkipExistingMaterials && File.Exists(path)) {
				return path;
			}

			var mat = ScriptableObject.CreateInstance<PhysicsMaterialAsset>();
			mat.Elasticity = material.Elasticity;
			mat.ElasticityFalloff = material.ElasticityFalloff;
			mat.ScatterAngle = material.ScatterAngle;
			mat.Friction = material.Friction;
			AssetDatabase.CreateAsset(mat, path);

			return path;
		}

		private void ExtractTextures()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var texture in _sourceContainer.Textures) {
					texture.WriteAsAsset(_assetsTextures, _options.SkipExistingTextures);
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// now they are in the asset database, we can load them.
			foreach (var texture in _sourceContainer.Textures) {
				var path = texture.GetUnityFilename(_assetsTextures, texture.IsWebp ? ".png" : null);
				var unityTexture = texture.IsHdr
					? (Texture)AssetDatabase.LoadAssetAtPath<Cubemap>(path) ?? AssetDatabase.LoadAssetAtPath<Texture2D>(path)
					: AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				_textures[texture.Name.ToLower()] = unityTexture;
				var legacyTexture = new LegacyTexture(texture.Data, unityTexture);
				if (texture.IsWebp) {
					legacyTexture.OriginalPath = texture.GetUnityFilename(_assetsTextures);
				}
				_tableComponent.LegacyContainer.Textures.Add(legacyTexture);
			}
		}

		private void FreeTextures()
		{
			foreach (var texture in _sourceContainer.Textures) {
				texture.Data.FreeBinaryData();
			}
		}

		private void ExtractSounds()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var sound in _sourceContainer.Sounds) {
					var path = sound.GetUnityFilename(_assetsSounds);
					if (_options.SkipExistingSounds && File.Exists(path)) {
						continue;
					}
					File.WriteAllBytes(path, sound.Data.GetFileData());
					sound.Data.Path = path;
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// now they are in the asset database, we can load them.
			foreach (var sound in _sourceContainer.Sounds) {
				var unitySound = AssetDatabase.LoadAssetAtPath<AudioClip>(sound.GetUnityFilename(_assetsSounds));
				_tableComponent.LegacyContainer.Sounds.Add(new LegacySound(sound.Data, unitySound));
			}
		}

		private void ConfigurePlayer(Dictionary<string, IMainComponent> components)
		{
			// add the player script and default game engine
			_tableGo.AddComponent<Player>();
			_tableGo.AddComponent<BallRollerComponent>();
			var dga = _tableGo.AddComponent<DefaultGamelogicEngine>();

			// add trough if none available
			if (!_sourceContainer.HasTrough) {
				CreateTrough(components);
			}

			// populate hardware
			_tableComponent.RepopulateHardware(dga);
		}

		private void CreateTrough(Dictionary<string, IMainComponent> components)
		{
			var troughData = new TroughData("Trough") {
				BallCount = 4,
				SwitchCount = 4,
				Type = TroughType.ModernMech
			};
			if (_sourceContainer.Has<Kicker>("BallRelease")) {
				troughData.PlayfieldExitKicker = "BallRelease";
			}
			if (_sourceContainer.Has<Kicker>("Drain")) {
				troughData.PlayfieldEntrySwitch = "Drain";
			}
			var item = new Trough(troughData) {
				StorageIndex = _sourceContainer.ItemDatas.Count()
			};

			InstantiateAndPersistPrefab(item, components);
		}

		private void CreateFileHierarchy()
		{
			if (!Directory.Exists("Assets/Tables/")) {
				Directory.CreateDirectory("Assets/Tables/");
			}

			_assetsTableRoot = $"Assets/Tables/{_tableGo.name}/";
			if (!Directory.Exists(_assetsTableRoot)) {
				Directory.CreateDirectory(_assetsTableRoot);
			}

			_assetsTextures = $"{_assetsTableRoot}Textures/";
			if (!Directory.Exists(_assetsTextures)) {
				Directory.CreateDirectory(_assetsTextures);
			}

			_assetsMaterials = $"{_assetsTableRoot}Materials/";
			if (!Directory.Exists(_assetsMaterials)) {
				Directory.CreateDirectory(_assetsMaterials);
			}

			_assetsPhysicsMaterials = $"{_assetsTableRoot}Physics Materials/";
			if (!Directory.Exists(_assetsPhysicsMaterials)) {
				Directory.CreateDirectory(_assetsPhysicsMaterials);
			}

			_assetsMeshes = $"{_assetsTableRoot}Meshes/";
			if (!Directory.Exists(_assetsMeshes)) {
				Directory.CreateDirectory(_assetsMeshes);
			}

			_assetsSounds = $"{_assetsTableRoot}Sounds/";
			if (!Directory.Exists(_assetsSounds)) {
				Directory.CreateDirectory(_assetsSounds);
			}
		}

		private void CreateRootHierarchy(string tableName = null)
		{
			// set the GameObject name; this needs to happen after MakeSerializable because the name is set there as well
			if (string.IsNullOrEmpty(tableName)) {
				tableName = _sourceTable.Name;

			} else {
				tableName = tableName
					.Replace("%TABLENAME%", _sourceTable.Name)
					.Replace("%INFONAME%", _sourceContainer.InfoName);
			}

			// 1. create game object hierarchy
			_tableGo = new GameObject(tableName);
			_playfieldGo = new GameObject("Playfield");
			var backglassGo = new GameObject("Backglass");
			var cabinetGo = new GameObject("Cabinet");

			_tableComponent = _tableGo.AddComponent<TableComponent>();
			_tableComponent.SetData(_sourceTable.Data);

			_playfieldGo.transform.SetParent(_tableGo.transform, false);
			backglassGo.transform.SetParent(_tableGo.transform, false);
			cabinetGo.transform.SetParent(_tableGo.transform, false);

			// 2. add components
			_playfieldGo.AddComponent<PhysicsEngine>();
			_playfieldComponent = _playfieldGo.AddComponent<PlayfieldComponent>();
			_playfieldGo.AddComponent<PlayfieldColliderComponent>();
			_playfieldGo.AddComponent<PlayfieldMeshComponent>();
			_playfieldGo.AddComponent<MeshFilter>();
			_playfieldComponent.SetData(_sourceTable.Data);
		}
		
		private GameObject MakeSubScene()
		{
			// var sceneName = _tableScene.name;
			// var scenePath = GetScenePath(sceneName);
			// EditorSceneManager.SaveScene(_tableScene, scenePath);
			// EditorSceneManager.CloseScene(_tableScene, true);
			//
			// // link table scene as sub scene 
			// var subSceneGo = new GameObject(sceneName);
			// var subSceneMb = subSceneGo.AddComponent<SubScene>();
			// var subSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
			// subSceneMb.SceneAsset = subSceneAsset;
			//
			// return subSceneGo;

			return _tableGo;
		}

		private static string GetScenePath(string tableName)
		{
			const string sceneRoot = "Assets/Tables/";
			var i = -1;
			do {
				var suffix = i >= 0 ? "." + i.ToString("D3") : "";
				var scenePath = $"{sceneRoot}{tableName}{suffix}.unity";
				if (!File.Exists(scenePath)) {
					return scenePath;
				}
				i++;
			} while (i < 999);

			return null;
		}

		private GameObject GetGroupParent(IItem item) => GetGroupParent(item.ItemGroupName);

		public GameObject GetGroupParent(string name)
		{
			// create group parent if not created (if null, attach it to the table directly).
			if (!string.IsNullOrEmpty(name)) {
				if (!_groupParents.ContainsKey(name)) {
					var parent = new GameObject(name);
					parent.transform.SetParent(_playfieldGo.transform, false);
					_groupParents[name] = parent;
				}
			}
			var groupParent = !string.IsNullOrEmpty(name)
				? _groupParents[name]
				: _playfieldGo;

			return groupParent;
		}

		#region IMeshProvider

		public bool HasMesh(string parentName, string name) => File.Exists(GetMeshPath(parentName, name));

		public Mesh GetMesh(string parentName, string name) => AssetDatabase.LoadAssetAtPath<Mesh>(GetMeshPath(parentName, name));

		private string GetMeshPath(string parentName, string name)
		{
			var filename = parentName == name
				? $"{parentName.ToFilename()}.fbx"
				: $"{parentName.ToFilename()} ({name.ToFilename()}).fbx";
			return Path.Combine(_assetsMeshes, filename);
		}

		#endregion

		#region ITextureProvider

		public Texture GetTexture(string name)
		{
			if (!_textures.ContainsKey(name.ToLower())) {
				throw new ArgumentException($"Texture \"{name.ToLower()}\" not loaded!");
			}
			return _textures[name.ToLower()];
		}

		#endregion

		#region IMaterialProvider

		public bool HasMaterial(PbrMaterial material)
		{
			if (_materials.ContainsKey(material.Id)) {
				return true;
			}
			var path = material.GetUnityFilename(_assetsMaterials);
			if (File.Exists(path)) {
				_materials[material.Id] = AssetDatabase.LoadAssetAtPath<Material>(path);
				return true;
			}
			return false;
		}

		public Material GetMaterial(PbrMaterial material)
		{
			if (_materials.ContainsKey(material.Id)) {
				return _materials[material.Id];
			}
			var path = material.GetUnityFilename(_assetsMaterials);
			if (File.Exists(path)) {
				_materials[material.Id] = AssetDatabase.LoadAssetAtPath<Material>(path);
				return _materials[material.Id];
			}
			return null;
		}
		public PhysicsMaterialAsset GetPhysicsMaterial(string name)
		{
			if (string.IsNullOrEmpty(name)) {
				return null;
			}
			if (_physicalMaterials.ContainsKey(name)) {
				return _physicalMaterials[name];
			}

			var material = _sourceContainer.Table.Data.Materials
				.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.CurrentCultureIgnoreCase));
			if (material != null) {
				var path = SavePhysicsMaterial(material);
				_physicalMaterials[material.Name] = AssetDatabase.LoadAssetAtPath<PhysicsMaterialAsset>(path);
				return _physicalMaterials[material.Name];
			}

			return null;
		}

		public Material MergeMaterials(string vpxMaterial, Material textureMaterial)
		{
			var pbrMaterial = new PbrMaterial(_sourceTable.GetMaterial(vpxMaterial), id: $"{vpxMaterial.ToNormalizedName()} __textured");
			return pbrMaterial.ToUnityMaterial(this, textureMaterial);
		}

		public void SaveMaterial(PbrMaterial vpxMaterial, Material material)
		{
			_materials[vpxMaterial.Id] = material;
			var path = vpxMaterial.GetUnityFilename(_assetsMaterials);
			if (_options.SkipExistingMaterials && File.Exists(path)) {
				return;
			}
			AssetDatabase.CreateAsset(material, path);
		}

		#endregion
	}

	public class ConvertOptions
	{
		public bool SkipExistingTextures = true;
		public bool SkipExistingSounds = true;
		public bool SkipExistingMaterials = true;
		public bool SkipExistingMeshes = true;

		public static readonly ConvertOptions SkipNone = new ConvertOptions
		{
			SkipExistingMaterials = false,
			SkipExistingMeshes = false,
			SkipExistingSounds = false,
			SkipExistingTextures = false
		};
	}

	public class FxbExporter
	{
		private readonly MethodInfo _exportObject;
		private readonly object _optionsValue;
		
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public FxbExporter()
		{
			// thanks unity for not letting me pass the options to ModelExporter.ExportObject().
			var modelExporter = typeof(ModelExporter);
			var optionsProp = modelExporter.GetProperty("DefaultOptions", BindingFlags.Static | BindingFlags.NonPublic);
			_optionsValue = optionsProp!.GetValue(null, null);
			var optionsType = _optionsValue.GetType();
			var exportFormatField = optionsType.BaseType!.GetField("exportFormat", BindingFlags.Instance | BindingFlags.NonPublic);
			exportFormatField!.SetValue(_optionsValue, 1); // set to binary
			_exportObject = modelExporter.GetMethod("ExportObjects", BindingFlags.NonPublic | BindingFlags.Static);
		}

		public void Export(GameObject go, string path, bool includeChildren = true)
		{
			var objectToExport = go;
			if (!includeChildren) {
				
				// create a new gameobject and copy the original's mf and mr
				var mfSrc = go.GetComponent<MeshFilter>();
				var mrSrc = go.GetComponent<MeshRenderer>();
				if (mfSrc && mrSrc) {
					objectToExport = new GameObject(go.name);
					var mfDest = objectToExport.AddComponent<MeshFilter>();
					var mrDest = objectToExport.AddComponent<MeshRenderer>();

					if (UnityEditorInternal.ComponentUtility.CopyComponent(mfSrc)) {
						UnityEditorInternal.ComponentUtility.PasteComponentValues(mfDest);
					}
					if (UnityEditorInternal.ComponentUtility.CopyComponent(mrSrc)) {
						UnityEditorInternal.ComponentUtility.PasteComponentValues(mrDest);
					}
				} else {
					Logger.Error($"Cannot retrieve mesh filter or renderer from game object {go.name}.");
					objectToExport = go;
				}
			}
			
			// export via reflection, because we need binary.
			_exportObject!.Invoke(null, new[] { path, new Object[] {objectToExport}, _optionsValue, null });

			if (!includeChildren) {
				Object.DestroyImmediate(objectToExport);
			}
		}

		public void Export(Mesh mesh, string path)
		{
			var go = new GameObject(); 
			go.AddComponent<MeshRenderer>();
			var mf = go.AddComponent<MeshFilter>();

			mf.sharedMesh = mesh;
			Export(go, path);
			
			Object.DestroyImmediate(go);
		}
	}
}
