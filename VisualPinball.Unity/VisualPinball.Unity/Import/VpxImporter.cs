// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ClassNeverInstantiated.Global

using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Bumper;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.HitTarget;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Light;
using VisualPinball.Unity.VPT.Plunger;
using VisualPinball.Unity.VPT.Primitive;
using VisualPinball.Unity.VPT.Ramp;
using VisualPinball.Unity.VPT.Rubber;
using VisualPinball.Unity.VPT.Spinner;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Table;
using VisualPinball.Unity.VPT.Trigger;
using Logger = NLog.Logger;
using Player = VisualPinball.Unity.Game.Player;

namespace VisualPinball.Unity.Import
{
	public class VpxImporter : MonoBehaviour
	{
		private static readonly Quaternion GlobalRotation = Quaternion.Euler(-90, 0, 0);
		public const float GlobalScale = 0.001f;
		public const int ChildObjectsLayer = 8;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly Dictionary<IRenderable, RenderObjectGroup> _renderObjects = new Dictionary<IRenderable, RenderObjectGroup>();
		private readonly Dictionary<string, GameObject> _parents = new Dictionary<string, GameObject>();

		private Table _table;
		private TableBehavior _tb;
		private bool _applyPatch = true;

		public void Import(string fileName, Table table, bool applyPatch = true, string tableName = null)
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


			_tb.Patcher = new Patcher.Patcher.Patcher(_table, fileName);

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
			ImportTextures();
			ImportGameItems();

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-_table.Width / 2 * GlobalScale, 0f, _table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			//ScaleNormalizer.Normalize(go, GlobalScale);

			// finally, add the player script
			go.AddComponent<Player>();
		}

		public static void ImportRenderObject(IRenderable item, RenderObject ro, GameObject obj, TableBehavior table)
		{
			if (ro.Mesh == null) {
				Logger.Warn($"No mesh for object {obj.name}, skipping.");
				return;
			}

			var mesh = ro.Mesh.ToUnityMesh($"{obj.name}_mesh");

			// apply mesh to game object
			var mf = obj.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			var mr = obj.AddComponent<MeshRenderer>();
			mr.sharedMaterial = ro.Material.ToUnityMaterial(table);
			mr.enabled = ro.IsVisible;

			// patch
			table.Patcher.ApplyPatches(item, ro, obj);
		}

		private void ImportTextures()
		{
			foreach (var kvp in _table.Textures) {
				_tb.AddTexture(kvp.Key, kvp.Value.ToUnityTexture());
			}
		}

		private void ImportGameItems()
		{
			// import game objects
			ImportRenderables();
		}

		private void ImportRenderables()
		{
			foreach (var renderable in _renderObjects.Keys) {
				var ro = _renderObjects[renderable];
				if (!_parents.ContainsKey(ro.Parent)) {
					var parent = new GameObject(ro.Parent);
					parent.transform.parent = gameObject.transform;
					_parents[ro.Parent] = parent;
				}
				ImportRenderObjects(renderable, ro, _parents[ro.Parent]);
			}
		}

		private void ImportRenderObjects(IRenderable item, RenderObjectGroup rog, GameObject parent)
		{
			var obj = new GameObject(rog.Name);
			obj.transform.parent = parent.transform;

			if (rog.HasOnlyChild && !rog.ForceChild) {
				ImportRenderObject(item, rog.RenderObjects[0], obj, _tb);
			} else if (rog.HasChildren) {
				foreach (var ro in rog.RenderObjects) {
					var subObj = new GameObject(ro.Name);
					subObj.transform.SetParent(obj.transform, false);
					subObj.layer = ChildObjectsLayer;
					ImportRenderObject(item, ro, subObj, _tb);
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
				case Kicker kicker:					ic = obj.AddComponent<KickerBehavior>().SetData(kicker.Data); break;
				case Engine.VPT.Light.Light lt:		ic = lt.SetupGameObject(obj, rog); break;
				case Plunger plunger:				ic = plunger.SetupGameObject(obj, rog); break;
				case Primitive primitive:			ic = obj.AddComponent<PrimitiveBehavior>().SetData(primitive.Data); break;
				case Ramp ramp:						ic = obj.AddComponent<RampBehavior>().SetData(ramp.Data); break;
				case Rubber rubber:					ic = obj.AddComponent<RubberBehavior>().SetData(rubber.Data); break;
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
		}

		private void MakeSerializable(GameObject go, Table table)
		{
			// add table component (plus other data)
			_tb = go.AddComponent<TableBehavior>();
			_tb.SetItemAndData(table);

			var sidecar = _tb.GetOrCreateSidecar();

			foreach (var key in table.TableInfo.Keys) {
				sidecar.tableInfo[key] = table.TableInfo[key];
			}
			sidecar.textures = table.Textures.Values.Select(d => d.Data).ToArray();
			sidecar.customInfoTags = table.CustomInfoTags;
			sidecar.collections = table.Collections.Values.Select(c => c.Data).ToArray();
			sidecar.decals = table.Decals.Select(d => d.Data).ToArray();
			sidecar.dispReels = table.DispReels.Values.Select(d => d.Data).ToArray();
			sidecar.flashers = table.Flashers.Values.Select(d => d.Data).ToArray();
			sidecar.lightSeqs = table.LightSeqs.Values.Select(d => d.Data).ToArray();
			sidecar.plungers = table.Plungers.Values.Select(d => d.Data).ToArray();
			sidecar.sounds = table.Sounds.Values.Select(d => d.Data).ToArray();
			sidecar.textBoxes = table.TextBoxes.Values.Select(d => d.Data).ToArray();
			sidecar.timers = table.Timers.Values.Select(d => d.Data).ToArray();

			Logger.Info("Collections saved: [ {0} ] [ {1} ]",
				string.Join(", ", table.Collections.Keys),
				string.Join(", ", sidecar.collections.Select(c => c.Name))
			);
		}
	}
}
