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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VisualPinball.Engine.Math;
using Color = UnityEngine.Color;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace VisualPinball.Unity.Editor
{
	public class DragPointsSceneViewHandler
	{
		/// <summary>
		/// Owning Handler
		/// </summary>
		private readonly DragPointsHandler _handler;

		/// <summary>
		/// Curve points in world space
		/// </summary>
		private readonly List<Vector3> _pathPoints = new List<Vector3>();

		private bool _curveTravellerMoved = false;

		public float CurveWidth { get; set; } = 10.0f;

		public float CurveTravellerSizeRatio { get; set; } = 1.0f;

		public Color CurveColor { get; set; } = Color.blue;

		public Color CurveSlingShotColor { get; set; } = Color.red;

		public DragPointsSceneViewHandler(DragPointsHandler handler)
		{
			_handler = handler;
		}

		public void OnSceneGUI()
		{
			if (_handler == null) {
				return;
			}
			
			DisplayCurve();
			DisplayControlPoints();
		}

		/// <summary>
		/// Construct & display the curve from DragPointsHandler control points
		/// Find the curve traveller position along the curve
		/// </summary>
		///
		/// <remarks>
		/// Will use the DragPointExposure from the handler's item to display slingshot segments accordingly
		/// Will update handler's curve traveller position and control point base index for point insertion
		/// </remarks>
		private void DisplayCurve()
		{
			Profiler.BeginSample("DisplayCurve");
			
			// Display Curve & handle curve traveller
			if (_handler.ControlPoints.Count > 1) {
				
				Profiler.BeginSample("Transform Points");
				var dragPointsVpx = new DragPointData[_handler.ControlPoints.Count];
				for (var i = 0; i < _handler.ControlPoints.Count; i++) {
					dragPointsVpx[i] = new DragPointData(_handler.ControlPoints[i].DragPoint) {
						Center = _handler.ControlPoints[i].AbsolutePosition.ToVertex3D()
					};
					Handles.DrawWireCube(dragPointsVpx[i].Center.ToUnityVector3().TranslateToWorld(), Vector3.one * 0.01f);
				}
				Profiler.EndSample();
			
				Profiler.BeginSample("Create Curve Vertices");
				var vAccuracy = Vector3.one;
				vAccuracy = _handler.Transform.localToWorldMatrix.MultiplyVector(vAccuracy);
				var accuracy = Mathf.Abs(vAccuracy.x * vAccuracy.y * vAccuracy.z);
				accuracy *= HandleUtility.GetHandleSize(_handler.CurveTravellerPosition) * ControlPoint.ScreenRadius;
				var curveVerticesVpx = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(
					dragPointsVpx, _handler.DragPointInspector.PointsAreLooping, accuracy
				);
				Profiler.EndSample();
			
				if (curveVerticesVpx.Length > 0) {
					
					var curveVerticesByDragPoint = new List<Vector3>[_handler.ControlPoints.Count].Select(_ => new List<Vector3>()).ToArray();

					Profiler.BeginSample("Fill Control Points");
					// Fill Control points paths
					ControlPoint currentControlPoint = null;
					foreach (var curveVertex in curveVerticesVpx) {
//						Handles.DrawWireCube(curveVertex.ToUnityVector3().TranslateToWorld(), Vector3.one * 0.005f);
						if (curveVertex.IsControlPoint) {
							if (currentControlPoint != null) {
								curveVerticesByDragPoint[currentControlPoint.Index].Add(curveVertex.ToUnityVector3());
							}
							currentControlPoint = _handler.ControlPoints.Find(cp => cp.AbsolutePosition == curveVertex.ToUnityVector3());
						}
						if (currentControlPoint != null) {
							curveVerticesByDragPoint[currentControlPoint.Index].Add(curveVertex.ToUnityVector3());
						}
					}
					Profiler.EndSample();
			
					// close loop if needed
					Profiler.BeginSample("Close Loops");
					if (_handler.DragPointInspector.PointsAreLooping) {
						curveVerticesByDragPoint[_handler.ControlPoints.Count - 1].Add(curveVerticesByDragPoint[0][0]);
					}
					Profiler.EndSample();
			
					// construct full path
					Profiler.BeginSample("Construct full path");
					_pathPoints.Clear();
					const float splitRatio = 0.1f;
					foreach (var controlPoint in _handler.ControlPoints) {
						// Split straight segments to avoid HandleUtility.ClosestPointToPolyLine issues
						ref var segments = ref curveVerticesByDragPoint[controlPoint.Index];
						if (segments.Count == 2) {
							var dir = segments[1] - segments[0];
							var dist = dir.magnitude;
							dir = Vector3.Normalize(dir);
							var newPath = new List<Vector3> {
								segments[0]
							};
							for (var splitDist = dist * splitRatio; splitDist < dist; splitDist += dist * splitRatio) {
								newPath.Add(newPath[0] + dir * splitDist);
							}
							newPath.Add(segments[1]);
							segments = newPath;
						}
						foreach (var segment in segments) {
							_pathPoints.Add(segment.TranslateToWorld());
						}
					}
					Profiler.EndSample();
			
					Profiler.BeginSample("Handle Traveller");
					_curveTravellerMoved = false;
					if (_pathPoints.Count > 1) {
						Profiler.BeginSample("Convert Points");
						var points = _pathPoints.ToArray();
						Profiler.EndSample();
						Profiler.BeginSample("Calculate closest");
						var newPos = HandleUtility.ClosestPointToPolyLine(points);
						Profiler.EndSample();
						Profiler.BeginSample("Calculate if moved");
						if ((newPos - _handler.CurveTravellerPosition).magnitude >= HandleUtility.GetHandleSize(_handler.CurveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio * 0.1f) {
							_handler.CurveTravellerPosition = newPos;
							_curveTravellerMoved = true;
						}
						Profiler.EndSample();
					}
					Profiler.EndSample();
			
					Profiler.BeginSample("Draw Line");
					// Render Curve with correct color regarding drag point properties & find curve section where the curve traveller is
					_handler.CurveTravellerControlPointIdx = -1;
					var minDist = float.MaxValue;
					Handles.matrix = Matrix4x4.identity;
					foreach (var controlPoint in _handler.ControlPoints) {
						Profiler.BeginSample("Compute Segments");
						var segments = curveVerticesByDragPoint[controlPoint.Index].Select(cp => cp.TranslateToWorld()).ToArray();
						Profiler.EndSample();
						if (segments.Length > 1) {
							Profiler.BeginSample("Determine Color");
							Handles.color = _handler.DragPointInspector.DragPointExposition.Contains(DragPointExposure.SlingShot) && controlPoint.DragPoint.IsSlingshot 
								? CurveSlingShotColor 
								: CurveColor;
							Profiler.EndSample();
							Profiler.BeginSample("Handles.DrawAAPolyLine");
							Handles.DrawAAPolyLine(CurveWidth, segments);
							Profiler.EndSample();
							Profiler.BeginSample("Calculate closes point");
							var closestToPath = HandleUtility.ClosestPointToPolyLine(segments);
							var dist = (closestToPath - _handler.CurveTravellerPosition).magnitude;
							if (dist < minDist) {
								minDist = dist;
								_handler.CurveTravellerControlPointIdx = controlPoint.Index;
							}
							Profiler.EndSample();
						}
					}
					Profiler.EndSample();
				}
			}
			
			Profiler.EndSample();
		}

		/// <summary>
		/// Display all the control points
		/// Evaluate if the curve traveller has to be shown and display it if it's needed.
		/// </summary>
		///
		/// <remarks>
		/// Will update handler's properties about curve traveller visibility
		/// </remarks>
		private void DisplayControlPoints()
		{
			Profiler.BeginSample("DisplayControlPoints");
			// Render Control Points and check traveler distance from CP
			var distToCPoint = Mathf.Infinity;
			Handles.matrix = Matrix4x4.identity;
			var style =  new GUIStyle {
				alignment = TextAnchor.MiddleCenter,
			};
			for (var i = 0; i < _handler.ControlPoints.Count; ++i) {
				var controlPoint = _handler.ControlPoints[i];
				Handles.color = controlPoint.DragPoint.IsLocked
					? Color.red
					: controlPoint.IsSelected
						? Color.green
						: Color.gray;

				var pos = controlPoint.Position;
				var handleSize = controlPoint.HandleSize;
				Handles.SphereHandleCap(-1, pos, Quaternion.identity, handleSize, EventType.Repaint);
				Handles.Label(pos - (Vector3.right * handleSize - Vector3.forward * handleSize * 2f) * 0.1f, $"{i}", style);
				var dist = Vector3.Distance(_handler.CurveTravellerPosition, controlPoint.Position);
				distToCPoint = Mathf.Min(distToCPoint, dist);
			}

			if (!_handler.MainComponent.IsLocked) {
				// curve traveller is not overlapping a control point, we can draw it.
				if (distToCPoint > HandleUtility.GetHandleSize(_handler.CurveTravellerPosition) * ControlPoint.ScreenRadius) {
					Handles.color = Color.grey;
					Handles.SphereHandleCap(_handler.CurveTravellerControlId, _handler.CurveTravellerPosition, Quaternion.identity, HandleUtility.GetHandleSize(_handler.CurveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio, EventType.Repaint);
					_handler.CurveTravellerVisible = true;
					if (EditorWindow.mouseOverWindow && _curveTravellerMoved) {
						HandleUtility.Repaint();
					}
				}
			}
			Profiler.EndSample();
		}
	}
}
