// ReSharper disable ConvertIfStatementToReturnStatement

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.IO;
using Logger = NLog.Logger;
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

		private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

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
			var watch = Stopwatch.StartNew();

			// open file dialog
			var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", null, new[] { "Visual Pinball Table Files", "vpx" });
			if (vpxPath.Length == 0) {
				return;
			}

			var rootGameObj = ImportVpx(vpxPath, true);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, menuCommand.context as GameObject);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, "Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;

			watch.Stop();
			Logger.Info("[VpxImporter] Imported in {0}ms.", watch.ElapsedMilliseconds);
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
			var table = Table.Load(path);
			var go = gameObject;
			go.name = table.Name;
			_patcher = new Patcher.Patcher.Patcher(table, Path.GetFileName(path));

			// set paths
			_saveToAssets = saveToAssets;
			if (_saveToAssets) {
				_tableFolder = $"Assets/{Path.GetFileNameWithoutExtension(path)}";
				_materialFolder = $"{_tableFolder}/Materials";
				_textureFolder = $"{_tableFolder}/Textures";
				_tableDataPath = $"{_tableFolder}/{AssetUtility.StringToFilename(table.Name)}_data.asset";
				_tablePrefabPath = $"{_tableFolder}/{AssetUtility.StringToFilename(table.Name)}.prefab";
				AssetUtility.CreateFolders(_tableFolder, _materialFolder, _textureFolder);
			}

			// create asset object
			_asset = ScriptableObject.CreateInstance<VpxAsset>();
			AssetDatabase.SaveAssets();

			// import textures
			ImportTextures(table);

			// import table
			ImportGameItems(table);

			// import lights
			ImportGiLights(table);

			// set root transformation
			go.transform.localRotation = GlobalRotation;
			go.transform.localPosition = new Vector3(-table.Width / 2 * GlobalScale, 0f, -table.Height / 2 * GlobalScale);
			go.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
			ScaleNormalizer.Normalize(go, GlobalScale);
		}

		private void ImportTextures(Table table)
		{
			foreach (var texture in table.Textures.Values) {
				SaveTexture(texture);
			}

			// also import local textures
			foreach (var texture in Texture.LocalTextures) {
				SaveTexture(texture);
			}
		}

		private void ImportGameItems(Table table)
		{
			// save game objects to asset folder
			if (_saveToAssets) {
				AssetDatabase.CreateAsset(_asset, _tableDataPath);
				AssetDatabase.SaveAssets();
			}

			// import game objects
			ImportRenderables(table);

			if (_saveToAssets) {
				PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, _tablePrefabPath, InteractionMode.UserAction);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
		}

		private void ImportGiLights(Table table)
		{
			var lightsObj = new GameObject("Lights");
			lightsObj.transform.parent = gameObject.transform;
			foreach (var vpxLight in table.Lights.Values.Where(l => l.Data.IsBulbLight)) {
				var unityLight = vpxLight.ToUnityPointLight();
				unityLight.transform.parent = lightsObj.transform;
			}
		}

		private void ImportRenderables(Table table)
		{
			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = gameObject.transform;
			foreach (var renderable in table.Renderables) {
				ImportRenderObjects(renderable, renderable.GetRenderObjects(table, Origin.Original, false), renderable.Name, primitivesObj);
			}
		}

		private void ImportRenderObjects(IRenderable item, IReadOnlyList<RenderObject> renderObjects, string objectName, GameObject parent)
		{
			var obj = new GameObject(objectName);
			obj.transform.parent = parent.transform;

			if (renderObjects.Count == 1) {
				ImportRenderObject(item, renderObjects[0], obj);

			} else if (renderObjects.Count > 1) {
				foreach (var ro in renderObjects) {
					var subObj = new GameObject(ro.Name);
					subObj.transform.parent = obj.transform;
					ImportRenderObject(item, ro, subObj);
				}
			}

			// apply transformation
			if (renderObjects.Count > 0) {
				SetTransform(obj.transform, renderObjects[0].TransformationMatrix.ToUnityMatrix());
			}
		}

		private void ImportRenderObject(IRenderable item, RenderObject renderObject, GameObject obj)
		{
			if (renderObject.Mesh == null) {
				Logger.Warn($"No mesh for object {obj.name}, skipping.");
				return;
			}
			var mesh = renderObject.Mesh.ToUnityMesh($"{obj.name}_mesh");
			obj.SetActive(renderObject.IsVisible);


			// apply mesh to game object
			var mf = obj.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			var mr = obj.AddComponent<MeshRenderer>();
			mr.sharedMaterial = GetMaterial(renderObject, obj.name);
			if (mr.sharedMaterial.name == RenderObject.MaterialNameNoMaterial) {
				mr.enabled = false;
			}

			// patch
			_patcher.ApplyPatches(item, renderObject, obj);

			// add mesh to asset
			if (_saveToAssets) {
				AssetDatabase.AddObjectToAsset(mesh, asset);
			}
		}

		private Material GetMaterial(RenderObject ro, string objectName)
		{
			var material = LoadMaterial(ro);
			if (material == null) {
				material = ro.Material?.ToUnityMaterial(ro) ?? new Material(Shader.Find("Standard"));
				if (ro.Map != null) {
					material.SetTexture(MainTex, LoadTexture(ro.Map));
				}
				if (ro.NormalMap != null) {
					material.SetTexture(BumpMap, LoadTexture(ro.NormalMap));
				}
				SaveMaterial(ro, material);
			}

			return material;
		}

		private void SaveTexture(Texture texture)
		{
			if (_saveToAssets) {
				AssetUtility.CreateTexture(texture, _textureFolder);
			} else {
				_textures[texture.Name] = texture.ToUnityTexture();
			}
		}

		private Texture2D LoadTexture(Texture texture)
		{
			if (_saveToAssets) {
				return AssetDatabase.LoadAssetAtPath<Texture2D>(texture.GetUnityFilename(_textureFolder));
			}
			return _textures[texture.Name];
		}

		private void SaveMaterial(RenderObject ro, Material material)
		{
			if (_saveToAssets) {
				var assetPath = $"{_materialFolder}/{ro.MaterialId}.mat";
				AssetDatabase.CreateAsset(material, assetPath);
			} else {
				_materials[ro.MaterialId] = material;
			}
		}

		private Material LoadMaterial(RenderObject ro)
		{
			if (_saveToAssets) {
				var assetPath = $"{_materialFolder}/{ro.MaterialId}.mat";
				return AssetDatabase.LoadAssetAtPath<Material>(assetPath);
			}

			return _materials.ContainsKey(ro.MaterialId) ? _materials[ro.MaterialId] : null;
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
