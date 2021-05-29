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

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable RedundantAssignment

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
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

namespace VisualPinball.Unity
{
	public class VpxConverter : MonoBehaviour
	{
		private static readonly Quaternion GlobalRotation = Quaternion.Euler(-90, 0, 0);
		public const float GlobalScale = 0.001f;
		public const int ChildObjectsLayer = 16;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		//private readonly Dictionary<IRenderable, RenderObjectGroup> _renderObjects = new Dictionary<IRenderable, RenderObjectGroup>();
		private readonly Dictionary<string, GameObject> _groupParents = new Dictionary<string, GameObject>();

		private TableContainer _tableContainer;
		private TableAuthoring _tableAuthoring;
		private bool _applyPatch = true;
		private IPatcher _patcher;

		public void Convert(string fileName, TableContainer th, bool applyPatch = true, string tableName = null)
		{
			_tableContainer = th;

			// TODO: implement disabling patching; not so obvious because of the static methods being used for the import
			if( !applyPatch)
				Logger.Warn("Disabling patch import not implemented yet!");

			var go = gameObject;

			MakeSerializable(go);

			// set the GameObject name; this needs to happen after MakeSerializable because the name is set there as well
			if (string.IsNullOrEmpty(tableName)) {
				go.name = _tableContainer.Table.Name;

			} else {
				go.name = tableName
					.Replace("%TABLENAME%", _tableContainer.Table.Name)
					.Replace("%INFONAME%", _tableContainer.InfoName);
			}

			_patcher = PatcherManager.GetPatcher();
			_patcher?.Set(_tableContainer, fileName);

			// import
			ConvertGameItems(go);

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-_tableContainer.Table.Width / 2 * GlobalScale, 0f, _tableContainer.Table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			//ScaleNormalizer.Normalize(go, GlobalScale);

			// add the player script and default game engine
			go.AddComponent<Player>();
			var dga = go.AddComponent<DefaultGamelogicEngine>();

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

			// don't need that anymore.
			DestroyImmediate(this);
		}

		private void ConvertGameItems(GameObject tableGameObject)
		{
			var convertedItems = new Dictionary<string, ConvertedItem>();
			var renderableLookup = new Dictionary<string, IRenderable>();
			var renderables = from renderable in _tableContainer.Renderables
				orderby renderable.SubComponent
				select renderable;

			foreach (var renderable in renderables) {

				_patcher?.ApplyPrePatches(renderable);

				var lookupName = renderable.Name.ToLower();
				renderableLookup[lookupName] = renderable;

				var groupParent = GetGroupParent(renderable);

				if (renderable.SubComponent == ItemSubComponent.None) {
					// create object(s)
					convertedItems[lookupName] = CreateGameObjects(_tableContainer.Table, renderable, groupParent);

				} else {
					// if the object's names was parsed to be part of another object, re-link to other object.
					var parentName = renderable.ComponentName.ToLower();
					if (convertedItems.ContainsKey(parentName)) {
						var parent = convertedItems[parentName];

						var convertedItem = CreateGameObjects(_tableContainer.Table, renderable, groupParent);
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
							convertedItems[lookupName] = CreateGameObjects(_tableContainer.Table, renderable, groupParent);

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
				foreach (var meshMb in convertedItems[lookupName].MeshAuthoring) {
					_patcher?.ApplyPatches(renderableLookup[lookupName], meshMb.gameObject, tableGameObject);
				}
			}

			// convert non-renderables
			foreach (var item in _tableContainer.NonRenderables) {
				var groupParent = GetGroupParent(item);

				// create object(s)
				CreateGameObjects(_tableContainer.Table, item, groupParent);
			}
		}

		private GameObject GetGroupParent(IItem item)
		{
			// create group parent if not created (if null, attach it to the table directly).
			if (!string.IsNullOrEmpty(item.ItemGroupName)) {
				if (!_groupParents.ContainsKey(item.ItemGroupName)) {
					var parent = new GameObject(item.ItemGroupName);
					parent.transform.parent = gameObject.transform;
					_groupParents[item.ItemGroupName] = parent;
				}
			}
			var groupParent = !string.IsNullOrEmpty(item.ItemGroupName)
				? _groupParents[item.ItemGroupName]
				: gameObject;

			return groupParent;
		}

		public static ConvertedItem CreateGameObjects(Table table, IItem item, GameObject parent)
		{
			var obj = new GameObject(item.Name);
			obj.transform.parent = parent.transform;

			var importedObject = SetupGameObjects(item, obj);

			// apply transformation
			if (item is IRenderable renderable) {
				obj.transform.SetFromMatrix(renderable.TransformationMatrix(table, Origin.Original).ToUnityMatrix());
			}

			return importedObject;
		}

		private static ConvertedItem SetupGameObjects(IItem item, GameObject obj)
		{
			switch (item) {
				case Bumper bumper:             return bumper.SetupGameObject(obj);
				case Flipper flipper:           return flipper.SetupGameObject(obj);
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

		private void MakeSerializable(GameObject go)
		{
			// add table component (plus other data)
			_tableAuthoring = go.AddComponent<TableAuthoring>();
			_tableAuthoring.SetItem(_tableContainer.Table);

			var sidecar = _tableAuthoring.GetOrCreateSidecar();

			foreach (var key in _tableContainer.TableInfo.Keys) {
				sidecar.tableInfo[key] = _tableContainer.TableInfo[key];
			}

			// copy each serializable ref into the sidecar's serialized storage
			// sidecar.textures.AddRange(table.Textures);
			// sidecar.sounds.AddRange(table.Sounds);

			// and tell the engine's table to now use the sidecar as its container so we can all operate on the same underlying container
			// table.SetTextureContainer(sidecar.textures);
			// table.SetSoundContainer(sidecar.sounds);

			sidecar.customInfoTags = _tableContainer.CustomInfoTags;
			sidecar.collections = _tableContainer.Collections.Values.Select(c => c.Data).ToList();
			sidecar.mappings = _tableContainer.Mappings.Data;
			sidecar.decals = _tableContainer.GetAllData<Decal, DecalData>();
			sidecar.dispReels = _tableContainer.GetAllData<DispReel, DispReelData>();
			sidecar.flashers = _tableContainer.GetAllData<Flasher, FlasherData>();
			sidecar.lightSeqs = _tableContainer.GetAllData<LightSeq, LightSeqData>();
			sidecar.textBoxes = _tableContainer.GetAllData<TextBox, TextBoxData>();
			sidecar.timers = _tableContainer.GetAllData<Timer, TimerData>();

			Logger.Info("Collections saved: [ {0} ] [ {1} ]",
				string.Join(", ", _tableContainer.Collections.Keys),
				string.Join(", ", sidecar.collections.Select(c => c.Name))
			);
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
			CreateGameObjects(_tableAuthoring.Table, item, _tableAuthoring.gameObject);
		}
	}
}
