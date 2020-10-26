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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberAuthoring))]
	public class RubberInspector : DragPointsItemInspector<Rubber, RubberData, RubberAuthoring>
	{
		private bool _foldoutGeometry = true;
		private bool _foldoutMesh;
		private bool _foldoutMisc;

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			OnPreInspectorGUI();

			if (_foldoutGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGeometry, "Geometry")) {
				ItemDataField("Height", ref Data.Height);
				ItemDataField("Thickness", ref Data.Thickness);
				EditorGUILayout.LabelField("Orientation");
				EditorGUI.indentLevel++;
				ItemDataField("RotX", ref Data.RotX);
				ItemDataField("RotY", ref Data.RotY);
				ItemDataField("RotZ", ref Data.RotZ);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMesh = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMesh, "Mesh")) {
				TextureField("Image", ref Data.Image);
				MaterialField("Material", ref Data.Material);
				ItemDataField("Static", ref Data.StaticRendering);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref Data.IsTimerEnabled, false);
				ItemDataField("Timer Interval", ref Data.TimerInterval, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
