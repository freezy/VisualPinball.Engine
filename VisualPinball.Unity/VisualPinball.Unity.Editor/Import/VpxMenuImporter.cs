// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public static class VpxMenuImporter
	{

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

			// perform import
			VpxImportEngine.Import(vpxPath, menuCommand.context as GameObject);
		}
	}
}
