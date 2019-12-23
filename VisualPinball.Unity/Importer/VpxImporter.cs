using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Importer
{
	[ScriptedImporter(1, "vpx")]
	public class VpxImporter : ScriptedImporter
	{
		private const float Scale = 0.1f;

		public override void OnImportAsset(AssetImportContext ctx)
		{
			var table = Table.Load(ctx.assetPath);
			var material = new Material(Shader.Find("Standard"));
			ctx.AddObjectToAsset("StandardMaterial", material);

			var rootObj = new GameObject(table.Name);
			rootObj.transform.localScale = new Vector3(Scale, Scale, Scale);
			ctx.AddObjectToAsset(table.Name, rootObj);

			var primitivesObj = new GameObject("Primitives");
			primitivesObj.transform.parent = rootObj.transform;
			ctx.AddObjectToAsset("Primitives", primitivesObj);

			foreach (var primitive in table.Primitives.Values) {
				var vpMesh = primitive.GetMesh(table);
				var mesh = vpMesh.ToUnityMesh();
				var obj = new GameObject(primitive.Name);
				obj.AddComponent<MeshFilter>();
				obj.AddComponent<MeshRenderer>();

				obj.transform.parent = primitivesObj.transform;

				obj.GetComponent<MeshFilter>().mesh = mesh;
				obj.GetComponent<MeshRenderer>().material = material;

				ctx.AddObjectToAsset(primitive.Name, obj);
			}
		}
	}
}
