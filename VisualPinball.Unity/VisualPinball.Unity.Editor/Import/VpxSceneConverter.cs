// Visual Pinball Engine
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
using UnityEngine;
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
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity.Editor
{
	public class VpxSceneConverter : ITextureProvider, IMaterialProvider
	{
		private readonly TableContainer _tableContainer;
		private GameObject _tableGo;
		private TableAuthoring _tableAuthoring;

		private GameObject _playfieldGo;

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
		public VpxSceneConverter(TableContainer tableContainer, string fileName = "")
		{
			_tableContainer = tableContainer;
			if (tableContainer is FileTableContainer fileTableContainer) {
				_patcher = PatcherManager.GetPatcher();
				_patcher?.Set(fileTableContainer, fileName);
			} else {
				_patcher = null;
			}
		}

		/// <summary>
		/// Creates a converter based on an existing table in the scene.
		/// </summary>
		/// <param name="tableAuthoring">Existing component</param>
		public VpxSceneConverter(TableAuthoring tableAuthoring)
		{
			_tableGo = tableAuthoring.gameObject;
			var tablePlayfieldAuthoring = _tableGo.GetComponentInChildren<TablePlayfieldAuthoring>();
			if (!tablePlayfieldAuthoring) {
				throw new InvalidOperationException("Cannot find playfield hierarchy.");
			}
			_playfieldGo = tablePlayfieldAuthoring.gameObject;
			_tableAuthoring = tableAuthoring;
		}

		public GameObject Convert(bool applyPatch = true, string tableName = null)
		{
			_applyPatch = applyPatch;

			CreateRootHierarchy(tableName);
			CreateFileHierarchy();

			ExtractPhysicsMaterials();
			ExtractTextures();
			//ExtractSounds();

			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				SaveData();
				SaveLegacyData();

				ConvertGameItems();

			} finally {

				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			ConfigurePlayer();
			FreeTextures();

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
			_tableAuthoring.LegacyContainer.decals = _tableContainer.GetAllData<Decal, DecalData>();
			_tableAuthoring.LegacyContainer.dispReels = _tableContainer.GetAllData<DispReel, DispReelData>();
			_tableAuthoring.LegacyContainer.flashers = _tableContainer.GetAllData<Flasher, FlasherData>();
			_tableAuthoring.LegacyContainer.lightSeqs = _tableContainer.GetAllData<LightSeq, LightSeqData>();
			_tableAuthoring.LegacyContainer.textBoxes = _tableContainer.GetAllData<TextBox, TextBoxData>();
			_tableAuthoring.LegacyContainer.timers = _tableContainer.GetAllData<Timer, TimerData>();
		}

		private void ConvertGameItems()
		{
			var convertedItems = new Dictionary<string, IConvertedItem>();
			var renderableLookup = new Dictionary<string, IRenderable>();
			var renderables = from renderable in _tableContainer.Renderables
				orderby renderable.SubComponent
				select renderable;

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

			// now we have all renderables imported, patch them.
			foreach (var lookupName in convertedItems.Keys) {

				if (!convertedItems.ContainsKey(lookupName) || convertedItems[lookupName] == null) {
					continue;
				}

				if (!_applyPatch) {
					continue;
				}
				foreach (var meshMb in convertedItems[lookupName].MeshAuthoring) {
					_patcher?.ApplyPatches(renderableLookup[lookupName], meshMb.gameObject, _tableGo);
				}
			}

			// convert non-renderables
			foreach (var item in _tableContainer.NonRenderables) {

				// create object(s)
				CreateGameObjects(item);
			}
		}

		public IConvertedItem CreateGameObjects(IItem item)
		{
			var parentGo = GetGroupParent(item);
			var itemGo = new GameObject(item.Name);
			itemGo.transform.SetParent(parentGo.transform, false);

			var importedObject = SetupGameObjects(item, itemGo);
			foreach (var meshAuthoring in importedObject.MeshAuthoring) {
				meshAuthoring.CreateMesh(this, this);
			}
			item.FreeBinaryData();

			// apply transformation
			if (item is IRenderable renderable) {
				itemGo.transform.SetFromMatrix(renderable.TransformationMatrix(_tableContainer.Table, Origin.Original).ToUnityMatrix());
			}

			CreateAssetFromGameObject(itemGo, !importedObject.IsProceduralMesh);

			return importedObject;
		}

		private void CreateAssetFromGameObject(GameObject go, bool extractMesh)
		{
			var name = go.name;
			var mfs = go.GetComponentsInChildren<MeshFilter>();

			if (extractMesh) {
				foreach (var mf in mfs) {
					var suffix = mfs.Length == 1 ? "" : $" ({mf.gameObject.name})";
					var meshFilename = $"{name}{suffix}.mesh";
					var meshPath = Path.Combine(_assetsMeshes, meshFilename);
					if (File.Exists(meshPath)) {
						AssetDatabase.DeleteAsset(meshPath);
					}
					AssetDatabase.CreateAsset(mf.sharedMesh, meshPath);
				}
			}

			if (mfs.Length > 0) {
				// Make sure the file name is unique, in case an existing Prefab has the same name.
				var prefabPath = Path.Combine(_assetsPrefabs, $"{name}.prefab");
				//prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
				if (File.Exists(prefabPath)) {
					AssetDatabase.DeleteAsset(prefabPath);
				}
				PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
			}
		}

		private IConvertedItem SetupGameObjects(IItem item, GameObject obj)
		{
			switch (item) {
				case Bumper bumper:       return bumper.SetupGameObject(obj, this);
				case Flipper flipper:     return flipper.SetupGameObject(obj, this);
				case Gate gate:           return gate.SetupGameObject(obj, this);
				case HitTarget hitTarget: return hitTarget.SetupGameObject(obj, this);
				case Kicker kicker:       return kicker.SetupGameObject(obj, this);
				case Light lt:            return lt.SetupGameObject(obj);
				case Plunger plunger:     return plunger.SetupGameObject(obj, this);
				case Primitive primitive: return primitive.SetupGameObject(obj, this);
				case Ramp ramp:           return ramp.SetupGameObject(obj, this);
				case Rubber rubber:       return rubber.SetupGameObject(obj, this);
				case Spinner spinner:     return spinner.SetupGameObject(obj, this);
				case Surface surface:     return surface.SetupGameObject(obj, this);
				case Table table:         return table.SetupGameObject(obj, this);
				case Trigger trigger:     return trigger.SetupGameObject(obj, this);
				case Trough trough:       return trough.SetupGameObject(obj);
			}

			throw new InvalidOperationException("Unknown item " + item + " to setup!");
		}

		private void ExtractPhysicsMaterials()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var material in _tableContainer.Table.Data.Materials) {

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

			foreach (var material in _tableContainer.Table.Data.Materials) {
				_physicalMaterials[material.Name] = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{_assetsPhysicsMaterials}/{material.Name}.asset");
			}
		}

		private string SavePhysicsMaterial(Engine.VPT.Material material)
		{
			var mat = ScriptableObject.CreateInstance<PhysicsMaterial>();
			mat.Elasticity = material.Elasticity;
			mat.ElasticityFalloff = material.ElasticityFalloff;
			mat.ScatterAngle = material.ScatterAngle;
			mat.Friction = material.Friction;
			var path = $"{_assetsPhysicsMaterials}/{material.Name}.asset";
			AssetDatabase.CreateAsset(mat, path);

			return path;
		}

		private void ExtractTextures()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var texture in _tableContainer.Textures) {
					texture.WriteAsAsset(_assetsTextures);
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// todo lazy load and don't import local textures once they are in the prefabs
			// now they are in the asset database, we can load them.
			foreach (var texture in _tableContainer.Textures.Concat(Engine.VPT.Texture.LocalTextures)) {
				var path = texture.GetUnityFilename(_assetsTextures);
				var unityTexture = texture.IsHdr
					? (Texture)AssetDatabase.LoadAssetAtPath<Cubemap>(path)
					: AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
					var fileName = Path.GetFileName(sound.Data.Path).ToNormalizedName();
					var path = $"{_assetsSounds}/{fileName}";
					File.WriteAllBytes(path, sound.Data.GetWavData());
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
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
			var item = new Trough(troughData);
			_tableContainer.Add(item, true);
			CreateGameObjects(item);
		}


		private void CreateFileHierarchy()
		{
			if (!Directory.Exists("Assets/Tables/")) {
				Directory.CreateDirectory("Assets/Tables/");
			}

			var assetsTableRoot = $"Assets/Tables/{_tableContainer.Table.Name}/";
			if (!Directory.Exists(assetsTableRoot)) {
				Directory.CreateDirectory(assetsTableRoot);
			}

			_assetsPrefabs = $"{assetsTableRoot}Items/";
			if (!Directory.Exists(_assetsPrefabs)) {
				Directory.CreateDirectory(_assetsPrefabs);
			}

			_assetsTextures = $"{assetsTableRoot}Textures/";
			if (!Directory.Exists(_assetsTextures)) {
				Directory.CreateDirectory(_assetsTextures);
			}

			_assetsMaterials = $"{assetsTableRoot}Materials/";
			if (!Directory.Exists(_assetsMaterials)) {
				Directory.CreateDirectory(_assetsMaterials);
			}

			_assetsPhysicsMaterials = $"{assetsTableRoot}Physics Materials/";
			if (!Directory.Exists(_assetsPhysicsMaterials)) {
				Directory.CreateDirectory(_assetsPhysicsMaterials);
			}

			_assetsMeshes = $"{assetsTableRoot}Models/";
			if (!Directory.Exists(_assetsMeshes)) {
				Directory.CreateDirectory(_assetsMeshes);
			}

			_assetsSounds = $"{assetsTableRoot}Sounds/";
			if (!Directory.Exists(_assetsSounds)) {
				Directory.CreateDirectory(_assetsSounds);
			}
		}

		private void CreateRootHierarchy(string tableName)
		{
			// set the GameObject name; this needs to happen after MakeSerializable because the name is set there as well
			if (string.IsNullOrEmpty(tableName)) {
				tableName = _tableContainer.Table.Name;

			} else {
				tableName = tableName
					.Replace("%TABLENAME%", _tableContainer.Table.Name)
					.Replace("%INFONAME%", _tableContainer.InfoName);
			}

			_tableGo = new GameObject(tableName);
			_playfieldGo = new GameObject("Playfield");
			var backglassGo = new GameObject("Backglass");
			var cabinetGo = new GameObject("Cabinet");

			_tableAuthoring = _tableGo.AddComponent<TableAuthoring>();
			_tableAuthoring.SetItem(_tableContainer.Table);

			_playfieldGo.transform.SetParent(_tableGo.transform, false);
			backglassGo.transform.SetParent(_tableGo.transform, false);
			cabinetGo.transform.SetParent(_tableGo.transform, false);

			_playfieldGo.AddComponent<TablePlayfieldAuthoring>();
			_playfieldGo.transform.localRotation = TablePlayfieldAuthoring.GlobalRotation;
			_playfieldGo.transform.localPosition = new Vector3(-_tableContainer.Table.Width / 2 * TablePlayfieldAuthoring.GlobalScale, 0f, _tableContainer.Table.Height / 2 * TablePlayfieldAuthoring.GlobalScale);
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

		public bool HasMaterial(string name) => _materials.ContainsKey(name);
		public Material GetMaterial(string name) => string.IsNullOrEmpty(name) ? null : _materials[name];
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

		public void SaveMaterial(PbrMaterial vpxMaterial, Material material)
		{
			_materials[vpxMaterial.Id] = material;
			AssetDatabase.CreateAsset(material, vpxMaterial.GetUnityFilename(_assetsMaterials));
		}

		#endregion
	}
}
