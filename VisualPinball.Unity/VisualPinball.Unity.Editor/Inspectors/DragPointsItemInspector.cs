using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Inspectors
{
	public abstract class DragPointsItemInspector : ItemInspector
	{
		public enum FlipAxes
		{
			X,
			Y,
			Z,
		}

		// A ControlPoint will map on each DragPointData exposed from the IDragPointEditable
		// The Ispector will manage adding/removing control point & will synch DragPointdata array on the IDragPointEditable side.
		// ControlPoint will also handle the ControlId used by Unity's Handles system.
		// Controlpoint will keep the curve segment points starting from it
		public class ControlPoint
		{
			public static float ScreenRadius = 0.25f;
			public DragPointData DragPoint;
			public Vector3 WorldPos = Vector3.zero;
			public Vector3 ScrPos = Vector3.zero;
			public bool IsSelected = false;
			public readonly int ControlId = 0;
			public readonly int Index = -1;
			public readonly float IndexRatio = 0.0f;
			public List<Vector3> pathPoints = new List<Vector3>();

			public ControlPoint(DragPointData dp, int controlID, int idx, float idxratio)
			{
				DragPoint = dp;
				ControlId = controlID;
				Index = idx;
				IndexRatio = idxratio;
			}
		}

		//Control points storing & rendering
		private List<ControlPoint> _controlPoints = new List<ControlPoint>();

		//DragPoints flipping
		private Vector3 _flipAxes = Vector3.zero;

		//Curve points
		private List<Vector3> _allPathPoints = new List<Vector3>();

		//Control points position Handle
		private List<ControlPoint> _selectedCP = new List<ControlPoint>();
		private int _positionHandleControlId = 0;
		private Vector3 _positionHandlePosition = Vector3.zero;

		//Curve Traveller 
		public static float CurveTravellerSizeRatio = 0.75f;
		private int _curveTravellerControlId = 0;
		private Vector3 _curveTravellerPosition = Vector3.zero;
		private bool _curveTravellerVisible = false;
		private int _curveTravellerControlPointIdx = -1;

		//Inspector
		private bool _foldoutControlPoints = false;

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

				inspector.FlipDragPoints(DragPointsItemInspector.FlipAxes.X);
			}

			[MenuItem(CURVETRAVELLER_MENUPATH + "/Flip Drag Points/Y", false, 102)]
			[MenuItem(CONTROLPOINTS_MENUPATH + "/Flip Drag Points/Y", false, 202)]
			private static void FlipYDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.FlipDragPoints(DragPointsItemInspector.FlipAxes.Y);
			}

			[MenuItem(CURVETRAVELLER_MENUPATH + "/Flip Drag Points/Z", false, 103)]
			[MenuItem(CONTROLPOINTS_MENUPATH + "/Flip Drag Points/Z", false, 203)]
			private static void FlipZDP(MenuCommand command)
			{
				DragPointsItemInspector inspector = command.context as DragPointsItemInspector;
				if (inspector == null) {
					return;
				}

				inspector.FlipDragPoints(DragPointsItemInspector.FlipAxes.Z);
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

		public DragPointData GetDragPoint(int controlId)
		{
			var cpoint = GetControlPoint(controlId);
			if (cpoint != null) {
				return cpoint.DragPoint;
			}
			return null;
		}

		public ControlPoint GetControlPoint(int controlId)
		{
			return _controlPoints.Find(cp => cp.ControlId == controlId);
		}

		public void FlipDragPoints(FlipAxes flipAxe)
		{
			PrepareUndo($"Flipping Drag Points on axe {flipAxe}");

			float axe = (flipAxe == FlipAxes.X) ? _flipAxes.x : (flipAxe == FlipAxes.Y) ? _flipAxes.y : _flipAxes.z;

			foreach (var cpoint in _controlPoints) {

				float coord = (flipAxe == FlipAxes.X) ? cpoint.DragPoint.Vertex.X : (flipAxe == FlipAxes.Y) ? cpoint.DragPoint.Vertex.Y : cpoint.DragPoint.Vertex.Z;
				coord = axe + (axe - coord);
				if (flipAxe == FlipAxes.X) {
					cpoint.DragPoint.Vertex.X = coord;
				} else if (flipAxe == FlipAxes.Y) {
					cpoint.DragPoint.Vertex.Y = coord;
				} else {
					cpoint.DragPoint.Vertex.Z = coord;
				}
			}
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			IEditableItemBehavior editable = target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			if (editable == null || dpeditable == null) {
				return;
			}

			string enabledString = dpeditable.DragPointEditEnabled ? "(ON)" : "(OFF)";
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
						for (int i = 0; i < _controlPoints.Count; ++i) {
							var cpoint = _controlPoints[i];
							EditorGUILayout.LabelField($"Dragpoint [{i}] : ({cpoint.DragPoint.Vertex.X},{cpoint.DragPoint.Vertex.Y},{cpoint.DragPoint.Vertex.Z})");
							EditorGUI.indentLevel++;
							if (HasDragPointExposition(DragPointExposition.SlingShot)) {
								DataFieldUtils.ItemDataField("Slingshot", ref cpoint.DragPoint.IsSlingshot, FinishDragPointEdit);
							}
							if (HasDragPointExposition(DragPointExposition.Smooth)) {
								DataFieldUtils.ItemDataField("Smooth", ref cpoint.DragPoint.IsSmooth, FinishDragPointEdit);
							}
							if (HasDragPointExposition(DragPointExposition.Texture)) {
								DataFieldUtils.ItemDataField("Has AutoTexture", ref cpoint.DragPoint.HasAutoTexture, FinishDragPointEdit);
								DataFieldUtils.ItemDataSlider("Texture Coord", ref cpoint.DragPoint.TextureCoord, 0.0f, 1.0f, FinishDragPointEdit);
							}
							EditorGUI.indentLevel--;
						}
						EditorGUI.indentLevel--;
					}
					EditorGUILayout.EndFoldoutHeaderGroup();
				}
			}
		}

		protected void RebuildControlPoints(IDragPointsEditable dpEditable)
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			if (editable != null) {
				editable.MeshDirty = true;
			}

			_controlPoints.Clear();

			for (int i = 0; i < dpEditable.GetDragPoints().Length; ++i) {
				ControlPoint cp = new ControlPoint(dpEditable.GetDragPoints()[i], GUIUtility.GetControlID(FocusType.Passive), i, (float)i / dpEditable.GetDragPoints().Length);
				_controlPoints.Add(cp);
			}

			_positionHandleControlId = GUIUtility.GetControlID(FocusType.Passive);
			_curveTravellerControlId = GUIUtility.GetControlID(FocusType.Passive);
		}

		public void RemapControlPoints(IDragPointsEditable dpEditable)
		{
			if (_controlPoints.Count != dpEditable.GetDragPoints().Length) {
				RebuildControlPoints(dpEditable);
			} else {
				for (int i = 0; i < dpEditable.GetDragPoints().Length; ++i) {
					_controlPoints[i].DragPoint = dpEditable.GetDragPoints()[i];
				}
			}
		}

		protected bool FinishDragPointEdit(string label, out string message, List<UnityEngine.Object> recordObjs, params (string, object)[] pList)
		{
			if (target == null) {
				message = "";
				return false;
			}

			bool dirtyMesh = Enumerable.Count<(string, object)>(pList, pair => pair.Item1 == "dirtyMesh") > 0 ? (bool)Enumerable.First<(string, object)>(pList, pair => pair.Item1 == "dirtyMesh").Item2 : true;
			message = $"[{target?.name}] Edit {label}";
			if (dirtyMesh) {
				// set dirty flag true before recording object state for the undo so meshes will rebuild after the undo as well
				var item = (target as IEditableItemBehavior);
				if (item != null) {
					item.MeshDirty = true;
					recordObjs.Add(this);
				}
			}
			recordObjs.Add(target);
			return true;
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
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;
			if (dpeditable == null || bh == null) {
				return;
			}

			if (_curveTravellerControlPointIdx < 0 || _curveTravellerControlPointIdx >= _controlPoints.Count) {
				return;
			}

			PrepareUndo($"Adding Drag Point at position {_curveTravellerPosition}");

			//compute ratio between the two control points
			var cp0 = _controlPoints[_curveTravellerControlPointIdx];
			var cp1 = _controlPoints[_curveTravellerControlPointIdx + 1];
			Vector3 segment = cp1.WorldPos - cp0.WorldPos;
			float ratio = segment.magnitude > 0.0f ? (_curveTravellerPosition - cp0.WorldPos).magnitude / segment.magnitude : 0.0f;

			List<DragPointData> dpoints = new List<DragPointData>(dpeditable.GetDragPoints());
			DragPointData dpoint = new DragPointData(dpeditable.GetDragPoints()[_curveTravellerControlPointIdx]);
			dpoint.IsLocked = false;

			Vector3 offset = dpeditable.GetEditableOffset();
			Vector3 dpos = bh.transform.worldToLocalMatrix.MultiplyPoint(_curveTravellerPosition);
			dpos -= offset;
			dpoint.Vertex = dpos.ToVertex3D();

			int newIdx = _curveTravellerControlPointIdx + 1;
			dpoints.Insert(newIdx, dpoint);
			dpeditable.SetDragPoints(dpoints.ToArray());
			_controlPoints.Insert(newIdx, new ControlPoint(dpeditable.GetDragPoints()[newIdx], GUIUtility.GetControlID(FocusType.Passive), newIdx, (float)(newIdx) / dpeditable.GetDragPoints().Length));
			RemapControlPoints(dpeditable);
		}

		public void RemoveDragPoint(int controlId)
		{
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			if (dpeditable == null) {
				return;
			}

			var idx = _controlPoints.FindIndex(cpoint => cpoint.ControlId == controlId);
			if (idx >= 0) {
				bool removalOK = !_controlPoints[idx].DragPoint.IsLocked;
				if (!removalOK) {
					removalOK = EditorUtility.DisplayDialog("Locked DragPoint Removal", "This Dragpoint is Locked !!\nAre you really sure you want to remove it ?", "Yes", "No");
				}

				if (removalOK) {
					PrepareUndo("Removing Drag Point");
					List<DragPointData> dpoints = new List<DragPointData>(dpeditable.GetDragPoints());
					dpoints.RemoveAt(idx);
					dpeditable.SetDragPoints(dpoints.ToArray());
					_controlPoints.RemoveAt(idx);
					RemapControlPoints(dpeditable);
				}
			}
		}

		private void ClearAllSelection()
		{
			foreach (var cpoint in _controlPoints) {
				cpoint.IsSelected = false;
			}
		}

		private void UpdateDragPointsLock()
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			bool lockChange = false;
			foreach (var cpoint in _controlPoints) {
				if (cpoint.DragPoint.IsLocked != editable.IsLocked) {
					cpoint.DragPoint.IsLocked = editable.IsLocked;
					lockChange = true;
				}
			}
			if (lockChange) {
				SceneView.RepaintAll();
			}
		}

		private void OnDragPointPositionChange(Vector3 newPos, params (string, object)[] plist)
		{
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || dpeditable == null) {
				return;
			}

			Vector3 offset = dpeditable.GetEditableOffset();
			Matrix4x4 wlMat = bh.transform.worldToLocalMatrix;

			PrepareUndo($"[{target?.name}] Change DragPoint Position for {_selectedCP.Count} Control points.");

			Vector3 deltaPosition = newPos - _positionHandlePosition;
			foreach (var cpoint in _selectedCP) {
				cpoint.WorldPos += deltaPosition;
				Vector3 dpos = wlMat.MultiplyPoint(cpoint.WorldPos);
				dpos -= offset;
				dpos -= dpeditable.GetDragPointOffset(cpoint.IndexRatio);
				cpoint.DragPoint.Vertex = dpos.ToVertex3D();
			}
		}

		protected virtual void OnSceneGUI()
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || dpeditable == null || !dpeditable.DragPointEditEnabled) {
				return;
			}

			RemapControlPoints(dpeditable);
			UpdateDragPointsLock();

			Vector3 offset = dpeditable.GetEditableOffset();
			Matrix4x4 lwMat = bh.transform.localToWorldMatrix;
			Matrix4x4 wlMat = bh.transform.worldToLocalMatrix;

			switch (Event.current.type) {
				case EventType.Layout: {
					_selectedCP.Clear();
					_flipAxes = Vector3.zero;

					//Setup Screen positions & controlID for controlpoints (in case of modification of dragpoints ccordinates outside)
					foreach (var cpoint in _controlPoints) {
						cpoint.WorldPos = cpoint.DragPoint.Vertex.ToUnityVector3();
						_flipAxes += cpoint.WorldPos;
						cpoint.WorldPos += offset;
						cpoint.WorldPos += dpeditable.GetDragPointOffset(cpoint.IndexRatio);
						cpoint.WorldPos = lwMat.MultiplyPoint(cpoint.WorldPos);
						cpoint.ScrPos = Handles.matrix.MultiplyPoint(cpoint.WorldPos);
						if (cpoint.IsSelected) {
							if (!cpoint.DragPoint.IsLocked) {
								_selectedCP.Add(cpoint);
							}
						}
						HandleUtility.AddControl(cpoint.ControlId, HandleUtility.DistanceToCircle(cpoint.ScrPos, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius));
					}

					if (_controlPoints.Count > 0) {
						_flipAxes /= _controlPoints.Count;
					}

					//Setup PositionHandle if some control points are selected
					if (_selectedCP.Count > 0) {
						_positionHandlePosition = Vector3.zero;
						foreach (var sCp in _selectedCP) {
							_positionHandlePosition += sCp.WorldPos;
						}
						_positionHandlePosition /= _selectedCP.Count;
					}

					if (_curveTravellerVisible) {
						HandleUtility.AddControl(_curveTravellerControlId, HandleUtility.DistanceToCircle(Handles.matrix.MultiplyPoint(_curveTravellerPosition), HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio * 0.5f));
					}
				}
				break;

				case EventType.MouseDown: {
					if (Event.current.button == 0) {
						var nearCP = _controlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);
						if (nearCP != null && !nearCP.DragPoint.IsLocked) {
							if (!Event.current.control) {
								ClearAllSelection();
								nearCP.IsSelected = true;
							} else {
								nearCP.IsSelected = !nearCP.IsSelected;
							}
							Event.current.Use();
						}
					} else if (Event.current.button == 1) {
						var nearCP = _controlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);
						if (nearCP != null) {
							MenuCommand command = new MenuCommand(this, nearCP.ControlId);
							EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), MenuItems.CONTROLPOINTS_MENUPATH, command);
							Event.current.Use();
						} else if (HandleUtility.nearestControl == _curveTravellerControlId) {
							MenuCommand command = new MenuCommand(this, 0);
							EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), MenuItems.CURVETRAVELLER_MENUPATH, command);
							Event.current.Use();
						}
					}
				}
				break;

				case EventType.Repaint: {
					_curveTravellerVisible = false;
				}
				break;
			}

			//Handle the common position handler for all selected control points
			if (_selectedCP.Count > 0) {
				Quaternion parentRot = Quaternion.identity;
				if (bh.transform.parent != null) {
					parentRot = bh.transform.parent.transform.rotation;
				}
				Utils.HandlesUtils.HandlePosition(_positionHandlePosition, dpeditable.GetHandleType(), parentRot, OnDragPointPositionChange);
			}

			//Display Curve & handle curvetraveller
			if (_controlPoints.Count > 3) {
				List<DragPointData> transformedDPoints = new List<DragPointData>();
				foreach (var cpoint in _controlPoints) {
					DragPointData newDp = new DragPointData(cpoint.DragPoint);
					newDp.Vertex = cpoint.WorldPos.ToVertex3D();
					transformedDPoints.Add(newDp);
				}

				Vector3 vAccuracy = Vector3.one;
				vAccuracy = lwMat.MultiplyVector(vAccuracy);
				float accuracy = Mathf.Abs(vAccuracy.x * vAccuracy.y * vAccuracy.z);
				accuracy *= accuracy;
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(transformedDPoints.ToArray(), dpeditable.PointsAreLooping(), accuracy);

				if (vVertex.Length > 0) {
					ControlPoint curCP = null;
					_allPathPoints.Clear();
					foreach (RenderVertex3D v in vVertex) {
						if (v.IsControlPoint) {
							if (curCP != null) {
								curCP.pathPoints.Add(v.ToUnityVector3());
							}
							curCP = _controlPoints.Find(cp => cp.WorldPos == v.ToUnityVector3());
							if (curCP != null) {
								curCP.pathPoints.Clear();
							}
						}
						curCP.pathPoints.Add(v.ToUnityVector3());
						_allPathPoints.Add(v.ToUnityVector3());
					}

					_curveTravellerPosition = HandleUtility.ClosestPointToPolyLine(_allPathPoints.ToArray());

					//Render Curve with correct color regarding drag point properties & find curve section where the curve traveller is
					float width = 10.0f;
					_curveTravellerControlPointIdx = -1;
					foreach (var cp in _controlPoints) {
						if (cp.pathPoints.Count > 1) {
							Handles.color = HasDragPointExposition(DragPointExposition.SlingShot) && cp.DragPoint.IsSlingshot ? UnityEngine.Color.red : UnityEngine.Color.blue;
							Handles.DrawAAPolyLine(width, cp.pathPoints.ToArray());
							Vector3 closestToPath = HandleUtility.ClosestPointToPolyLine(cp.pathPoints.ToArray());
							if (closestToPath == _curveTravellerPosition) {
								_curveTravellerControlPointIdx = cp.Index;
							}
						}
					}
				}

				//Render Control Points and check traveler distance from CP
				float distToCPoint = Mathf.Infinity;
				for (int i = 0; i < _controlPoints.Count; ++i) {
					var cpoint = _controlPoints[i];
					Handles.color = cpoint.DragPoint.IsLocked ? UnityEngine.Color.red : (cpoint.IsSelected ? UnityEngine.Color.green : UnityEngine.Color.gray);
					Handles.SphereHandleCap(0, cpoint.WorldPos, Quaternion.identity, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius, EventType.Repaint);
					float decal = (HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius * 0.1f);
					Handles.Label(cpoint.WorldPos - Vector3.right * decal + Vector3.forward * decal * 2.0f, $"{i}");
					float dist = Vector3.Distance(_curveTravellerPosition, cpoint.WorldPos);
					distToCPoint = Mathf.Min(distToCPoint, dist);
				}

				if (!IsItemLocked()) {
					if (distToCPoint > HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius) {
						SceneView.RepaintAll();
						Handles.color = UnityEngine.Color.grey;
						Handles.SphereHandleCap(_curveTravellerControlId, _curveTravellerPosition, Quaternion.identity, HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio, EventType.Repaint);
						_curveTravellerVisible = true;
					}
				}
			}
		}
	}
}
