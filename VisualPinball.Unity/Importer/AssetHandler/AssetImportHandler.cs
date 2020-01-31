using System;
using System.Collections.Generic;
using NLog;
using UnityEditor.Experimental.AssetImporters;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

namespace VisualPinball.Unity.Importer.AssetHandler
{
	public class AssetImportHandler : AssetMemoryHandler, IAssetHandler
	{
		private readonly AssetImportContext _ctx;

		public AssetImportHandler(AssetImportContext ctx)
		{
			_ctx = ctx;
		}

		public new void ImportTextures(IEnumerable<Texture> textures)
		{
			base.ImportTextures(textures);
			foreach (var texture in Textures.Values) {
				_ctx.AddObjectToAsset($"texture-{texture.name.ToNormalizedName()}", texture);
			}
		}

		public new void SaveMaterial(PbrMaterial material, Material unityMaterial)
		{
			base.SaveMaterial(material, unityMaterial);
			_ctx.AddObjectToAsset($"material-{material.Id}", unityMaterial);
		}

		public new void SaveMesh(Mesh mesh)
		{
			base.SaveMesh(mesh);
			_ctx.AddObjectToAsset($"mesh-{mesh.name.ToNormalizedName()}", mesh);
		}
	}
}
