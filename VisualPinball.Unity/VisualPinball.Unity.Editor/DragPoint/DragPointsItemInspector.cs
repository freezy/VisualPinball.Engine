using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Editor.Inspectors;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.DragPoint
{
	public abstract class DragPointsItemInspector : ItemInspector
	{
		//Catmull Curve Handle
		private CatmullCurveHandler _catmullCurveHandler;

		//Inspector
		private bool _foldoutControlPoints;
		private static Vector3 _storedControlPoint = Vector3.zero;

		protected override void OnEnable()
		{
			base.OnEnable();

			_catmullCurveHandler = new CatmullCurveHandler(target) {
				CurveWidth = 10.0f,
				CurveColor = UnityEngine.Color.blue,
				CurveSlingShotColor = UnityEngine.Color.red,
				ControlPointsSizeRatio = 1.0f,
				CurveTravellerSizeRatio = 0.75f
			};

			Undo.undoRedoPerformed += OnUndoRedoPerformed;
			Undo.postprocessModifications += OnUndoRedoModifications;
		}

		protected virtual void OnDisable()
		{
			_catmullCurveHandler = null;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			Undo.postprocessModifications -= OnUndoRedoModifications;
		}

		private void OnUndoRedoPerformed()
		{
			RemapControlPoints();
			if (target is IEditableItemBehavior item) {
				item.MeshDirty = true;
			}
		}

		private static UndoPropertyModification[] OnUndoRedoModifications(UndoPropertyModification[] modifs)
		{
			return modifs;
		}

		public DragPointData GetDragPoint(int controlId)
		{
			return _catmullCurveHandler?.GetDragPoint(controlId);
		}


		public void CopyDragPoint(int controlId)
		{
			var dp = GetDragPoint(controlId);
			if (dp != null) {
				_storedControlPoint = dp.Vertex.ToUnityVector3();
			}
		}
		public void PasteDragPoint(int controlId)
		{
			var dp = GetDragPoint(controlId);
			if (dp != null) {
				PrepareUndo($"Paste Drag point {controlId}");
				dp.Vertex = _storedControlPoint.ToVertex3D();
			}
		}

		public bool IsItemLocked()
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			if (editable == null) {
				return true;
			}
			return editable.IsLocked;
		}

		public bool HasDragPointExposition(DragPointExposition dpExpo)
		{
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			if (dpeditable == null) {
				return false;
			}
			return dpeditable.GetDragPointExposition().Contains(dpExpo);
		}

		public void FlipDragPoints(FlipAxis flipAxe)
		{
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			if (dpeditable == null || (dpeditable.GetHandleType() != ItemDataTransformType.ThreeD && flipAxe == FlipAxis.Z)) {
				return;
			}

			PrepareUndo($"Flipping Drag Points on axe {flipAxe}");

			_catmullCurveHandler.FlipDragPoints(flipAxe);
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			IEditableItemBehavior editable = target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			if (editable == null || dpeditable == null) {
				return;
			}

			string enabledString = dpeditable.DragPointEditEnabled ? $"(ON), Stored Coordinates {_storedControlPoint}" : "(OFF)";
			if (GUILayout.Button($"Edit Drag Points {enabledString}")) {
				dpeditable.DragPointEditEnabled = !dpeditable.DragPointEditEnabled;
				SceneView.RepaintAll();
			}

			if (dpeditable.DragPointEditEnabled) {
				if (editable.IsLocked) {
					EditorGUILayout.LabelField("Drag Points are Locked");
				} else {
					if (_foldoutControlPoints = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutControlPoints, "Drag Points")) {
						EditorGUI.indentLevel++;
						for (int i = 0; i < _catmullCurveHandler.ControlPoints.Count; ++i) {
							var cpoint = _catmullCurveHandler.ControlPoints[i];
							EditorGUILayout.BeginHorizontal("DragpointBar");
							EditorGUILayout.LabelField($"Dragpoint [{i}] : ({cpoint.DragPoint.Vertex.X},{cpoint.DragPoint.Vertex.Y},{cpoint.DragPoint.Vertex.Z})");
							if (GUILayout.Button("Copy")) {
								CopyDragPoint(cpoint.ControlId);
							}
							else if (GUILayout.Button("Paste")) {
								PasteDragPoint(cpoint.ControlId);
							}
							EditorGUILayout.EndHorizontal();
							EditorGUI.indentLevel++;
							if (HasDragPointExposition(DragPointExposition.SlingShot)) {
								ItemDataField("Slingshot", ref cpoint.DragPoint.IsSlingshot);
							}
							if (HasDragPointExposition(DragPointExposition.Smooth)) {
								ItemDataField("Smooth", ref cpoint.DragPoint.IsSmooth);
							}
							if (HasDragPointExposition(DragPointExposition.Texture)) {
								ItemDataField("Has AutoTexture", ref cpoint.DragPoint.HasAutoTexture);
								ItemDataSlider("Texture Coord", ref cpoint.DragPoint.TextureCoord, 0.0f, 1.0f);
							}
							EditorGUI.indentLevel--;
						}
						EditorGUI.indentLevel--;
					}
					EditorGUILayout.EndFoldoutHeaderGroup();
				}
			}
		}

		public void RemapControlPoints()
		{
			var rebuilt = _catmullCurveHandler.RemapControlPoints();
			if (rebuilt && target is IEditableItemBehavior editable) {
				editable.MeshDirty = true;
			}
		}

		public void PrepareUndo(string message)
		{
			if (target == null) {
				return;
			}

			//Set Meshdirty to true there so it'll trigger again after Undo
			List<Object> recordObjs = new List<Object>();
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			if (editable != null) {
				editable.MeshDirty = true;
				recordObjs.Add(this);
			}
			recordObjs.Add(target);
			Undo.RecordObjects(recordObjs.ToArray(), $"Item {target} : {message}");
		}

		public void AddDragPointOnTraveller()
		{
			PrepareUndo($"Adding Drag Point at position {_catmullCurveHandler.CurveTravellerPosition}");
			_catmullCurveHandler.AddDragPointOnTraveller();
		}

		public void RemoveDragPoint(int controlId)
		{
			PrepareUndo($"Remove Drag Point at Control Point ID : {controlId}");
			_catmullCurveHandler.RemoveDragPoint(controlId);
		}

		private void UpdateDragPointsLock()
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			if (_catmullCurveHandler.UpdateDragPointsLock(editable.IsLocked)) {
				SceneView.RepaintAll();
			}
		}

		private void OnDragPointPositionChange(Vector3 newPos)
		{
			PrepareUndo($"[{target?.name}] Change DragPoint Position for {_catmullCurveHandler.SelectedControlPoints.Count} Control points.");
		}

		protected virtual void OnSceneGUI()
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || dpeditable == null || !dpeditable.DragPointEditEnabled) {
				return;
			}

			RemapControlPoints();
			UpdateDragPointsLock();

			_catmullCurveHandler.OnSceneGUI(Event.current, editable.IsLocked, OnDragPointPositionChange);

			switch (Event.current.type) {
				case EventType.MouseDown: {
					if (Event.current.button == 1) {
						var nearCP = _catmullCurveHandler.ControlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);
						if (nearCP != null) {
							MenuCommand command = new MenuCommand(this, nearCP.ControlId);
							EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), DragPointMenuItems.ControlPointsMenuPath, command);
							Event.current.Use();
						} else if (HandleUtility.nearestControl == _catmullCurveHandler.CurveTravellerControlId) {
							MenuCommand command = new MenuCommand(this, 0);
							EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), DragPointMenuItems.CurveTravellerMenuPath, command);
							Event.current.Use();
						}
					}
				}
				break;
			}
		}
	}
}
