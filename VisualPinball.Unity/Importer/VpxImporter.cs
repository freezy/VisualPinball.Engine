// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ClassNeverInstantiated.Global

using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Importer.AssetHandler;
using VisualPinball.Unity.Importer.Job;
using Logger = NLog.Logger;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer
{
	public class VpxImporter : MonoBehaviour
	{
		private static readonly Quaternion GlobalRotation = Quaternion.Euler(-90, 0, 0);
		private const float GlobalScale = 0.01f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private Patcher.Patcher.Patcher _patcher;

		private readonly Dictionary<IRenderable, RenderObjectGroup> _renderObjects = new Dictionary<IRenderable, RenderObjectGroup>();
		private readonly Dictionary<string, GameObject> _parents = new Dictionary<string, GameObject>();

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
			ImportMaterials(materials);
			ImportGameItems();
			ImportGiLights();

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-_table.Width / 2 * GlobalScale, 0f, _table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			//ScaleNormalizer.Normalize(go, GlobalScale);

			// add table component
			_table.AddComponent(go);
		}

		private void ImportTextures()
		{
			// import textures
			Profiler.Start("TextureImporter");
			var textureImporter = new TextureImporter(
				_table.Textures.Values.Concat(Texture.LocalTextures).ToArray(),
				_assetHandler
			);
			textureImporter.ImportTextures();
			Profiler.Stop("TextureImporter");
		}

		private void ImportMaterials(Dictionary<string, PbrMaterial> materials)
		{
			// import materials
			Profiler.Start("MaterialImporter");
			var materialImporter = new MaterialImporter(
				materials.Values.ToArray(),
				_assetHandler
			);
			materialImporter.ImportMaterials();
			Profiler.Stop("MaterialImporter");
		}

		private void ImportGameItems()
		{
			Profiler.Start("VpxImporter.ImportGameItems()");

			// import game objects
			ImportRenderables();
			_assetHandler.OnMeshesImported(gameObject);

			Profiler.Stop("VpxImporter.ImportGameItems()");
		}

		private void ImportGiLights()
		{
			var lightsObj = new GameObject("Lights");
			lightsObj.transform.parent = gameObject.transform;
			foreach (var vpxLight in _table.Lights.Values.Where(l => l.Data.IsBulbLight)) {
				var unityLight = vpxLight.ToUnityPointLight();
				unityLight.transform.parent = lightsObj.transform;
			}
		}

		private void ImportRenderables()
		{
			Profiler.Start("VpxImporter.ImportRenderables()");
			foreach (var renderable in _renderObjects.Keys) {
				var ro = _renderObjects[renderable];
				if (!_parents.ContainsKey(ro.Parent)) {
					var parent = new GameObject(ro.Parent);
					parent.transform.parent = gameObject.transform;
					_parents[ro.Parent] = parent;
				}
				ImportRenderObjects(renderable, ro, _parents[ro.Parent]);
			}
			Profiler.Stop("VpxImporter.ImportRenderables()");
		}

		private void ImportRenderObjects(IRenderable item, RenderObjectGroup rog, GameObject parent)
		{
			var obj = new GameObject(rog.Name);
			obj.transform.parent = parent.transform;

			if (rog.HasOnlyChild) {
				ImportRenderObject(item, rog.RenderObjects[0], obj);

			} else if (rog.HasChildren) {
				foreach (var ro in rog.RenderObjects) {
					var subObj = new GameObject(ro.Name);
					subObj.transform.parent = obj.transform;
					ImportRenderObject(item, ro, subObj);
				}
			}

			// apply transformation
			if (rog.HasChildren) {
				SetTransform(obj.transform, rog.TransformationMatrix.ToUnityMatrix());
			}

			if (rog.Parent == "Flippers") {
				Logger.Info("Adding flipper component!");
				(item as Flipper).AddComponent(obj);
			}
		}

		private void ImportRenderObject(IRenderable item, RenderObject ro, GameObject obj)
		{
			if (ro.Mesh == null) {
				Logger.Warn($"No mesh for object {obj.name}, skipping.");
				return;
			}

			Profiler.Start("ToUnityMesh");
			var mesh = ro.Mesh.ToUnityMesh($"{obj.name}_mesh");
			Profiler.Stop("ToUnityMesh");
			obj.SetActive(ro.IsVisible);

			// apply mesh to game object
			var mf = obj.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			var mr = obj.AddComponent<MeshRenderer>();
			mr.sharedMaterial = _assetHandler.LoadMaterial(ro.Material);

			// patch
			Profiler.Start("Patch & Assets");
			_patcher.ApplyPatches(item, ro, obj);

			// add mesh to asset
			_assetHandler.SaveMesh(mesh, item.Name);
			Profiler.Stop("Patch & Assets");
		}

		private static void SetTransform(Transform tf, Matrix4x4 trs)
		{
			tf.localScale = new Vector3(
				trs.GetColumn(0).magnitude,
				trs.GetColumn(1).magnitude,
				trs.GetColumn(2).magnitude
			);
			//Logger.Info($"Scaling at {trs.GetColumn(0).magnitude}/{trs.GetColumn(1).magnitude}/{trs.GetColumn(2).magnitude}");
			tf.localPosition = trs.GetColumn(3);
			tf.localRotation = Quaternion.LookRotation(
				trs.GetColumn(2),
				trs.GetColumn(1)
			);
		}
	}
}
