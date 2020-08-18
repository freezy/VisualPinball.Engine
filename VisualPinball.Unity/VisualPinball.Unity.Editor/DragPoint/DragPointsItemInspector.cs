using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	public abstract class DragPointsItemInspector : ItemInspector
	{
		/// <summary>
		/// Catmull Curve Handler
		/// </summary>
		public DragPointsHandler DragPointsHandler { get; private set; }

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

		protected override void OnEnable()
		{
			base.OnEnable();
			DragPointsHandler = new DragPointsHandler(target);
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
		}

		protected override void OnDisable()
		{
			base.OnDisable();
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
				PrepareUndo($"Paste drag point {controlId}");
				dp.Center = _storedControlPoint.ToVertex3D();
			}
		}

		/// <summary>
		/// Returns true if the game item is locked.
		/// </summary>
		/// <returns>True if game item is locked, false otherwise.</returns>
		public bool IsItemLocked()
		{
			return !(target is IEditableItemAuthoring editable) || editable.IsLocked;
		}

		/// <summary>
		/// Returns whether this game item has a given drag point exposure.
		/// </summary>
		/// <param name="exposure">Exposure to check</param>
		/// <returns>True if exposed, false otherwise.</returns>
		public bool HasDragPointExposure(DragPointExposure exposure)
		{
			if (!(target is IDragPointsEditable editable)) {
				return false;
			}
			return editable.GetDragPointExposition().Contains(exposure);
		}

		/// <summary>
		/// Flips all drag points on a given axis.
		/// </summary>
		/// <param name="flipAxis">Axis to flip on</param>
		public void FlipDragPoints(FlipAxis flipAxis)
		{
			if (!(target is IDragPointsEditable editable)) {
				return;
			}

			if (editable.GetHandleType() != ItemDataTransformType.ThreeD && flipAxis == FlipAxis.Z) {
				return;
			}

			PrepareUndo($"Flip drag points on {flipAxis} axis");
			DragPointsHandler.FlipDragPoints(flipAxis);
		}

		/// <summary>
		/// Copies drag point data to the control points used in the editor.
		/// </summary>
		public void RemapControlPoints()
		{
			var rebuilt = DragPointsHandler.RemapControlPoints();
			if (rebuilt && target is IEditableItemAuthoring editable) {
				editable.MeshDirty = true;
			}
		}

		/// <summary>
		/// Adds a new drag point at the traveller's current position.
		/// </summary>
		public void AddDragPointOnTraveller()
		{
			PrepareUndo($"Add drag point at position {DragPointsHandler.CurveTravellerPosition}");
			DragPointsHandler.AddDragPointOnTraveller();
		}

		/// <summary>
		/// Removes a drag point of a given control ID.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point to remove.</param>
		public void RemoveDragPoint(int controlId)
		{
			PrepareUndo($"Remove drag point at ID {controlId}");
			DragPointsHandler.RemoveDragPoint(controlId);
		}

		/// <summary>
		/// Sets an UNDO point before the next operation.
		/// </summary>
		/// <param name="message">Message to appear in the UNDO menu</param>
		public void PrepareUndo(string message)
		{
			if (target == null) {
				return;
			}

			// Set MeshDirty to true there so it'll trigger again after Undo
			var recordObjs = new List<Object>();
			if (target is IEditableItemAuthoring editable) {
				editable.MeshDirty = true;
				recordObjs.Add(this);
			}
			recordObjs.Add(target);
			Undo.RecordObjects(recordObjs.ToArray(), $"Item {target} : {message}");
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var editable = target as IEditableItemAuthoring;
			var dragPointEditable = target as IDragPointsEditable;
			if (editable == null || dragPointEditable == null) {
				return;
			}

			var enabledString = dragPointEditable.DragPointEditEnabled ? $"(ON), Stored Coordinates {_storedControlPoint}" : "(OFF)";
			if (GUILayout.Button($"Edit Drag Points {enabledString}")) {
				dragPointEditable.DragPointEditEnabled = !dragPointEditable.DragPointEditEnabled;
				SceneView.RepaintAll();
			}

			if (dragPointEditable.DragPointEditEnabled) {
				if (editable.IsLocked) {
					EditorGUILayout.LabelField("Drag Points are Locked");
				} else {
					_foldoutControlPoints = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutControlPoints, "Drag Points");
					if (_foldoutControlPoints) {
						EditorGUI.indentLevel++;
						for (var i = 0; i < DragPointsHandler.ControlPoints.Count; ++i) {
							var controlPoint = DragPointsHandler.ControlPoints[i];
							EditorGUILayout.BeginHorizontal();
							EditorGUILayout.LabelField($"#{i} ({controlPoint.DragPoint.Center.X},{controlPoint.DragPoint.Center.Y},{controlPoint.DragPoint.Center.Z})");
							if (GUILayout.Button("Copy")) {
								CopyDragPoint(controlPoint.ControlId);
							}
							else if (GUILayout.Button("Paste")) {
								PasteDragPoint(controlPoint.ControlId);
							}
							EditorGUILayout.EndHorizontal();
							EditorGUI.indentLevel++;
							if (HasDragPointExposure(DragPointExposure.SlingShot)) {
								ItemDataField("Slingshot", ref controlPoint.DragPoint.IsSlingshot);
							}
							if (HasDragPointExposure(DragPointExposure.Smooth)) {
								ItemDataField("Smooth", ref controlPoint.DragPoint.IsSmooth);
							}
							if (HasDragPointExposure(DragPointExposure.Texture)) {
								ItemDataField("Has AutoTexture", ref controlPoint.DragPoint.HasAutoTexture);
								ItemDataSlider("Texture Coord", ref controlPoint.DragPoint.TextureCoord, 0.0f, 1.0f);
							}
							EditorGUI.indentLevel--;
						}
						EditorGUI.indentLevel--;
					}
					EditorGUILayout.EndFoldoutHeaderGroup();
				}
			}
		}

		private void UpdateDragPointsLock()
		{
			if (target is IEditableItemAuthoring editable && DragPointsHandler.UpdateDragPointsLock(editable.IsLocked)) {
				HandleUtility.Repaint();
			}
		}

		private void OnDragPointPositionChange(Vector3 newPos)
		{
			PrepareUndo($"[{target?.name}] Change drag point position for {DragPointsHandler.SelectedControlPoints.Count} control points.");
		}

		private void OnUndoRedoPerformed()
		{
			RemapControlPoints();
			if (target is IEditableItemAuthoring item) {
				item.MeshDirty = true;
			}
		}

		protected virtual void OnSceneGUI()
		{
			var editable = target as IEditableItemAuthoring;
			var dragPointEditable = target as IDragPointsEditable;
			var bh = target as Behaviour;

			if (editable == null || bh == null || dragPointEditable == null || !dragPointEditable.DragPointEditEnabled) {
				return;
			}

			RemapControlPoints();
			UpdateDragPointsLock();

			DragPointsHandler.OnSceneGUI(Event.current, OnDragPointPositionChange);

			// right mouse button clicked?
			if (Event.current.type == EventType.MouseDown && Event.current.button == 1) {
				var nearestControlPoint = DragPointsHandler.ControlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);

				if (nearestControlPoint != null) {
					var command = new MenuCommand(this, nearestControlPoint.ControlId);
					EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), DragPointMenuItems.ControlPointsMenuPath, command);
					Event.current.Use();
				} else if (DragPointsHandler.CurveTravellerVisible && HandleUtility.nearestControl == DragPointsHandler.CurveTravellerControlId) {
					var command = new MenuCommand(this, 0);
					EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), DragPointMenuItems.CurveTravellerMenuPath, command);
				}
			}
		}
	}
}
