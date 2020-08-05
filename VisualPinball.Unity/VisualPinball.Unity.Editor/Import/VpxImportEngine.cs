using System.IO;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.Import.Job;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor.Import
{
	public static class VpxImportEngine
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Import the table specified with the given path and assign it to the given parent.
		/// </summary>
		/// <param name="vpxPath"></param>
		/// <param name="parent"></param>
		public static void Import( string vpxPath, GameObject parent)
		{
			var rootGameObj = ImportVpx(vpxPath);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, parent);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, "Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;

			Logger.Info("Imported {0}", vpxPath);
		}

		private static GameObject ImportVpx(string path)
		{
			// create root object
			var rootGameObj = new GameObject();
			var importer = rootGameObj.AddComponent<VpxImporter>();

			// load table
			var table = TableLoader.LoadTable(path);

			importer.Import(Path.GetFileName(path), table);

			return rootGameObj;
		}
	}
}
