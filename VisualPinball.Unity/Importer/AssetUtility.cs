using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;

namespace VisualPinball.Unity.Importer
{
	internal class AssetUtility
	{
		public AssetUtility()
		{			

		}

		public string CreateDirectory(string basePath, string directoryPath)
		{
			string fullpath = ConcatPathsWithForwardSlash(new string[] { basePath, directoryPath });
			if (!Directory.Exists(fullpath))
			{
				AssetDatabase.CreateFolder(basePath, directoryPath);

			}
			return fullpath;
		}

		public Material LoadMatarial(string basePath, string materialPath)
		{
			string fullpath = ConcatPathsWithForwardSlash(new string[] { basePath, materialPath });
			return AssetDatabase.LoadAssetAtPath(fullpath, typeof(Material)) as Material;
		}

		public string ConcatPathsWithForwardSlash(string[] paths)
		{
			string fullPath = "";
			for (int i = 0; i < paths.Length; i++)
			{
				fullPath += paths[i];
				if (i < paths.Length - 1)
				{
					fullPath += "/";
				}


			}
			return fullPath;

		}
	}
}
