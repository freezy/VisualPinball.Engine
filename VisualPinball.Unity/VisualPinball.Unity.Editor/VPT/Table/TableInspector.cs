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

// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TableAuthoring))]
	[CanEditMultipleObjects]
	public class TableInspector : ItemInspector
	{
		public override MonoBehaviour UndoTarget => target as MonoBehaviour;

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			var tableComponent = (TableAuthoring) target;
			if (!EditorApplication.isPlaying) {
				DrawDefaultInspector();
				if (GUILayout.Button("Export VPX")) {
					var table = tableComponent.RecreateTable(tableComponent.Data);
					var path = EditorUtility.SaveFilePanel(
						"Export table as VPX",
						"",
						table.Name + ".vpx",
						"vpx");

					if (!string.IsNullOrEmpty(path)) {
						table.Save(path);
					}
				}
			}
		}
	}
}
