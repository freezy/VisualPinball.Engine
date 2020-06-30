using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.Extensions;
using System.Linq;

namespace VisualPinball.Unity.Editor.Handle
{
	public class CatmullCurveHandle
	{
		public delegate void OnDragPointPositionChange(Vector3 newPosition);

		//CTor
		public CatmullCurveHandle(Object target)
		{
			_editable = target as IDragPointsEditable;
			_behaviour = target as Behaviour;
		}

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

		//Attached Object
		private IDragPointsEditable _editable = null;
		private Behaviour _behaviour = null;

		//Control points storing & rendering
		private List<ControlPoint> _controlPoints = new List<ControlPoint>();
		public List<ControlPoint> ControlPoints => _controlPoints;

		//Curve points
		private List<Vector3> _allPathPoints = new List<Vector3>();

		//Control points position Handle
		private List<ControlPoint> _selectedCP = new List<ControlPoint>();
		public List<ControlPoint> SelectedControlPoints => _selectedCP;
		private int _positionHandleControlId = 0;
		private Vector3 _positionHandlePosition = Vector3.zero;

		//Curve Traveller
		private int _curveTravellerControlId = 0;
		public int CurveTravellerControlId => _curveTravellerControlId;
		private Vector3 _curveTravellerPosition = Vector3.zero;
		public Vector3 CurveTravellerPosition => _curveTravellerPosition;
		private bool _curveTravellerVisible = false;
		public bool CurveTravellerVisible => _curveTravellerVisible;
		private int _curveTravellerControlPointIdx = -1;

		//Rendering 
		private float _curveWidth = 10.0f;
		public float CurveWidth { get => _curveWidth; set { _curveWidth = value; } }
		private float _controlPointsSizeRatio = 1.0f;
		public float ControlPointsSizeRatio { get => _controlPointsSizeRatio; set { _controlPointsSizeRatio = value; } }
		private float _curveTravellerSizeRatio = 1.0f;
		public float CurveTravellerSizeRatio { get => _curveTravellerSizeRatio; set { _curveTravellerSizeRatio = value; } }
		private UnityEngine.Color _curveColor = UnityEngine.Color.blue;
		public UnityEngine.Color CurveColor { get => _curveColor; set { _curveColor = value; } }
		private UnityEngine.Color _curveSlingShotColor = UnityEngine.Color.red;
		public UnityEngine.Color CurveSlingShotColor { get => _curveSlingShotColor; set { _curveSlingShotColor = value; } }

		//DragPoints flipping
		private Vector3 _flipAxes = Vector3.zero;


		//DragPoints Remapping
		protected void RebuildControlPoints()
		{
			_controlPoints.Clear();

			for (int i = 0; i < _editable.GetDragPoints().Length; ++i) {
				ControlPoint cp = new ControlPoint(_editable.GetDragPoints()[i], GUIUtility.GetControlID(FocusType.Passive), i, (float)i / _editable.GetDragPoints().Length);
				_controlPoints.Add(cp);
			}

			_positionHandleControlId = GUIUtility.GetControlID(FocusType.Passive);
			_curveTravellerControlId = GUIUtility.GetControlID(FocusType.Passive);
		}

		public enum RemapReturn
		{
			Ok,
			ControlPointsRebuilt
		}

