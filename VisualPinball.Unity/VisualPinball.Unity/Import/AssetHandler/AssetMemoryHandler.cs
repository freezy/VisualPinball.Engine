using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Import.AssetHandler
{
	/// <summary>
	/// This is a trivial asset handler that keeps all the assets in a
	/// dictionary. <p/>
	///
	/// It has a very quick import time and is mainly useful when testing
	/// code changes.
	/// </summary>
	/// <param name="texture"></param>
	public class AssetMemoryHandler : IAssetHandler
	{
		public string TextureFolder => null;
		public string SoundFolder => null;

		protected readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
		protected readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

		public void HandleTextureData(Texture texture)
		{
			// nothing to do
		}

		public void ImportTextures(IEnumerable<Texture> textures)
		{
			foreach (var tex in textures) {
				Textures[tex.Name] = tex.ToUnityTexture();
			}
		}

		public Texture2D LoadTexture(Texture texture, bool asNormalMap)
		{
			return asNormalMap ? NormalMap(Textures[texture.Name]) : Textures[texture.Name];
		}

		public void SaveMaterial(PbrMaterial material, Material unityMaterial)
		{
			Materials[material.Id] = unityMaterial;
		}

		public Material LoadMaterial(PbrMaterial material)
		{
			return Materials[material.Id];
		}

		public void OnMaterialsSaved(PbrMaterial[] materials)
		{
			// nothing to do
		}

		public void OnMeshesImported(GameObject gameObject)
		{
			// nothing to do
		}

		public void SaveMesh(Mesh mesh, string itemName)
		{
			// nothing to do
		}

		/// <summary>
		/// When importing via disk, we set TextureImporter.textureType = TextureImporterType.NormalMap. We don't have
		/// an importer when importing into memory, so we need to do that manually.
		///
		/// Basically, we need to re-map the UV values of the texture to DXT5nm which is what Unity uses for PC and
		/// consoles platforms.
		/// </summary>
		/// <param name="map"></param>
		/// <returns></returns>
		private static Texture2D NormalMap(Texture2D map) {
			var normalTexture = new Texture2D(map.width, map.height, TextureFormat.ARGB32, false, true);
			var normalTexturePixels = map.GetPixels32();
			foreach (var t in normalTexturePixels) {
				var c = t;
				c.a = c.r;
				c.r = 1;
				c.b = 0;
			}
			normalTexture.SetPixels32(normalTexturePixels);
			normalTexture.Apply();
			return normalTexture;
		}
	}
}
