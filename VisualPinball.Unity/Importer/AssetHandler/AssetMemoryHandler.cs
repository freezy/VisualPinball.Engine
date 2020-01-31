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

		public Texture2D LoadTexture(Texture texture)
		{
			return Textures[texture.Name];
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
	}
}
