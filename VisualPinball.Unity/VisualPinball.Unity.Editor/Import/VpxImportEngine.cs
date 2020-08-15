using System.IO;
using NLog;
using UnityEditor;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	public static class VpxImportEngine
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Import the table specified with the given path and assign it to the given parent.
		/// </summary>
		/// <param name="vpxPath"></param>
		/// <param name="parent"></param>
		public static void Import( string vpxPath, GameObject parent, bool applyPatch = true, string tableName = null)
		{
			var rootGameObj = ImportVpx(vpxPath, applyPatch, tableName);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, parent);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, "Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;

			Logger.Info("Imported {0}", vpxPath);
		}

		private static GameObject ImportVpx(string path, bool applyPatch, string tableName)
		{
			// create root object
			var rootGameObj = new GameObject();
			var importer = rootGameObj.AddComponent<VpxConverter>();

			// load table
			var table = TableLoader.LoadTable(path);

			Logger.Info("Importing Table\nInfoName={0}\nInfoAuthorName={1}", table.InfoName, table.InfoAuthorName);

			importer.Convert(Path.GetFileName(path), table, applyPatch, tableName);

			return rootGameObj;
		}
	}
}
