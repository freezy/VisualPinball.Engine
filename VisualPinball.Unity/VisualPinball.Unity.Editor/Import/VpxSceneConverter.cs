﻿// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using NLog;
using UnityEditor;
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
using VisualPinball.Engine.VPT.Mappings;
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
using Light = VisualPinball.Engine.VPT.Light.Light;
using Logger = NLog.Logger;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity.Editor
{
	public class VpxSceneConverter : ITextureProvider, IMaterialProvider, IMeshProvider
	{
		private readonly FileTableContainer _tableContainer;
		private readonly Table _table;
		private readonly ConvertOptions _options;

		private GameObject _tableGo;
		private TableAuthoring _tableAuthoring;

		private GameObject _playfieldGo;

		private string _assetsTableRoot;
		private string _assetsPrefabs;
		private string _assetsTextures;
		private string _assetsMaterials;
		private string _assetsPhysicsMaterials;
		private string _assetsMeshes;
		private string _assetsSounds;

		private readonly Dictionary<string, GameObject> _groupParents = new Dictionary<string, GameObject>();
		private readonly Dictionary<string, Texture> _textures = new Dictionary<string, Texture>();
		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
		private readonly Dictionary<string, PhysicsMaterial> _physicalMaterials = new Dictionary<string, PhysicsMaterial>();

		private readonly IPatcher _patcher;
		private bool _applyPatch = true;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Creates a new converter for a new table
		/// </summary>
		/// <param name="tableContainer">Source table container</param>
		/// <param name="fileName">File name of the file being imported</param>
		/// <param name="options">Optional convert options</param>
		public VpxSceneConverter(FileTableContainer tableContainer, string fileName = "", ConvertOptions options = null)
		{
			_tableContainer = tableContainer;
			_table = tableContainer.Table;
			_patcher = PatcherManager.GetPatcher();
			_patcher?.Set(tableContainer, fileName);
			_options = options ?? new ConvertOptions();
		}

		/// <summary>
		/// Creates a converter based on an existing table in the scene.
		/// </summary>
		/// <param name="tableAuthoring">Existing component</param>
		public VpxSceneConverter(TableAuthoring tableAuthoring)
		{
			_options = new ConvertOptions();
			_tableGo = tableAuthoring.gameObject;
			var tablePlayfieldAuthoring = _tableGo.GetComponentInChildren<TablePlayfieldAuthoring>();
			if (!tablePlayfieldAuthoring) {
				throw new InvalidOperationException("Cannot find playfield hierarchy.");
			}
			_playfieldGo = tablePlayfieldAuthoring.gameObject;
			_tableAuthoring = tableAuthoring;
			_table = tableAuthoring.Table;

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

			_tableAuthoring.LegacyContainer = ScriptableObject.CreateInstance<LegacyContainer>();

			ExtractPhysicsMaterials();
			ExtractTextures();
			ExtractSounds();
			SaveData();

			ConvertGameItems();

			FreeTextures();
			SaveLegacyData();

			ConfigurePlayer();

			return _tableGo;
		}

		private void SaveData()
		{
			_tableAuthoring.Mappings = _tableContainer.Mappings.Data;
			foreach (var key in _tableContainer.TableInfo.Keys) {
				_tableAuthoring.TableInfo[key] = _tableContainer.TableInfo[key];
			}
			_tableAuthoring.CustomInfoTags = _tableContainer.CustomInfoTags;
			_tableAuthoring.Collections = _tableContainer.Collections;
		}

		private void SaveLegacyData()
		{
			_tableContainer.WriteDataToDict<Bumper, BumperData>(_tableAuthoring.LegacyContainer.Bumpers);
			_tableContainer.WriteDataToDict<Flipper, FlipperData>(_tableAuthoring.LegacyContainer.Flippers);
			_tableContainer.WriteDataToDict<Gate, GateData>(_tableAuthoring.LegacyContainer.Gates);
			_tableContainer.WriteDataToDict<HitTarget, HitTargetData>(_tableAuthoring.LegacyContainer.HitTargets);
			_tableContainer.WriteDataToDict<Kicker, KickerData>(_tableAuthoring.LegacyContainer.Kickers);
			_tableContainer.WriteDataToDict<Light, LightData>(_tableAuthoring.LegacyContainer.Lights);
			_tableContainer.WriteDataToDict<Plunger, PlungerData>(_tableAuthoring.LegacyContainer.Plungers);
			_tableContainer.WriteDataToDict<Primitive, PrimitiveData>(_tableAuthoring.LegacyContainer.Primitives);
			_tableContainer.WriteDataToDict<Ramp, RampData>(_tableAuthoring.LegacyContainer.Ramps);
			_tableContainer.WriteDataToDict<Rubber, RubberData>(_tableAuthoring.LegacyContainer.Rubbers);
			_tableContainer.WriteDataToDict<Spinner, SpinnerData>(_tableAuthoring.LegacyContainer.Spinners);
			_tableContainer.WriteDataToDict<Surface, SurfaceData>(_tableAuthoring.LegacyContainer.Surfaces);
			_tableContainer.WriteDataToDict<Trigger, TriggerData>(_tableAuthoring.LegacyContainer.Triggers);
			_tableContainer.WriteDataToDict<Bumper, BumperData>(_tableAuthoring.LegacyContainer.Bumpers);

			_tableAuthoring.LegacyContainer.Decals = _tableContainer.GetAllData<Decal, DecalData>();
			_tableAuthoring.LegacyContainer.DispReels = _tableContainer.GetAllData<DispReel, DispReelData>();
			_tableAuthoring.LegacyContainer.Flashers = _tableContainer.GetAllData<Flasher, FlasherData>();
			_tableAuthoring.LegacyContainer.LightSeqs = _tableContainer.GetAllData<LightSeq, LightSeqData>();
			_tableAuthoring.LegacyContainer.TextBoxes = _tableContainer.GetAllData<TextBox, TextBoxData>();
			_tableAuthoring.LegacyContainer.Timers = _tableContainer.GetAllData<Timer, TimerData>();

			var path = Path.Combine(_assetsTableRoot, "Legacy Data.asset");
			if (File.Exists(path)) {
				File.Delete(path);
			}
			AssetDatabase.CreateAsset(_tableAuthoring.LegacyContainer, path);
		}

		private void ConvertGameItems()
		{
			var convertedItems = new Dictionary<string, IConvertedItem>();
			var renderableLookup = new Dictionary<string, IRenderable>();
			var renderables = _tableContainer.Renderables
				.OrderBy(renderable => renderable.SubComponent)
				.ToArray();

			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var renderable in renderables) {

					if (_applyPatch) {
						_patcher?.ApplyPrePatches(renderable);
					}

					var lookupName = renderable.Name.ToLower();
					renderableLookup[lookupName] = renderable;

					if (renderable.SubComponent == ItemSubComponent.None) {
						// create object(s)
						convertedItems[lookupName] = CreateGameObjects(renderable);

					} else {
						// if the object's names was parsed to be part of another object, re-link to other object.
						var parentName = renderable.ComponentName.ToLower();
						if (convertedItems.ContainsKey(parentName)) {
							var parent = convertedItems[parentName];

							var convertedItem = CreateGameObjects(renderable);
							if (convertedItem.IsValidChild(parent)) {

								if (convertedItem.MeshAuthoring.Any()) {

									// move and rotate into parent
									if (parent.MainAuthoring.IItem is IRenderable parentRenderable) {
										renderable.Position -= parentRenderable.Position;
										renderable.RotationY -= parentRenderable.RotationY;
									}

									parent.DestroyMeshComponents();
								}
								if (convertedItem.ColliderAuthoring != null) {
									parent.DestroyColliderComponent();
								}
								convertedItem.MainAuthoring.gameObject.transform.SetParent(parent.MainAuthoring.gameObject.transform, false);
								convertedItems[lookupName] = convertedItem;

							} else {

								renderable.DisableSubComponent();

								// invalid parenting, re-convert the item, because it returned only the sub component.
								convertedItems[lookupName] = CreateGameObjects(renderable);

								// ..and destroy the other one
								convertedItem.Destroy();
							}

						} else {
							Logger.Warn($"Cannot find component \"{parentName}\" that is supposed to be the parent of \"{renderable.Name}\".");
						}
					}
				}

			} finally {
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// now we have all renderables imported, set data and patch
			var dataDict = _tableContainer.SupportedDatas;
			var componentDict = convertedItems
				.ToDictionary(x => x.Key, x => x.Value.MainAuthoring);
			IEnumerable<MonoBehaviour> updatedComponents = null;
			foreach (var renderable in renderables) {

				var lookupName = renderable.Name.ToLower();
				if (!convertedItems.ContainsKey(lookupName) || convertedItems[lookupName] == null) {
					continue;
				}
				var convertedItem = convertedItems[lookupName];

				// set data
				if (dataDict.ContainsKey(lookupName)) {
					updatedComponents = convertedItem.SetData(dataDict[lookupName], this, this, componentDict);
					dataDict[lookupName].FreeBinaryData();

				} else {
					Debug.LogError($"Could not find data of {lookupName} to apply to game object.");
				}

				// patch
				if (_applyPatch) {
					foreach (var meshMb in convertedItem.MeshAuthoring) {
						_patcher?.ApplyPatches(renderableLookup[lookupName], meshMb.gameObject, _tableGo);
					}
				}

				// persist changes
				EditorUtility.SetDirty(convertedItem.GameObject);
				if (updatedComponents != null) {
					PrefabUtility.RecordPrefabInstancePropertyModifications(convertedItem.GameObject.transform);
					foreach (var updatedComponent in updatedComponents) {
						PrefabUtility.RecordPrefabInstancePropertyModifications(updatedComponent);
					}
				}
			}

			// finally, convert non-renderables
			foreach (var item in _tableContainer.NonRenderables) {

				// create object(s)
				CreateGameObjects(item);
			}

			// yes, really persist changes..
			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
		}

		public IConvertedItem CreateGameObjects(IItem item)
		{
			var convertedItem = InstantiateGameObject(item);
			var parentGo = GetGroupParent(item);

			convertedItem.GameObject.transform.SetParent(parentGo.transform, false);

			// apply transformation
			if (item is IRenderable renderable) {
				//convertedItem.MainAuthoring
				// todo can probably remove that, it's in setData already..
				convertedItem.GameObject.transform.SetFromMatrix(renderable.TransformationMatrix(_table, Origin.Original).ToUnityMatrix());
			}
			return convertedItem;
		}

		private IConvertedItem InstantiateGameObject(IItem item)
		{
			switch (item) {
				case Bumper bumper:       return bumper.InstantiateGameObject(item, this);
				case Flipper flipper:     return flipper.InstantiateGameObject(item, this);
				case Gate gate:           return gate.InstantiateGameObject(item, this);
				case HitTarget hitTarget: return hitTarget.InstantiateGameObject(item, this);
				case Kicker kicker:       return kicker.InstantiateGameObject(item, this);
				case Light lt:            return lt.InstantiateGameObject(item);
				case Plunger plunger:     return plunger.InstantiateGameObject(item, this);
				case Primitive primitive: return primitive.InstantiateGameObject(item, this);
				case Ramp ramp:           return ramp.InstantiateGameObject(item, this);
				case Rubber rubber:       return rubber.InstantiateGameObject(item, this);
				case Spinner spinner:     return spinner.InstantiateGameObject(item, this);
				case Surface surface:     return surface.InstantiateGameObject(item, this);
				case Table table:         return table.InstantiateGameObject(item, this);
				case Trigger trigger:     return trigger.InstantiateGameObject(item, this);
				case Trough trough:       return trough.InstantiateGameObject(item);
			}

			throw new InvalidOperationException("Unknown item " + item + " to setup!");
		}

		private void ExtractPhysicsMaterials()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var material in _table.Data.Materials) {

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

			foreach (var material in _table.Data.Materials) {
				_physicalMaterials[material.Name] = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{_assetsPhysicsMaterials}/{material.Name}.asset");
			}
		}

		private string SavePhysicsMaterial(Engine.VPT.Material material)
		{
			var path = $"{_assetsPhysicsMaterials}/{material.Name}.asset";
			if (_options.SkipExistingMaterials && File.Exists(path)) {
				return path;
			}

			var mat = ScriptableObject.CreateInstance<PhysicsMaterial>();
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

				foreach (var texture in _tableContainer.Textures) {
					texture.WriteAsAsset(_assetsTextures, _options.SkipExistingTextures);
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// now they are in the asset database, we can load them.
			foreach (var texture in _tableContainer.Textures) {
				var path = texture.GetUnityFilename(_assetsTextures, texture.IsWebp ? ".png" : null);
				var unityTexture = texture.IsHdr
					? (Texture)AssetDatabase.LoadAssetAtPath<Cubemap>(path) ?? AssetDatabase.LoadAssetAtPath<Texture2D>(path)
					: AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				_textures[texture.Name.ToLower()] = unityTexture;
				var legacyTexture = new LegacyTexture(texture.Data, unityTexture);
				if (texture.IsWebp) {
					legacyTexture.OriginalPath = texture.GetUnityFilename(_assetsTextures);
				}
				_tableAuthoring.LegacyContainer.Textures.Add(legacyTexture);
			}

			// todo lazy load and don't import local textures once they are in the prefabs
			foreach (var texture in Engine.VPT.Texture.LocalTextures) {
				var path = texture.GetUnityFilename(_assetsTextures);
				var unityTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				_textures[texture.Name.ToLower()] = unityTexture;
			}
		}

		private void FreeTextures()
		{
			foreach (var texture in _tableContainer.Textures) {
				texture.Data.FreeBinaryData();
			}
		}

		private void ExtractSounds()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var sound in _tableContainer.Sounds) {
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
			foreach (var sound in _tableContainer.Sounds) {
				var unitySound = AssetDatabase.LoadAssetAtPath<AudioClip>(sound.GetUnityFilename(_assetsSounds));
				_tableAuthoring.LegacyContainer.Sounds.Add(new LegacySound(sound.Data, unitySound));
			}
		}

		private void ConfigurePlayer()
		{
			// add the player script and default game engine
			_tableGo.AddComponent<Player>();
			var dga = _tableGo.AddComponent<DefaultGamelogicEngine>();

			// add trough if none available
			if (!_tableContainer.HasTrough) {
				CreateTrough();
			}

			// populate mappings
			if (_tableContainer.Mappings.IsEmpty()) {
				_tableContainer.Mappings.PopulateSwitches(dga.AvailableSwitches, _tableContainer.Switchables, _tableContainer.SwitchableDevices);
				_tableContainer.Mappings.PopulateCoils(dga.AvailableCoils, _tableContainer.Coilables, _tableContainer.CoilableDevices);

				// wire up plunger
				var plunger = _tableContainer.Plunger();
				if (plunger != null) {
					_tableContainer.Mappings.Data.AddWire(new MappingsWireData {
						Description = "Plunger",
						Source = SwitchSource.InputSystem,
						SourceInputActionMap = InputConstants.MapCabinetSwitches,
						SourceInputAction = InputConstants.ActionPlunger,
						Destination = WireDestination.Device,
						DestinationDevice = plunger.Name,
						DestinationDeviceItem = Plunger.PullCoilId
					});
				}
			}
		}

		private void CreateTrough()
		{
			var troughData = new TroughData("Trough") {
				BallCount = 4,
				SwitchCount = 4,
				Type = TroughType.ModernMech
			};
			if (_tableContainer.Has<Kicker>("BallRelease")) {
				troughData.PlayfieldExitKicker = "BallRelease";
			}
			if (_tableContainer.Has<Kicker>("Drain")) {
				troughData.PlayfieldEntrySwitch = "Drain";
			}
			var item = new Trough(troughData) {
				StorageIndex = _tableContainer.ItemDatas.Count()
			};

			CreateGameObjects(item);
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

			_assetsPrefabs = $"{_assetsTableRoot}Prefabs/";
			if (!Directory.Exists(_assetsPrefabs)) {
				Directory.CreateDirectory(_assetsPrefabs);
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
				tableName = _table.Name;

			} else {
				tableName = tableName
					.Replace("%TABLENAME%", _table.Name)
					.Replace("%INFONAME%", _tableContainer.InfoName);
			}

			_tableGo = new GameObject();
			_playfieldGo = new GameObject("Playfield");
			var backglassGo = new GameObject("Backglass");
			var cabinetGo = new GameObject("Cabinet");

			_tableAuthoring = _tableGo.AddComponent<TableAuthoring>();
			_tableAuthoring.SetItem(_table, tableName);
			_tableAuthoring.SetData(_table.Data, this, this, new Dictionary<string, IItemMainAuthoring>());

			_playfieldGo.transform.SetParent(_tableGo.transform, false);
			backglassGo.transform.SetParent(_tableGo.transform, false);
			cabinetGo.transform.SetParent(_tableGo.transform, false);

			_playfieldGo.AddComponent<TablePlayfieldAuthoring>();
			_playfieldGo.transform.localRotation = TablePlayfieldAuthoring.GlobalRotation;
			_playfieldGo.transform.localPosition = new Vector3(-_table.Width / 2 * TablePlayfieldAuthoring.GlobalScale, 0f, _table.Height / 2 * TablePlayfieldAuthoring.GlobalScale);
			_playfieldGo.transform.localScale = new Vector3(TablePlayfieldAuthoring.GlobalScale, TablePlayfieldAuthoring.GlobalScale, TablePlayfieldAuthoring.GlobalScale);
		}

		private GameObject GetGroupParent(IItem item)
		{
			// create group parent if not created (if null, attach it to the table directly).
			if (!string.IsNullOrEmpty(item.ItemGroupName)) {
				if (!_groupParents.ContainsKey(item.ItemGroupName)) {
					var parent = new GameObject(item.ItemGroupName);
					parent.transform.SetParent(_playfieldGo.transform, false);
					_groupParents[item.ItemGroupName] = parent;
				}
			}
			var groupParent = !string.IsNullOrEmpty(item.ItemGroupName)
				? _groupParents[item.ItemGroupName]
				: _playfieldGo;

			return groupParent;
		}

		#region IMeshProvider

		public bool HasMesh(string parentName, string name) => File.Exists(GetMeshPath(parentName, name));

		public Mesh GetMesh(string parentName, string name) => AssetDatabase.LoadAssetAtPath<Mesh>(GetMeshPath(parentName, name));

		private string GetMeshPath(string parentName, string name)
		{
			var filename = parentName == name
				? $"{parentName.ToFilename()}.mesh"
				: $"{parentName.ToFilename()} ({name.ToFilename()}).mesh";
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
		public PhysicsMaterial GetPhysicsMaterial(string name)
		{
			if (string.IsNullOrEmpty(name)) {
				return null;
			}
			if (_physicalMaterials.ContainsKey(name)) {
				return _physicalMaterials[name];
			}

			var material = _tableAuthoring.Table.Data.Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.CurrentCultureIgnoreCase));
			if (material != null) {
				var path = SavePhysicsMaterial(material);
				_physicalMaterials[material.Name] = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
				return _physicalMaterials[material.Name];
			}

			return null;
		}

		public Material MergeMaterials(string vpxMaterial, Material textureMaterial)
		{
			var pbrMaterial = new PbrMaterial(_table.GetMaterial(vpxMaterial), id: $"{vpxMaterial.ToNormalizedName()} __textured");
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
}
