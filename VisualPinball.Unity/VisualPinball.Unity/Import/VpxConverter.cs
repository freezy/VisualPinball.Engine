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
		public const int ChildObjectsLayer = 8;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly Dictionary<IRenderable, RenderObjectGroup> _renderObjects = new Dictionary<IRenderable, RenderObjectGroup>();
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

			// set the gameobject name; this needs to happen after MakeSerializable because the name is set there as well
			if( string.IsNullOrEmpty( tableName))
			{
				go.name = _table.Name;
			}
			else
			{
				go.name = tableName
					.Replace("%TABLENAME%", _table.Name)
					.Replace("%INFONAME%", _table.InfoName);
			}

			_tableAuthoring.Patcher = new Patcher.Patcher(_table, fileName);

			// generate meshes and save (pbr) materials
			var materials = new Dictionary<string, PbrMaterial>();
			foreach (var r in _table.Renderables) {
				_renderObjects[r] = r.GetRenderObjects(_table, Origin.Original, false);
				foreach (var ro in _renderObjects[r].RenderObjects) {
					if (!materials.ContainsKey(ro.Material.Id)) {
						materials[ro.Material.Id] = ro.Material;
					}
				}
			}

			// import
			ConvertGameItems(go);

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-_table.Width / 2 * GlobalScale, 0f, _table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			//ScaleNormalizer.Normalize(go, GlobalScale);

			// finally, add the player script
			go.AddComponent<Player>();
		}

		public static GameObject ConvertRenderObject(RenderObject ro, GameObject obj, TableAuthoring ta)
		{
			if (ro.Mesh == null) {
				Logger.Warn($"No mesh for object {obj.name}, skipping.");
				return null;
			}

			var mesh = ro.Mesh.ToUnityMesh($"{obj.name}_mesh");

			// apply mesh to game object
			var mf = obj.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			if (ro.Mesh.AnimationFrames.Count > 0) {
				var smr = obj.AddComponent<SkinnedMeshRenderer>();
				smr.sharedMaterial = ro.Material.ToUnityMaterial(table);
				smr.sharedMesh = mesh;
				smr.enabled = ro.IsVisible;
			}
			else {
				var mr = obj.AddComponent<MeshRenderer>();
				mr.sharedMaterial = ro.Material.ToUnityMaterial(table);
				mr.enabled = ro.IsVisible;
			}

			return obj;
		}

		private void ConvertGameItems(GameObject tableGameObject)
		{
			// convert game objects
			ConvertRenderables(tableGameObject);
		}

		private void ConvertRenderables(GameObject tableGameObject)
		{
			var createdObjs = new Dictionary<IRenderable, IEnumerable<Tuple<GameObject, RenderObject>>>();
			foreach (var renderable in _renderObjects.Keys) {
				var ro = _renderObjects[renderable];
				if (!_parents.ContainsKey(ro.Parent)) {
					var parent = new GameObject(ro.Parent);
					parent.transform.parent = gameObject.transform;
					_parents[ro.Parent] = parent;
				}
				createdObjs[renderable] = ConvertRenderObjects(renderable, ro, _parents[ro.Parent], _tableAuthoring, out _);
			}

			// now we have all renderables imported, patch them.
			foreach (var renderable in createdObjs.Keys) {
				foreach (var (obj, ro) in createdObjs[renderable]) {
					_tableAuthoring.Patcher.ApplyPatches(renderable, ro, obj, tableGameObject);
				}
			}
		}

		public static IEnumerable<Tuple<GameObject, RenderObject>> ConvertRenderObjects(IRenderable item, RenderObjectGroup rog, GameObject parent, TableAuthoring tb, out GameObject obj)
		{
			obj = new GameObject(rog.Name);
			obj.transform.parent = parent.transform;

			var createdObjs = new Tuple<GameObject, RenderObject>[0];

			if (rog.HasOnlyChild && !rog.ForceChild) {
				ConvertRenderObject(rog.RenderObjects[0], obj, tb);
				createdObjs = new[] { new Tuple<GameObject, RenderObject>(obj, rog.RenderObjects[0]) };

			} else if (rog.HasChildren) {
				createdObjs = new Tuple<GameObject, RenderObject>[rog.RenderObjects.Length];
				var i = 0;
				foreach (var ro in rog.RenderObjects) {
					var subObj = new GameObject(ro.Name);
					subObj.transform.SetParent(obj.transform, false);
					subObj.layer = ChildObjectsLayer;
					ConvertRenderObject(ro, subObj, tb);
					createdObjs[i++] = new Tuple<GameObject, RenderObject>(subObj, ro);
				}
			}

			// apply transformation
			obj.transform.SetFromMatrix(rog.TransformationMatrix.ToUnityMatrix());

			// add unity component
			MonoBehaviour ic = null;
			switch (item) {
				case Bumper bumper:					ic = bumper.SetupGameObject(obj, rog); break;
				case Flipper flipper:				ic = flipper.SetupGameObject(obj, rog); break;
				case Gate gate:						ic = gate.SetupGameObject(obj, rog); break;
				case HitTarget hitTarget:			ic = hitTarget.SetupGameObject(obj, rog); break;
				case Kicker kicker:					ic = kicker.SetupGameObject(obj, rog); break;
				case Engine.VPT.Light.Light lt:		ic = lt.SetupGameObject(obj, rog); break;
				case Plunger plunger:				ic = plunger.SetupGameObject(obj, rog); break;
				case Primitive primitive:			ic = obj.AddComponent<PrimitiveAuthoring>().SetItem(primitive); break;
				case Ramp ramp:						ic = ramp.SetupGameObject(obj, rog); break;
				case Rubber rubber:					ic = rubber.SetupGameObject(obj, rog); break;
				case Spinner spinner:				ic = spinner.SetupGameObject(obj, rog); break;
				case Surface surface:				ic = surface.SetupGameObject(obj, rog); break;
				case Table table:					ic = table.SetupGameObject(obj, rog); break;
				case Trigger trigger:				ic = trigger.SetupGameObject(obj, rog); break;
			}
#if UNITY_EDITOR
			// for convenience move item behavior to the top of the list
			if (ic != null) {
				int numComp = obj.GetComponents<MonoBehaviour>().Length;
				for (int i = 0; i <= numComp; i++) {
					UnityEditorInternal.ComponentUtility.MoveComponentUp(ic);
				}
			}
#endif
			return createdObjs;
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

			// copy each texture ref into the sidecar's serialized storage
			sidecar.textures.AddRange(table.Textures);
			// and tell the engine's table to now use the sidecar as its container so we can all operate on the same underlying container
			table.SetTextureContainer(sidecar.textures);

			// copy each sound ref into the sidecar's serialized storage
			sidecar.sounds.AddRange(table.Sounds);
			// and tell the engine's table to now use the sidecar as its container so we can all operate on the same underlying container
			table.SetSoundContainer(sidecar.sounds);

			sidecar.customInfoTags = table.CustomInfoTags;
			sidecar.collections = table.Collections.Values.Select(c => c.Data).ToArray();
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
