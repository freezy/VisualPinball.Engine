using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Importer
{
	[ScriptedImporter(1, "vpx")]
	public class VpxImporter : ScriptedImporter
	{
		

		public override void OnImportAsset(AssetImportContext ctx)
		{
			var table = Table.Load(ctx.assetPath);
			var material = new Material(Shader.Find("Standard"));
			ctx.AddObjectToAsset("StandardMaterial", material);

			var rootObj = new GameObject(table.Name);			
			ctx.AddObjectToAsset(table.Name, rootObj);

			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = rootObj.transform;
			ctx.AddObjectToAsset("Primitives", primitivesObj);

			foreach (var primitive in table.Primitives.Values) {
				bool error = false;
				var vpMeshTemp = primitive.GetMeshSimple();
				if (vpMeshTemp.Vertices == null) {
					Debug.Log("primitive " + primitive.Name + " mesh vertices are null");
					error = true;
				}
				if (!error)
				{
					var vpMesh = primitive.GetMesh(table);
					var mesh = vpMesh.ToUnityMesh();					
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
					ctx.AddObjectToAsset(primitive.Name+"_mesh", mesh);
					mf.sharedMesh = mesh;				
					mr.material = material;
					ctx.AddObjectToAsset(primitive.Name, obj);
				}
				
			}
		}
	}
}
