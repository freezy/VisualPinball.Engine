// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightGroupComponent)), CanEditMultipleObjects]
	public class LightGroupInspector : ItemInspector
	{
		private LightGroupComponent _lightGroupComponent;
		private SerializedProperty _lightGroupProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_lightGroupComponent = target as LightGroupComponent;
			_lightGroupProperty = serializedObject.FindProperty(nameof(LightGroupComponent._lights));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_lightGroupProperty, "Lights");

			GUILayout.Space(10);
			if (GUILayout.Button("Add Children")) {
				var lights = _lightGroupComponent.GetComponentsInChildren<ILampDeviceComponent>();
				foreach (var light in lights) {
					if (_lightGroupComponent._lights.Contains(light as MonoBehaviour) || ReferenceEquals(light, _lightGroupComponent)) {
						continue;
					}
					_lightGroupProperty.InsertArrayElementAtIndex(_lightGroupProperty.arraySize);
					_lightGroupProperty.GetArrayElementAtIndex(_lightGroupProperty.arraySize - 1).objectReferenceValue = light as MonoBehaviour;
				}
			}

			if (GUILayout.Button("Replace With Children")) {
				var lights = _lightGroupComponent.GetComponentsInChildren<ILampDeviceComponent>();
				_lightGroupProperty.ClearArray();
				foreach (var light in lights) {
					if (ReferenceEquals(light, _lightGroupComponent)) {
						continue;
					}
					_lightGroupProperty.InsertArrayElementAtIndex(_lightGroupProperty.arraySize);
					_lightGroupProperty.GetArrayElementAtIndex(_lightGroupProperty.arraySize - 1).objectReferenceValue = light as MonoBehaviour;
				}
			}

			if (GUILayout.Button("Clear")) {
				_lightGroupProperty.ClearArray();
			}

			base.OnInspectorGUI();

			EndEditing();

			GUILayout.Space(10);
			if (GUILayout.Button("Select Light Sources")) {
				var selection = new List<Object>();
				foreach (var light in _lightGroupComponent.Lights) {
					selection.AddRange(light.LightSources.Select(l => l.gameObject));
				}

				Selection.objects = selection.ToArray();
			}
		}
	}
}
