using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer.AssetHandler
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

		private static Texture2D NormalMap(Texture2D map) {
			var normalTexture = new Texture2D(map.width, map.height, TextureFormat.ARGB32, false, true);
			var normalColor = new UnityEngine.Color();
			for (var x = 0; x < map.width; x++) {
				for (var y = 0; y < map.height; y++) {
					normalColor.r = 1;
					normalColor.g = map.GetPixel(x, y).g;
					normalColor.b = 0;
					normalColor.a = map.GetPixel(x, y).r;
					normalTexture.SetPixel(x, y, normalColor);
				}
			}
			normalTexture.Apply();
			return normalTexture;
		}
	}
}
