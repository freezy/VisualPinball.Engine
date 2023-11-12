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
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	public class DragPointsInspectorHelper
	{
		/// <summary>
		/// Catmull Curve Handler
		/// </summary>
		public DragPointsHandler DragPointsHandler { get; private set; }

		private bool EditingEnabled => _dragPointsInspector.DragPointsActive;

		/// <summary>
		/// If true, a list of the drag points is displayed in the inspector.
		/// </summary>
		private bool _foldoutControlPoints;

		/// <summary>
		/// stored vector during the copy/paste process
		/// </summary>
		///
		/// <remarks>
		/// It is globally stored so it can be copy/pasted on different items
		/// </remarks>
		private static Vector3 _storedControlPoint = Vector3.zero;

		private readonly MonoBehaviour _mb;
		private readonly IMainRenderableComponent _mainComponent;
		private readonly IDragPointsInspector _dragPointsInspector;
		private readonly PlayfieldComponent _playfieldComponent;

		public DragPointsInspectorHelper(IMainRenderableComponent mainComponent, IDragPointsInspector dragPointsInspector)
		{
			Assert.IsNotNull(mainComponent);
			_mb = mainComponent as MonoBehaviour;
			_mainComponent = mainComponent;
			_dragPointsInspector = dragPointsInspector;
			_playfieldComponent = mainComponent.gameObject.GetComponentInParent<PlayfieldComponent>();
			DragPointsHandler = new DragPointsHandler(mainComponent, _dragPointsInspector);
		}

		public void RebuildMeshes()
		{
			_mainComponent.RebuildMeshes();
			if (_playfieldComponent) {
				WalkChildren(_playfieldComponent.transform, UpdateSurfaceReferences);
			} else {
				Debug.LogWarning($"{_mainComponent.name} doesn't seem to have a playfield parent.");
			}
		}

		public void OnEnable()
		{
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
		}

		public void OnDisable()
		{
			DragPointsHandler = null;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		}

		/// <summary>
		/// Returns a reference to the drag point data for a given control ID.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point</param>
		/// <returns>Drag point data or null if no linked data.</returns>
		public DragPointData GetDragPoint(int controlId)
		{
			return DragPointsHandler?.GetDragPoint(controlId);
		}

		/// <summary>
		/// Copies the position of a drag point.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point</param>
		public void CopyDragPoint(int controlId)
		{
			var dp = GetDragPoint(controlId);
			if (dp != null) {
				_storedControlPoint = dp.Center.ToUnityVector3();
			}
		}

		/// <summary>
		/// Sets the position of a previously copied drag point to another drag point.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point to which the new position is applied.</param>
		public void PasteDragPoint(int controlId)
		{
			var dp = GetDragPoint(controlId);
			if (dp != null) {
				PrepareUndo($"Paste Drag Point {controlId}");
				dp.Center = _storedControlPoint.ToVertex3D();
				RebuildMeshes();
			}
		}

		/// <summary>
		/// Returns true if the game item is locked.
		/// </summary>
		/// <returns>True if game item is locked, false otherwise.</returns>
		public bool IsItemLocked()
		{
			return _mainComponent.IsLocked;
		}

		/// <summary>
		/// Returns whether this game item has a given drag point exposure.
		/// </summary>
		/// <param name="exposure">Exposure to check</param>
		/// <returns>True if exposed, false otherwise.</returns>
		public bool HasDragPointExposure(DragPointExposure exposure)
		{
			return _dragPointsInspector.DragPointExposition.Contains(exposure);
		}

		/// <summary>
		/// Flips all drag points on a given axis.
		/// </summary>
		/// <param name="flipAxis">Axis to flip on</param>
		public void FlipDragPoints(FlipAxis flipAxis)
		{
			if (_dragPointsInspector.HandleType != ItemDataTransformType.ThreeD && flipAxis == FlipAxis.Z) {
				return;
			}

			PrepareUndo($"Flip-{flipAxis} Drag Points");
			DragPointsHandler.FlipDragPoints(flipAxis);
			if (_dragPointsInspector.PointsAreLooping) {
				DragPointsHandler.ReverseDragPoints(); // keep counter-clockwise orientation
			}
			RebuildMeshes();
		}

		/// <summary>
		/// Copies drag point data to the control points used in the editor.
		/// </summary>
		public void RemapControlPoints()
		{
			var rebuilt = DragPointsHandler.RemapControlPoints();
			if (rebuilt) {
				RebuildMeshes();
			}
		}

		/// <summary>
		/// Adds a new drag point at the traveller's current position.
		/// </summary>
		public void AddDragPointOnTraveller()
		{
			PrepareUndo($"Add drag point at position {DragPointsHandler.CurveTravellerPosition}");
			DragPointsHandler.AddDragPointOnTraveller();
			RebuildMeshes();
		}

		/// <summary>
		/// Removes a drag point of a given control ID.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point to remove.</param>
		public void RemoveDragPoint(int controlId)
		{
			PrepareUndo("Remove Drag Point");
			DragPointsHandler.RemoveDragPoint(controlId);
			RebuildMeshes();
		}

		public void Reverse()
		{
			PrepareUndo("Reverse Drag Points");
			DragPointsHandler.ReverseDragPoints();
			RebuildMeshes();
		}

		/// <summary>
		/// Sets an UNDO point before the next operation.
		/// </summary>
		/// <param name="message">Message to appear in the UNDO menu</param>
		public void PrepareUndo(string message)
		{
			Undo.RecordObject(_mb, message);
		}

		public void OnInspectorGUI(ItemInspector inspector)
		{
			if (_mainComponent.IsLocked) {
				EditorGUILayout.LabelField("Drag Points are Locked");
				return;
			}

			GUILayout.Space(10);
			if (GUILayout.Button("Center Origin")) {
				DragPointsHandler.CenterPivot();
			}

			_foldoutControlPoints = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutControlPoints, "Drag Points");
			if (_foldoutControlPoints) {
				EditorGUI.indentLevel++;
				for (var i = 0; i < DragPointsHandler.ControlPoints.Count; ++i) {
					var controlPoint = DragPointsHandler.ControlPoints[i];
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField($"Drag Point #{i}");
					if (GUILayout.Button("Copy")) {
						CopyDragPoint(controlPoint.ControlId);
					}
					else if (GUILayout.Button("Paste")) {
						PasteDragPoint(controlPoint.ControlId);
					}
					EditorGUILayout.EndHorizontal();
					EditorGUI.indentLevel++;
					EditorGUI.BeginChangeCheck();
					if (_dragPointsInspector.HandleType == ItemDataTransformType.TwoD) {
						var pos = EditorGUILayout.Vector2Field("Position", controlPoint.DragPoint.Center.ToUnityVector2());
						if (EditorGUI.EndChangeCheck()) {
							controlPoint.DragPoint.Center.X = pos.x;
							controlPoint.DragPoint.Center.Y = pos.y;
							RebuildMeshes();
						}
					} else {
						var pos = EditorGUILayout.Vector3Field("Position", controlPoint.DragPoint.Center.ToUnityVector3());
						if (EditorGUI.EndChangeCheck()) {
							controlPoint.DragPoint.Center = pos.ToVertex3D();
							RebuildMeshes();
						}
					}

					if (HasDragPointExposure(DragPointExposure.SlingShot)) {
						inspector.ItemDataField("Slingshot", ref controlPoint.DragPoint.IsSlingshot);
					}
					if (HasDragPointExposure(DragPointExposure.Smooth)) {
						inspector.ItemDataField("Smooth", ref controlPoint.DragPoint.IsSmooth);
					}
					if (HasDragPointExposure(DragPointExposure.Texture)) {
						inspector.ItemDataField("Has AutoTexture", ref controlPoint.DragPoint.HasAutoTexture);
						inspector.ItemDataSlider("Texture Coord", ref controlPoint.DragPoint.TextureCoord, 0.0f, 1.0f);
					}
					EditorGUI.indentLevel--;
				}
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
		}

		private void UpdateDragPointsLock()
		{
			if (DragPointsHandler.UpdateDragPointsLock(_mainComponent.IsLocked)) {
				HandleUtility.Repaint();
			}
		}

		private void OnDragPointPositionChange()
		{
			RebuildMeshes();
			PrepareUndo("Change Drag Point Position");
		}

		private void OnUndoRedoPerformed()
		{
			RemapControlPoints();
			WalkChildren(_playfieldComponent.transform, UpdateSurfaceReferences);
		}

		public void OnSceneGUI(ItemInspector inspector)
		{
			if (!EditingEnabled) {
				return;
			}

			RemapControlPoints();
			UpdateDragPointsLock();

			DragPointsHandler.OnSceneGUI(Event.current, OnDragPointPositionChange);

			// right mouse button clicked?
			Handles.matrix = Matrix4x4.identity;
			if (Event.current.type == EventType.MouseDown && Event.current.button == 1) {
				var nearestControlPoint = DragPointsHandler.ControlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);

				if (nearestControlPoint != null) {
					var command = new MenuCommand(inspector, nearestControlPoint.ControlId);
					EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), DragPointMenuItems.ControlPointsMenuPath, command);
					Event.current.Use();
				} else if (DragPointsHandler.CurveTravellerVisible && HandleUtility.nearestControl == DragPointsHandler.CurveTravellerControlId) {
					var command = new MenuCommand(inspector, 0);
					EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), DragPointMenuItems.CurveTravellerMenuPath, command);
					Event.current.Use();
				}
			}
		}

		private static void WalkChildren(IEnumerable node, Action<Transform> action)
		{
			foreach (Transform childTransform in node) {
				action(childTransform);
				WalkChildren(childTransform, action);
			}
		}

		private void UpdateSurfaceReferences(Transform obj)
		{
			var surfaceComponent = obj.gameObject.GetComponent<IOnSurfaceComponent>();
			if (surfaceComponent != null && surfaceComponent.Surface == _mainComponent) {
				surfaceComponent.OnSurfaceUpdated();
			}
		}

	}
}
