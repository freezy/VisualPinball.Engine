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
using VisualPinball.Unity.Import.AssetHandler;
using VisualPinball.Unity.Import.Job;
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
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Import
{
	public class VpxImporter : MonoBehaviour, ITextureStore
	{
		private static readonly Quaternion GlobalRotation = Quaternion.Euler(-90, 0, 0);
		public const float GlobalScale = 0.001f;
		public const int ChildObjectsLayer = 8;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Patcher.Patcher.Patcher _patcher;

		private readonly Dictionary<IRenderable, RenderObjectGroup> _renderObjects = new Dictionary<IRenderable, RenderObjectGroup>();
		private readonly Dictionary<string, GameObject> _parents = new Dictionary<string, GameObject>();
		private readonly Dictionary<string, Texture2D> _unityTextures = new Dictionary<string, Texture2D>();

		private Table _table;
		private IAssetHandler _assetHandler;

		public void Import(string fileName, Table table, IAssetHandler assetHandler)
		{
			_table = table;
			_assetHandler = assetHandler;

			var go = gameObject;
			go.name = _table.Name;
			_patcher = new Patcher.Patcher.Patcher(_table, fileName);

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
			//ImportMaterials(materials);
			ImportGameItems();

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-_table.Width / 2 * GlobalScale, 0f, _table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			//ScaleNormalizer.Normalize(go, GlobalScale);

			MakeSerializable(go, table, assetHandler);

			// finally, add the player script
			go.AddComponent<Player>();
		}

		public void AddTexture(string name, Texture2D texture)
		{
			_unityTextures[name.ToLower()] = texture;
		}

		public Texture2D GetTexture(string name)
		{
			var lowerName = name.ToLower();
			if (_unityTextures.ContainsKey(lowerName)) {
				return _unityTextures[lowerName];
			}
			return null;
		}

		private void ImportTextures()
		{
			// import textures
			var textureImporter = new TextureImporter(
				_table.Textures.Values.Concat(Texture.LocalTextures).ToArray(),
				_assetHandler
			);
			textureImporter.ImportTextures();

			foreach (var kvp in _table.Textures) {
				AddTexture(kvp.Key, kvp.Value.ToUnityTexture());
			}
		}

		private void ImportMaterials(Dictionary<string, PbrMaterial> materials)
		{
			// import materials
			var materialImporter = new MaterialImporter(
				materials.Values.ToArray(),
				_assetHandler
			);
			materialImporter.ImportMaterials();
		}

		private void ImportGameItems()
		{

			// import game objects
			ImportRenderables();
			_assetHandler.OnMeshesImported(gameObject);
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
				ImportRenderObject(item, rog.RenderObjects[0], obj);
			} else if (rog.HasChildren) {
				foreach (var ro in rog.RenderObjects) {
					var subObj = new GameObject(ro.Name);
					subObj.transform.parent = obj.transform;
					subObj.layer = ChildObjectsLayer;
					ImportRenderObject(item, ro, subObj);
				}
			}

			// apply transformation
			if (rog.HasChildren) {
				obj.transform.SetFromMatrix(rog.TransformationMatrix.ToUnityMatrix());
			}

			// add unity component
			MonoBehaviour ic = null;
			switch (item) {
				case Bumper bumper:					ic = bumper.SetupGameObject(obj, rog); break;
				case Flipper flipper:				ic = flipper.SetupGameObject(obj, rog); break;
				case Gate gate:						ic = gate.SetupGameObject(obj, rog); break;
				case HitTarget hitTarget:			ic = obj.AddComponent<HitTargetBehavior>().SetData(hitTarget.Data); break;
				case Kicker kicker:					ic = obj.AddComponent<KickerBehavior>().SetData(kicker.Data); break;
				case Engine.VPT.Light.Light lt:		ic = lt.SetupGameObject(obj, rog); break;
				case Plunger plunger:				ic = plunger.SetupGameObject(obj, rog); break;
				case Primitive primitive:			ic = obj.AddComponent<PrimitiveBehavior>().SetData(primitive.Data); break;
				case Ramp ramp:						ic = obj.AddComponent<RampBehavior>().SetData(ramp.Data); break;
				case Rubber rubber:					ic = obj.AddComponent<RubberBehavior>().SetData(rubber.Data); break;
				case Spinner spinner:				ic = spinner.SetupGameObject(obj, rog); break;
				case Surface surface:				ic = surface.SetupGameObject(obj, rog); break;
				case Table table:					ic = table.SetupGameObject(obj, rog); break;
				case Trigger trigger:				ic = obj.AddComponent<TriggerBehavior>().SetData(trigger.Data); break;
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
		private void ImportRenderObject(IRenderable item, RenderObject ro, GameObject obj)
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
			//mr.sharedMaterial = _assetHandler.LoadMaterial(ro.Material);
			mr.sharedMaterial = ro.Material.ToUnityMaterial(null, this);
			mr.enabled = ro.IsVisible;

			// patch
			_patcher.ApplyPatches(item, ro, obj);

			// add mesh to asset
			_assetHandler.SaveMesh(mesh, item.Name);
		}

		private void MakeSerializable(GameObject go, Table table, IAssetHandler assetHandler)
		{
			// add table component (plus other data)
			var component = go.AddComponent<TableBehavior>();
			component.AssetHandler = assetHandler;
			component.SetData(table.Data);
			foreach (var key in table.TableInfo.Keys) {
				component.tableInfo[key] = table.TableInfo[key];
			}
			component.textureFolder = assetHandler.TextureFolder;
			component.textures = table.Textures.Values.Select(d => d.Data).ToArray();
			component.customInfoTags = table.CustomInfoTags;
			component.collections = table.Collections.Values.Select(c => c.Data).ToArray();
			component.decals = table.Decals.Select(d => d.Data).ToArray();
			component.dispReels = table.DispReels.Values.Select(d => d.Data).ToArray();
			component.flashers = table.Flashers.Values.Select(d => d.Data).ToArray();
			component.lightSeqs = table.LightSeqs.Values.Select(d => d.Data).ToArray();
			component.plungers = table.Plungers.Values.Select(d => d.Data).ToArray();
			component.sounds = table.Sounds.Values.Select(d => d.Data).ToArray();
			component.textBoxes = table.TextBoxes.Values.Select(d => d.Data).ToArray();
			component.timers = table.Timers.Values.Select(d => d.Data).ToArray();
			foreach (var kvp in _unityTextures) {
				component.AddTexture(kvp.Key, kvp.Value);
			}

			Logger.Info("Collections saved: [ {0} ] [ {1} ]",
				string.Join(", ", table.Collections.Keys),
				string.Join(", ", component.collections.Select(c => c.Name))
			);
		}
	}
}
