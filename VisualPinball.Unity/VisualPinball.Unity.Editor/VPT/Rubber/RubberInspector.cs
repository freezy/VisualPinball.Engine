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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberComponent)), CanEditMultipleObjects]
	public class RubberInspector : MainInspector<RubberData, RubberComponent>
	{
		private SerializedProperty _thicknessProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_thicknessProperty = serializedObject.FindProperty(nameof(RubberComponent._thickness));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			// height
			EditorGUI.BeginChangeCheck();
			var newHeight = EditorGUILayout.FloatField(new GUIContent("Height", "Height of the rubber (in VPX units."), MainComponent.Height);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Rubber Height");
				MainComponent.Height = newHeight;
			}
			PropertyField(_thicknessProperty, rebuildMesh: true);

			DragPointSplineInspectorGUI.OnInspectorGUI(MainComponent.DragPointSpline);

			base.OnInspectorGUI();

			EndEditing();
		}

	}
}
