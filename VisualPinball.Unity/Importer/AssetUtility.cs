using System.IO;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Importer
{
	internal static class AssetUtility
	{
		public static string CreateDirectory(string basePath, string directoryPath)
		{
			var fullPath = ConcatPathsWithForwardSlash(new[] { basePath, directoryPath });
			if (!Directory.Exists(fullPath)) {
				AssetDatabase.CreateFolder(basePath, directoryPath);
			}
			return fullPath;
		}

		public static Material LoadMaterial(string basePath, string materialPath)
		{
			var fullPath = ConcatPathsWithForwardSlash(new[] { basePath, materialPath });
			return AssetDatabase.LoadAssetAtPath(fullPath, typeof(Material)) as Material;
		}

		public static string ConcatPathsWithForwardSlash(string[] paths)
		{
			var fullPath = "";
			for (var i = 0; i < paths.Length; i++) {
				fullPath += paths[i];
				if (i < paths.Length - 1) {
					fullPath += "/";
				}
			}
			return fullPath;
		}
	}
}
