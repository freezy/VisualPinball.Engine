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

				case IColliderComponent colliderComponent:
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

		/// <summary>
		/// Shows a dropdown for selecting a mesh from an FBX file. The selected mesh will be assigned to the
		/// MeshFilter of the given GameObject. It also updates another property based on the selected mesh.
		/// </summary>
		/// <param name="label">Label of the dropdown in the editor</param>
		/// <param name="meshProp">Serialized property of the mesh</param>
		/// <param name="fbxPath">Path to the FBX file</param>
		/// <param name="go">GameObject to apply the mesh to</param>
		/// <param name="typeProp">Serialized property of the mesh type</param>
		/// <param name="meshTypeMap">Map of the type based on the name of the mesh</param>
		/// <param name="prefix">Prefix to filter out non-selectable meshes in the FBX file</param>
		protected void MeshDropdownPropertyFbx(string label, SerializedProperty meshProp, string fbxPath, GameObject go,
			SerializedProperty typeProp, Dictionary<string, int> meshTypeMap, string prefix)
		{
			var allObjects = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
			var meshObjects = allObjects
				.Where(o => o is Mesh)
				.Cast<Mesh>()
				.Where(o => o.name.StartsWith(prefix))
				.ToArray();
			var meshNames = meshObjects
				.Select(o => o.name[prefix.Length..])
				.Concat(new[] { CustomMeshLabel })
				.ToArray();

			var selectedIndex = meshNames.ToList().IndexOf(meshProp.stringValue);
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUILayout.Popup(label, selectedIndex, meshNames);
			if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < meshNames.Length && go != null) {
				var mf = go.GetComponent<MeshFilter>();
				var mr = go.GetComponent<MeshRenderer>();
				if (newIndex < meshObjects.Length) {
					if (!mf) {
						mf = go.AddComponent<MeshFilter>();
					}
					if (!mr) {
						go.AddComponent<MeshRenderer>();
					}
					var mesh = meshObjects[newIndex];
					mr.enabled = true;
					mf.sharedMesh = mesh;

				} else {
					if (mr) {
						mr.enabled = false;
					}
					if (mf) {
						mf.sharedMesh = null;
					}
				}

				meshProp.stringValue = meshNames[newIndex];
				if (meshTypeMap.ContainsKey(meshNames[newIndex])) {
					typeProp.intValue = meshTypeMap[meshNames[newIndex]];
				}
				meshProp.serializedObject.ApplyModifiedProperties();
				if (target is MonoBehaviour mb) {
					var colliderComponent = mb.GetComponent<IColliderComponent>();
					if (colliderComponent != null) {
						colliderComponent.CollidersDirty = true;
					}
				}

				if (target is IMainRenderableComponent mrc) {
					mrc.UpdateTransforms();
				}
			}
		}

		protected void MeshDropdownProperty(string label, SerializedProperty meshProp, string meshFolder, GameObject go,
			SerializedProperty typeProp, Dictionary<string, int> meshTypeMap)
		{
			var files = Directory.GetFiles(meshFolder, "*.mesh")
				.Select(Path.GetFileNameWithoutExtension)
				.Concat(new[] { CustomMeshLabel })
				.ToArray();

			var selectedIndex = files.ToList().IndexOf(meshProp.stringValue);
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUILayout.Popup(label, selectedIndex, files);
			if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < files.Length && go != null) {
				var meshPath = Path.Combine(meshFolder, $"{files[newIndex]}.mesh");
				var mf = go.GetComponent<MeshFilter>();
				var mr = go.GetComponent<MeshRenderer>();
				if (File.Exists(meshPath)) {
					if (!mf) {
						mf = go.AddComponent<MeshFilter>();
					}
					if (!mr) {
						go.AddComponent<MeshRenderer>();
					}
					var mesh = (Mesh)AssetDatabase.LoadAssetAtPath(meshPath, typeof(Mesh));
					mr.enabled = true;
					mf.sharedMesh = mesh;
					
				} else {
					if (mr) {
						mr.enabled = false;
					}
					if (mf) {
						mf.sharedMesh = null;
					}
				}
					
				meshProp.stringValue = files[newIndex];
				if (meshTypeMap.ContainsKey(files[newIndex])) {
					typeProp.intValue = meshTypeMap[files[newIndex]];
				}
				meshProp.serializedObject.ApplyModifiedProperties();
				if (target is MonoBehaviour mb) {
					var colliderComponent = mb.GetComponent<IColliderComponent>();
					if (colliderComponent != null) {
						colliderComponent.CollidersDirty = true;
					}
				}

				if (target is IMainRenderableComponent mrc) {
					mrc.UpdateTransforms();
				}
			}
		}

		protected void OnPreInspectorGUI()
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

					case IColliderComponent _:
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
