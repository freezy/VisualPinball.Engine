// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public static class MenuImporter
	{

		[MenuItem("Visual Pinball/Import VPX", false, 1)]
		public static void ImportVpxEditorMemory(MenuCommand menuCommand)
		{
			ImportVpxEditor(menuCommand);
		}

		private static string lastVpxPath = "";

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
			var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", lastVpxPath, new[] { "Visual Pinball Table Files", "vpx" });
			if (vpxPath.Length == 0) {
				return;
			}
			lastVpxPath = vpxPath;
			// perform import
			VpxImportEngine.Import(vpxPath, menuCommand.context as GameObject);
		}

		[MenuItem("Visual Pinball/Import FPT", false, 1)]
		public static void ImportFptEditorMemory(MenuCommand menuCommand)
		{
			ImportFptEditor(menuCommand);
		}

		private static string lastFptPath = "";
		private static void ImportFptEditor(MenuCommand menuCommand)
		{
			// open file dialog
			var fptPath = EditorUtility.OpenFilePanelWithFilters("Import .FPT File", lastFptPath, new[] { "Future Pinball Table Files", "fpt" });
			if (fptPath.Length == 0)
			{
				return;
			}
			lastFptPath = fptPath;
			// perform import
			FptImportEngine.Import(fptPath, menuCommand.context as GameObject);
		}
	}
}
