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

		private string _tableFolder;
		private string _materialFolder;
		private string _tableDataPath;
		private string _tablePrefabPath;

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

			// create root object
			var rootGameObj = new GameObject();
			var importer = rootGameObj.AddComponent<VpxImporter>();

			importer.Import(vpxPath);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, menuCommand.context as GameObject);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, $"Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;
		}

		public void Import(string path)
		{
			// parse table
			var table = Table.Load(path);
			gameObject.name = table.Name;

			// set paths
			_tableFolder = $"Assets/{Path.GetFileNameWithoutExtension(path)}";
			_materialFolder = $"{_tableFolder}/Materials";
			_tableDataPath = $"{_tableFolder}/{AssetUtility.StringToFilename(table.Name)}_data.asset";
			_tablePrefabPath = $"{_tableFolder}/{AssetUtility.StringToFilename(table.Name)}.prefab";
			AssetUtility.CreateFolders(_tableFolder, _materialFolder);

			// create asset object
			var asset = ScriptableObject.CreateInstance<VpxAsset>();

			// import materials
			ImportMaterials(table);

			// import table
			ImportGameItems(table, asset);
		}

		private void ImportMaterials(Table table)
		{
			foreach (var material in table.Materials) {
				AssetDatabase.CreateAsset(material.ToUnityMaterial(), material.GetUnityFilename(_materialFolder));
			}
		}

		private void ImportGameItems(Table table, VpxAsset asset)
		{
			// save game objects to asset folder
			AssetDatabase.CreateAsset(asset, _tableDataPath);
			AssetDatabase.SaveAssets();

			// import game objects
			ImportPrimitives(table, asset);

			PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, _tablePrefabPath, InteractionMode.UserAction);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}


		private void ImportPrimitives(Table table, VpxAsset asset)
		{
			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = gameObject.transform;

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
					var materialUnity = AssetDatabase.LoadAssetAtPath(materialVpx.GetUnityFilename(_materialFolder), typeof(Material)) as Material;
					var mr = obj.AddComponent<MeshRenderer>();
					mr.sharedMaterial = materialUnity;
				}

				// add mesh to asset
				AssetDatabase.AddObjectToAsset(mesh, asset);
			}
		}
	}

	internal class VpxAsset : ScriptableObject
	{
	}
}
