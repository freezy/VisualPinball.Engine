// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using Logger = NLog.Logger;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer.AssetHandler
{
	public class AssetDatabaseHandler : IAssetHandler
	{
		private readonly string _materialFolder;
		private readonly string _textureFolder;
		private readonly string _tablePrefabPath;
		private readonly VpxAsset _asset;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public AssetDatabaseHandler(Table table, string tablePath)
		{
			// setup paths
			var tableFolder = $"Assets/{Path.GetFileNameWithoutExtension(tablePath)?.Trim()}";
			_materialFolder = $"{tableFolder}/Materials";
			_textureFolder = $"{tableFolder}/Textures";
			_tablePrefabPath = $"{tableFolder}/{table.Name.ToNormalizedName()}.prefab";
			CreateFolders(tableFolder, _materialFolder, _textureFolder);

			// setup game asset to save
			var tableDataPath = $"{tableFolder}/{table.Name.ToNormalizedName()}_data.asset";
			_asset = ScriptableObject.CreateInstance<VpxAsset>();
			AssetDatabase.CreateAsset(_asset, tableDataPath);
			AssetDatabase.SaveAssets();
		}

		public void HandleTextureData(Texture texture)
		{
			var path = texture.GetUnityFilename(_textureFolder);
			File.WriteAllBytes(path, texture.FileContent);
		}

		public void ImportTextures(IEnumerable<Texture> textures)
		{
			// set filename -> texture map for OnPreprocessTexture()
			foreach (var texture in textures) {
				var path = texture.GetUnityFilename(_textureFolder);
				TexturePostProcessor.Textures[path] = texture;
			}

			// now the assets are written to disk, explicitly import them
			Profiler.Start("AssetDatabase.ImportAsset");
			AssetDatabase.ImportAsset(_textureFolder, ImportAssetOptions.ImportRecursive);
			Profiler.Stop("AssetDatabase.ImportAsset");
		}

		public Texture2D LoadTexture(Texture texture)
		{
			return AssetDatabase.LoadAssetAtPath<Texture2D>(texture.GetUnityFilename(_textureFolder));
		}

		public void SaveMaterial(PbrMaterial material, Material unityMaterial)
		{
			Profiler.Start("SaveMaterial");
			var path = material.GetUnityFilename(_materialFolder);
			AssetDatabase.CreateAsset(unityMaterial, path);
			Profiler.Stop("SaveMaterial");
		}

		public void OnMaterialsSaved(PbrMaterial[] materials)
		{
			AssetDatabase.SaveAssets();
			Logger.Info("Saved {0} materials to {1}.", materials.Length, _materialFolder);
		}

		public void OnMeshesImported(GameObject gameObject)
		{
			Profiler.Start("OnMeshesImported");
			Profiler.Start("PrefabUtility.SaveAsPrefabAssetAndConnect");
			PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, _tablePrefabPath, InteractionMode.UserAction);
			Profiler.Stop("PrefabUtility.SaveAsPrefabAssetAndConnect");
			Profiler.Start("AssetDatabase.SaveAssets");
			AssetDatabase.SaveAssets();
			Profiler.Stop("AssetDatabase.SaveAssets");
			Profiler.Stop("OnMeshesImported");
		}

		public void SaveMesh(Mesh mesh)
		{
			AssetDatabase.AddObjectToAsset(mesh, _asset);
		}

		public Material LoadMaterial(PbrMaterial material)
		{
			return AssetDatabase.LoadAssetAtPath<Material>(material.GetUnityFilename(_materialFolder));
		}

		private static void CreateFolders(params string[] folders)
		{
			foreach (var folder in folders) {
				if (Directory.Exists(folder)) {
					continue;
				}
				var dirNames = folder.Split('/');
				var baseDir = string.Join("/", dirNames.Take(dirNames.Length - 1));
				var newDir = dirNames.Last();
				Logger.Info("Creating folder {0} at {1}", newDir, baseDir);
				AssetDatabase.CreateFolder(baseDir, newDir);
			}
		}
	}

	public class TexturePostProcessor : AssetPostprocessor
	{
		public static readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();

		public void OnPreprocessTexture()
		{
			var importer = assetImporter as TextureImporter;
			if (importer != null) {
				var texture = Textures[importer.assetPath];

				importer.textureType = texture.UsageNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
				importer.alphaIsTransparency = !texture.IsOpaque;
				importer.isReadable = true;
				importer.mipmapEnabled = true;
				importer.filterMode = FilterMode.Bilinear;
				//EditorUtility.CompressTexture(AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath), texture.HasTransparentPixels ? TextureFormat.ARGB32 : TextureFormat.RGB24, UnityEditor.TextureCompressionQuality.Best);
			}
		}
	}

	internal class VpxAsset : ScriptableObject
	{
	}
}
