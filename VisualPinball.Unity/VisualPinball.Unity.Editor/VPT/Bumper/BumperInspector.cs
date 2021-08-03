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
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BumperAuthoring))]
	public class BumperInspector : ItemMainInspector<Bumper, BumperData, BumperAuthoring>
	{
		private SerializedProperty _positionProperty;
		private SerializedProperty _radiusProperty;
		private SerializedProperty _heightScaleProperty;
		private SerializedProperty _orientationProperty;
		private SerializedProperty _surfaceProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(BumperAuthoring.Position));
			_radiusProperty = serializedObject.FindProperty(nameof(BumperAuthoring.Radius));
			_heightScaleProperty = serializedObject.FindProperty(nameof(BumperAuthoring.HeightScale));
			_orientationProperty = serializedObject.FindProperty(nameof(BumperAuthoring.Orientation));
			_surfaceProperty = serializedObject.FindProperty(nameof(BumperAuthoring._surface));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_radiusProperty, updateTransforms: true);
			PropertyField(_heightScaleProperty, updateTransforms: true);
			PropertyField(_orientationProperty, updateTransforms: true);

			ComponentReferenceField("Surface", "Surfaces & Ramps", "None", "surface",
				ItemAuthoring.Surface, surface => {
					_surfaceProperty.objectReferenceValue = surface as MonoBehaviour;
					serializedObject.ApplyModifiedProperties();
				});

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
