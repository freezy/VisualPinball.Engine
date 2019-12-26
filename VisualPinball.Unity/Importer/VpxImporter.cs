using System;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity.Importer
{
	public class VpxImporter : MonoBehaviour
	{
		[MenuItem("Tools/Import VPX", false, 10)]
		static void ImportVPX(MenuCommand menuCommand)
		{
			var vpxGO = new GameObject("VPX");
			var vpxI = vpxGO.AddComponent<VpxImporter>();

			// Ensure it gets reparented if this was a context click (otherwise does nothing)
			GameObjectUtility.SetParentAndAlign(vpxGO, menuCommand.context as GameObject);

			// Register the creation in the undo system
			Undo.RegisterCreatedObjectUndo(vpxGO, "Create " + vpxGO.name);
			Selection.activeObject = vpxGO;
			var path = EditorUtility.OpenFilePanelWithFilters("Load .VPX File", "Assets/", new string[] { "vpx files", "vpx" });
			if (path.Length == 0) return;
			vpxI.ParseAsset(path);
		}


		public void ParseAsset(string path)
		{
			//handle saving a new material asset ......................................................................................

			var mustSaveNewMaterials = false;
			var directortPath = AssetUtility.CreateDirectory("Assets", "materials");
			var materialName = "vpxDefault.mat";
			var mat_1 = AssetUtility.LoadMaterial(directortPath, materialName);
			if (mat_1 == null)
			{
				mat_1 = new Material(Shader.Find("Standard"));
				var materialFilePath1 = AssetUtility.ConcatPathsWithForwardSlash(new string[] { directortPath, materialName });
				AssetDatabase.CreateAsset(mat_1, materialFilePath1);
				mustSaveNewMaterials = true;
			}
			if (mustSaveNewMaterials)
			{
				AssetDatabase.SaveAssets();
				mat_1 = AssetUtility.LoadMaterial(directortPath, materialName);
			}

			//load and parse vpx file
			var table = Table.Load(path);

			//handle custom .asset for vpx mesh and any other non scene objects that needs to be serialized------------------------------

			var newAssetPath = AssetUtility.CreateDirectory("Assets", "vpx");
			newAssetPath += "/" + table.Name + "_data.asset";
			var vpxData = ScriptableObject.CreateInstance<VpxData>();
			AssetDatabase.CreateAsset(vpxData, newAssetPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			//--------------------------------------------------------------------------------------------------------------------------

			newAssetPath = AssetUtility.CreateDirectory("Assets", "vpx");
			newAssetPath += "/"+ table.Name + ".prefab";
			gameObject.name = table.Name;
			PrefabUtility.SaveAsPrefabAsset(gameObject, newAssetPath);


			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = transform;

			var fixVertsTRS = new Matrix4x4();
			fixVertsTRS.SetTRS(Vector3.zero, Quaternion.Euler(-90, 0, 0), new Vector3(0.01f, 0.01f, 0.01f));
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
				var mr = obj.AddComponent<MeshRenderer>();
				obj.transform.parent = primitivesObj.transform;
				
				var vertices = mesh.vertices;
				for (var i = 0; i < vertices.Length; i++) {
					vertices[i] = fixVertsTRS.MultiplyPoint(vertices[i]);
				}
				mesh.vertices = vertices;
				mesh.RecalculateBounds();
				AssetDatabase.AddObjectToAsset(mesh, vpxData);
				mf.sharedMesh = mesh;
				mr.material = mat_1;
			}

			PrefabUtility.SaveAsPrefabAsset(gameObject, newAssetPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
	}
}
