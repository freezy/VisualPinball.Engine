// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A collapsible section header for the insert inspectors, in the look of
	/// Unity's foldout headers, without their rule against anything with a
	/// foldout of its own being drawn inside.
	/// </summary>
	internal static class InsertSection
	{
		private static GUIStyle Header => EditorStyles.foldoutHeader;

		/// <summary>Draws the header and says whether the section is open. The state outlives the inspector, not the editor session.</summary>
		public static bool Begin(string key, string title)
		{
			var open = SessionState.GetBool(key, true);
			var rect = GUILayoutUtility.GetRect(0f, Header.fixedHeight > 0f ? Header.fixedHeight : EditorGUIUtility.singleLineHeight + 2f, GUILayout.ExpandWidth(true));
			// Out to the inspector's edge, like the component header above it. The
			// style paints the bar itself, arrow and all.
			rect.xMin -= 14f;
			rect.xMax += 4f;
			EditorGUI.BeginChangeCheck();
			open = EditorGUI.Foldout(rect, open, title, true, Header);
			if (EditorGUI.EndChangeCheck()) {
				SessionState.SetBool(key, open);
			}
			if (open) {
				EditorGUILayout.Space(2f);
			}
			return open;
		}

		/// <summary>The gap after a section's contents.</summary>
		public static void End() => EditorGUILayout.Space(6f);
	}
}
