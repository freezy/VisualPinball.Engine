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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public abstract class DragPointsItemInspector<TData, TMainAuthoring>
		: ItemMainInspector<TData, TMainAuthoring>,
			IDragPointsItemInspector, IDragPointsEditable
		where TData : ItemData
		where TMainAuthoring : ItemMainRenderableComponent<TData>
	{
		/// <summary>
		/// Catmull Curve Handler
		/// </summary>
		public DragPointsHandler DragPointsHandler { get; private set; }

		private bool EditingEnabled => Selection.count == 1 && target is MonoBehaviour mb && Selection.activeGameObject == mb.gameObject;

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

		private IItemMainRenderableComponent _renderable;
		private IDragPointsComponent _dragPointsComponent;

		public void RebuildMeshes()
		{
			_renderable.RebuildMeshes();
			WalkChildren(PlayfieldComponent.transform, UpdateSurfaceReferences);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			_dragPointsComponent = MainComponent as IDragPointsComponent;
			_renderable = target as IItemMainRenderableComponent;
			DragPointsHandler = new DragPointsHandler(target, this);
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
			return !(target is IItemMainRenderableComponent editable) || editable.IsLocked;
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
			return editable.DragPointExposition.Contains(exposure);
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

			if (editable.HandleType != ItemDataTransformType.ThreeD && flipAxis == FlipAxis.Z) {
				return;
			}

			PrepareUndo($"Flip-{flipAxis} Drag Points");
			DragPointsHandler.FlipDragPoints(flipAxis);
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

		/// <summary>
		/// Sets an UNDO point before the next operation.
		/// </summary>
		/// <param name="message">Message to appear in the UNDO menu</param>
		public void PrepareUndo(string message)
		{
			Undo.RecordObjects(new []{this, target}, message);
		}

		public override void OnInspectorGUI()
		{
			if (!(target is IItemMainRenderableComponent editable)) {
				base.OnInspectorGUI();
				return;
			}

			// var editButtonText = dragPointEditable.DragPointEditEnabled ? "Stop Editing Drag Points" : "Edit Drag Points";
			// if (GUILayout.Button(editButtonText)) {
			// 	dragPointEditable.DragPointEditEnabled = !dragPointEditable.DragPointEditEnabled;
			// 	SceneView.RepaintAll();
			// }

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
			base.OnInspectorGUI();
		}

		private void UpdateDragPointsLock()
		{
			if (target is IItemMainRenderableComponent editable && DragPointsHandler.UpdateDragPointsLock(editable.IsLocked)) {
				HandleUtility.Repaint();
			}
		}

		private void OnDragPointPositionChange(Vector3 newPos)
		{
			RebuildMeshes();
			PrepareUndo("Change Drag Point Position");
		}

		private void OnUndoRedoPerformed()
		{
			RemapControlPoints();
			WalkChildren(PlayfieldComponent.transform, UpdateSurfaceReferences);
		}

		protected virtual void OnSceneGUI()
		{
			var editable = target as IItemMainRenderableComponent;
			var bh = target as Behaviour;

			if (editable == null || bh == null || !EditingEnabled) {
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
					Event.current.Use();
				}
			}
		}

		public DragPointData[] DragPoints {
			get => _dragPointsComponent.DragPoints;
			set {
				_dragPointsComponent.DragPoints = value;
				EditorUtility.SetDirty(MainComponent.gameObject);
				PrefabUtility.RecordPrefabInstancePropertyModifications(MainComponent);
				EditorSceneManager.MarkSceneDirty(MainComponent.gameObject.scene);
			}
		}
		public abstract Vector3 EditableOffset { get; }
		public abstract Vector3 GetDragPointOffset(float ratio);
		public abstract bool PointsAreLooping { get; }
		public abstract IEnumerable<DragPointExposure> DragPointExposition { get; }
		public abstract ItemDataTransformType HandleType { get; }
	}
}
