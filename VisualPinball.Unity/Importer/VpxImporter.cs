// ReSharper disable ConvertIfStatementToReturnStatement

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using Logger = NLog.Logger;
using Logging = VisualPinball.Unity.IO.Logging;
using Material = UnityEngine.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer
{
	public class VpxImporter : MonoBehaviour
	{
		private static readonly Quaternion GlobalRotation = Quaternion.Euler(-90, 0, 0);
		private const float GlobalScale = 0.01f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private VpxAsset _asset;
		private bool _saveToAssets;
		private string _tableFolder;
		private string _materialFolder;
		private string _textureFolder;
		private string _tableDataPath;
		private string _tablePrefabPath;

		private Patcher.Patcher.Patcher _patcher;

		private readonly Dictionary<IRenderable, RenderObjectGroup> _renderObjects = new Dictionary<IRenderable, RenderObjectGroup>();
		private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
		private readonly Dictionary<string, GameObject> _parents = new Dictionary<string, GameObject>();

		private Table _table;

		public static void ImportVpxRuntime(string path)
		{
			ImportVpx(path, false);
		}

		/// <summary>
		/// Imports a Visual Pinball File (.vpx) into the Unity Editor. <p/>
		///
		/// The goal of this is to be able to iterate rapidly without having to
		/// execute the runtime on every test. This importer also saves the
		/// imported data to the Assets folder so a project with an imported table
		/// can be saved and loaded
		/// </summary>
		/// <param name="menuCommand">Context provided by the Editor</param>
		[MenuItem("Visual Pinball/Import VPX", false, 10)]
		public static void ImportVpxEditor(MenuCommand menuCommand)
		{
			// TODO that somewhere else
			Logging.Setup();

			// open file dialog
			var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", null, new[] { "Visual Pinball Table Files", "vpx" });
			if (vpxPath.Length == 0) {
				return;
			}

			Profiler.Start("VpxImporter.ImportVpxEditor()");
			var rootGameObj = ImportVpx(vpxPath, true);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, menuCommand.context as GameObject);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, "Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;

			Profiler.Stop("VpxImporter.ImportVpxEditor()");
			Logger.Info("[VpxImporter] Imported!");
			Profiler.Print();
			Profiler.Reset();
		}

		private static GameObject ImportVpx(string path, bool saveToAssets) {

			// create root object
			var rootGameObj = new GameObject();
			var importer = rootGameObj.AddComponent<VpxImporter>();

			importer.Import(path, saveToAssets);

			return rootGameObj;
		}

		private void Import(string path, bool saveToAssets)
		{
			// parse table
			Profiler.Start("VpxImporter.Import()");
			_table = TableLoader.LoadTable(path);
			//var table = Table.Load(path);

			var go = gameObject;
			go.name = _table.Name;
			_patcher = new Patcher.Patcher.Patcher(_table, Path.GetFileName(path));

			// set paths
			_saveToAssets = saveToAssets;
			if (_saveToAssets) {
				_tableFolder = $"Assets/{Path.GetFileNameWithoutExtension(path)?.Trim()}";
				_materialFolder = $"{_tableFolder}/Materials";
				_textureFolder = $"{_tableFolder}/Textures";
				_tableDataPath = $"{_tableFolder}/{AssetUtility.StringToFilename(_table.Name)}_data.asset";
				_tablePrefabPath = $"{_tableFolder}/{AssetUtility.StringToFilename(_table.Name)}.prefab";
				AssetUtility.CreateFolders(_tableFolder, _materialFolder, _textureFolder);
			}

			// create asset object
			_asset = ScriptableObject.CreateInstance<VpxAsset>();
			AssetDatabase.SaveAssets();

			// generate meshes now
			var materials = new Dictionary<string, PbrMaterial>();
			foreach (var r in _table.Renderables) {
				_renderObjects[r] = r.GetRenderObjects(_table, Origin.Original, false);
				foreach (var ro in _renderObjects[r].RenderObjects) {
					if (!materials.ContainsKey(ro.Material.Id)) {
						materials[ro.Material.Id] = ro.Material;
					}
				}
			}

			// import textures
			Profiler.Start("TextureImporter");
			var textureImporter = new TextureImporter(_table.Textures.Values.Concat(Texture.LocalTextures).ToArray());
			textureImporter.ImportTextures(_textureFolder);
			Profiler.Stop("TextureImporter");

			// import materials
			Profiler.Start("ImportMaterials via Job");
			var materialImporter = new MaterialImporter(materials.Values.ToArray());
			materialImporter.ImportMaterials(_materialFolder, _textureFolder);
			Profiler.Stop("ImportMaterials via Job");

			// import table
			ImportGameItems();

			// import lights
			ImportGiLights();

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-_table.Width / 2 * GlobalScale, 0f, -_table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			//ScaleNormalizer.Normalize(go, GlobalScale);
			Profiler.Stop("VpxImporter.Import()");
		}

		private void ImportGameItems()
		{
			Profiler.Start("VpxImporter.ImportGameItems()");

			// save game objects to asset folder
			if (_saveToAssets) {
				AssetDatabase.CreateAsset(_asset, _tableDataPath);
				AssetDatabase.SaveAssets();
			}

			// import game objects
			ImportRenderables();

			if (_saveToAssets) {
				Profiler.Start("ItemsAssets");
				Profiler.Start("PrefabUtility.SaveAsPrefabAssetAndConnect");
				PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, _tablePrefabPath, InteractionMode.UserAction);
				Profiler.Stop("PrefabUtility.SaveAsPrefabAssetAndConnect");
				Profiler.Start("AssetDatabase.SaveAssets");
				AssetDatabase.SaveAssets();
				Profiler.Stop("AssetDatabase.SaveAssets");
				Profiler.Start("AssetDatabase.Refresh");
				AssetDatabase.Refresh();
				Profiler.Stop("AssetDatabase.Refresh");
				Profiler.Stop("ItemsAssets");
			}

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
				Profiler.Start("Convert");
				ImportRenderObjects(renderable, ro, _parents[ro.Parent]);
				Profiler.Stop("Convert");
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
			mr.sharedMaterial = LoadMaterial(ro.Material);
			if (mr.sharedMaterial.name == PbrMaterial.NameNoMaterial) {
				mr.enabled = false;
			}

			// patch
			Profiler.Start("Patch & Assets");
			_patcher.ApplyPatches(item, ro, obj);

			// add mesh to asset
			if (_saveToAssets) {
				AssetDatabase.AddObjectToAsset(mesh, _asset);
			}
			Profiler.Stop("Patch & Assets");
		}

		private Material LoadMaterial(PbrMaterial mat)
		{
			if (_saveToAssets) {
				return AssetDatabase.LoadAssetAtPath<Material>(mat.GetUnityFilename(_materialFolder));
			}

			return _materials.ContainsKey(mat.Id) ? _materials[mat.Id] : null;
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

	internal class VpxAsset : ScriptableObject
	{
	}
}
