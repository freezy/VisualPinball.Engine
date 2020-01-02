using System.IO;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.IO;
using Logger = NLog.Logger;
using Material = UnityEngine.Material;

namespace VisualPinball.Unity.Importer
{
	public class VpxImporter : MonoBehaviour
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[MenuItem("Visual Pinball/Import VPX", false, 10)]
		static void ImportVpxMenu(MenuCommand menuCommand)
		{
			// TODO that somewhere else
			Logging.Setup();

			// open file dialog
			var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", "Assets/", new[] { "Visual Pinball Table Files", "vpx" });
			if (vpxPath.Length == 0) {
				return;
			}

			// parse table
			var table = Table.Load(vpxPath);

			// create asset object
			var asset = ScriptableObject.CreateInstance<VpxAsset>();
			asset.TableFolder = $"Assets/{Path.GetFileNameWithoutExtension(vpxPath)}";
			asset.MaterialFolder = $"{asset.TableFolder}/Materials";
			asset.TableDataPath = $"{asset.TableFolder}/{AssetUtility.StringToFilename(table.Name)}_data.asset";
			asset.TablePrefabPath = $"{asset.TableFolder}/{AssetUtility.StringToFilename(table.Name)}.prefab";
			AssetUtility.CreateFolders(asset.TableFolder, asset.MaterialFolder);

			// import materials
			ImportMaterials(table, asset);

			// import table
			var rootGameObj = ImportGameItems(table, asset);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, menuCommand.context as GameObject);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, $"Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;
		}

		private static void ImportMaterials(Table table, VpxAsset assets)
		{
			foreach (var material in table.Materials) {
				AssetDatabase.CreateAsset(material.ToUnityMaterial(), material.GetUnityFilename(assets.MaterialFolder));
			}
		}


		private static GameObject ImportGameItems(Table table, VpxAsset asset)
		{
			// save game objects to asset folder
			AssetDatabase.CreateAsset(asset, asset.TableDataPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			var rootGameObj = ImportMeshes(table, asset);

			PrefabUtility.SaveAsPrefabAssetAndConnect(rootGameObj.gameObject, asset.TablePrefabPath, InteractionMode.UserAction);

			return rootGameObj;
		}


		private static GameObject ImportMeshes(Table table, VpxAsset asset)
		{
			// create root object
			var rootGameObj = new GameObject();
			rootGameObj.gameObject.name = table.Name;

			// persist assets
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			// import game objects
			ImportPrimitives(table, rootGameObj, asset);

			return rootGameObj;
		}

		private static void ImportPrimitives(Table table, GameObject rootGameObj, VpxAsset asset)
		{
			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = rootGameObj.transform;

			foreach (var primitive in table.Primitives.Values) {

				// convert mesh
				var mesh = primitive.GetMesh(table).ToUnityMesh();
				mesh.name = primitive.Name + "_mesh";

				// create game object for primitive
				var obj = new GameObject(primitive.Name);
				obj.transform.parent = primitivesObj.transform;

				// apply mesh to game object
				var mf = obj.AddComponent<MeshFilter>();
				mf.sharedMesh = mesh;

				// apply loaded material
				var materialVpx = primitive.GetMaterial(table);
				if (materialVpx != null) {
					var materialUnity = AssetDatabase.LoadAssetAtPath(materialVpx.GetUnityFilename(asset.MaterialFolder), typeof(Material)) as Material;
					var mr = obj.AddComponent<MeshRenderer>();
					mr.sharedMaterial = materialUnity;
				}

				// add mesh to asset
				AssetDatabase.AddObjectToAsset(mesh, asset);
			}
		}


		// private static GameObject ImportVpx(string path)
		// {
		// 	var rootGameObj = new GameObject();
		// 	var import = rootGameObj.AddComponent<VpxImporter>();
		//
		// 	// load and parse vpx file
		//
		//
		// 	// handle custom .asset for vpx mesh and any other non scene objects that needs to be serialized
		// 	var assetPath = $"{AssetUtility.CreateDirectory("Assets", "vpx")}/{table.Name}_data.asset";
		// 	var vpxData = ScriptableObject.CreateInstance<VpxData>();
		// 	AssetDatabase.CreateAsset(vpxData, assetPath);
		// 	AssetDatabase.SaveAssets();
		// 	AssetDatabase.Refresh();
		//
		// 	var prefabPath = $"{AssetUtility.ConcatPathsWithForwardSlash("Assets", "vpx")}/{table.Name}.prefab";
		// 	rootGameObj.gameObject.name = table.Name;
		//
		// 	// create directory if needed
		// 	var directoryPath = AssetUtility.CreateDirectory("Assets/vpx", "materials");
		//
		// 	var primitivesObj = new GameObject("Primitives");
		// 	primitivesObj.transform.parent = rootGameObj.transform;
		//
		// 	var fixVertsTRS = new Matrix4x4();
		// 	fixVertsTRS.SetTRS(Vector3.zero, Quaternion.Euler(-90, 0, 0), Vector3.one);
		// 	//rootGameObj.transform.localRotation = Quaternion.Euler(-90, 0, 0);
		// 	rootGameObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
		// 	foreach (var primitive in table.Primitives.Values) {
		//
		// 		Mesh vpMesh;
		// 		try {
		// 			vpMesh = primitive.GetMesh(table);
		//
		// 		} catch (Exception e) {
		// 			Debug.Log("primitive : "+ primitive.Name+", Error : "+e);
		// 			continue;
		// 		}
		//
		// 		var mesh = vpMesh.ToUnityMesh();
		// 		mesh.name = primitive.Name + "_mesh";
		// 		var obj = new GameObject(primitive.Name);
		// 		var mf = obj.AddComponent<MeshFilter>();
		// 		obj.transform.parent = primitivesObj.transform;
		//
		// 		var vertices = mesh.vertices;
		// 		for (var i = 0; i < vertices.Length; i++) {
		// 			vertices[i] = fixVertsTRS.MultiplyPoint(vertices[i]);
		// 		}
		// 		mesh.vertices = vertices;
		// 		mesh.RecalculateBounds();
		// 		AssetDatabase.AddObjectToAsset(mesh, vpxData);
		// 		mf.sharedMesh = mesh;
		//
		//
		// 		//handle materials ......................................................................................
		//
		// 		VisualPinball.Engine.VPT.Material materialVPX = primitive.GetMaterial(table);
		// 		if (materialVPX != null)
		// 		{
		//
		// 			var materialName = materialVPX.Name + ".mat";
		//
		// 			//if the material already exists load it
		// 			UnityEngine.Material materialUnity = AssetUtility.LoadMaterial(directoryPath, materialName);
		// 			//if result is null create the material
		// 			if (materialUnity == null)
		// 			{
		// 				materialUnity = materialVPX.ToUnityMaterial();
		// 				var materialFilePath1 = AssetUtility.ConcatPathsWithForwardSlash(new string[] { directoryPath, materialName });
		// 				AssetDatabase.CreateAsset(materialUnity, materialFilePath1);
		//
		// 			}
		//
		// 			var mr = obj.AddComponent<MeshRenderer>();
		// 			mr.sharedMaterial = materialUnity;
		// 		}
		// 		else {
		// 			Debug.Log("material is null for primitive " + primitive.Name);
		// 		}
		// 	}
		//
		//
		// 	PrefabUtility.SaveAsPrefabAssetAndConnect(rootGameObj.gameObject, prefabPath, InteractionMode.UserAction);
		// 	AssetDatabase.SaveAssets();
		// 	AssetDatabase.Refresh();
		//
		// 	return rootGameObj;
		// }
	}

	internal class VpxAsset : ScriptableObject
	{
		public string TableFolder;
		public string MaterialFolder;
		public string TableDataPath;
		public string TablePrefabPath;
	}
}
