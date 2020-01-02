using System.IO;
using System.Linq;
using NLog;
using UnityEditor;

namespace VisualPinball.Unity.Importer
{
	internal static class AssetUtility
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
			return Path.GetInvalidFileNameChars()
				.Aggregate(str, (current, c) => current.Replace(c, '_'))
				.Replace(" ", "_");
		}
	}
}
