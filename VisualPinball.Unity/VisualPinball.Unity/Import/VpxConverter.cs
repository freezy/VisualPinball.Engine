// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
		private readonly Dictionary<string, GameObject> _parents = new Dictionary<string, GameObject>();

		private Table _table;
		private TableAuthoring _tableAuthoring;
		private bool _applyPatch = true;

		public void Convert(string fileName, Table table, bool applyPatch = true, string tableName = null)
		{
			_table = table;

			// TODO: implement disabling patching; not so obvious because of the static methods being used for the import
			if( !applyPatch)
				Logger.Warn("Disabling patch import not implemented yet!");

			var go = gameObject;

			MakeSerializable(go, table);

			// set the GameObject name; this needs to happen after MakeSerializable because the name is set there as well
			if (string.IsNullOrEmpty(tableName)) {
				go.name = _table.Name;

			} else {
				go.name = tableName
					.Replace("%TABLENAME%", _table.Name)
					.Replace("%INFONAME%", _table.InfoName);
			}

			_tableAuthoring.Patcher = new Patcher.Patcher(_table, fileName);

			// import
			ConvertGameItems(go);

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-_table.Width / 2 * GlobalScale, 0f, _table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			//ScaleNormalizer.Normalize(go, GlobalScale);

			// add the player script and default game engine
			go.AddComponent<Player>();
			var dga = go.AddComponent<DefaultGameEngineAuthoring>();

			// populate mappings
			if (_table.Mappings.IsEmpty()) {
				_table.Mappings.PopulateSwitches((dga.GameEngine as IGamelogicEngineWithSwitches).AvailableSwitches, table.Switchables);
				_table.Mappings.PopulateCoils((dga.GameEngine as IGamelogicEngineWithCoils).AvailableCoils, table.Coilables);
			}

			// don't need that anymore.
			DestroyImmediate(this);
		}

		private void ConvertGameItems(GameObject tableGameObject)
		{
			var createdMainObjs = new Dictionary<string, GameObject>();
			var createdMainMbs = new Dictionary<string, IItemMainAuthoring>();
			var createdMeshMbs = new Dictionary<string, IEnumerable<IItemMeshAuthoring>>();
			var renderableLookup = new Dictionary<string, IRenderable>();
			var renderables = from renderable in _table.Renderables
				orderby renderable.SubComponent
				select renderable;

			foreach (var renderable in renderables) {

				_tableAuthoring.Patcher.ApplyPrePatches(renderable);

				var lookupName = renderable.Name.ToLower();
				renderableLookup[lookupName] = renderable;

				// create group parent if not created
				if (!_parents.ContainsKey(renderable.ItemGroupName)) {
					var parent = new GameObject(renderable.ItemGroupName);
					parent.transform.parent = gameObject.transform;
					_parents[renderable.ItemGroupName] = parent;
				}

				if (renderable.SubComponent == ItemSubComponent.None) {
					// create object(s)
					var (rootObj, rootMb, meshMbs) = CreateGameObjects(_table, renderable, _parents[renderable.ItemGroupName]);

					createdMainObjs[lookupName] = rootObj;
					createdMainMbs[lookupName] = rootMb;
					createdMeshMbs[lookupName] = meshMbs;

				} else {
					// if the object's names was parsed to be part of another object, re-link to other object.
					var parentName = renderable.ComponentName.ToLower();
					if (createdMainObjs.ContainsKey(parentName)) {
						var mainObj = createdMainObjs[parentName];
						var mainMb = createdMainMbs[parentName];

						// move and rotate into parent
						if (mainMb.IItem is IRenderable parentRenderable) {
							renderable.Position.Sub(parentRenderable.Position);
							renderable.RotationY -= parentRenderable.RotationY;
						}

						var (rootObj, rootMb, meshMbs) = CreateGameObjects(_table, renderable, _parents[renderable.ItemGroupName], mainMb);

						createdMeshMbs[lookupName] = meshMbs;

						rootObj.transform.SetParent(mainObj.transform, false);

					} else {
						throw new InvalidOperationException($"Cannot find component \"{parentName}\" that is supposed to be the parent of \"{renderable.Name}\".");
					}
				}
			}

			// now we have all renderables imported, patch them.
			foreach (var lookupName in createdMeshMbs.Keys) {
				foreach (var meshMb in createdMeshMbs[lookupName]) {
					_tableAuthoring.Patcher.ApplyPatches(renderableLookup[lookupName], meshMb.gameObject, tableGameObject);
				}
			}
		}

		public static (GameObject, IItemMainAuthoring, IEnumerable<IItemMeshAuthoring>) CreateGameObjects(Table table, IRenderable renderable, GameObject parent, IItemMainAuthoring parentAuthoring = null)
		{
			var obj = new GameObject(renderable.Name);
			obj.transform.parent = parent.transform;

			var (mainAuthoring, meshAuthoring) = SetupGameObjects(renderable, obj, parentAuthoring);

			// apply transformation
			obj.transform.SetFromMatrix(renderable.TransformationMatrix(table, Origin.Original).ToUnityMatrix());

			return (obj, mainAuthoring, meshAuthoring);
		}

		private static (IItemMainAuthoring, IEnumerable<IItemMeshAuthoring>) SetupGameObjects(IRenderable item, GameObject obj, IItemMainAuthoring parentAuthoring = null)
		{
			switch (item) {
				case Bumper bumper:             return bumper.SetupGameObject(obj, parentAuthoring);
				case Flipper flipper:           return flipper.SetupGameObject(obj, parentAuthoring);
				case Gate gate:                 return gate.SetupGameObject(obj, parentAuthoring);
				case HitTarget hitTarget:       return hitTarget.SetupGameObject(obj, parentAuthoring);
				case Kicker kicker:             return kicker.SetupGameObject(obj, parentAuthoring);
				case Engine.VPT.Light.Light lt: return lt.SetupGameObject(obj, parentAuthoring);
				case Plunger plunger:           return plunger.SetupGameObject(obj, parentAuthoring);
				case Primitive primitive:       return primitive.SetupGameObject(obj, parentAuthoring);
				case Ramp ramp:                 return ramp.SetupGameObject(obj, parentAuthoring);
				case Rubber rubber:             return rubber.SetupGameObject(obj, parentAuthoring);
				case Spinner spinner:           return spinner.SetupGameObject(obj, parentAuthoring);
				case Surface surface:           return surface.SetupGameObject(obj, parentAuthoring);
				case Table table:               return table.SetupGameObject(obj, parentAuthoring);
				case Trigger trigger:           return trigger.SetupGameObject(obj, parentAuthoring);
			}

			throw new InvalidOperationException("Unknown item " + item + " to setup!");
		}

		private void MakeSerializable(GameObject go, Table table)
		{
			// add table component (plus other data)
			_tableAuthoring = go.AddComponent<TableAuthoring>();
			_tableAuthoring.SetItem(table);

			var sidecar = _tableAuthoring.GetOrCreateSidecar();

			foreach (var key in table.TableInfo.Keys) {
				sidecar.tableInfo[key] = table.TableInfo[key];
			}

			// copy each serializable ref into the sidecar's serialized storage
			sidecar.textures.AddRange(table.Textures);
			sidecar.sounds.AddRange(table.Sounds);

			// and tell the engine's table to now use the sidecar as its container so we can all operate on the same underlying container
			table.SetTextureContainer(sidecar.textures);
			table.SetSoundContainer(sidecar.sounds);

			sidecar.customInfoTags = table.CustomInfoTags;
			sidecar.collections = table.Collections.Values.Select(c => c.Data).ToList();
			sidecar.mappings = table.Mappings.Data;
			sidecar.decals = table.GetAllData<Decal, DecalData>();
			sidecar.dispReels = table.GetAllData<DispReel, DispReelData>();
			sidecar.flashers = table.GetAllData<Flasher, FlasherData>();
			sidecar.lightSeqs = table.GetAllData<LightSeq, LightSeqData>();
			sidecar.plungers = table.GetAllData<Plunger, PlungerData>();
			sidecar.textBoxes = table.GetAllData<TextBox, TextBoxData>();
			sidecar.timers = table.GetAllData<Timer, TimerData>();

			Logger.Info("Collections saved: [ {0} ] [ {1} ]",
				string.Join(", ", table.Collections.Keys),
				string.Join(", ", sidecar.collections.Select(c => c.Name))
			);
		}
	}
}
