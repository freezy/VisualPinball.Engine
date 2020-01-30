using System.Collections.Generic;
using UnityEditor.Experimental.AssetImporters;
using VisualPinball.Engine.VPT;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

namespace VisualPinball.Unity.Importer.AssetHandler
{
	public class AssetVpxHandler : AssetMemoryHandler
	{
		private readonly AssetImportContext _ctx;

		public AssetVpxHandler(AssetImportContext ctx)
		{
			_ctx = ctx;
		}

		public new void ImportTextures(IEnumerable<Texture> textures)
		{
			base.ImportTextures(textures);
			foreach (var texture in _textures.Values) {
				_ctx.AddObjectToAsset(texture.name, texture);
			}
		}

		public new void SaveMaterial(PbrMaterial material, Material unityMaterial)
		{
			base.SaveMaterial(material, unityMaterial);
			_ctx.AddObjectToAsset(material.Id, unityMaterial);
		}

		public new void SaveMesh(Mesh mesh)
		{
			base.SaveMesh(mesh);
			_ctx.AddObjectToAsset(mesh.name, mesh);
		}
	}
}
