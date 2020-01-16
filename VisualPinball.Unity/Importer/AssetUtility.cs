using System;
using System.IO;
using System.Linq;
using NLog;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity.Importer
{
	internal static class AssetUtility
	{
		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public static void CreateFolders(params string[] folders)
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

		public static string StringToFilename(string str)
		{
			if (str == null) {
				throw new ArgumentException("String cannot be null.");
			}
			return Path.GetInvalidFileNameChars()
				.Aggregate(str, (current, c) => current.Replace(c, '_'))
				.Replace(" ", "_");
		}

		public static List<T> FindObjectsOfTypeAll<T>() {
			List<T> results = new List<T>();
			for (int i = 0; i < SceneManager.sceneCount; i++) {
				var s = SceneManager.GetSceneAt(i);

				var allGameObjects = s.GetRootGameObjects();

				for (int j = 0; j < allGameObjects.Length; j++) {
					var go = allGameObjects[j];
					results.AddRange(go.GetComponentsInChildren<T>(true));
				}

			}
			return results;
		}
	}
}
