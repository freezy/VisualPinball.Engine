using System.IO;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Importer
{
	internal static class AssetUtility
	{
		public static string CreateDirectory(string basePath, string directoryPath)
		{
			var fullPath = ConcatPathsWithForwardSlash(basePath, directoryPath);
			if (!Directory.Exists(fullPath)) {
				AssetDatabase.CreateFolder(basePath, directoryPath);
			}
			return fullPath;
		}

		public static Material LoadMaterial(string basePath, string materialPath)
		{
			var fullPath = ConcatPathsWithForwardSlash(basePath, materialPath);
			return AssetDatabase.LoadAssetAtPath(fullPath, typeof(Material)) as Material;
		}

		public static string ConcatPathsWithForwardSlash(params string[] paths)
		{
			return string.Join("/", paths);
		}
	}
}
