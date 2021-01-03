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

using System;
using System.IO;
using System.Reflection;

namespace VisualPinball.Unity.Editor
{
	public static class LayoutUtility
	{
		/// <summary>
		/// A value indicating whether all required Unity API functionality is available for usage.
		/// </summary>
		public static readonly bool IsAvailable;

		/// <summary>
		/// The absolute path of layouts directory or `null` when not available.
		/// </summary>
		public static readonly string LayoutsPath;

		private static readonly MethodInfo LoadWindowLayout;
		private static readonly MethodInfo SaveWindowLayout;

		static LayoutUtility()
		{
			var windowLayout = Type.GetType("UnityEditor.WindowLayout,UnityEditor");
			var editorUtility = Type.GetType("UnityEditor.EditorUtility,UnityEditor");
			var internalEditorUtility = Type.GetType("UnityEditorInternal.InternalEditorUtility,UnityEditor");

			if (windowLayout != null && editorUtility != null && internalEditorUtility != null) {

				var getLayoutsPath = windowLayout.GetMethod("GetLayoutsPath", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
				var reloadWindowLayoutMenu = internalEditorUtility.GetMethod("ReloadWindowLayoutMenu", BindingFlags.Public | BindingFlags.Static);
				LoadWindowLayout = windowLayout.GetMethod("LoadWindowLayout", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] {typeof(string), typeof(bool)}, null);
				SaveWindowLayout = windowLayout.GetMethod("SaveWindowLayout",BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] {typeof(string)}, null);

				if (getLayoutsPath == null || reloadWindowLayoutMenu == null || LoadWindowLayout == null || SaveWindowLayout == null) {
					return;
				}

				LayoutsPath = (string) getLayoutsPath.Invoke(null, null);
				if (string.IsNullOrEmpty(LayoutsPath)) {
					return;
				}

				IsAvailable = true;
			}
		}

		/// <summary>
		/// Saves current window layout to asset file.
		/// </summary>
		/// <param name="assetPath">Path relative to project directory.</param>
		public static void SaveLayoutToAsset(string assetPath)
		{
			SaveLayout(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
		}

		/// <summary>
		/// Loads window layout from asset file.
		/// </summary>
		/// <param name="assetPath">Path relative to project directory.</param>
		public static void LoadLayoutFromAsset(string assetPath)
		{
			if (LoadWindowLayout != null) {
				var path = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
				LoadWindowLayout.Invoke(null, new object[] {path});
			}
		}

		/// <summary>
		/// Saves current window layout to file.
		/// </summary>
		/// <param name="path">Absolute path</param>
		public static void SaveLayout(string path)
		{
			if (SaveWindowLayout != null) {
				SaveWindowLayout.Invoke(null, new object[] {path});
			}
		}
	}
}
