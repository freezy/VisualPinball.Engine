using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.Extensions;
using VisualPinball.Engine.Math;


namespace VisualPinball.Unity.Editor.DragPoints
{
	public abstract class DragPointsEditor : UnityEditor.Editor
	{
		List<DragPointData> controlPoints = new List<DragPointData>();

        List<Vector3> pathPoints = new List<Vector3>();
        float resolution = 0.2f;

		public virtual void OnSceneGUI()
		{
			IDragPointsEditable editable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || editable == null)
				return;

			controlPoints.Clear();

			Vector3 offset = editable.GetEditableOffset();
			Matrix4x4 lwMat = bh.transform.localToWorldMatrix;
			Matrix4x4 wlMat = bh.transform.worldToLocalMatrix;

			foreach (var dpoint in editable.GetDragPoints())
			{
				Vector3 dpos = MathExtensions.ToUnityVector3(dpoint.Vertex);
				dpos.z += dpoint.CalcHeight;
				dpos += offset;
				dpos = lwMat.MultiplyPoint(dpos);
				EditorGUI.BeginChangeCheck();
				dpos = Handles.PositionHandle(dpos, Quaternion.identity);
				DragPointData transformedData = new DragPointData(dpoint);
				transformedData.Vertex = MathExtensions.FromUnityVector3(dpos);
				controlPoints.Add(transformedData);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(bh, "Change DragPoint Position to " + dpos.ToString());
					dpos = wlMat.MultiplyPoint(dpos);
					dpos -= offset;
					dpos.z -= dpoint.CalcHeight;
					dpoint.Vertex = MathExtensions.FromUnityVector3(dpos);
				}
			}

			if (controlPoints.Count > 3)
			{
				var cross = controlPoints[1].Vertex.Clone().Sub(controlPoints[0].Vertex).Cross(controlPoints[2].Vertex.Clone().Sub(controlPoints[0].Vertex));
				var areaSq = cross.LengthSq();
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(controlPoints.ToArray(), editable.PointsAreLooping(), areaSq * 0.000001f);

				if (vVertex.Length > 0)
				{
					float width = 10.0f;
					Handles.color = UnityEngine.Color.blue;
					pathPoints.Clear();
					foreach (RenderVertex3D v in vVertex)
					{
						pathPoints.Add(new Vector3(v.X, v.Y, v.Z));
					}
					Handles.DrawAAPolyLine(width, pathPoints.ToArray());
				}
			}

		}
	}

	[CustomEditor(typeof(VPT.Ramp.RampBehavior))]
	public class RampDragPointsEditor : DragPointsEditor { }

	[CustomEditor(typeof(VPT.Rubber.RubberBehavior))]
	public class RubberDragPointsEditor : DragPointsEditor { }

	[CustomEditor(typeof(VPT.Surface.SurfaceBehavior))]
	public class SurfaceDragPointsEditor : DragPointsEditor { }

	[CustomEditor(typeof(VPT.Trigger.TriggerBehavior))]
	public class TriggerDragPointsEditor : DragPointsEditor { }
}
