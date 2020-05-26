using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Flipper;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(Transform))]
	[CanEditMultipleObjects] // TODO: transform all targets
	public class TransformInspector : UnityEditor.Editor
	{
		private UnityEditor.Editor _defaultEditor;
		private Transform _transform;
		private IItemDataTransformable _primaryItem;
		private List<SecondaryItem> _secondaryItems = new List<SecondaryItem>();

		protected virtual void OnEnable()
		{
			_transform = target as Transform;

			bool useDefault = true;
			foreach (var t in targets) {
				var item = (t as Transform).GetComponent<IItemDataTransformable>();
				if (item != null) {
					useDefault = false;
					if (_primaryItem == null) {
						_primaryItem = item;
					} else {
						if (_primaryItem.EditorPositionType != item.EditorPositionType
							|| _primaryItem.EditorRotationType != item.EditorRotationType) {
							// differing var types in underlying data, null out item so we inspectors and tools are hidden
							_primaryItem = null;
							_secondaryItems.Clear();
							break;
						}
						_secondaryItems.Add(new SecondaryItem {
							Transform = t as Transform,
							Item = item,
							Offset = item.GetEditorPosition() - _primaryItem.GetEditorPosition(),
						});
					}
				}
			}
			if (useDefault) {
				_defaultEditor = CreateEditor(targets, Type.GetType("UnityEditor.TransformInspector, UnityEditor"));
			}
		}

		protected virtual void OnDisable()
		{
			if (_defaultEditor != null) {
				var defaultDisableMethod = _defaultEditor.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				defaultDisableMethod?.Invoke(_defaultEditor, null);
				DestroyImmediate(_defaultEditor);
				_defaultEditor = null;
			}
			// restore tools
			Tools.hidden = false;
		}

		public override void OnInspectorGUI()
		{
			if (_defaultEditor != null) {
				_defaultEditor.OnInspectorGUI();
				return;
			}

			if (_transform == null || _primaryItem == null) {
				return;
			}

			if (_primaryItem.EditorPositionType != ItemDataTransformType.None) {
				EditorGUI.BeginChangeCheck();
				var pos = ItemDataTransformField("Position", _primaryItem.EditorPositionType, _primaryItem.GetEditorPosition());
				if (EditorGUI.EndChangeCheck()) {
					FinishMove(pos, isLocalPos: true);
				}
			}

			if (_primaryItem.EditorRotationType != ItemDataTransformType.None) {
				EditorGUI.BeginChangeCheck();
				var rot = ItemDataTransformField("Rotation", _primaryItem.EditorRotationType, _primaryItem.GetEditorRotation());
				if (EditorGUI.EndChangeCheck()) {
					FinishRotate(rot);
				}
			}

			if (_primaryItem.EditorScaleType != ItemDataTransformType.None) {
				EditorGUI.BeginChangeCheck();
				var scale = ItemDataTransformField("Scale", _primaryItem.EditorScaleType, _primaryItem.GetEditorScale());
				if (EditorGUI.EndChangeCheck()) {
					FinishScale(scale);
				}
			}

			RebuildMeshes();
		}

		private void RebuildMeshes()
		{
			if (_primaryItem.MeshDirty) {
				_primaryItem.RebuildMeshes();
			}
			foreach (var secondary in _secondaryItems) {
				if (secondary.Item.MeshDirty) {
					secondary.Item.RebuildMeshes();
				}
			}
		}

		private Vector3 ItemDataTransformField(string label, ItemDataTransformType type, Vector3 val)
		{
			switch (type) {
				case ItemDataTransformType.OneD:
					val.x = EditorGUILayout.FloatField(label, val.x);
					break;
				case ItemDataTransformType.TwoD:
					val = EditorGUILayout.Vector2Field(label, val);
					break;
				case ItemDataTransformType.ThreeD:
					val = EditorGUILayout.Vector3Field(label, val);
					break;
			}
			return val;
		}

		protected virtual void OnSceneGUI()
		{
			if (_defaultEditor != null) {
				return;
			}
			if (_transform == null || _primaryItem == null) {
				Tools.hidden = true;
				return;
			}

			switch (Tools.current) {
				case Tool.Rotate:
					HandleRotationTool();
					break;

				case Tool.Move:
					HandleMoveTool();
					break;

				case Tool.Scale:
					HandleScaleTool();
					break;

				default:
					Tools.hidden = false;
					break;
			}

			RebuildMeshes();
		}

		private void HandleRotationTool()
		{
			Tools.hidden = true;
			var handlePos = _primaryItem.GetEditorPosition();
			if (_transform.parent != null) {
				handlePos = _transform.parent.TransformPoint(handlePos);
			}
			switch (_primaryItem.EditorRotationType) {
				case ItemDataTransformType.OneD: {
					EditorGUI.BeginChangeCheck();
					Handles.color = Handles.zAxisColor;
					var rot = Handles.Disc(_transform.rotation, handlePos, _transform.forward, HandleUtility.GetHandleSize(handlePos), false, 10f);
					if (EditorGUI.EndChangeCheck()) {
						if (_transform.parent != null) {
							rot = Quaternion.Inverse(_transform.parent.rotation) * rot;
						}
						FinishRotate(new Vector3(rot.eulerAngles.z, 0f, 0f));
					}
					break;
				}
				case ItemDataTransformType.ThreeD: {
					EditorGUI.BeginChangeCheck();
					Quaternion newRot = Handles.RotationHandle(_transform.rotation, handlePos);
					if (EditorGUI.EndChangeCheck()) {
						if (_transform.parent != null) {
							newRot = Quaternion.Inverse(_transform.parent.rotation) * newRot;
						}
						FinishRotate(newRot.eulerAngles);
					}
					break;
				}
				default:
					break;
			}
		}

		private void HandleMoveTool()
		{
			Tools.hidden = true;
			var handlePos = _primaryItem.GetEditorPosition();
			if (_transform.parent != null) {
				handlePos = _transform.parent.TransformPoint(handlePos);
			}
			switch (_primaryItem.EditorPositionType) {
				case ItemDataTransformType.TwoD: {
					EditorGUI.BeginChangeCheck();
					Handles.color = Handles.xAxisColor;
					var newPos = Handles.Slider(handlePos, _transform.right);
					if (EditorGUI.EndChangeCheck()) {
						FinishMove(newPos);
					}

					EditorGUI.BeginChangeCheck();
					Handles.color = Handles.yAxisColor;
					newPos = Handles.Slider(handlePos, _transform.up);
					if (EditorGUI.EndChangeCheck()) {
						FinishMove(newPos);
					}

					EditorGUI.BeginChangeCheck();
					Handles.color = Handles.zAxisColor;
					newPos = Handles.Slider2D(
						handlePos,
						_transform.forward,
						_transform.right,
						_transform.up,
						HandleUtility.GetHandleSize(handlePos) * 0.2f,
						Handles.RectangleHandleCap,
						0f);
					if (EditorGUI.EndChangeCheck()) {
						FinishMove(newPos);
					}
					break;
				}
				case ItemDataTransformType.ThreeD: {
					EditorGUI.BeginChangeCheck();
					Vector3 newPos = Handles.PositionHandle(handlePos, _transform.rotation);
					if (EditorGUI.EndChangeCheck()) {
						FinishMove(newPos);
					}
					break;
				}
				default:
					break;
			}
		}

		private void HandleScaleTool()
		{
			Tools.hidden = true;
			var handlePos = _primaryItem.GetEditorPosition();
			if (_transform.parent != null) {
				handlePos = _transform.parent.TransformPoint(handlePos);
			}
			var handleRot = _transform.rotation;
			var handleScale = HandleUtility.GetHandleSize(handlePos);
			switch (_primaryItem.EditorScaleType) {
				case ItemDataTransformType.OneD: {
					EditorGUI.BeginChangeCheck();
					float scale = Handles.ScaleSlider(_primaryItem.GetEditorScale().x, handlePos, _transform.right, handleRot, handleScale, 0f);
					if (EditorGUI.EndChangeCheck()) {
						FinishScale(new Vector3(scale, 0f, 0f));
					}
					break;
				}
				case ItemDataTransformType.ThreeD: {
					EditorGUI.BeginChangeCheck();
					Vector3 newScale = Handles.ScaleHandle(_primaryItem.GetEditorScale(), handlePos, handleRot, handleScale);
					if (EditorGUI.EndChangeCheck()) {
						FinishScale(newScale);
					}
					break;
				}
				default:
					break;
			}
		}

		private void FinishMove(Vector3 newWorldPos, bool isLocalPos = false)
		{
			if (_primaryItem.RebuildMeshOnMove) {
				_primaryItem.MeshDirty = true;
			}
			string undoLabel = "Move " + _transform.gameObject.name;
			Undo.RecordObject(_primaryItem as UnityEngine.Object, undoLabel);
			Undo.RecordObject(_transform, undoLabel);
			var finalPos = newWorldPos;
			if (_transform.parent != null && !isLocalPos) {
				finalPos = _transform.parent.InverseTransformPoint(newWorldPos);
			}
			_primaryItem.SetEditorPosition(finalPos);

			foreach (var secondary in _secondaryItems) {
				if (secondary.Item.RebuildMeshOnMove) {
					secondary.Item.MeshDirty = true;
				}
				Undo.RecordObject(secondary.Item as UnityEngine.Object, undoLabel);
				Undo.RecordObject(secondary.Transform, undoLabel);
				secondary.Item.SetEditorPosition(finalPos + secondary.Offset);
			}
		}

		private void FinishRotate(Vector3 newEuler)
		{
			if (_primaryItem.RebuildMeshOnMove) {
				_primaryItem.MeshDirty = true;
			}
			string undoLabel = "Rotate " + _transform.gameObject.name;
			Undo.RecordObject(_primaryItem as UnityEngine.Object, undoLabel);
			Undo.RecordObject(_transform, undoLabel);
			_primaryItem.SetEditorRotation(newEuler);
		}

		private void FinishScale(Vector3 newScale)
		{
			if (_primaryItem.RebuildMeshOnScale) {
				_primaryItem.MeshDirty = true;
			}
			string undoLabel = "Scale " + _transform.gameObject.name;
			Undo.RecordObject(_primaryItem as UnityEngine.Object, undoLabel);
			Undo.RecordObject(_transform, undoLabel);
			_primaryItem.SetEditorScale(newScale);
		}

		private class SecondaryItem
		{
			public Transform Transform;
			public IItemDataTransformable Item;
			public Vector3 Offset;
		}
	}
}
