using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity.Editor
{
	public class DragPointsSceneViewHandler
	{
		/// <summary>
		/// Owning Handler
		/// </summary>
		private readonly DragPointsHandler _handler;

		/// <summary>
		/// Curve points
		/// </summary>
		private readonly List<Vector3> _pathPoints = new List<Vector3>();

		public float CurveWidth { get; set; } = 10.0f;

		public float ControlPointsSizeRatio { get; set; } = 1.0f;
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
			List<Vector3>[] controlPointsSegments = new List<Vector3>[_handler.ControlPoints.Count].Select(item => new List<Vector3>()).ToArray();

			// Display Curve & handle curve traveller
			if (_handler.ControlPoints.Count > 1) {
				var transformedDPoints = new List<DragPointData>();
				foreach (var controlPoint in _handler.ControlPoints) {
					var newDp = new DragPointData(controlPoint.DragPoint) {
						Center = controlPoint.WorldPos.ToVertex3D()
					};
					transformedDPoints.Add(newDp);
				}

				var vAccuracy = Vector3.one;
				vAccuracy = _handler.Transform.localToWorldMatrix.MultiplyVector(vAccuracy);
				var accuracy = Mathf.Abs(vAccuracy.x * vAccuracy.y * vAccuracy.z);
				accuracy *= HandleUtility.GetHandleSize(_handler.CurveTravellerPosition) * ControlPoint.ScreenRadius;
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(
					transformedDPoints.ToArray(), _handler.DragPointEditable.PointsAreLooping(), accuracy
				);

				if (vVertex.Length > 0) {
					// Fill Control points paths
					ControlPoint currentControlPoint = null;
					foreach (var v in vVertex) {
						if (v.IsControlPoint) {
							if (currentControlPoint != null) {
								controlPointsSegments[currentControlPoint.Index].Add(v.ToUnityVector3());
							}
							currentControlPoint = _handler.ControlPoints.Find(cp => cp.WorldPos == v.ToUnityVector3());
						}
						if (currentControlPoint != null) {
							controlPointsSegments[currentControlPoint.Index].Add(v.ToUnityVector3());
						}
					}

					// close loop if needed
					if (_handler.DragPointEditable.PointsAreLooping()) {
						controlPointsSegments[_handler.ControlPoints.Count - 1].Add(controlPointsSegments[0][0]);
					}

					// construct full path
					_pathPoints.Clear();
					const float splitRatio = 0.05f;
					foreach (var controlPoint in _handler.ControlPoints) {
						// Split straight segments to avoid HandleUtility.ClosestPointToPolyLine issues
						var segments = controlPointsSegments[controlPoint.Index];
						if (!controlPoint.DragPoint.IsSmooth && segments.Count == 2) {
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
						_pathPoints.AddRange(segments);
					}

					_handler.CurveTravellerPosition = HandleUtility.ClosestPointToPolyLine(_pathPoints.ToArray());

					// Render Curve with correct color regarding drag point properties & find curve section where the curve traveller is
					_handler.CurveTravellerControlPointIdx = -1;
					foreach (var controlPoint in _handler.ControlPoints) {
						var segments = controlPointsSegments[controlPoint.Index].ToArray();
						if (segments.Length > 1) {
							Handles.color = _handler.DragPointEditable.GetDragPointExposition().Contains(DragPointExposure.SlingShot) && controlPoint.DragPoint.IsSlingshot ? CurveSlingShotColor : CurveColor;
							Handles.DrawAAPolyLine(CurveWidth, segments);
							var closestToPath = HandleUtility.ClosestPointToPolyLine(segments);
							if (_handler.CurveTravellerControlPointIdx == -1 && closestToPath == _handler.CurveTravellerPosition) {
								_handler.CurveTravellerControlPointIdx = controlPoint.Index;
							}
						}
					}
				}
			}
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
			// Render Control Points and check traveler distance from CP
			var distToCPoint = Mathf.Infinity;
			for (var i = 0; i < _handler.ControlPoints.Count; ++i) {
				var controlPoint = _handler.ControlPoints[i];
				Handles.color = controlPoint.DragPoint.IsLocked
					? Color.red
					: controlPoint.IsSelected
						? Color.green
						: Color.gray;

				Handles.SphereHandleCap(0,
					controlPoint.WorldPos,
					Quaternion.identity,
					HandleUtility.GetHandleSize(controlPoint.WorldPos) * ControlPoint.ScreenRadius * ControlPointsSizeRatio,
					EventType.Repaint
				);
				var decal = HandleUtility.GetHandleSize(controlPoint.WorldPos) * ControlPoint.ScreenRadius * ControlPointsSizeRatio * 0.1f;
				Handles.Label(controlPoint.WorldPos - Vector3.right * decal + Vector3.forward * decal * 2.0f, $"{i}");
				var dist = Vector3.Distance(_handler.CurveTravellerPosition, controlPoint.WorldPos);
				distToCPoint = Mathf.Min(distToCPoint, dist);
			}

			if (!_handler.Editable.IsLocked) {
				// curve traveller is not overlapping a control point, we can draw it.
				if (distToCPoint > HandleUtility.GetHandleSize(_handler.CurveTravellerPosition) * ControlPoint.ScreenRadius) {
					Handles.color = Color.grey;
					Handles.SphereHandleCap(_handler.CurveTravellerControlId, _handler.CurveTravellerPosition, Quaternion.identity, HandleUtility.GetHandleSize(_handler.CurveTravellerPosition) * ControlPoint.ScreenRadius * CurveTravellerSizeRatio, EventType.Repaint);
					_handler.CurveTravellerVisible = true;
					HandleUtility.Repaint();
				}
			}
		}
	}
}
