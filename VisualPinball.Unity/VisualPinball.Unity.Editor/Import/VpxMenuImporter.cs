// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity.Editor
{
	public static class VpxMenuImporter
	{
		[MenuItem("Visual Pinball/Import VPX", false, 2)]
		public static void ImportVpxIntoScene(MenuCommand menuCommand)
		{
			// if it's an untitled scene, save first.
			if (!EnsureUntitledSceneHasBeenSaved("Before importing, you need to make your current scene an asset by saving it.")) {
				return;
			}
			
			// open file dialog
			var vpxPath = EditorUtility.OpenFilePanelWithFilters("Import .VPX File", null, new[] { "Visual Pinball Table Files", "vpx" });
			if (vpxPath.Length == 0) {
				return;
			}

			VpxImportEngine.ImportIntoScene(vpxPath, tableName: Path.GetFileNameWithoutExtension(vpxPath));
		}
		
		private static bool EnsureUntitledSceneHasBeenSaved(string message)
		{
			if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) {

				if (!EditorUtility.DisplayDialog("Info", "Before importing, you need to make your current scene an asset by saving it.\nSave your current scene?", "Yes", "No")) {
					return false;
				}
				
				// Ask the user to save
				EditorSceneManager.SaveOpenScenes();

				// Check that the scene was saved
				if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) {
					return true;
				}
				return false;
			}
			return true;
		}
	}
}
