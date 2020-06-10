using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Editors
{
	public class DragPointsEditor
	{
		private class ControlPoint
		{
			static public float ScreenRadius = 0.25f;
			public DragPointData DragPoint;
			public Vector3 WorldPos = Vector3.zero;
			public Vector3 ScrPos = Vector3.zero;
			public bool IsSelected = false;
			public int ControlId = 0;

			public ControlPoint(ref DragPointData dp, int controlID)
			{
				DragPoint = dp;
				ControlId = controlID;
			}
		}

		//Control points storing & rendering
		private List<ControlPoint> _controlPoints = new List<ControlPoint>();
		private List<Vector3> _pathPoints = new List<Vector3>();

		//Control points position Handle
		private List<ControlPoint> _selectedCP = new List<ControlPoint>();
		private int _positionHandleControlId = 0;
		private Vector3 _positionHandlePosition = Vector3.zero;

		//Curve Traveller 
		static public float CurveTravellerSizeRatio = 0.5f;
		private int _curveTravellerControlId = 0;
		private Vector3 _curveTravellerPosition = Vector3.zero;
		private bool _curveTravellerVisible = false;

		public void OnInspectorGUI(Object target)
		{
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			if (dpeditable == null) return;

			string enabledString = dpeditable.DragPointEditEnabled ? "(ON)" : "(OFF)";
			if (GUILayout.Button($"Edit Drag Points {enabledString}")) {
				dpeditable.DragPointEditEnabled = !dpeditable.DragPointEditEnabled;
				SceneView.RepaintAll();
			}
		}

		protected void RebuildControlPoints(IDragPointsEditable dpEditable)
		{
			_controlPoints.Clear();

			for (int i = 0; i < dpEditable.GetDragPoints().Length; ++i)
			{
				_controlPoints.Add(new ControlPoint(ref dpEditable.GetDragPoints()[i], GUIUtility.GetControlID(FocusType.Passive)));
			}

			_positionHandleControlId = GUIUtility.GetControlID(FocusType.Passive);
			_curveTravellerControlId = GUIUtility.GetControlID(FocusType.Passive);
		}

		public void OnSceneGUI(Object target)
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || dpeditable == null || !dpeditable.DragPointEditEnabled)
				return;

			if (_controlPoints.Count != dpeditable.GetDragPoints().Length)
				RebuildControlPoints(dpeditable);

			Vector3 offset = dpeditable.GetEditableOffset();
			Matrix4x4 lwMat = bh.transform.localToWorldMatrix;
			Matrix4x4 wlMat = bh.transform.worldToLocalMatrix;

			switch (Event.current.type)
			{
				case EventType.Layout:
					{
						_selectedCP.Clear();
						//Setup Screen positions & controlID for controlpoints (in case of modification of dragpoints ccordinates outside)
						foreach (var cpoint in _controlPoints)
						{
							cpoint.WorldPos = cpoint.DragPoint.Vertex.ToUnityVector3();
							cpoint.WorldPos.z += cpoint.DragPoint.CalcHeight;
							cpoint.WorldPos += offset;
							cpoint.WorldPos = lwMat.MultiplyPoint(cpoint.WorldPos);
							cpoint.ScrPos = Handles.matrix.MultiplyPoint(cpoint.WorldPos);
							if (cpoint.IsSelected)
								_selectedCP.Add(cpoint);
							HandleUtility.AddControl(cpoint.ControlId, HandleUtility.DistanceToCircle(cpoint.ScrPos, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius * 0.5f));
						}

						//Setup PositionHandle if some control points are selected
						if (_selectedCP.Count > 0)
						{
							_positionHandlePosition = Vector3.zero;
							foreach (var sCp in _selectedCP)
							{
								_positionHandlePosition += sCp.WorldPos;
							}
							_positionHandlePosition /= _selectedCP.Count;
						}

						if (_curveTravellerVisible)
							HandleUtility.AddControl(_curveTravellerControlId, HandleUtility.DistanceToCircle(Handles.matrix.MultiplyPoint(_curveTravellerPosition), HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio * 0.5f));
					}
					break;

				case EventType.MouseDown:
					{
						if (Event.current.button == 0)
						{
							foreach (var cpoint in _controlPoints)
							{
								if (cpoint.ControlId == HandleUtility.nearestControl)
								{
									if (!cpoint.DragPoint.IsLocked)
									{
										cpoint.IsSelected = !cpoint.IsSelected;
										Event.current.Use();
									}
									break;
								}
							}
						}
						else if (Event.current.button == 1)
						{
							if (HandleUtility.nearestControl == _curveTravellerControlId)
							{
//								float dist = HandleUtility.DistanceToCircle(Handles.matrix.MultiplyPoint(_curveTravellerPosition), HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * 0.5f);
								//if (dist <= 0.0f)
								{
									foreach (var cpoint in _controlPoints)
									{
										cpoint.IsSelected = false;
									}
									Event.current.Use();
								}
							}
						}
						Debug.Log("Right Click handle " + HandleUtility.nearestControl.ToString());
					}
					break;

				case EventType.Repaint:
					{
						_curveTravellerVisible = false;
					}
					break;
			}

			//Handle the common position handler for all selected control points
			if (_selectedCP.Count > 0)
			{
				EditorGUI.BeginChangeCheck();
				Vector3 newHandlePos = Handles.PositionHandle(_positionHandlePosition, Quaternion.identity);
				if (EditorGUI.EndChangeCheck())
				{
					//Set Meshdirty to true there so it'll trigger again after Undo
					if (editable != null)
						editable.MeshDirty = true;

					Undo.RecordObject(bh, "Change DragPoint Position for " + _selectedCP.Count + " Control points.");

					Vector3 deltaPosition = newHandlePos - _positionHandlePosition;
					foreach (var cpoint in _selectedCP)
					{
						cpoint.WorldPos += deltaPosition;
						Vector3 dpos = wlMat.MultiplyPoint(cpoint.WorldPos);
						dpos -= offset;
						dpos.z -= cpoint.DragPoint.CalcHeight;
						cpoint.DragPoint.Vertex = dpos.ToVertex3D();
					}
				}
			}

			//Display Curve & handle curvetraveller
			if (_controlPoints.Count > 3)
			{
				List<DragPointData> transformedDPoints = new List<DragPointData>();
				for (int i = 0; i < _controlPoints.Count; ++i)
				{
					var cpoint = _controlPoints[i];
					DragPointData newDp = new DragPointData(cpoint.DragPoint);
					newDp.Vertex = cpoint.WorldPos.ToVertex3D();
					transformedDPoints.Add(newDp);
				}

				var cross = transformedDPoints[1].Vertex.Clone().Sub(transformedDPoints[0].Vertex).Cross(transformedDPoints[2].Vertex.Clone().Sub(transformedDPoints[0].Vertex));
				var areaSq = cross.LengthSq();
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(transformedDPoints.ToArray(), dpeditable.PointsAreLooping(), areaSq * 0.000001f);

				if (vVertex.Length > 0)
				{
					float width = 10.0f;
					Handles.color = UnityEngine.Color.blue;
					_pathPoints.Clear();
					foreach (RenderVertex3D v in vVertex)
					{
						_pathPoints.Add(new Vector3(v.X, v.Y, v.Z));
					}
					Handles.DrawAAPolyLine(width, _pathPoints.ToArray());
				}

				//World Position of the curve traveller
				_curveTravellerPosition = HandleUtility.ClosestPointToPolyLine(_pathPoints.ToArray());

				float distToCPoint = Mathf.Infinity;
				foreach (var cpoint in _controlPoints)
				{
					Handles.color = cpoint.DragPoint.IsLocked ? UnityEngine.Color.red : (cpoint.IsSelected ? UnityEngine.Color.green : UnityEngine.Color.gray);
					Handles.SphereHandleCap(0, cpoint.WorldPos, Quaternion.identity, HandleUtility.GetHandleSize(cpoint.WorldPos) * ControlPoint.ScreenRadius, EventType.Repaint);
					distToCPoint = Mathf.Min(distToCPoint, Vector3.Distance(_curveTravellerPosition, cpoint.WorldPos));
				}

				if (distToCPoint > HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius)
				{
					SceneView.RepaintAll();
					Handles.color = UnityEngine.Color.grey;
					Handles.SphereHandleCap(_curveTravellerControlId, _curveTravellerPosition, Quaternion.identity, HandleUtility.GetHandleSize(_curveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio, EventType.Repaint);
					_curveTravellerVisible = true;
				}
			}

		}
	}
}
