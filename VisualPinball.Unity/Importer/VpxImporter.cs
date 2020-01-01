using System;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.IO;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity.Importer
{
	public class VpxImporter : MonoBehaviour
	{
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

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

			var rootGameObj = ImportVpx(vpxPath);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, menuCommand.context as GameObject);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, $"Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;

		}

		private static GameObject ImportVpx(string path)
		{
			var rootGameObj = new GameObject();
			var import = rootGameObj.AddComponent<VpxImporter>();

			// load and parse vpx file
			var table = Table.Load(path);

			// handle custom .asset for vpx mesh and any other non scene objects that needs to be serialized
			var assetPath = $"{AssetUtility.CreateDirectory("Assets", "vpx")}/{table.Name}_data.asset";
			var vpxData = ScriptableObject.CreateInstance<VpxData>();
			AssetDatabase.CreateAsset(vpxData, assetPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			var prefabPath = $"{AssetUtility.ConcatPathsWithForwardSlash("Assets", "vpx")}/{table.Name}.prefab";
			rootGameObj.gameObject.name = table.Name;

			// create directory if needed
			var directoryPath = AssetUtility.CreateDirectory("Assets/vpx", "materials");

			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = rootGameObj.transform;

			var fixVertsTRS = new Matrix4x4();
			fixVertsTRS.SetTRS(Vector3.zero, Quaternion.Euler(-90, 0, 0), Vector3.one);
			//rootGameObj.transform.localRotation = Quaternion.Euler(-90, 0, 0);
			rootGameObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
			foreach (var primitive in table.Primitives.Values) {

				Mesh vpMesh;
				try {
					vpMesh = primitive.GetMesh(table);

				} catch (Exception e) {
					Debug.Log("primitive : "+ primitive.Name+", Error : "+e);
					continue;
				}

				var mesh = vpMesh.ToUnityMesh();
				mesh.name = primitive.Name + "_mesh";
				var obj = new GameObject(primitive.Name);
				var mf = obj.AddComponent<MeshFilter>();
				obj.transform.parent = primitivesObj.transform;

				var vertices = mesh.vertices;
				for (var i = 0; i < vertices.Length; i++) {
					vertices[i] = fixVertsTRS.MultiplyPoint(vertices[i]);
				}
				mesh.vertices = vertices;
				mesh.RecalculateBounds();
				AssetDatabase.AddObjectToAsset(mesh, vpxData);
				mf.sharedMesh = mesh;


				//handle materials ......................................................................................

				VisualPinball.Engine.VPT.Material materialVPX = primitive.GetMaterial(table);
				if (materialVPX != null)
				{

					var materialName = materialVPX.Name + ".mat";

					//if the material already exists load it
					UnityEngine.Material materialUnity = AssetUtility.LoadMaterial(directoryPath, materialName);
					//if result is null create the material
					if (materialUnity == null)
					{
						materialUnity = materialVPX.ToUnityMaterial();
						var materialFilePath1 = AssetUtility.ConcatPathsWithForwardSlash(new string[] { directoryPath, materialName });
						AssetDatabase.CreateAsset(materialUnity, materialFilePath1);

					}

					var mr = obj.AddComponent<MeshRenderer>();
					mr.sharedMaterial = materialUnity;
				}
				else {
					Debug.Log("material is null for primitive " + primitive.Name);
				}
			}


			PrefabUtility.SaveAsPrefabAssetAndConnect(rootGameObj.gameObject, prefabPath, InteractionMode.UserAction);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			return rootGameObj;
		}
	}
}
