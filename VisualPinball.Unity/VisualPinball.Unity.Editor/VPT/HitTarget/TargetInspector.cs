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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Editor
{
	public abstract class TargetInspector : MainInspector<HitTargetData, TargetComponent>
	{
		private SerializedProperty _meshNameProperty;
		private SerializedProperty _typeNameProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_meshNameProperty = serializedObject.FindProperty(nameof(TargetComponent._meshName));
			_typeNameProperty = serializedObject.FindProperty(nameof(TargetComponent._targetType));
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
			var newPos = EditorGUILayout.Vector3Field(new GUIContent("Position", "Position of the target on the playfield, relative to its parent."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Target Position");
				MainComponent.Position = newPos;
			}

			// rotation
			EditorGUI.BeginChangeCheck();
			var newAngle = EditorGUILayout.Slider(new GUIContent("Rotation", "Z-Axis rotation of the target."), MainComponent.Rotation, -180f, 180f);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Target Rotation");
				MainComponent.Rotation = newAngle;
			}

			// size
			EditorGUI.BeginChangeCheck();
			var newSize = EditorGUILayout.Vector3Field(new GUIContent("Size", "Overall scaling of the target. 32 equals 100%."), MainComponent.Size);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Gate Length");
				MainComponent.Size = newSize;
			}

			EndEditing();
		}
	}
}
