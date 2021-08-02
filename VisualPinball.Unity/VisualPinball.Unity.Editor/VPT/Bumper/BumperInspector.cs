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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BumperAuthoring))]
	public class BumperInspector : ItemMainInspector<Bumper, BumperData, BumperAuthoring>
	{
		private bool _foldoutGeometry = true;

		private SerializedProperty _surfaceProperty;
		private SerializedProperty _radiusProperty;
		private SerializedProperty _orientationProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_radiusProperty = serializedObject.FindProperty(nameof(BumperAuthoring.Radius));
			_orientationProperty = serializedObject.FindProperty(nameof(BumperAuthoring.Orientation));
			_surfaceProperty = serializedObject.FindProperty(nameof(BumperAuthoring.Surface));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			OnPreInspectorGUI();

			if (_foldoutGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGeometry, "Geometry")) {
				PropertyField(_surfaceProperty);
				PropertyField(_radiusProperty);
				PropertyField(_orientationProperty);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
