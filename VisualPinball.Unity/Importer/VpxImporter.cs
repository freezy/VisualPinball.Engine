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
		//move declaration from local in method to class as it was loosing reference while building .. it you think you can solve that , be my guest ;) 
		internal VpxAsset asset;

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
			
			asset = ScriptableObject.CreateInstance<VpxAsset>();			
			AssetDatabase.SaveAssets();
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


		private void ImportRenderObject(RenderObject renderObject, GameObject obj, VpxAsset asset)
		{
			if (renderObject.Mesh == null) {
				Logger.Warn($"No mesh for object {obj.name}, skipping.");
				return;
			}
			var mesh = renderObject.Mesh.ToUnityMesh($"{obj.name}_mesh");
			CalculateTangents(mesh);

			//resetgameObject origin
			ResetGOOrigin(obj, mesh);

			// add mesh to asset
			if (_saveToAssets) {
				AssetDatabase.AddObjectToAsset(mesh, asset);
			}


			// apply mesh to game object
			var mf = obj.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			var mr = obj.AddComponent<MeshRenderer>();
			mr.sharedMaterial = GetMaterial(renderObject, obj.name);

			if (mr.sharedMaterial.name == "__no_material") {
				mr.enabled = false;

			}

			
		}

		private Material GetMaterial(RenderObject ro, string objectName) {
			
			var material = LoadMaterial(ro);
			if (material == null) {
				
				material = ro.Material?.ToUnityMaterial(ro) ?? new Material(Shader.Find("Standard"));
				if (ro.Map != null) {
					UnityEngine.Texture tex = LoadTexture(ro.Map, ".png");
					string path = AssetDatabase.GetAssetPath(tex);					
					TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
					textureImporter.textureType = TextureImporterType.Default;
					textureImporter.alphaIsTransparency = true;
					textureImporter.isReadable = true;
					textureImporter.mipmapEnabled = true;
					textureImporter.filterMode = FilterMode.Bilinear;
					EditorUtility.CompressTexture(AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D, TextureFormat.ARGB32, UnityEditor.TextureCompressionQuality.Best);
					AssetDatabase.ImportAsset(path);
					material.SetTexture(MainTex, tex);
				}

				if (ro.NormalMap != null) {
					UnityEngine.Texture tex = LoadTexture(ro.NormalMap, ".png");
					string path = AssetDatabase.GetAssetPath(tex);					
					TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
					textureImporter.textureType = TextureImporterType.NormalMap;
					textureImporter.isReadable = true;
					textureImporter.mipmapEnabled = true;
					textureImporter.filterMode = FilterMode.Bilinear;
					EditorUtility.CompressTexture(AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D, TextureFormat.ARGB32, UnityEditor.TextureCompressionQuality.Best);
					AssetDatabase.ImportAsset(path);
					material.SetTexture(BumpMap, tex);


					//---------------------------

				}
				SaveMaterial(ro, material);


			
			} 

			return material;
		}

		private void SaveTexture(Texture texture)
		{

			
			UnityEngine.Texture2D tex;
			if (!texture.IsHdr) {
				tex = texture.ToUnityTexture();
			} else {
				tex = texture.ToUnityHDRTexture();				
				Logger.Info("SaveTexture");
				Logger.Info("tex.width  " + tex.width);
				Logger.Info("tex.height  " + tex.height);
			}





			string path = "";		
			if (texture.IsHdr) {				
				path = texture.GetUnityFilename(".exr", _textureFolder);
			} else {
				path = texture.GetUnityFilename(".png",_textureFolder);
			}			

			if (_saveToAssets) {				
				byte[] bytes = null;
				if (texture.IsHdr) {
					//this is a hack to decompress the texture or unity will throw an error as it cant write compressed files.
					RenderTexture renderTex = RenderTexture.GetTemporary(
					tex.width,
					tex.height,
					0,
					RenderTextureFormat.Default,
					RenderTextureReadWrite.Linear);						
					RenderTexture previous = RenderTexture.active;
					RenderTexture.active = renderTex;					
					Graphics.Blit(tex, renderTex);					
					Texture2D unCpmpressedImage = new Texture2D(tex.width, tex.height, TextureFormat.RGBAFloat, false);
					unCpmpressedImage.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
					unCpmpressedImage.Apply();					
					RenderTexture.active = previous;
					RenderTexture.ReleaseTemporary(renderTex);					
					bytes = unCpmpressedImage.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);					
				} else {
					bytes = tex.EncodeToPNG();
				}				
				File.WriteAllBytes(path, bytes);
				AssetDatabase.ImportAsset(path);
			} else {
				_textures[texture.Name] = tex;
			}
		}

		private Texture2D LoadTexture(Texture texture,string extension)
		{
			if (_saveToAssets) {
				return AssetDatabase.LoadAssetAtPath<Texture2D>(texture.GetUnityFilename(extension, _textureFolder));
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


		private void CalculateTangents(UnityEngine.Mesh mesh) {


			// Speed: Cache mesh arrays
			int[] triangles = mesh.triangles;
			Vector3[] vertices = mesh.vertices;
			Vector2[] uv = mesh.uv;
			Vector3[] normals = mesh.normals;

			//variable definitions
			int triangleCount = triangles.Length;
			int vertexCount = vertices.Length;

			Vector3[] tan1 = new Vector3[vertexCount];
			Vector3[] tan2 = new Vector3[vertexCount];

			Vector4[] tangents = new Vector4[vertexCount];

			for (int a = 0; a < triangleCount; a += 3) {
				int i1 = triangles[a + 0];
				int i2 = triangles[a + 1];
				int i3 = triangles[a + 2];

				Vector3 v1 = vertices[i1];
				Vector3 v2 = vertices[i2];
				Vector3 v3 = vertices[i3];

				Vector2 w1 = uv[i1];
				Vector2 w2 = uv[i2];
				Vector2 w3 = uv[i3];

				float x1 = v2.x - v1.x;
				float x2 = v3.x - v1.x;
				float y1 = v2.y - v1.y;
				float y2 = v3.y - v1.y;
				float z1 = v2.z - v1.z;
				float z2 = v3.z - v1.z;

				float s1 = w2.x - w1.x;
				float s2 = w3.x - w1.x;
				float t1 = w2.y - w1.y;
				float t2 = w3.y - w1.y;

				float r = 1.0f / (s1 * t2 - s2 * t1);

				Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
				Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

				tan1[i1] += sdir;
				tan1[i2] += sdir;
				tan1[i3] += sdir;

				tan2[i1] += tdir;
				tan2[i2] += tdir;
				tan2[i3] += tdir;

			}


			for (int a = 0; a < vertexCount; ++a) {
				Vector3 n = normals[a];
				Vector3 t = tan1[a];

				Vector3.OrthoNormalize(ref n, ref t);

				tangents[a].x = t.x;
				tangents[a].y = t.y;
				tangents[a].z = t.z;

				tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
			}

			mesh.tangents = tangents;




		}

		private void ResetGOOrigin(GameObject obj, UnityEngine.Mesh mesh) {



			Quaternion rot = obj.transform.rotation;
			obj.transform.rotation = Quaternion.identity;
			var vertices = mesh.vertices;

			int len = vertices.Length;
			int v;
			Vector3 c = Vector3.zero;
			for (v = 0; v < len; v++) {
				c += vertices[v];
			}

			c /= len;
			Vector3 d = Vector3.zero;
			for (v = 0; v < len; v++) {
				d += obj.transform.TransformPoint(vertices[v]);
			}
			d /= len;
			Matrix4x4 trs = Matrix4x4.TRS(-(c), Quaternion.identity, Vector3.one);
			for (v = 0; v < len; v++) {
				vertices[v] = trs.MultiplyPoint(vertices[v]);

			}

			mesh.vertices = vertices;
			mesh.RecalculateBounds();

			Undo.RecordObject(obj, "set origin of parent");

			obj.transform.position = d;
			obj.transform.rotation = rot;




		}

	}

	internal class VpxAsset : ScriptableObject
	{
	}
}
