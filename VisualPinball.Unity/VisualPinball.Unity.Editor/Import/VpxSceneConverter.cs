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
		private readonly TableHolder _th;
		private GameObject _tableGo;
		private TableAuthoring _tableAuthoring;

		private GameObject _playfieldGo;

		private string _assetsPrefabs;
		private string _assetsTextures;
		private string _assetsMaterials;
		private string _assetsMeshes;
		private string _assetsSounds;

		private readonly Dictionary<string, GameObject> _groupParents = new Dictionary<string, GameObject>();
		private readonly Dictionary<string, Texture> _textures = new Dictionary<string, Texture>();
		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

		private readonly IPatcher _patcher;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly Quaternion GlobalRotation = Quaternion.Euler(-90, 0, 0);
		public const float GlobalScale = 0.001f;

		public VpxSceneConverter(TableHolder th, string fileName)
		{
			_th = th;
			_patcher = PatcherManager.GetPatcher();
			_patcher?.Set(_th, fileName);
		}

		public GameObject Convert(string tableName = null)
		{
			CreateRootHierarchy(tableName);
			CreateFileHierarchy();

			ExtractTextures();
			//ExtractSounds();

			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				MakeSerializable();
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

		private void ConvertGameItems()
		{
			var convertedItems = new Dictionary<string, ConvertedItem>();
			var renderableLookup = new Dictionary<string, IRenderable>();
			var renderables = from renderable in _th.Renderables
				orderby renderable.SubComponent
				select renderable;

			foreach (var renderable in renderables) {

				_patcher?.ApplyPrePatches(renderable);

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

								parent.DestroyMeshComponent();
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

				foreach (var meshMb in convertedItems[lookupName].MeshAuthoring) {
					_patcher?.ApplyPatches(renderableLookup[lookupName], meshMb.gameObject, _tableGo);
				}
			}

			// convert non-renderables
			foreach (var item in _th.NonRenderables) {

				// create object(s)
				CreateGameObjects(item);
			}
		}

		public ConvertedItem CreateGameObjects(IItem item)
		{
			var parentGo = GetGroupParent(item);
			var itemGo = new GameObject(item.Name);
			itemGo.transform.SetParent(parentGo.transform, false);

			var importedObject = SetupGameObjects(item, itemGo);
			if (importedObject != null) {
				foreach (var meshAuthoring in importedObject.MeshAuthoring) {
					meshAuthoring.CreateMesh(this, this);
				}
				item.ClearBinaryData();

				// apply transformation
				if (item is IRenderable renderable) {
					itemGo.transform.SetFromMatrix(renderable.TransformationMatrix(_th.Table, Origin.Original).ToUnityMatrix());
				}

				CreateAssetFromGameObject(itemGo, !importedObject.IsProceduralMesh);

			} else {
				var convertedComponent = SetupComponents(item, itemGo);

				if (item is IRenderable renderable) {

					foreach (var meshComp in convertedComponent.MeshComponents) {
						meshComp.CreateMesh(renderable, this, this);
					}
					item.ClearBinaryData();
					itemGo.transform.SetFromMatrix(renderable.TransformationMatrix(_th.Table, Origin.Original).ToUnityMatrix());

					CreateAssetFromGameObject(itemGo, false);
				}
			}


			return importedObject;
		}

		private GameObject CreateAssetFromGameObject(GameObject go, bool extractMesh)
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
				return PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
			}

			return go;
		}

		private static ConvertedComponent SetupComponents(IItem item, GameObject go)
		{
			switch (item) {
				case Flipper flipper:           return flipper.SetupComponents(go);
			}
			throw new ArgumentException("Unknown item type " + item.GetType());
		}

		private static ConvertedItem SetupGameObjects(IItem item, GameObject obj)
		{
			switch (item) {
				case Bumper bumper:             return bumper.SetupGameObject(obj);
				case Flipper flipper:           return null;
				case Gate gate:                 return gate.SetupGameObject(obj);
				case HitTarget hitTarget:       return hitTarget.SetupGameObject(obj);
				case Kicker kicker:             return kicker.SetupGameObject(obj);
				case Light lt:                  return lt.SetupGameObject(obj);
				case Plunger plunger:           return plunger.SetupGameObject(obj);
				case Primitive primitive:       return primitive.SetupGameObject(obj);
				case Ramp ramp:                 return ramp.SetupGameObject(obj);
				case Rubber rubber:             return rubber.SetupGameObject(obj);
				case Spinner spinner:           return spinner.SetupGameObject(obj);
				case Surface surface:           return surface.SetupGameObject(obj);
				case Table table:               return table.SetupGameObject(obj);
				case Trigger trigger:           return trigger.SetupGameObject(obj);
				case Trough trough:             return trough.SetupGameObject(obj);
			}

			throw new InvalidOperationException("Unknown item " + item + " to setup!");
		}

		private void MakeSerializable()
		{
			var sidecar = _tableAuthoring.GetOrCreateSidecar();

			foreach (var key in _th.TableInfo.Keys) {
				sidecar.tableInfo[key] = _th.TableInfo[key];
			}

			// // copy each serializable ref into the sidecar's serialized storage
			// sidecar.textures.AddRange(_table.Textures);
			// sidecar.sounds.AddRange(_table.Sounds);
			//
			// // and tell the engine's table to now use the sidecar as its container so we can all operate on the same underlying container
			// _table.SetTextureContainer(sidecar.textures);
			// _table.SetSoundContainer(sidecar.sounds);

			sidecar.customInfoTags = _th.CustomInfoTags;
			sidecar.collections = _th.Collections.Values.Select(c => c.Data).ToList();
			sidecar.mappings = _th.Mappings.Data;
			sidecar.decals = _th.GetAllData<Decal, DecalData>();
			sidecar.dispReels = _th.GetAllData<DispReel, DispReelData>();
			sidecar.flashers = _th.GetAllData<Flasher, FlasherData>();
			sidecar.lightSeqs = _th.GetAllData<LightSeq, LightSeqData>();
			sidecar.textBoxes = _th.GetAllData<TextBox, TextBoxData>();
			sidecar.timers = _th.GetAllData<Timer, TimerData>();

			Logger.Info("Collections saved: [ {0} ] [ {1} ]",
				string.Join(", ", _th.Collections.Keys),
				string.Join(", ", sidecar.collections.Select(c => c.Name))
			);
		}

		private void ExtractTextures()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var texture in _th.Textures) {
					var path = texture.GetUnityFilename(_assetsTextures);
					File.WriteAllBytes(path, texture.Content);
				}

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// todo lazy load
			// now they are in the asset database, we can load them.
			foreach (var texture in _th.Textures) {
				var path = texture.GetUnityFilename(_assetsTextures);
				var unityTexture = texture.IsHdr
					? (Texture)AssetDatabase.LoadAssetAtPath<Cubemap>(path)
					: AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				_textures[texture.Name.ToLower()] = unityTexture;
			}
		}

		private void FreeTextures()
		{
			foreach (var texture in _th.Textures) {
				texture.Data.FreeBinaryData();
			}
		}

		private void ExtractSounds()
		{
			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();

				foreach (var sound in _th.Sounds) {
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
			if (!_th.HasTrough) {
				CreateTrough();
			}

			// populate mappings
			if (_th.Mappings.IsEmpty()) {
				_th.Mappings.PopulateSwitches(dga.AvailableSwitches, _th.Switchables, _th.SwitchableDevices);
				_th.Mappings.PopulateCoils(dga.AvailableCoils, _th.Coilables, _th.CoilableDevices);

				// wire up plunger
				var plunger = _th.Plunger();
				if (plunger != null) {
					_th.Mappings.Data.AddWire(new MappingsWireData {
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
			if (_th.Has<Kicker>("BallRelease")) {
				troughData.PlayfieldExitKicker = "BallRelease";
			}
			if (_th.Has<Kicker>("Drain")) {
				troughData.PlayfieldEntrySwitch = "Drain";
			}
			var item = new Trough(troughData);
			_th.Add(item, true);
			CreateGameObjects(item);
		}


		private void CreateFileHierarchy()
		{
			if (!Directory.Exists("Assets/Tables/")) {
				Directory.CreateDirectory("Assets/Tables/");
			}

			var assetsTableRoot = $"Assets/Tables/{_th.Table.Name}/";
			if (!Directory.Exists(assetsTableRoot)) {
				Directory.CreateDirectory(assetsTableRoot);
			}

			_assetsPrefabs = $"{assetsTableRoot}/Items/";
			if (!Directory.Exists(_assetsPrefabs)) {
				Directory.CreateDirectory(_assetsPrefabs);
			}

			_assetsTextures = $"{assetsTableRoot}/Textures/";
			if (!Directory.Exists(_assetsTextures)) {
				Directory.CreateDirectory(_assetsTextures);
			}

			_assetsMaterials = $"{assetsTableRoot}/Materials/";
			if (!Directory.Exists(_assetsMaterials)) {
				Directory.CreateDirectory(_assetsMaterials);
			}

			_assetsMeshes = $"{assetsTableRoot}/Models/";
			if (!Directory.Exists(_assetsMeshes)) {
				Directory.CreateDirectory(_assetsMeshes);
			}

			_assetsSounds = $"{assetsTableRoot}/Sounds/";
			if (!Directory.Exists(_assetsSounds)) {
				Directory.CreateDirectory(_assetsSounds);
			}
		}

		private void CreateRootHierarchy(string tableName)
		{
			// set the GameObject name; this needs to happen after MakeSerializable because the name is set there as well
			if (string.IsNullOrEmpty(tableName)) {
				tableName = _th.Table.Name;

			} else {
				tableName = tableName
					.Replace("%TABLENAME%", _th.Table.Name)
					.Replace("%INFONAME%", _th.InfoName);
			}

			_tableGo = new GameObject(tableName);
			_playfieldGo = new GameObject("Playfield");
			var backglassGo = new GameObject("Backglass");
			var cabinetGo = new GameObject("Cabinet");

			_tableAuthoring = _tableGo.AddComponent<TableAuthoring>();
			_tableAuthoring.SetItem(_th.Table);

			_playfieldGo.transform.SetParent(_tableGo.transform, false);
			backglassGo.transform.SetParent(_tableGo.transform, false);
			cabinetGo.transform.SetParent(_tableGo.transform, false);

			_playfieldGo.transform.localRotation = GlobalRotation;
			_playfieldGo.transform.localPosition = new Vector3(-_th.Table.Width / 2 * GlobalScale, 0f, _th.Table.Height / 2 * GlobalScale);
			_playfieldGo.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
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
		public Material GetMaterial(string name) => _materials[name];
		public void SaveMaterial(PbrMaterial vpxMaterial, Material material)
		{
			_materials[vpxMaterial.Id] = material;
			AssetDatabase.CreateAsset(material, vpxMaterial.GetUnityFilename(_assetsMaterials));
		}

		#endregion
	}
}
