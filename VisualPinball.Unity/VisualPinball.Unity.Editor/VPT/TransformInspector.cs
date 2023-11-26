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

// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	//[CustomEditor(typeof(Transform))]
	[CanEditMultipleObjects]
	public class TransformInspector : UnityEditor.Editor
	{
		private UnityEditor.Editor _defaultEditor;
		private Transform _transform;

		/// <summary>
		/// The first selected item
		/// </summary>
		private IMainRenderableComponent _primaryItem;

		/// <summary>
		/// On multi-selection, these are the other selected items.
		/// </summary>
		private readonly List<SecondaryItem> _secondaryItems = new List<SecondaryItem>();

		private ItemDataTransformType _positionType = ItemDataTransformType.ThreeD;
		private ItemDataTransformType _rotationType = ItemDataTransformType.ThreeD;
		private ItemDataTransformType _scaleType = ItemDataTransformType.ThreeD;

		// work around for scale handle weirdness
		private float _scaleFactor = 1.0f;

		// control when to rotate each axis of your custom rotation handle
		private Matrix4x4? _pauseAxisX;
		private Matrix4x4? _pauseAxisY;
		private Matrix4x4? _pauseAxisZ;
		private bool _itemSelected;

		protected virtual void OnEnable()
		{
			_transform = target as Transform;

			// use default inspector. we do that when no vpe components are selected.
			var useDefault = true;

			// loop through selected objects
			foreach (var t in targets) {

				var itemTransform = t as Transform;
				if (!itemTransform) {
					continue;
				}

				var item = itemTransform.GetComponent<IMainRenderableComponent>();
				var isRenderableItem = item != null;
				var overrideTransform = isRenderableItem && item.OverrideTransform;
				_itemSelected = itemTransform.GetComponent<IMainComponent>() != null;
				useDefault = useDefault && (!_itemSelected || !overrideTransform) && itemTransform.GetComponent<IMeshComponent>() == null;

				// must be main but not the table itself
				if (isRenderableItem && item is not TableComponent) {

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

			GUILayout.Label(_itemSelected
				? "Transforms are below. Don't collapse this component."
				: "Cannot transform GameObjects on the playfield yet."
			);
		}

		private void OnSceneGUI()
		{
			if (_defaultEditor != null) {
				return;
			}

			Tools.hidden = true;

			if (_transform == null || _primaryItem == null) {
				return;
			}

			if (!_primaryItem.CanBeTransformed) {
				return;
			}

			//var dragPointEditEnabled = (_primaryItem as IDragPointsEditable)?.DragPointEditEnabled ?? false;

			//if (!dragPointEditEnabled) {
				if (_primaryItem.IsLocked) {
					HandleLockedTool();

				} else {
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
			//}
		}

		private void HandleLockedTool()
		{
			Handles.matrix = Matrix4x4.identity;
			var handlePos = _primaryItem.GetEditorPosition().TranslateToWorld();
			if (_transform.parent != null) {
				handlePos = _transform.parent.TransformPoint(handlePos);
			}
			Handles.color = Color.red;
			Handles.Button(handlePos, Quaternion.identity, HandleUtility.GetHandleSize(handlePos) * 0.25f, HandleUtility.GetHandleSize(handlePos) * 0.25f, Handles.SphereHandleCap);
			Handles.Label(handlePos + Vector3.right * (HandleUtility.GetHandleSize(handlePos) * 0.3f), "LOCKED");
		}

		private void HandleRotationTool()
		{
			var e = Event.current;
			if (e.type == EventType.MouseDown || e.type == EventType.MouseUp) {
				_pauseAxisX = _pauseAxisY = _pauseAxisZ = null;
			}
			if (_secondaryItems.Count > 0) {
				return;
			}
			var handlePos = _primaryItem.GetEditorPosition().TranslateToWorld();
			if (_transform.parent != null) {
				handlePos = _transform.parent.TransformPoint(handlePos);
			}
			var handleSize = HandleUtility.GetHandleSize(handlePos);
			var currentRot = _primaryItem.GetEditorRotation();
			switch (_primaryItem.EditorRotationType) {
				case ItemDataTransformType.OneD: {
					EditorGUI.BeginChangeCheck();
					if (_transform.parent != null) {
						Handles.matrix = Matrix4x4.TRS(handlePos, _transform.parent.transform.rotation, Vector3.one);
					}
					Handles.color = Handles.zAxisColor;
					var rot = Handles.Disc(Quaternion.AngleAxis(currentRot.x, Vector3.up), Vector3.zero, Vector3.up, handleSize, false, 10f);
					if (EditorGUI.EndChangeCheck()) {
						FinishRotate(new Vector3(rot.eulerAngles.y, 0f, 0f));
					}
					break;
				}
				case ItemDataTransformType.ThreeD: {
					EditorGUI.BeginChangeCheck();
					var baseMatrix = Handles.matrix;
					if (_transform.parent != null) {
						baseMatrix = Matrix4x4.TRS(handlePos, _transform.parent.transform.rotation, Vector3.one);
					}
					var currentRotTran = Matrix4x4.identity;
					currentRotTran *= Matrix4x4.Rotate(Quaternion.Euler(currentRot.x, 0, 0));
					currentRotTran *= Matrix4x4.Rotate(Quaternion.Euler(0, currentRot.y, 0));

					Handles.matrix = baseMatrix * /*(_pauseAxisX ?? currentRotTran) **/ Matrix4x4.Rotate(Quaternion.Euler(0, 0, -90));
					Handles.color = Handles.xAxisColor;
					var rotX = Handles.Disc(Quaternion.AngleAxis(currentRot.x, Vector3.up), Vector3.zero, Vector3.up, handleSize, true, 10f);

					Handles.matrix = baseMatrix * (_pauseAxisY ?? currentRotTran) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, 0));
					Handles.color = Handles.yAxisColor;
					var rotY = Handles.Disc(Quaternion.AngleAxis(currentRot.y, Vector3.up), Vector3.zero, Vector3.up, handleSize, true, 10f);

					Handles.matrix = baseMatrix * (_pauseAxisZ ?? currentRotTran) * Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0));
					Handles.color = Handles.zAxisColor;
					var rotZ = Handles.Disc(Quaternion.AngleAxis(currentRot.z, Vector3.up), Vector3.zero, Vector3.up, handleSize, true, 10f);

					if (EditorGUI.EndChangeCheck()) {
						// check which axis had the biggest change (they'll all change slightly due to float precision)
						// and pause that axis' local rotation so the gizmo doesn't flip out
						var xDiff = System.Math.Abs(rotX.eulerAngles.y - currentRot.x);
						var yDiff = System.Math.Abs(rotY.eulerAngles.y - currentRot.y);
						var zDiff = System.Math.Abs(rotZ.eulerAngles.y - currentRot.z);
						if (_pauseAxisX == null && xDiff > yDiff && xDiff > zDiff) {
							_pauseAxisX = currentRotTran;
						} else if (_pauseAxisY == null && yDiff > xDiff && yDiff > zDiff) {
							_pauseAxisY = currentRotTran;
						} else if (_pauseAxisZ == null && zDiff > xDiff && zDiff > yDiff) {
							_pauseAxisZ = currentRotTran;
						}

						FinishRotate(new Vector3(rotX.eulerAngles.y, rotY.eulerAngles.y, rotZ.eulerAngles.y));
					}
					break;
				}
			}
		}

		private void HandleMoveTool()
		{
			// var handlePos = _primaryItem.GetEditorPosition();
			// EditorGUI.BeginChangeCheck();
			// handlePos = HandlesUtils.HandlePosition(_transform.GetComponentInParent<PlayfieldComponent>(), handlePos, _primaryItem.EditorPositionType);
			// if (EditorGUI.EndChangeCheck()) {
			// 	FinishMove(handlePos);
			// }
		}

		private void HandleScaleTool()
		{
			var e = Event.current;
			var startScaling = e.type == EventType.MouseDown;
			var endScaling = e.type == EventType.MouseUp;
			if (startScaling || endScaling) {
				_scaleFactor = _primaryItem.GetEditorScale().x;
			}

			if (_secondaryItems.Count > 0) {
				return;
			}
			var handlePos = _primaryItem.GetEditorPosition().TranslateToWorld();
			if (_transform.parent != null) {
				handlePos = _transform.parent.TransformPoint(handlePos);
			}
			var handleRot = _transform.rotation.RotateToWorld();
			var handleScale = HandleUtility.GetHandleSize(handlePos);

			if (startScaling) {
				_primaryItem.EditorStartScaling();
			}
			if (endScaling) {
				_primaryItem.EditorEndScaling();
			}
			Handles.matrix = Matrix4x4.identity;

			switch (_primaryItem.EditorScaleType) {

				case ItemDataTransformType.OneD: {
					EditorGUI.BeginChangeCheck();
					var scale = Handles.ScaleSlider(_primaryItem.GetEditorScale().x, handlePos, _transform.right, handleRot, handleScale, 0f);
					if (EditorGUI.EndChangeCheck()) {
						FinishScale(new Vector3(scale, 0f, 0f));
					}
					break;
				}

				case ItemDataTransformType.ThreeD: {
					EditorGUI.BeginChangeCheck();
					var oldScale = _primaryItem.GetEditorScale();
					var newScale = Handles.ScaleHandle(oldScale, handlePos, handleRot, handleScale);
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
			}
		}

		/// <summary>
		/// Applies the new translation to the item.
		/// </summary>
		/// <param name="newPosition">New position in VPX space.</param>
		private void FinishMove(Vector3 newPosition)
		{
			var undoLabel = "Move " + _transform.gameObject.name;
			Undo.RecordObjects(new[]{ _transform, _primaryItem as UnityEngine.Object }, undoLabel);
			var finalPos = newPosition;
			
			_primaryItem.SetEditorPosition(finalPos);
			_primaryItem.UpdateTransforms();

			foreach (var secondary in _secondaryItems) {
				secondary.Item.UpdateTransforms();
				Undo.RecordObjects(new[]{ secondary.Item as UnityEngine.Object, secondary.Transform }, undoLabel);
				secondary.Item.SetEditorPosition(finalPos + secondary.Offset);
			}
		}

		private void FinishRotate(Vector3 newEuler)
		{
			var undoLabel = "Rotate " + _transform.gameObject.name;
			Undo.RecordObjects(new [] {_primaryItem as UnityEngine.Object, _transform }, undoLabel);
			_primaryItem.SetEditorRotation(newEuler);
			_primaryItem.UpdateTransforms();
		}

		private void FinishScale(Vector3 newScale)
		{
			var undoLabel = "Scale " + _transform.gameObject.name;
			Undo.RecordObjects(new [] {_primaryItem as UnityEngine.Object, _transform }, undoLabel);
			_primaryItem.SetEditorScale(newScale);
			_primaryItem.UpdateTransforms();
		}

		private class SecondaryItem
		{
			public Transform Transform;
			public IMainRenderableComponent Item;
			public Vector3 Offset;
		}
	}
}
