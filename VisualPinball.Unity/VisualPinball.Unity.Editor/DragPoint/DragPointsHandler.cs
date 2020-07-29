using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.Editor.Utils;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor.DragPoint
{
	public delegate void OnDragPointPositionChange(Vector3 newPosition);

	public class DragPointsHandler
	{
		/// <summary>
		/// Authoring item
		/// </summary>
		public IEditableItemBehavior Editable { get; private set; }

		/// <summary>
		/// Authoring item as IDragPointsEditable
		/// </summary>
		public IDragPointsEditable DragPointEditable { get; private set; }

		/// <summary>
		/// Transform component of the game object
		/// </summary>
		public Transform Transform { get; private set; }

		/// <summary>
		/// Control points storing & rendering
		/// </summary>
		public List<ControlPoint> ControlPoints { get; } = new List<ControlPoint>();

		/// <summary>
		/// Scene view handler
		/// </summary>
		///
		/// <remarks>
		/// Will handle all the rendering part and update some handler's variables about curve traveller
		/// </remarks>
		private readonly DragPointsSceneViewHandler _sceneViewHandler = null;

		/// <summary>
		/// Drag points selection
		/// </summary>
		public List<ControlPoint> SelectedControlPoints { get; } = new List<ControlPoint>();
		private Vector3 _positionHandlePosition = Vector3.zero;

		/// <summary>
		/// Curve traveller handling
		/// </summary>
		///
		/// <remarks>
		/// CurveTravellerPosition, CurveTravellerControlPointIdx & CurveTravellerVisible will be updated by the DragPointsSceneViewHandler
		/// </remarks>
		public int CurveTravellerControlId { get; private set; }
		public Vector3 CurveTravellerPosition { get; set; } = Vector3.zero;
		public bool CurveTravellerVisible { get; set; } = false;
		public int CurveTravellerControlPointIdx { get; set; } = -1;

		/// <summary>
		/// DragPoints flipping
		/// </summary>
		private Vector3 _flipAxis = Vector3.zero;

		/// <summary>
		/// Every DragPointsInspector instantiates this to manage its curve handling.
		/// </summary>
		/// <param name="target"></param>
		/// <exception cref="ArgumentException"></exception>
		public DragPointsHandler(Object target)
		{
			Editable = target as IEditableItemBehavior
			    ?? throw new ArgumentException("Target must extend `IEditableItemBehavior`.");

			DragPointEditable = target as IDragPointsEditable
			    ?? throw new ArgumentException("Target must extend `IDragPointsEditable`.");

			if (!(target is Behaviour)) {
				throw new ArgumentException("Target must extend `Behavior`.");
			}
			Transform = (target as Behaviour).transform;

			_sceneViewHandler = new DragPointsSceneViewHandler(this){
				CurveWidth = 10.0f,
				CurveColor = UnityEngine.Color.blue,
				CurveSlingShotColor = UnityEngine.Color.red,
				ControlPointsSizeRatio = 1.0f,
				CurveTravellerSizeRatio = 0.75f
			};
		}

		/// <summary>
		/// References drag point data to control points.
		/// </summary>
		/// <returns>True if control points were re-built, false otherwise.</returns>
		public bool RemapControlPoints()
		{
			// if count differs, rebuild
			if (ControlPoints.Count != DragPointEditable.GetDragPoints().Length) {
				RebuildControlPoints();
				return true;
			}

			for (var i = 0; i < DragPointEditable.GetDragPoints().Length; ++i) {
				ControlPoints[i].DragPoint = DragPointEditable.GetDragPoints()[i];
			}

			return false;
		}

		public DragPointData GetDragPoint(int controlId) => GetControlPoint(controlId)?.DragPoint;
		private ControlPoint GetControlPoint(int controlId)
			=> ControlPoints.Find(cp => cp.ControlId == controlId);

		/// <summary>
		/// Adds a new control point to the scene view and its drag point data
		/// to the game object.
		/// </summary>
		public void AddDragPointOnTraveller()
		{
			if (CurveTravellerControlPointIdx < 0 || CurveTravellerControlPointIdx >= ControlPoints.Count) {
				return;
			}

			var dragPoint = new DragPointData(DragPointEditable.GetDragPoints()[CurveTravellerControlPointIdx]) {
				IsLocked = false
			};

			var newIdx = CurveTravellerControlPointIdx + 1;
			float ratio = (float)newIdx / (DragPointEditable.GetDragPoints().Length - 1);
			var dragPointPosition = Transform.worldToLocalMatrix.MultiplyPoint(CurveTravellerPosition);
			dragPointPosition -= DragPointEditable.GetEditableOffset();
			dragPointPosition -= DragPointEditable.GetDragPointOffset(ratio);
			dragPoint.Center = dragPointPosition.ToVertex3D();
			var dragPoints = DragPointEditable.GetDragPoints().ToList();
			dragPoints.Insert(newIdx, dragPoint);
			DragPointEditable.SetDragPoints(dragPoints.ToArray());

			ControlPoints.Insert(newIdx,
			new ControlPoint(
					DragPointEditable.GetDragPoints()[newIdx],
					GUIUtility.GetControlID(FocusType.Passive),
					newIdx,
					ratio
			));
			RebuildControlPoints();
		}

		/// <summary>
		/// Removes a control point and its data.
		/// </summary>
		/// <param name="controlId"></param>
		public void RemoveDragPoint(int controlId)
		{
			var idx = ControlPoints.FindIndex(controlPoint => controlPoint.ControlId == controlId);
			if (idx >= 0) {
				var removalOk = !ControlPoints[idx].DragPoint.IsLocked;
				if (!removalOk) {
					removalOk = EditorUtility.DisplayDialog("Locked DragPoint Removal", "This drag point is locked!\nAre you really sure you want to remove it?", "Yes", "No");
				}

				if (removalOk) {
					var dragPoints = DragPointEditable.GetDragPoints().ToList();
					dragPoints.RemoveAt(idx);
					DragPointEditable.SetDragPoints(dragPoints.ToArray());
					ControlPoints.RemoveAt(idx);
					RebuildControlPoints();
				}
			}
		}

		/// <summary>
		/// Flips all drag points around the given axis.
		/// </summary>
		/// <param name="flipAxis">Axis to flip</param>
		public void FlipDragPoints(FlipAxis flipAxis)
		{
			var axis = flipAxis == FlipAxis.X
				? _flipAxis.x
				: flipAxis == FlipAxis.Y ? _flipAxis.y : _flipAxis.z;

			var offset = DragPointEditable.GetEditableOffset();
			var wlMat = Transform.worldToLocalMatrix;

			foreach (var controlPoint in ControlPoints) {
				var coord = flipAxis == FlipAxis.X
					? controlPoint.WorldPos.x
					: flipAxis == FlipAxis.Y
						? controlPoint.WorldPos.y
						: controlPoint.WorldPos.z;

				coord = axis + (axis - coord);
				switch (flipAxis) {
					case FlipAxis.X:
						controlPoint.WorldPos.x = coord;
						break;
					case FlipAxis.Y:
						controlPoint.WorldPos.y = coord;
						break;
					case FlipAxis.Z:
						controlPoint.WorldPos.z = coord;
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(flipAxis), flipAxis, null);
				}
				controlPoint.UpdateDragPoint(DragPointEditable, Transform);
			}
		}

		/// <summary>
		/// Updates the lock status on all drag points to the given value.
		/// </summary>
		/// <param name="itemLock">New lock status</param>
		/// <returns>True if at least one lock status changed, false otherwise.</returns>
		public bool UpdateDragPointsLock(bool itemLock)
		{
			var lockChanged = false;
			foreach (var controlPoint in ControlPoints) {
				if (controlPoint.DragPoint.IsLocked != itemLock) {
					controlPoint.DragPoint.IsLocked = itemLock;
					lockChanged = true;
				}
			}
			return lockChanged;
		}

		/// <summary>
		/// Re-creates the control points of the scene view and references their
		/// drag point data.
		/// </summary>
		private void RebuildControlPoints()
		{
			ControlPoints.Clear();

			for (var i = 0; i < DragPointEditable.GetDragPoints().Length; ++i) {
				var cp = new ControlPoint(
					DragPointEditable.GetDragPoints()[i],
					GUIUtility.GetControlID(FocusType.Passive),
					i,
					DragPointEditable.GetDragPoints().Length >= 2 ? (float)i / (DragPointEditable.GetDragPoints().Length-1) : 0.0f
				);
				ControlPoints.Add(cp);
			}

			CurveTravellerControlId = GUIUtility.GetControlID(FocusType.Passive);
		}

		/// <summary>
		/// Un-selects all control points.
		/// </summary>
		private void ClearAllSelection()
		{
			foreach (var controlPoint in ControlPoints) {
				controlPoint.IsSelected = false;
			}
		}

		/// <summary>
		/// Takes care of the rendering-related stuff.
		/// </summary>
		///
		/// <remarks>
		/// This is called by the drag point inspector.
		/// </remarks>
		///
		/// <param name="evt">Event from the inspector</param>
		/// <param name="onChange"></param>
		public void OnSceneGUI(Event evt, OnDragPointPositionChange onChange = null)
		{
			switch (evt.type) {

				case EventType.Layout:
					InitSceneGui();
					break;

				case EventType.MouseDown:
					OnMouseDown();
					break;

				case EventType.Repaint:
					CurveTravellerVisible = false;
					break;
			}

			// Handle the common position handler for all selected control points
			if (SelectedControlPoints.Count > 0) {
				var parentRot = Quaternion.identity;
				if (Transform.parent != null) {
					parentRot = Transform.parent.transform.rotation;
				}
				EditorGUI.BeginChangeCheck();
				var newHandlePos = HandlesUtils.HandlePosition(_positionHandlePosition, DragPointEditable.GetHandleType(), parentRot);
				if (EditorGUI.EndChangeCheck()) {
					onChange?.Invoke(newHandlePos);
					var deltaPosition = newHandlePos - _positionHandlePosition;
					foreach (var controlPoint in SelectedControlPoints) {
						controlPoint.WorldPos += deltaPosition;
						controlPoint.UpdateDragPoint(DragPointEditable, Transform);
					}
				}
			}

			//Render the curve & drag points
			_sceneViewHandler.OnSceneGUI();
		}

		private void InitSceneGui()
		{
			SelectedControlPoints.Clear();
			_flipAxis = Vector3.zero;

			//Setup Screen positions & controlID for control points (in case of modification of drag points coordinates outside)
			foreach (var controlPoint in ControlPoints) {
				controlPoint.WorldPos = controlPoint.DragPoint.Center.ToUnityVector3();
				controlPoint.WorldPos += DragPointEditable.GetEditableOffset();
				controlPoint.WorldPos += DragPointEditable.GetDragPointOffset(controlPoint.IndexRatio);
				controlPoint.WorldPos = Transform.localToWorldMatrix.MultiplyPoint(controlPoint.WorldPos);
				_flipAxis += controlPoint.WorldPos;
				controlPoint.ScrPos = Handles.matrix.MultiplyPoint(controlPoint.WorldPos);
				if (controlPoint.IsSelected) {
					if (!controlPoint.DragPoint.IsLocked) {
						SelectedControlPoints.Add(controlPoint);
					}
				}

				HandleUtility.AddControl(controlPoint.ControlId,
					HandleUtility.DistanceToCircle(controlPoint.ScrPos,
						HandleUtility.GetHandleSize(controlPoint.WorldPos) * ControlPoint.ScreenRadius *
						_sceneViewHandler.ControlPointsSizeRatio));
			}

			if (ControlPoints.Count > 0) {
				_flipAxis /= ControlPoints.Count;
			}

			//Setup PositionHandle if some control points are selected
			if (SelectedControlPoints.Count > 0) {
				_positionHandlePosition = Vector3.zero;
				foreach (var sCp in SelectedControlPoints) {
					_positionHandlePosition += sCp.WorldPos;
				}

				_positionHandlePosition /= SelectedControlPoints.Count;
			}

			if (CurveTravellerVisible) {
				HandleUtility.AddControl(CurveTravellerControlId,
					HandleUtility.DistanceToCircle(Handles.matrix.MultiplyPoint(CurveTravellerPosition),
						HandleUtility.GetHandleSize(CurveTravellerPosition) * ControlPoint.ScreenRadius *
						_sceneViewHandler.CurveTravellerSizeRatio * 0.5f));
			}
		}

		private void OnMouseDown()
		{
			if (Event.current.button == 0) {
				var nearestControlPoint = ControlPoints.Find(cp => cp.ControlId == HandleUtility.nearestControl);
				if (nearestControlPoint != null && !nearestControlPoint.DragPoint.IsLocked) {
					if (!Event.current.control) {
						ClearAllSelection();
						nearestControlPoint.IsSelected = true;
					}
					else {
						nearestControlPoint.IsSelected = !nearestControlPoint.IsSelected;
					}

					Event.current.Use();
				}
			}
		}
	}
}
