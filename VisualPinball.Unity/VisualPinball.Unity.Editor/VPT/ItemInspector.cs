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

using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public abstract class ItemInspector : UnityEditor.Editor
	{
		protected abstract MonoBehaviour UndoTarget { get; }

		protected TableComponent TableComponent;
		protected PlayfieldComponent PlayfieldComponent;

		private bool _meshDirty;
		private bool _collidersDirty;
		private bool _transformsDirty;
		private bool _visibilityDirty;

		private SerializedProperty _isLockedProperty;

		internal const string CustomMeshLabel = "Custom Mesh";

		#region Unity Events

		protected virtual void OnEnable()
		{
			Undo.undoRedoPerformed += OnUndoRedoPerformed;

			TableComponent = (target as MonoBehaviour)?.gameObject.GetComponentInParent<TableComponent>();
			PlayfieldComponent = (target as MonoBehaviour)?.gameObject.GetComponentInParent<PlayfieldComponent>();

			_isLockedProperty = serializedObject.FindProperty("_isLocked");
		}

		protected virtual void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		}

		private void OnUndoRedoPerformed()
		{
			switch (target) {
				case IMeshComponent meshItem:
					meshItem.MainRenderableComponent.RebuildMeshes();
					meshItem.MainRenderableComponent.UpdateTransforms();
					// meshItem.MainRenderableComponent.UpdateVisibility();
					break;
				case IMainRenderableComponent mainItem:
					mainItem.RebuildMeshes();
					mainItem.UpdateTransforms();
					// mainItem.UpdateVisibility();
					break;
			}
		}

		public override void OnInspectorGUI()
		{
			if (!(target is IMainRenderableComponent item)) {
				return;
			}

			GUILayout.Space(10);
			if (GUILayout.Button("Force Update Mesh")) {
				item.RebuildMeshes();
			}
		}

		#endregion

		protected void BeginEditing()
		{
			serializedObject.Update();
		}

		protected void EndEditing()
		{
			serializedObject.ApplyModifiedProperties();

			switch (target) {
				case IMeshComponent meshItem:
					if (_meshDirty) {
						meshItem.MainRenderableComponent.RebuildMeshes();
					}
					if (_transformsDirty) {
						meshItem.MainRenderableComponent.UpdateTransforms();
					}
					if (_visibilityDirty) {
						// meshItem.MainRenderableComponent.UpdateVisibility();
					}
					break;

				case IMainRenderableComponent mainItem:
					if (_meshDirty) {
						mainItem.RebuildMeshes();
					}
					if (_transformsDirty) {
						mainItem.UpdateTransforms();
					}
					if (_visibilityDirty) {
						// mainItem.UpdateVisibility();
					}
					break;

				case ICollidableComponent colliderComponent:
					if (_collidersDirty) {
						colliderComponent.CollidersDirty = true;
					}
					break;

				case IAnimationComponent animationComponent:
					if (_transformsDirty) {
						animationComponent.UpdateTransforms();
					}
					break;
			}

			_meshDirty = false;
			_collidersDirty = false;
			_transformsDirty = false;
			_visibilityDirty = false;
		}

		protected void PropertyField(SerializedProperty serializedProperty, string label = null,
			bool rebuildMesh = false, bool updateTransforms = false, bool updateVisibility = false, bool updateColliders = false,
			Action onChanged = null, Action onChanging = null)
		{

			EditorGUI.BeginChangeCheck();

			if (string.IsNullOrEmpty(label)) {
				if (string.IsNullOrEmpty(serializedProperty.tooltip))
					EditorGUILayout.PropertyField(serializedProperty);
				else
					EditorGUILayout.PropertyField(serializedProperty, new GUIContent(serializedProperty.displayName, serializedProperty.tooltip));
			} else {
				if (string.IsNullOrEmpty(serializedProperty.tooltip))
					EditorGUILayout.PropertyField(serializedProperty, new GUIContent(label));
				else
					EditorGUILayout.PropertyField(serializedProperty, new GUIContent(label, serializedProperty.tooltip));
			}

			if (EditorGUI.EndChangeCheck()) {
				onChanging?.Invoke();
				_meshDirty = rebuildMesh;
				_collidersDirty = updateColliders;
				_transformsDirty = updateTransforms;
				_visibilityDirty = updateVisibility;
				onChanged?.Invoke();
			}
		}

		protected void PropertyField(SerializedProperty serializedProperty, string label, ref bool isUpdating,
			Action<Transform> onStartChanging, Action onChanging, bool updateTransforms = false)
		{
			var controlName = "__detectFinished_" + serializedProperty.name;
			GUI.SetNextControlName(controlName);
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(serializedProperty, string.IsNullOrEmpty(serializedProperty.tooltip)
					? new GUIContent(label)
					: new GUIContent(label, serializedProperty.tooltip)
			);
			var hasFocus = GUI.GetNameOfFocusedControl() == controlName;

			switch (hasFocus) {
				case true when !isUpdating:
					isUpdating = true;
					onStartChanging.Invoke(((MonoBehaviour)target).transform);
					break;

				case false when isUpdating:
					isUpdating = false;
					break;
			}

			if (EditorGUI.EndChangeCheck()) {
				GUI.changed = true;
				_transformsDirty = updateTransforms;
				onChanging?.Invoke();
			}
		}

		protected void DropDownProperty(string label, SerializedProperty prop, string[] optionStrings, int[] optionValues,
			bool rebuildMesh = false, bool updateVisibility = false)
		{
			if (optionStrings == null || optionValues == null || optionStrings.Length != optionValues.Length) {
				return;
			}

			var selectedIndex = 0;
			for (var i = 0; i < optionValues.Length; i++) {
				if (optionValues[i].Equals(prop.intValue)) {
					selectedIndex = i;
					break;
				}
			}

			EditorGUI.BeginChangeCheck();
			selectedIndex = EditorGUILayout.Popup(label, selectedIndex, optionStrings);
			if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < optionValues.Length) {
				prop.intValue = optionValues[selectedIndex];
				prop.serializedObject.ApplyModifiedProperties();
				switch (target) {
					case IMeshComponent meshItem:
						if (rebuildMesh) {
							meshItem.MainRenderableComponent.RebuildMeshes();
						}
						if (updateVisibility) {
							// meshItem.MainRenderableComponent.UpdateVisibility();
						}
						break;

					case IMainRenderableComponent mainItem:
						if (rebuildMesh) {
							mainItem.RebuildMeshes();
						}
						if (updateVisibility) {
							// mainItem.UpdateVisibility();
						}
						break;
				}
			}
		}

		protected virtual void OnPreInspectorGUI()
		{
			if (!(target is IMainRenderableComponent)) {
				return;
			}

			EditorGUI.BeginChangeCheck();
			PropertyField(_isLockedProperty, "Locked");
			if (EditorGUI.EndChangeCheck()) {
				SceneView.RepaintAll();
			}
		}

		#region Data Fields

		public void ItemDataSlider(string label, ref float field, float leftVal, float rightVal, bool dirtyMesh = true, Action<float, float> onChanged = null)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.Slider(label, field, leftVal, rightVal);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				var fieldBefore = field;
				field = val;
				onChanged?.Invoke(fieldBefore, field);
			}
		}

		public void ItemDataField(string label, ref bool field, bool dirtyMesh = true, Action<bool, bool> onChanged = null)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.Toggle(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				var fieldBefore = field;
				field = val;
				onChanged?.Invoke(fieldBefore, field);
			}
		}

		#endregion

		protected virtual void FinishEdit(string label, bool dirtyMesh = true)
		{
			var undoLabel = $"Edit {label} of {target?.name}";
			if (dirtyMesh) {
				// set dirty flag true before recording object state for the undo so meshes will rebuild after the undo as well
				switch (target) {

					case IMeshComponent meshItem:
						Undo.RecordObjects(new Object[] {UndoTarget, UndoTarget.transform}, undoLabel);
						meshItem.MainRenderableComponent.RebuildMeshes();
						break;

					case ICollidableComponent _:
						Undo.RecordObject(UndoTarget, undoLabel);
						break;

					case IMainRenderableComponent mainItem:
						Undo.RecordObjects(new Object[] {UndoTarget, UndoTarget.transform }, undoLabel);
						mainItem.RebuildMeshes();
						break;
				}
			}
		}

		protected static void WalkChildren(IEnumerable node, Action<Transform> action)
		{
			foreach (Transform childTransform in node) {
				action(childTransform);
				WalkChildren(childTransform, action);
			}
		}
	}
}
