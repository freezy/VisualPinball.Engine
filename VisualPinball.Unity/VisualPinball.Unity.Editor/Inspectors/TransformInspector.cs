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
	[CanEditMultipleObjects]
	public class TransformInspector : UnityEditor.Editor
	{
		private UnityEditor.Editor _defaultEditor;
		private Transform _transform;
		private IEditableItemBehavior _primaryItem;
		private List<SecondaryItem> _secondaryItems = new List<SecondaryItem>();
		private ItemDataTransformType _positionType = ItemDataTransformType.ThreeD;
		private ItemDataTransformType _rotationType = ItemDataTransformType.ThreeD;
		private ItemDataTransformType _scaleType = ItemDataTransformType.ThreeD;

		// work around for scale handle weirdness
		private float _scaleFactor = 1.0f;

		protected virtual void OnEnable()
		{
			_transform = target as Transform;

			bool useDefault = true;
			foreach (var t in targets) {
				var item = (t as Transform)?.GetComponent<IEditableItemBehavior>();
				if (item != null) {
					useDefault = false;
					if (_primaryItem == null) {
						_primaryItem = item;
						_positionType = item.EditorPositionType;
						_rotationType = item.EditorRotationType;
						_scaleType = item.EditorScaleType;
					} else {
						// only transform on axes supported by all
						if (item.EditorPositionType < _positionType) {
							_positionType = item.EditorPositionType;
						}
						if (item.EditorRotationType < _rotationType) {
							_rotationType = item.EditorRotationType;
						}
						if (item.EditorScaleType < _scaleType) {
							_scaleType = item.EditorScaleType;
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

			GUILayout.Label("VPE item transforms driven by data on the component below.");
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

		protected virtual void OnSceneGUI()
		{
			if (_defaultEditor != null) {
				return;
			}

			Tools.hidden = true;

			if (_transform == null || _primaryItem == null) {
				return;
			}

			bool dragPointEditEnabled = (_primaryItem as IDragPointsEditable)?.DragPointEditEnabled ?? false;
			if (!dragPointEditEnabled) {
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
				}
			}

			RebuildMeshes();
		}

		private void HandleRotationTool()
		{
			if (_secondaryItems.Count > 0) {
				return;
			}
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
			var handlePos = _primaryItem.GetEditorPosition();
			if (_transform.parent != null) {
				handlePos = _transform.parent.TransformPoint(handlePos);
			}
			switch (_primaryItem.EditorPositionType) {
				case ItemDataTransformType.TwoD: {
					EditorGUI.BeginChangeCheck();
					Handles.color = Handles.xAxisColor;
					var newPos = Handles.Slider(handlePos, Vector3.right);
					if (EditorGUI.EndChangeCheck()) {
						FinishMove(newPos);
					}

					EditorGUI.BeginChangeCheck();
					Handles.color = Handles.yAxisColor;
					newPos = Handles.Slider(handlePos, Vector3.forward);
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
					Vector3 newPos = Handles.PositionHandle(handlePos, Quaternion.identity);
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
			var e = Event.current;
			if (e.type == EventType.MouseDown || e.type == EventType.MouseUp) {
				_scaleFactor = _primaryItem.GetEditorScale().x;
			}

			if (_secondaryItems.Count > 0) {
				return;
			}
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
					Vector3 oldScale = _primaryItem.GetEditorScale();
					Vector3 newScale = Handles.ScaleHandle(oldScale, handlePos, handleRot, handleScale);
					if (Mathf.Abs(newScale.x - oldScale.x) > Mathf.Epsilon && Mathf.Abs(newScale.y - oldScale.y) > Mathf.Epsilon && Mathf.Abs(newScale.z - oldScale.z) > Mathf.Epsilon) {
						// the center bit of the scale handle appears to be doing some extra multiplying, not totally sure what's going on, but experimentally
						// it seems like its a factor of one of the axes too much, so (on click) we'll update a factor and adjust accordingly
						if (_scaleFactor != 0) {
							newScale /= _scaleFactor;
						} else {
							newScale = Vector3.zero;
						}
					}
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
			_primaryItem.MeshDirty = true;
			string undoLabel = "Move " + _transform.gameObject.name;
			Undo.RecordObject(_primaryItem as UnityEngine.Object, undoLabel);
			Undo.RecordObject(_transform, undoLabel);
			var finalPos = newWorldPos;
			if (_transform.parent != null && !isLocalPos) {
				finalPos = _transform.parent.InverseTransformPoint(newWorldPos);
			}
			_primaryItem.SetEditorPosition(finalPos);

			foreach (var secondary in _secondaryItems) {
				secondary.Item.MeshDirty = true;
				Undo.RecordObject(secondary.Item as UnityEngine.Object, undoLabel);
				Undo.RecordObject(secondary.Transform, undoLabel);
				secondary.Item.SetEditorPosition(finalPos + secondary.Offset);
			}
		}

		private void FinishRotate(Vector3 newEuler)
		{
			_primaryItem.MeshDirty = true;
			string undoLabel = "Rotate " + _transform.gameObject.name;
			Undo.RecordObject(_primaryItem as UnityEngine.Object, undoLabel);
			Undo.RecordObject(_transform, undoLabel);
			_primaryItem.SetEditorRotation(newEuler);
		}

		private void FinishScale(Vector3 newScale)
		{
			_primaryItem.MeshDirty = true;
			string undoLabel = "Scale " + _transform.gameObject.name;
			Undo.RecordObject(_primaryItem as UnityEngine.Object, undoLabel);
			Undo.RecordObject(_transform, undoLabel);
			_primaryItem.SetEditorScale(newScale);
		}

		private class SecondaryItem
		{
			public Transform Transform;
			public IEditableItemBehavior Item;
			public Vector3 Offset;
		}
	}
}
