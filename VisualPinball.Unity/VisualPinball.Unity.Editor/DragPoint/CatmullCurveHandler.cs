using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor.DragPoint
{
	public delegate void OnDragPointPositionChange(Vector3 newPosition);

	public class CatmullCurveHandler
	{
		/// <summary>
		/// Authoring element
		/// </summary>
		private readonly IDragPointsEditable _editable;

		/// <summary>
		/// Transform component of the game object
		/// </summary>
		private readonly Transform _transform;

		/// <summary>
		/// Control points storing & rendering
		/// </summary>
		public List<ControlPoint> ControlPoints { get; } = new List<ControlPoint>();

		/// <summary>
		/// Curve points
		/// </summary>
		private readonly List<Vector3> _allPathPoints = new List<Vector3>();

		public List<ControlPoint> SelectedControlPoints { get; } = new List<ControlPoint>();
		private Vector3 _positionHandlePosition = Vector3.zero;

		public int CurveTravellerControlId { get; private set; }
		public Vector3 CurveTravellerPosition { get; private set; } = Vector3.zero;
		private bool _curveTravellerVisible;
		private int _curveTravellerControlPointIdx = -1;

		public float CurveWidth { get; set; } = 10.0f;

		public float ControlPointsSizeRatio { get; set; } = 1.0f;
		public float CurveTravellerSizeRatio { get; set; } = 1.0f;

		public UnityEngine.Color CurveColor { get; set; } = UnityEngine.Color.blue;

		public UnityEngine.Color CurveSlingShotColor { get; set; } = UnityEngine.Color.red;

		/// <summary>
		/// DragPoints flipping
		/// </summary>
		private Vector3 _flipAxis = Vector3.zero;

		/// <summary>
		/// Every DragPointsInspector instantiates this to manage its curve handling.
		/// </summary>
		/// <param name="target"></param>
		/// <exception cref="ArgumentException"></exception>
		public CatmullCurveHandler(Object target)
		{
			_editable = target as IDragPointsEditable
			            ?? throw new ArgumentException("Target must extend `IDragPointsEditable`.");

			if (!(target is Behaviour)) {
				throw new ArgumentException("Target must extend `Behavior`.");
			}
			_transform = (target as Behaviour).transform;
		}

		/// <summary>
		/// References drag point data to control points.
		/// </summary>
		/// <returns>True if control points were re-built, false otherwise.</returns>
		public bool RemapControlPoints()
		{
			// if count differs, rebuild
			if (ControlPoints.Count != _editable.GetDragPoints().Length) {
				RebuildControlPoints();
				return true;
			}

			for (var i = 0; i < _editable.GetDragPoints().Length; ++i) {
				ControlPoints[i].DragPoint = _editable.GetDragPoints()[i];
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
			if (_curveTravellerControlPointIdx < 0 || _curveTravellerControlPointIdx >= ControlPoints.Count) {
				return;
			}

			var dragPoint = new DragPointData(_editable.GetDragPoints()[_curveTravellerControlPointIdx]) {
				IsLocked = false
			};

			var offset = _editable.GetEditableOffset();
			var dragPointPosition = _transform.worldToLocalMatrix.MultiplyPoint(CurveTravellerPosition);
			dragPointPosition -= offset;
			dragPoint.Vertex = dragPointPosition.ToVertex3D();
			var newIdx = _curveTravellerControlPointIdx + 1;

			var dragPoints = _editable.GetDragPoints().ToList();
			dragPoints.Insert(newIdx, dragPoint);
			_editable.SetDragPoints(dragPoints.ToArray());

			ControlPoints.Insert(newIdx,
			new ControlPoint(
					_editable.GetDragPoints()[newIdx],
					GUIUtility.GetControlID(FocusType.Passive),
					newIdx,
					(float)newIdx / _editable.GetDragPoints().Length
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
					var dragPoints = _editable.GetDragPoints().ToList();
					dragPoints.RemoveAt(idx);
					_editable.SetDragPoints(dragPoints.ToArray());
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

			foreach (var controlPoint in ControlPoints) {
				var coord = flipAxis == FlipAxis.X
					? controlPoint.DragPoint.Vertex.X
					: flipAxis == FlipAxis.Y
						? controlPoint.DragPoint.Vertex.Y
						: controlPoint.DragPoint.Vertex.Z;

				coord = axis + (axis - coord);
				switch (flipAxis) {
					case FlipAxis.X:
						controlPoint.DragPoint.Vertex.X = coord;
						break;
					case FlipAxis.Y:
						controlPoint.DragPoint.Vertex.Y = coord;
						break;
					case FlipAxis.Z:
						controlPoint.DragPoint.Vertex.Z = coord;
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(flipAxis), flipAxis, null);
				}
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

			for (var i = 0; i < _editable.GetDragPoints().Length; ++i) {
				var cp = new ControlPoint(
					_editable.GetDragPoints()[i],
					GUIUtility.GetControlID(FocusType.Passive),
					i,
					(float)i / _editable.GetDragPoints().Length
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
		/// <param name="lockHandle">True if the game item is locked for editing, false otherwise</param>
		/// <param name="onChange"></param>
		public void OnSceneGUI(Event evt, bool lockHandle = false, OnDragPointPositionChange onChange = null)
		{
			var offset = _editable.GetEditableOffset();
			var lwMat = _transform.localToWorldMatrix;
			var wlMat = _transform.worldToLocalMatrix;

			switch (evt.type) {

				case EventType.Layout:
					InitSceneGui(offset, lwMat);
					break;

				case EventType.MouseDown:
					OnMouseDown();
					break;

				case EventType.Repaint:
					_curveTravellerVisible = false;
					break;
			}

			// Handle the common position handler for all selected control points
			if (SelectedControlPoints.Count > 0) {
				var parentRot = Quaternion.identity;
				if (_transform.parent != null) {
					parentRot = _transform.parent.transform.rotation;
				}
				EditorGUI.BeginChangeCheck();
				var newHandlePos = HandlesUtils.HandlePosition(_positionHandlePosition, _editable.GetHandleType(), parentRot);
				if (EditorGUI.EndChangeCheck()) {
					onChange?.Invoke(newHandlePos);
					var deltaPosition = newHandlePos - _positionHandlePosition;
					foreach (var controlPoint in SelectedControlPoints) {
						controlPoint.WorldPos += deltaPosition;
						var localPos = wlMat.MultiplyPoint(controlPoint.WorldPos);
						localPos -= offset;
						localPos -= _editable.GetDragPointOffset(controlPoint.IndexRatio);
						controlPoint.DragPoint.Vertex = localPos.ToVertex3D();
					}
				}
			}

			// Display Curve & handle curve traveller
			if (ControlPoints.Count > 3) {
				var transformedDPoints = new List<DragPointData>();
				foreach (var controlPoint in ControlPoints) {
					var newDp = new DragPointData(controlPoint.DragPoint) {
						Vertex = controlPoint.WorldPos.ToVertex3D()
					};
					transformedDPoints.Add(newDp);
				}

				var vAccuracy = Vector3.one;
				vAccuracy = lwMat.MultiplyVector(vAccuracy);
				var accuracy = Mathf.Abs(vAccuracy.x * vAccuracy.y * vAccuracy.z);
				accuracy *= accuracy;
				var vVertex = Engine.Math.DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(
					transformedDPoints.ToArray(), _editable.PointsAreLooping(), accuracy
				);

				if (vVertex.Length > 0) {
					// Fill Control points paths
					ControlPoint currentControlPoint = null;
					foreach (var v in vVertex) {
						if (v.IsControlPoint) {
							currentControlPoint?.PathPoints.Add(v.ToUnityVector3());
							currentControlPoint = ControlPoints.Find(cp => cp.WorldPos == v.ToUnityVector3());
							currentControlPoint?.PathPoints.Clear();
						}
						currentControlPoint?.PathPoints.Add(v.ToUnityVector3());
					}

					// close loop if needed
					if (_editable.PointsAreLooping()) {
						ControlPoints[ControlPoints.Count - 1].PathPoints.Add(ControlPoints[0].PathPoints[0]);
					}

					// construct full path
					_allPathPoints.Clear();
					foreach (var controlPoint in ControlPoints) {
						// Split straight segments to avoid HandleUtility.ClosestPointToPolyLine issues
						if (!controlPoint.DragPoint.IsSmooth && controlPoint.PathPoints.Count == 2) {
							var dir = controlPoint.PathPoints[1] - controlPoint.PathPoints[0];
							var dist = dir.magnitude;
							dir = Vector3.Normalize(dir);
							var newPath = new List<Vector3> {
								controlPoint.PathPoints[0]
							};
							for (var splitDist = dist * 0.1f; splitDist < dist; splitDist += dist * 0.1f) {
								newPath.Add(controlPoint.PathPoints[0] + dir * splitDist);
							}
							newPath.Add(controlPoint.PathPoints[1]);
							controlPoint.PathPoints = newPath;
						}
						_allPathPoints.AddRange(controlPoint.PathPoints);
					}

					CurveTravellerPosition = HandleUtility.ClosestPointToPolyLine(_allPathPoints.ToArray());

					// Render Curve with correct color regarding drag point properties & find curve section where the curve traveller is
					_curveTravellerControlPointIdx = -1;
					foreach (var controlPoint in ControlPoints) {
						if (controlPoint.PathPoints.Count > 1) {
							Handles.color = _editable.GetDragPointExposition().Contains(DragPointExposition.SlingShot) && controlPoint.DragPoint.IsSlingshot ? CurveSlingShotColor : CurveColor;
							Handles.DrawAAPolyLine(CurveWidth, controlPoint.PathPoints.ToArray());
							var closestToPath = HandleUtility.ClosestPointToPolyLine(controlPoint.PathPoints.ToArray());
							if (_curveTravellerControlPointIdx == -1 && closestToPath == CurveTravellerPosition) {
								_curveTravellerControlPointIdx = controlPoint.Index;
							}
						}
					}
				}

				// Render Control Points and check traveler distance from CP
				var distToCPoint = Mathf.Infinity;
				for (var i = 0; i < ControlPoints.Count; ++i) {
					var controlPoint = ControlPoints[i];
					Handles.color = controlPoint.DragPoint.IsLocked
						? UnityEngine.Color.red
						: controlPoint.IsSelected
							? UnityEngine.Color.green
							: UnityEngine.Color.gray;

					Handles.SphereHandleCap(0,
						controlPoint.WorldPos,
						Quaternion.identity,
						HandleUtility.GetHandleSize(controlPoint.WorldPos) * ControlPoint.ScreenRadius * ControlPointsSizeRatio,
						EventType.Repaint
					);
					var decal = HandleUtility.GetHandleSize(controlPoint.WorldPos) * ControlPoint.ScreenRadius * ControlPointsSizeRatio * 0.1f;
					Handles.Label(controlPoint.WorldPos - Vector3.right * decal + Vector3.forward * decal * 2.0f, $"{i}");
					var dist = Vector3.Distance(CurveTravellerPosition, controlPoint.WorldPos);
					distToCPoint = Mathf.Min(distToCPoint, dist);
				}

				if (!lockHandle) {
					if (distToCPoint > HandleUtility.GetHandleSize(CurveTravellerPosition) * ControlPoint.ScreenRadius) {
						Handles.color = UnityEngine.Color.grey;
						Handles.SphereHandleCap(CurveTravellerControlId, CurveTravellerPosition, Quaternion.identity, HandleUtility.GetHandleSize(CurveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio, EventType.Repaint);
						_curveTravellerVisible = true;
						HandleUtility.Repaint();
					}
				}
			}
		}

		private void InitSceneGui(Vector3 offset, Matrix4x4 lwMat)
		{
			SelectedControlPoints.Clear();
			_flipAxis = Vector3.zero;

			//Setup Screen positions & controlID for control points (in case of modification of drag points coordinates outside)
			foreach (var controlPoint in ControlPoints) {
				controlPoint.WorldPos = controlPoint.DragPoint.Vertex.ToUnityVector3();
				_flipAxis += controlPoint.WorldPos;
				controlPoint.WorldPos += offset;
				controlPoint.WorldPos += _editable.GetDragPointOffset(controlPoint.IndexRatio);
				controlPoint.WorldPos = lwMat.MultiplyPoint(controlPoint.WorldPos);
				controlPoint.ScrPos = Handles.matrix.MultiplyPoint(controlPoint.WorldPos);
				if (controlPoint.IsSelected) {
					if (!controlPoint.DragPoint.IsLocked) {
						SelectedControlPoints.Add(controlPoint);
					}
				}

				HandleUtility.AddControl(controlPoint.ControlId,
					HandleUtility.DistanceToCircle(controlPoint.ScrPos,
						HandleUtility.GetHandleSize(controlPoint.WorldPos) * ControlPoint.ScreenRadius *
						ControlPointsSizeRatio));
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

			if (_curveTravellerVisible) {
				HandleUtility.AddControl(CurveTravellerControlId,
					HandleUtility.DistanceToCircle(Handles.matrix.MultiplyPoint(CurveTravellerPosition),
						HandleUtility.GetHandleSize(CurveTravellerPosition) * ControlPoint.ScreenRadius *
						CurveTravellerSizeRatio * 0.5f));
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
