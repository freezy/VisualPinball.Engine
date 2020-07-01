using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Editor.Handle;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Inspectors
{
	public abstract class DragPointsItemInspector : ItemInspector
	{
		//Catmull Curve Handle
		CatmullCurveHandler _catmullCurveHandler = null;

		//Inspector
		private bool _foldoutControlPoints = false;
		static private Vector3 _storedControlPoint = Vector3.zero;

		//Drop down PopupMenus
		class MenuItems
		{
			public const string CONTROLPOINTS_MENUPATH = "CONTEXT/DragPointsItemInspector/ControlPoint";
			public const string CURVETRAVELLER_MENUPATH = "CONTEXT/DragPointsItemInspector/CurveTraveller";

			private static DragPointData RetrieveDragPoint(DragPointsItemInspector inspector, int controlId)
			{
				if (inspector == null) {
					return null;
				}
				return inspector.GetDragPoint(controlId);
			}

			//Drag Points
			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSlingshot", false, 1)]
			private static void SlingShot(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				var dpoint = RetrieveDragPoint(inspector, command.userData);
				if (dpoint != null) {
					inspector.PrepareUndo("Changing DragPoint IsSlingshot");
					dpoint.IsSlingshot = !dpoint.IsSlingshot;
				}
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSlingshot", true)]
			private static bool SlingshotValidate(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null || inspector.IsItemLocked()) {
					return false;
				}

				if (!inspector.HasDragPointExposition(DragPointExposition.SlingShot)) {
					Menu.SetChecked($"{CONTROLPOINTS_MENUPATH}/IsSlingshot", false);
					return false;
				}

				var dpoint = RetrieveDragPoint(inspector, command.userData);
				if (dpoint != null) {
					Menu.SetChecked($"{CONTROLPOINTS_MENUPATH}/IsSlingshot", dpoint.IsSlingshot);
				}

				return true;
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSmooth", false, 1)]
			private static void Smooth(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				var dpoint = RetrieveDragPoint(inspector, command.userData);
				if (dpoint != null) {
					inspector.PrepareUndo("Changing DragPoint IsSmooth");
					dpoint.IsSmooth = !dpoint.IsSmooth;
				}
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/IsSmooth", true)]
			private static bool SmoothValidate(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null || inspector.IsItemLocked()) {
					return false;
				}

				if (!inspector.HasDragPointExposition(DragPointExposition.Smooth)) {
					Menu.SetChecked($"{CONTROLPOINTS_MENUPATH}/IsSmooth", false);
					return false;
				}

				var dpoint = RetrieveDragPoint(inspector, command.userData);
				if (dpoint != null) {
					Menu.SetChecked($"{CONTROLPOINTS_MENUPATH}/IsSmooth", dpoint.IsSmooth);
				}

				return true;
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/Remove Point", false, 101)]
			private static void RemoveDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				if (EditorUtility.DisplayDialog("DragPoint Removal", "Are you sure you want to remove this Dragpoint ?", "Yes", "No")) {
					inspector.RemoveDragPoint(command.userData);
				}
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/Remove Point", true)]
			private static bool RemoveDPValidate(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null || inspector.IsItemLocked()) {
					return false;
				}

				return true;
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/Copy Point", false, 301)]
			private static void CopyDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.CopyDragPoint(command.userData);
			}

			[MenuItem(CONTROLPOINTS_MENUPATH + "/Paste Point", false, 302)]
			private static void PasteDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.PasteDragPoint(command.userData);
			}

			//Curve Traveller
			[MenuItem(CURVETRAVELLER_MENUPATH + "/Add Point", false, 1)]
			private static void AddDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.AddDragPointOnTraveller();
			}

			[MenuItem(CURVETRAVELLER_MENUPATH + "/Flip Drag Points/X", false, 101)]
			[MenuItem(CONTROLPOINTS_MENUPATH + "/Flip Drag Points/X", false, 201)]
			private static void FlipXDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.FlipDragPoints(FlipAxis.X);
			}

			[MenuItem(CURVETRAVELLER_MENUPATH + "/Flip Drag Points/Y", false, 102)]
			[MenuItem(CONTROLPOINTS_MENUPATH + "/Flip Drag Points/Y", false, 202)]
			private static void FlipYDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.FlipDragPoints(FlipAxis.Y);
			}

			[MenuItem(CURVETRAVELLER_MENUPATH + "/Flip Drag Points/Z", false, 103)]
			[MenuItem(CONTROLPOINTS_MENUPATH + "/Flip Drag Points/Z", false, 203)]
			private static void FlipZDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.FlipDragPoints(FlipAxis.Z);
			}
		}


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

		protected void OnUndoRedoPerformed()
		{
			RemapControlPoints();
			if (target is IEditableItemBehavior item) {
				item.MeshDirty = true;
			}
		}

		protected UndoPropertyModification[] OnUndoRedoModifications(UndoPropertyModification[] modifs)
		{
			return modifs;
		}

		public DragPointData GetDragPoint(int controlId)
		{
			if (_catmullCurveHandler != null) {
				return _catmullCurveHandler.GetDragPoint(controlId);
			}
			return null;
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
							EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), MenuItems.CONTROLPOINTS_MENUPATH, command);
							Event.current.Use();
						} else if (HandleUtility.nearestControl == _catmullCurveHandler.CurveTravellerControlId) {
							MenuCommand command = new MenuCommand(this, 0);
							EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), MenuItems.CURVETRAVELLER_MENUPATH, command);
							Event.current.Use();
						}
					}
				}
				break;
			}
		}
	}
}
