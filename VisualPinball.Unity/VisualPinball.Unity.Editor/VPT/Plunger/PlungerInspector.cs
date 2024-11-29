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
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlungerComponent)), CanEditMultipleObjects]
	public class PlungerInspector : MainInspector<PlungerData, PlungerComponent>
	{
		private SerializedProperty _widthProperty;
		private SerializedProperty _heightProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_widthProperty = serializedObject.FindProperty(nameof(PlungerComponent.Width));
			_heightProperty = serializedObject.FindProperty(nameof(PlungerComponent.Height));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			// position
			EditorGUI.BeginChangeCheck();
			var newPos = EditorGUILayout.Vector3Field(new GUIContent("Position", "The position of the plunger on the playfield."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Plunger Position");
				MainComponent.Position = newPos;
			}

			PropertyField(_widthProperty, rebuildMesh: true);
			PropertyField(_heightProperty, rebuildMesh: true);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
