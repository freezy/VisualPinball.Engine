using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private bool _saveToAssets;
		private string _tableFolder;
		private string _materialFolder;
		private string _textureFolder;
		private string _tableDataPath;
		private string _tablePrefabPath;

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
			var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", "Assets/", new[] { "Visual Pinball Table Files", "vpx" });
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
			gameObject.name = table.Name;

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
			var asset = ScriptableObject.CreateInstance<VpxAsset>();

			// import textures
			ImportTextures(table);

			// import table
			ImportGameItems(table, asset);
		}

		private void ImportTextures(Table table)
		{
			foreach (var texture in table.Textures.Values) {
				SaveTexture(texture);
			}
		}

		private void ImportGameItems(Table table, VpxAsset asset)
		{
			// save game objects to asset folder
			if (_saveToAssets) {
				AssetDatabase.CreateAsset(asset, _tableDataPath);
				AssetDatabase.SaveAssets();
			}

			// import game objects
			ImportRenderables(table, asset);

			if (_saveToAssets) {
				PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, _tablePrefabPath, InteractionMode.UserAction);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
		}

		private void ImportRenderables(Table table, VpxAsset asset)
		{
			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = gameObject.transform;
			foreach (var renderable in table.Renderables) {
				ImportRenderObjects(renderable.GetRenderObjects(table), renderable.Name, primitivesObj, asset);
			}
		}

		private void ImportRenderObjects(RenderObject[] renderObjects, string objectName, GameObject parent, VpxAsset asset)
		{
			var obj = new GameObject(objectName);
			obj.transform.parent = parent.transform;

			if (renderObjects.Length == 1) {
				ImportRenderObject(renderObjects[0], obj, asset);

			} else if (renderObjects.Length > 1) {
				foreach (var ro in renderObjects) {
					var subObj = new GameObject(ro.Name);
					subObj.transform.parent = obj.transform;
					ImportRenderObject(ro, subObj, asset);
				}
			}
		}

		private void ResetGOOrigin(GameObject obj, UnityEngine.Mesh mesh) {



			Quaternion rot = obj.transform.rotation;
			obj.transform.rotation = Quaternion.identity;
			var vertices = mesh.vertices;

			int len = vertices.Length;
			int v;
			Vector3 c = Vector3.zero;
			for (v = 0; v < len; v++)
			{
				c += vertices[v];
			}

			c /= len;
			Vector3 d = Vector3.zero;
			for (v = 0; v < len; v++)
			{
				d += obj.transform.TransformPoint(vertices[v]);
			}
			d /= len;
			Matrix4x4 trs = Matrix4x4.TRS(-(c), Quaternion.identity, Vector3.one);
			for (v = 0; v < len; v++)
			{
				vertices[v] = trs.MultiplyPoint(vertices[v]);

			}

			mesh.vertices = vertices;
			mesh.RecalculateBounds();

			Undo.RecordObject(obj, "set origin of parent");

			obj.transform.position = d;
			obj.transform.rotation = rot;




		}

		private void ImportRenderObject(RenderObject renderObject, GameObject obj, VpxAsset asset)
		{
			if (renderObject.Mesh == null) {
				Logger.Warn($"No mesh for object {obj.name}, skipping.");
				return;
			}
			var mesh = renderObject.Mesh.ToUnityMesh($"{obj.name}_mesh");


			//resetgameObject origin
			ResetGOOrigin(obj, mesh);


			// apply mesh to game object
			var mf = obj.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			var mr = obj.AddComponent<MeshRenderer>();
			mr.sharedMaterial = GetMaterial(renderObject, obj.name);

			// add mesh to asset
			if (_saveToAssets) {
				AssetDatabase.AddObjectToAsset(mesh, asset);
			}
		}

		private Material GetMaterial(RenderObject ro, string objectName)
		{
			var material = LoadMaterial(ro);
			if (material == null) {
				material = ro.Material?.ToUnityMaterial() ?? new Material(Shader.Find("Standard"));
				if (ro.Map != null)
				{
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

			UnityEngine.Texture2D tex = texture.ToUnityTexture();
			string path = texture.GetUnityFilename(_textureFolder);

			if (_saveToAssets) {
				//AssetDatabase.CreateAsset(tex, path);
				
				byte[] bytes = tex.EncodeToPNG();
				File.WriteAllBytes(path, bytes);
				AssetDatabase.ImportAsset(path);

				TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
				textureImporter.alphaIsTransparency = true;
				textureImporter.isReadable = true;
				textureImporter.mipmapEnabled = false;
				textureImporter.filterMode = FilterMode.Bilinear;
				EditorUtility.CompressTexture(AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D, TextureFormat.ARGB32, UnityEditor.TextureCompressionQuality.Best);
				AssetDatabase.ImportAsset(path);
			} else {
				_textures[texture.Name] = tex;
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
	}

	internal class VpxAsset : ScriptableObject
	{
	}
}
