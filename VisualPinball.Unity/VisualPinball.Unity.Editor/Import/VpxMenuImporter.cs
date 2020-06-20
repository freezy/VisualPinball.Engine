// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System.IO;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.Import.Job;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor.Import
{
	public static class VpxMenuImporter
	{

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[MenuItem("Visual Pinball/Import VPX", false, 10)]
		public static void ImportVpxEditorMemory(MenuCommand menuCommand)
		{
			ImportVpxEditor(menuCommand);
		}

		/// <summary>
		/// Imports a Visual Pinball File (.vpx) into the Unity Editor. <p/>
		///
		/// The goal of this is to be able to iterate rapidly without having to
		/// execute the runtime on every test. This importer also saves the
		/// imported data to the Assets folder so a project with an imported table
		/// can be saved and loaded
		/// </summary>
		/// <param name="menuCommand">Context provided by the Editor</param>
		private static void ImportVpxEditor(MenuCommand menuCommand)
		{
			// open file dialog
			var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", null, new[] { "Visual Pinball Table Files", "vpx" });
			if (vpxPath.Length == 0) {
				return;
			}

			var rootGameObj = ImportVpx(vpxPath);

			// if an object was selected in the editor, make it its parent
			GameObjectUtility.SetParentAndAlign(rootGameObj, menuCommand.context as GameObject);

			// register undo system
			Undo.RegisterCreatedObjectUndo(rootGameObj, "Import VPX table file");

			// select imported object
			Selection.activeObject = rootGameObj;

			Logger.Info("[VpxImporter] Imported!");
		}

		private static GameObject ImportVpx(string path) {

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
