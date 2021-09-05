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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public abstract class ItemInspector : UnityEditor.Editor
	{
		protected abstract MonoBehaviour UndoTarget { get; }

		protected TableAuthoring TableComponent;
		protected PlayfieldAuthoring PlayfieldComponent;

		#region Unity Events

		protected virtual void OnEnable()
		{
			Undo.undoRedoPerformed += OnUndoRedoPerformed;

			TableComponent = (target as MonoBehaviour)?.gameObject.GetComponentInParent<TableAuthoring>();
			PlayfieldComponent = (target as MonoBehaviour)?.gameObject.GetComponentInParent<PlayfieldAuthoring>();
		}

		protected virtual void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		}

		private void OnUndoRedoPerformed()
		{
			switch (target) {
				case IItemMeshAuthoring meshItem:
					meshItem.IMainAuthoring.RebuildMeshes();
					meshItem.IMainAuthoring.UpdateTransforms();
					meshItem.IMainAuthoring.UpdateVisibility();
					break;
				case IItemMainRenderableAuthoring mainItem:
					mainItem.RebuildMeshes();
					mainItem.UpdateTransforms();
					mainItem.UpdateVisibility();
					break;
			}
		}

		public override void OnInspectorGUI()
		{
			if (!(target is IItemMainRenderableAuthoring item)) {
				return;
			}

			GUILayout.Space(10);
			if (GUILayout.Button("Force Update Mesh")) {
				item.RebuildMeshes();
			}
		}

		#endregion

		protected void PropertyField(SerializedProperty serializedProperty, string label = null,
			bool rebuildMesh = false, bool updateTransforms = false, bool updateVisibility = false,
			Action onChanged = null, Action onChanging = null)
		{
			var checkForChanges = rebuildMesh || updateTransforms || onChanged != null;
			if (checkForChanges) {
				EditorGUI.BeginChangeCheck();
			}

			if (string.IsNullOrEmpty(label)) {
				EditorGUILayout.PropertyField(serializedProperty);
			} else {
				EditorGUILayout.PropertyField(serializedProperty, new GUIContent(label));
			}

			if (checkForChanges && EditorGUI.EndChangeCheck()) {
				onChanging?.Invoke();
				switch (target) {
					case IItemMeshAuthoring meshItem:
						if (rebuildMesh) {
							meshItem.IMainAuthoring.RebuildMeshes();
						}
						if (updateTransforms) {
							meshItem.IMainAuthoring.UpdateTransforms();
						}
						if (updateVisibility) {
							meshItem.IMainAuthoring.UpdateVisibility();
						}
						break;

					case IItemMainRenderableAuthoring mainItem:
						if (rebuildMesh) {
							mainItem.RebuildMeshes();
						}
						if (updateTransforms) {
							mainItem.UpdateTransforms();
						}
						if (updateVisibility) {
							mainItem.UpdateVisibility();
						}
						break;
				}
				onChanged?.Invoke();
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
					case IItemMeshAuthoring meshItem:
						if (rebuildMesh) {
							meshItem.IMainAuthoring.RebuildMeshes();
						}
						if (updateVisibility) {
							meshItem.IMainAuthoring.UpdateVisibility();
						}
						break;

					case IItemMainRenderableAuthoring mainItem:
						if (rebuildMesh) {
							mainItem.RebuildMeshes();
						}
						if (updateVisibility) {
							mainItem.UpdateVisibility();
						}
						break;
				}
			}
		}

		protected void MeshDropdownProperty(string label, SerializedProperty meshProp, string meshFolder, GameObject go,
			SerializedProperty typeProp, Dictionary<string, int> meshTypeMap)
		{
			var files = Directory.GetFiles(meshFolder, "*.mesh")
				.Select(Path.GetFileNameWithoutExtension)
				.ToArray();

			var selectedIndex = files.ToList().IndexOf(meshProp.stringValue);
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUILayout.Popup(label, selectedIndex, files);
			if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < files.Length && go != null) {
				var mf = go.GetComponent<MeshFilter>();
				if (mf) {
					var mesh = (Mesh)AssetDatabase.LoadAssetAtPath(Path.Combine(meshFolder, $"{files[newIndex]}.mesh"), typeof(Mesh));
					mf.sharedMesh = mesh;
					meshProp.stringValue = files[newIndex];
					if (meshTypeMap.ContainsKey(files[newIndex])) {
						typeProp.intValue = meshTypeMap[files[newIndex]];
					}
					meshProp.serializedObject.ApplyModifiedProperties();
				}
			}
		}

		protected void OnPreInspectorGUI()
		{
			if (!(target is IItemMainRenderableAuthoring item)) {
				return;
			}

			EditorGUI.BeginChangeCheck();
			var newLock = EditorGUILayout.Toggle("IsLocked", item.IsLocked);
			if (EditorGUI.EndChangeCheck())
			{
				FinishEdit("IsLocked");
				item.IsLocked = newLock;
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

		protected void ItemDataField(string label, ref bool field, bool dirtyMesh = true, Action<bool, bool> onChanged = null)
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

					case IItemMeshAuthoring meshItem:
						Undo.RecordObjects(new Object[] {UndoTarget, UndoTarget.transform}, undoLabel);
						meshItem.IMainAuthoring.RebuildMeshes();
						break;

					case IItemColliderAuthoring _:
						Undo.RecordObject(UndoTarget, undoLabel);
						break;

					case IItemMainRenderableAuthoring mainItem:
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
