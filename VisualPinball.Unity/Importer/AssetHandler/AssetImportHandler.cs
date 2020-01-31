using System.Collections.Generic;
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

		/// <summary>
		/// This asset handler is used when importing a .vpx file that has been
		/// dropped into the assets folder and is imported via <see cref="ScriptedImporter"/>. <p/>
		///
		/// It basically adds all objects (textures, materials and meshes) to
		/// the given context.
		/// </summary>
		/// <param name="ctx"></param>
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

		public new void SaveMesh(Mesh mesh, string itemName)
		{
			base.SaveMesh(mesh, itemName);
			_ctx.AddObjectToAsset($"mesh-{itemName.ToNormalizedName()}-{mesh.name.ToNormalizedName()}", mesh);
		}
	}
}
