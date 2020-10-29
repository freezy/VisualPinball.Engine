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
using VisualPinball.Engine.VPT.Trough;
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

			// add trough if none available
			if (!_table.HasTrough) {
				CreateTrough();
			}

			// populate mappings
			if (_table.Mappings.IsEmpty()) {
				_table.Mappings.PopulateSwitches((dga.GameEngine as IGamelogicEngineWithSwitches).AvailableSwitches, table.Switchables, table.SwitchableDevices);
				_table.Mappings.PopulateCoils((dga.GameEngine as IGamelogicEngineWithCoils).AvailableCoils, table.Coilables, table.CoilableDevices);
			}

			// don't need that anymore.
			DestroyImmediate(this);
		}

		private void ConvertGameItems(GameObject tableGameObject)
		{
			var convertedItems = new Dictionary<string, ConvertedItem>();
			var renderableLookup = new Dictionary<string, IRenderable>();
			var renderables = from renderable in _table.Renderables
				orderby renderable.SubComponent
				select renderable;

			foreach (var renderable in renderables) {

				_tableAuthoring.Patcher.ApplyPrePatches(renderable);

				var lookupName = renderable.Name.ToLower();
				renderableLookup[lookupName] = renderable;

				// create group parent if not created (if null, attach it to the table directly).
				if (!string.IsNullOrEmpty(renderable.ItemGroupName)) {
					if (!_groupParents.ContainsKey(renderable.ItemGroupName)) {
						var parent = new GameObject(renderable.ItemGroupName);
						parent.transform.parent = gameObject.transform;
						_groupParents[renderable.ItemGroupName] = parent;
					}
				}
				var groupParent = !string.IsNullOrEmpty(renderable.ItemGroupName)
					? _groupParents[renderable.ItemGroupName]
					: gameObject;

				if (renderable.SubComponent == ItemSubComponent.None) {
					// create object(s)
					convertedItems[lookupName] = CreateGameObjects(_table, renderable, groupParent);

				} else {
					// if the object's names was parsed to be part of another object, re-link to other object.
					var parentName = renderable.ComponentName.ToLower();
					if (convertedItems.ContainsKey(parentName)) {
						var parent = convertedItems[parentName];

						var convertedItem = CreateGameObjects(_table, renderable, groupParent);
						if (convertedItem.IsValidChild(parent)) {

							if (convertedItem.MeshAuthoring.Any()) {

								// move and rotate into parent
								if (parent.MainAuthoring.IItem is IRenderable parentRenderable) {
									renderable.Position.Sub(parentRenderable.Position);
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
							convertedItems[lookupName] = CreateGameObjects(_table, renderable, groupParent);

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
					_tableAuthoring.Patcher.ApplyPatches(renderableLookup[lookupName], meshMb.gameObject, tableGameObject);
				}
			}
		}

		public static ConvertedItem CreateGameObjects(Table table, IRenderable renderable, GameObject parent)
		{
			var obj = new GameObject(renderable.Name);
			obj.transform.parent = parent.transform;

			var importedObject = SetupGameObjects(renderable, obj);

			// apply transformation
			obj.transform.SetFromMatrix(renderable.TransformationMatrix(table, Origin.Original).ToUnityMatrix());

			return importedObject;
		}

		private static ConvertedItem SetupGameObjects(IRenderable item, GameObject obj)
		{
			switch (item) {
				case Bumper bumper:             return bumper.SetupGameObject(obj);
				case Flipper flipper:           return flipper.SetupGameObject(obj);
				case Gate gate:                 return gate.SetupGameObject(obj);
				case HitTarget hitTarget:       return hitTarget.SetupGameObject(obj);
				case Kicker kicker:             return kicker.SetupGameObject(obj);
				case Engine.VPT.Light.Light lt: return lt.SetupGameObject(obj);
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

		private void CreateTrough()
		{
			var troughData = new TroughData("Trough");
			if (_table.Kicker("BallRelease") != null) {
				troughData.ExitKicker = "BallRelease";
			}
			if (_table.Kicker("Drain") != null) {
				troughData.EntryKicker = "Drain";
			}
			var item = new Trough(troughData);
			_table.Add(item, true);
			CreateGameObjects(_tableAuthoring.Table, item, _tableAuthoring.gameObject);
		}
	}

	public class ConvertedItem
	{
		public readonly IItemMainAuthoring MainAuthoring;
		public IEnumerable<IItemMeshAuthoring> MeshAuthoring;
		public IItemColliderAuthoring ColliderAuthoring;

		public ConvertedItem()
		{
			MainAuthoring = null;
			MeshAuthoring = new IItemMeshAuthoring[0];
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = new IItemMeshAuthoring[0];
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring, IEnumerable<IItemMeshAuthoring> meshAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = meshAuthoring;
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring, IEnumerable<IItemMeshAuthoring> meshAuthoring, IItemColliderAuthoring colliderAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = meshAuthoring;
			ColliderAuthoring = colliderAuthoring;
		}

		public void Destroy()
		{
			MainAuthoring.Destroy();
		}

		public void DestroyMeshComponent()
		{
			MainAuthoring.DestroyMeshComponent();
			MeshAuthoring = new IItemMeshAuthoring[0];
		}

		public void DestroyColliderComponent()
		{
			MainAuthoring.DestroyColliderComponent();
			ColliderAuthoring = null;
		}

		public bool IsValidChild(ConvertedItem parent)
		{
			if (MeshAuthoring.Any()) {
				return MeshAuthoring.First().ValidParents.Contains(parent.MainAuthoring.GetType());
			}

			if (ColliderAuthoring != null) {
				return ColliderAuthoring.ValidParents.Contains(parent.MainAuthoring.GetType());
			}

			return MainAuthoring.ValidParents.Contains(parent.MainAuthoring.GetType());
		}

		public static T CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			var comp = subObj.AddComponent<T>();
			subObj.layer = VpxConverter.ChildObjectsLayer;
			return comp;
		}
	}
}
