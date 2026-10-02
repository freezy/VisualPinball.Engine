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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightGroupComponent)), CanEditMultipleObjects]
	public class LightGroupInspector : ItemInspector
	{
		private LightGroupComponent _lightGroupComponent;
		private SerializedProperty _lightGroupProperty;
		private SerializedProperty _lampIdsProperty;
		private ReorderableList _lightList;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_lightGroupComponent = target as LightGroupComponent;
			_lightGroupProperty = serializedObject.FindProperty(nameof(LightGroupComponent._lights));
			_lampIdsProperty = serializedObject.FindProperty(nameof(LightGroupComponent._lampIds));

			// the lamp ids go with the lights by index, so they're moved, added and removed along.
			_lightList = new ReorderableList(serializedObject, _lightGroupProperty, true, true, true, true) {
				drawHeaderCallback = rect => EditorGUI.LabelField(rect, new GUIContent("Lights", _lightGroupProperty.tooltip)),
				elementHeight = EditorGUIUtility.singleLineHeight + 4f,
				drawElementCallback = DrawLight,
				onReorderCallbackWithDetails = (_, from, to) => {
					SyncLampIds();
					_lampIdsProperty.MoveArrayElement(from, to);
				},
				onAddCallback = _ => {
					var index = _lightGroupProperty.arraySize;
					_lightGroupProperty.InsertArrayElementAtIndex(index);
					_lightGroupProperty.GetArrayElementAtIndex(index).objectReferenceValue = null;
					SyncLampIds();
				},
				onRemoveCallback = list => {
					var index = list.index >= 0 && list.index < _lightGroupProperty.arraySize ? list.index : _lightGroupProperty.arraySize - 1;
					if (index < 0) {
						return;
					}
					SyncLampIds();
					_lampIdsProperty.DeleteArrayElementAtIndex(index);
					_lightGroupProperty.GetArrayElementAtIndex(index).objectReferenceValue = null;
					_lightGroupProperty.DeleteArrayElementAtIndex(index);
					list.index = Mathf.Min(index, _lightGroupProperty.arraySize - 1);
				},
			};
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			if (serializedObject.isEditingMultipleObjects) {
				PropertyField(_lightGroupProperty, "Lights");
			} else {
				_lightList.DoLayoutList();
			}

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
				SyncLampIds();
			}

			if (GUILayout.Button("Replace With Children")) {
				var lights = _lightGroupComponent.GetComponentsInChildren<ILampDeviceComponent>();
				_lightGroupProperty.ClearArray();
				_lampIdsProperty.ClearArray();
				foreach (var light in lights) {
					if (ReferenceEquals(light, _lightGroupComponent)) {
						continue;
					}
					_lightGroupProperty.InsertArrayElementAtIndex(_lightGroupProperty.arraySize);
					_lightGroupProperty.GetArrayElementAtIndex(_lightGroupProperty.arraySize - 1).objectReferenceValue = light as MonoBehaviour;
				}
				SyncLampIds();
			}

			if (GUILayout.Button("Clear")) {
				_lightGroupProperty.ClearArray();
				_lampIdsProperty.ClearArray();
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

		/// <summary>
		/// A light of the group, and for one with several lamps, like an insert
		/// with more than one bulb, which of them the group drives.
		/// </summary>
		private void DrawLight(Rect rect, int index, bool isActive, bool isFocused)
		{
			rect.y += 2f;
			rect.height = EditorGUIUtility.singleLineHeight;
			var element = _lightGroupProperty.GetArrayElementAtIndex(index);
			var lamps = (element.objectReferenceValue as ILampDeviceComponent)?.AvailableLamps?.ToArray();
			if (lamps == null || lamps.Length < 2) {
				EditorGUI.PropertyField(rect, element, GUIContent.none);
				return;
			}

			const float padding = 4f;
			var popupWidth = Mathf.Min(120f, rect.width * 0.4f);
			EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width - popupWidth - padding, rect.height), element, GUIContent.none);

			SyncLampIds();
			var lampId = _lampIdsProperty.GetArrayElementAtIndex(index);
			// no id is the light's first lamp.
			var current = Mathf.Max(0, Array.FindIndex(lamps, lamp => lamp.Id == lampId.stringValue));
			var labels = lamps.Select(lamp => string.IsNullOrEmpty(lamp.Description) ? lamp.Id : lamp.Description).ToArray();
			var picked = EditorGUI.Popup(new Rect(rect.xMax - popupWidth, rect.y, popupWidth, rect.height), current, labels);
			if (picked != current) {
				lampId.stringValue = picked == 0 ? string.Empty : lamps[picked].Id;
			}
		}

		/// <summary>Gives every light a lamp id, empty for new ones, and drops those without a light.</summary>
		private void SyncLampIds()
		{
			var count = _lightGroupProperty.arraySize;
			while (_lampIdsProperty.arraySize < count) {
				_lampIdsProperty.InsertArrayElementAtIndex(_lampIdsProperty.arraySize);
				_lampIdsProperty.GetArrayElementAtIndex(_lampIdsProperty.arraySize - 1).stringValue = string.Empty;
			}
			if (_lampIdsProperty.arraySize > count) {
				_lampIdsProperty.arraySize = count;
			}
		}
	}
}
