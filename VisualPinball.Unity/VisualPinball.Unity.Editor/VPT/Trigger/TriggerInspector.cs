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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TriggerComponent)), CanEditMultipleObjects]
	public class TriggerInspector : MainInspector<TriggerData, TriggerComponent>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			// position
			EditorGUI.BeginChangeCheck();
			var newPos = EditorGUILayout.Vector2Field(new GUIContent("Position", "Position of the trigger on the playfield, relative to its parent."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Trigger Position");
				MainComponent.Position = newPos;
			}

			// scale
			EditorGUI.BeginChangeCheck();
			var newScale = EditorGUILayout.Slider(new GUIContent("Scale", "Scales the trigger mesh by this value."), MainComponent.Scale, 0.5f, 1.5f);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Trigger Scale");
				MainComponent.Scale = newScale;
			}

			// rotation
			EditorGUI.BeginChangeCheck();
			var newRotation = EditorGUILayout.Slider(new GUIContent("Rotation", "Orientation angle. Updates z rotation."), MainComponent.Rotation, -180f, 180f);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Trigger Rotation");
				MainComponent.Rotation = newRotation;
			}

			DragPointSplineInspectorGUI.OnInspectorGUI(MainComponent.DragPointSpline);

			base.OnInspectorGUI();

			EndEditing();
		}

	}
}
