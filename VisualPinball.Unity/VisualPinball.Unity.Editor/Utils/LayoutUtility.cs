// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;

namespace VisualPinball.Unity.Editor
{
	public static class LayoutUtility
	{
		private const string LayoutsAssetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Editor/WindowLayouts";

		[MenuItem("Visual Pinball/Editor/Setup Layouts", false, 511)]
		public static void PopulateEditorLayout()
		{
			var layouts = CollectCustomLayouts();
			var unityPrefs = InternalEditorUtility.unityPreferencesFolder;

			var i = 1;
			foreach (var (name, path) in layouts) {

				// Rename default unity views that interrupt the VPE layout naming.
				var theAnnoyingOne = unityPrefs + "/Layouts/default/2 by 3.wlt";
				var taoCorrected = unityPrefs + "/Layouts/default/Unity 2 by 3.wlt";
				if (File.Exists(theAnnoyingOne)) {
					File.Copy(theAnnoyingOne, taoCorrected);
					File.Delete(theAnnoyingOne);
				}
				var theAnnoyingOne2 = unityPrefs + "/Layouts/default/4 Split.wlt";
				var tao2Corrected = unityPrefs + "/Layouts/default/Unity 4 Split.wlt";
				if (File.Exists(theAnnoyingOne2)) {
					File.Copy(theAnnoyingOne2, tao2Corrected);
					File.Delete(theAnnoyingOne2);
				}

				// Copy the VPE preferences.  Overwrite the originals in case we change them.
				// TODO: Provide a modal dialog prompt to for overwriting.
				File.Copy(path, $"{unityPrefs}/Layouts/default/{i}) {name}.wlt", true);
				//Debug.Log("Setting up new layout at " + $"{unityPrefs}/Layouts/default/{i}) {name}.wlt");

				i++;
			}

			// Refresh the menu.
			InternalEditorUtility.ReloadWindowLayoutMenu();

			EditorUtility.DisplayDialog(
				"Visual Pinball Layouts",
				"Layouts added. You can switch between them using the drop down in the top right corner of the editor.",
				"Got it!");
		}

		/// <summary>
		/// Collects all of the VPE custom layouts.
		/// </summary>
		/// <returns>name / path tuples of VPE layouts</returns>
		private static IEnumerable<(string, string)> CollectCustomLayouts()
		{
			var layouts = new List<(string, string)>();
			var assets = AssetDatabase.FindAssets("t:DefaultAsset", new[] {LayoutsAssetPath});
			if (assets.Length > 0) {
				foreach (var guid in assets) {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					var name = AssetDatabase.LoadAssetAtPath(path, typeof(DefaultAsset)).name;
					layouts.Add((name, path));
				}
			}
			return layouts;
		}
	}
}