		public RemapReturn RemapControlPoints()
		{
			if (_controlPoints.Count != _editable.GetDragPoints().Length) {
				RebuildControlPoints();
				return RemapReturn.ControlPointsRebuilt;
			} else {
				for (int i = 0; i < _editable.GetDragPoints().Length; ++i) {
					_controlPoints[i].DragPoint = _editable.GetDragPoints()[i];
				}
				return RemapReturn.Ok;
			}
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

		public void AddDragPointOnTraveller()
		{
			if (_editable == null || _behaviour == null) {
				return;
			}

			if (_curveTravellerControlPointIdx < 0 || _curveTravellerControlPointIdx >= _controlPoints.Count) {
				return;
			}

			//compute ratio between the two control points
			var cp0 = _controlPoints[_curveTravellerControlPointIdx];
			var cp1 = _controlPoints[_curveTravellerControlPointIdx == _controlPoints.Count - 1 ? _editable.PointsAreLooping() ? 0 : _curveTravellerControlPointIdx : _curveTravellerControlPointIdx + 1];
			Vector3 segment = cp1.WorldPos - cp0.WorldPos;
			float ratio = segment.magnitude > 0.0f ? (_curveTravellerPosition - cp0.WorldPos).magnitude / segment.magnitude : 0.0f;

			List<DragPointData> dpoints = new List<DragPointData>(_editable.GetDragPoints());
			DragPointData dpoint = new DragPointData(_editable.GetDragPoints()[_curveTravellerControlPointIdx]);
			dpoint.IsLocked = false;

			Vector3 offset = _editable.GetEditableOffset();
			Vector3 dpos = _behaviour.transform.worldToLocalMatrix.MultiplyPoint(_curveTravellerPosition);
			dpos -= offset;
			dpoint.Vertex = dpos.ToVertex3D();

			int newIdx = _curveTravellerControlPointIdx + 1;
			dpoints.Insert(newIdx, dpoint);
			_editable.SetDragPoints(dpoints.ToArray());
			_controlPoints.Insert(newIdx, new ControlPoint(_editable.GetDragPoints()[newIdx], GUIUtility.GetControlID(FocusType.Passive), newIdx, (float)(newIdx) / _editable.GetDragPoints().Length));
			RebuildControlPoints();
		}

		public void RemoveDragPoint(int controlId)
		{
			var idx = _controlPoints.FindIndex(cpoint => cpoint.ControlId == controlId);
			if (idx >= 0) {
				bool removalOK = !_controlPoints[idx].DragPoint.IsLocked;
				if (!removalOK) {
					removalOK = EditorUtility.DisplayDialog("Locked DragPoint Removal", "This Dragpoint is Locked !!\nAre you really sure you want to remove it ?", "Yes", "No");
				}

				if (removalOK) {
					List<DragPointData> dpoints = new List<DragPointData>(_editable.GetDragPoints());
					dpoints.RemoveAt(idx);
					_editable.SetDragPoints(dpoints.ToArray());
					_controlPoints.RemoveAt(idx);
					RebuildControlPoints();
				}
			}
		}

		public void FlipDragPoints(FlipAxes flipAxe)
		{
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

		public bool UpdateDragPointsLock(bool itemLock)
		{
			bool lockChange = false;
			foreach (var cpoint in _controlPoints) {
				if (cpoint.DragPoint.IsLocked != itemLock) {
					cpoint.DragPoint.IsLocked = itemLock;
					lockChange = true;
				}
			}
			return lockChange;
		}

		private void ClearAllSelection()
		{
			foreach (var cpoint in _controlPoints) {
				cpoint.IsSelected = false;
			}
		}

		public void OnSceneGUI(Event evt, bool lockHandle = false, OnDragPointPositionChange onChange = null)
		{
			if (_editable == null || _behaviour == null) {
				return;
			}

			Vector3 offset = _editable.GetEditableOffset();
			Matrix4x4 lwMat = _behaviour.transform.localToWorldMatrix;
			Matrix4x4 wlMat = _behaviour.transform.worldToLocalMatrix;

			switch (evt.type) {
				case EventType.Layout: {
					_selectedCP.Clear();
					_flipAxes = Vector3.zero;

					//Setup Screen positions & controlID for controlpoints (in case of modification of dragpoints ccordinates outside)
					foreach (var cpoint in _controlPoints) {
						cpoint.WorldPos = cpoint.DragPoint.Vertex.ToUnityVector3();
						_flipAxes += cpoint.WorldPos;
						cpoint.WorldPos += offset;
						cpoint.WorldPos += _editable.GetDragPointOffset(cpoint.IndexRatio);
						cpoint.WorldPos = lwMat.MultiplyPoint(cpoint.WorldPos);
						cpoint.ScrPos = Handles.matrix.MultiplyPoint(cpoint.WorldPos);
						if (cpoint.IsSelected) {
							if (!cpoint.DragPoint.IsLocked) {
								_selectedCP.Add(cpoint);
							}
						}
						HandleUtility.AddControl(cpoint.ControlId, HandleUtility.DistanceToCircle(cpoint.ScrPos, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius * _controlPointsSizeRatio));
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
						HandleUtility.AddControl(_curveTravellerControlId, HandleUtility.DistanceToCircle(Handles.matrix.MultiplyPoint(_curveTravellerPosition), HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * _curveTravellerSizeRatio * 0.5f));
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
				if (_behaviour.transform.parent != null) {
					parentRot = _behaviour.transform.parent.transform.rotation;
				}
				EditorGUI.BeginChangeCheck();
				Vector3 newHandlePos = HandlesUtils.HandlePosition(_positionHandlePosition, _editable.GetHandleType(), parentRot);
				if (EditorGUI.EndChangeCheck()) {
					if (onChange != null) {
						onChange(newHandlePos);
					}
					Vector3 deltaPosition = newHandlePos - _positionHandlePosition;
					foreach (var cpoint in _selectedCP) {
						cpoint.WorldPos += deltaPosition;
						Vector3 dpos = wlMat.MultiplyPoint(cpoint.WorldPos);
						dpos -= offset;
						dpos -= _editable.GetDragPointOffset(cpoint.IndexRatio);
						cpoint.DragPoint.Vertex = dpos.ToVertex3D();
					}
				}
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
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(transformedDPoints.ToArray(), _editable.PointsAreLooping(), accuracy);

				if (vVertex.Length > 0) {
					//Fill Control points pathes
					ControlPoint curCP = null;
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
					}

					//close loop if needed
					if (_editable.PointsAreLooping()) {
						_controlPoints[_controlPoints.Count - 1].pathPoints.Add(_controlPoints[0].pathPoints[0]);
					}

					//contruct full path 
					_allPathPoints.Clear();
					foreach(var cpoint in _controlPoints) {
						//Split straight segments to avoid HandleUtility.ClosestPointToPolyLine issues
						if (!cpoint.DragPoint.IsSmooth && cpoint.pathPoints.Count == 2) {
							Vector3 dir = cpoint.pathPoints[1] - cpoint.pathPoints[0];
							float dist = dir.magnitude;
							dir = Vector3.Normalize(dir);
							List<Vector3> newPath = new List<Vector3>();
							newPath.Add(cpoint.pathPoints[0]);
							for (float splitDist = dist * 0.1f; splitDist < dist; splitDist += dist * 0.1f) {
								newPath.Add(cpoint.pathPoints[0] + dir * splitDist);
							}
							newPath.Add(cpoint.pathPoints[1]);
							cpoint.pathPoints = newPath;
						}
						_allPathPoints.AddRange(cpoint.pathPoints);
					}

					_curveTravellerPosition = HandleUtility.ClosestPointToPolyLine(_allPathPoints.ToArray());

					//Render Curve with correct color regarding drag point properties & find curve section where the curve traveller is
					_curveTravellerControlPointIdx = -1;
					foreach (var cp in _controlPoints) {
						if (cp.pathPoints.Count > 1) {
							Handles.color = _editable.GetDragPointExposition().Contains(DragPointExposition.SlingShot) && cp.DragPoint.IsSlingshot ? _curveSlingShotColor : _curveColor;
							Handles.DrawAAPolyLine(_curveWidth, cp.pathPoints.ToArray());
//							Handles.color = UnityEngine.Color.magenta;
//							foreach(var ppoint in cp.pathPoints) {
//								Handles.SphereHandleCap(0, ppoint, Quaternion.identity, HandleUtility.GetHandleSize(ppoint) * ControlPoint.ScreenRadius * _controlPointsSizeRatio * 0.25f, EventType.Repaint);
//							}
							Vector3 closestToPath = HandleUtility.ClosestPointToPolyLine(cp.pathPoints.ToArray());
							if (_curveTravellerControlPointIdx == -1 && closestToPath == _curveTravellerPosition) {
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
					Handles.SphereHandleCap(0, cpoint.WorldPos, Quaternion.identity, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius * _controlPointsSizeRatio, EventType.Repaint);
					float decal = (HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius * _controlPointsSizeRatio * 0.1f);
					Handles.Label(cpoint.WorldPos - Vector3.right * decal + Vector3.forward * decal * 2.0f, $"{i}");
					float dist = Vector3.Distance(_curveTravellerPosition, cpoint.WorldPos);
					distToCPoint = Mathf.Min(distToCPoint, dist);
				}

				if (!lockHandle) {
					if (distToCPoint > HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius) {
						Handles.color = UnityEngine.Color.grey;
						Handles.SphereHandleCap(_curveTravellerControlId, _curveTravellerPosition, Quaternion.identity, HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * _curveTravellerSizeRatio, EventType.Repaint);
						_curveTravellerVisible = true;
						HandleUtility.Repaint();
					}
				}
			}

		}

	}
}
