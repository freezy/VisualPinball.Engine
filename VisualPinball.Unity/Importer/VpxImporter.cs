using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Importer;
using System;
using System.IO;

namespace VisualPinball.Unity.Importer
{
	
	public class VpxImporter : MonoBehaviour
	{
		private AssetUtility assetUtility;

		[MenuItem("Tools/VPX", false, 10)]
		static void ImportVPX(MenuCommand menuCommand)
		{

			GameObject VPXGO = new GameObject("VPX");
			VpxImporter vpxI = VPXGO.AddComponent<VpxImporter>();			
			// Ensure it gets reparented if this was a context click (otherwise does nothing)
			GameObjectUtility.SetParentAndAlign(VPXGO, menuCommand.context as GameObject);
			// Register the creation in the undo system
			Undo.RegisterCreatedObjectUndo(VPXGO, "Create " + VPXGO.name);
			Selection.activeObject = VPXGO;
			string path = EditorUtility.OpenFilePanelWithFilters("Load .VPX File", "Assets/", new string[] { "vpx files", "vpx" });
			if (path.Length == 0) return;
			vpxI.ParseAsset(path);

		}


		public void ParseAsset(string path)
		{
			assetUtility = new AssetUtility();

			//handle saving a new material asset ......................................................................................

			bool mustSaveNewMaterials = false;
			string directortPath = assetUtility.CreateDirectory("Assets", "materials");
			string materialName = "vpxDefault.mat";
			Material mat_1 = assetUtility.LoadMatarial(directortPath, materialName);
			if (mat_1 == null)
			{
				mat_1 = new Material(Shader.Find("Standard"));
				string materialFilePath1 = assetUtility.ConcatPathsWithForwardSlash(new string[] { directortPath, materialName });
				AssetDatabase.CreateAsset(mat_1, materialFilePath1);
				mustSaveNewMaterials = true;
			}
			if (mustSaveNewMaterials)
			{
				AssetDatabase.SaveAssets();
				mat_1 = assetUtility.LoadMatarial(directortPath, materialName);
			}

			//load and parse vpx file
			var table = Table.Load(path);

			//handle custom .asset for vpx mesh and any other non scene objects that needs to be serialized------------------------------

			string newAssetPath = assetUtility.CreateDirectory("Assets", "vpx");
			newAssetPath += "/" + table.Name + "_data.asset";
			VPXData vpxData = ScriptableObject.CreateInstance<VPXData>();
			AssetDatabase.CreateAsset(vpxData, newAssetPath);			
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			

			//--------------------------------------------------------------------------------------------------------------------------


			

			newAssetPath = assetUtility.CreateDirectory("Assets", "vpx");
			newAssetPath += "/"+ table.Name + ".prefab";
			gameObject.name = table.Name;
			PrefabUtility.SaveAsPrefabAsset(gameObject, newAssetPath);


			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = transform;
			

			foreach (var primitive in table.Primitives.Values) {

				VisualPinball.Engine.VPT.Mesh vpMesh = null;
				try
				{
					vpMesh = primitive.GetMesh(table);
				}
				catch (Exception e) {
					Debug.Log("primitive : "+ primitive.Name+", Error : "+e);
					continue;
				}
				
				
				var mesh = vpMesh.ToUnityMesh();
				mesh.name = primitive.Name + "_mesh";
				var obj = new GameObject(primitive.Name);
				MeshFilter mf = obj.AddComponent<MeshFilter>();
				MeshRenderer mr = obj.AddComponent<MeshRenderer>();
				obj.transform.parent = primitivesObj.transform;
				Matrix4x4 fixVertsTRS = new Matrix4x4();
				fixVertsTRS.SetTRS(Vector3.zero, Quaternion.Euler(-90, 0, 0), new Vector3(0.01f, 0.01f, 0.01f));
				Vector3[] vertices = mesh.vertices;
				for (int i = 0; i < vertices.Length; i++)
				{
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
