using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.Extensions;
using VisualPinball.Engine.Math;


namespace VisualPinball.Unity.Editor.DragPoints
{
	public class DragPointsEditor
	{
		List<DragPointData> controlPoints = new List<DragPointData>();

        List<Vector3> pathPoints = new List<Vector3>();

		public void OnSceneGUI(Object target)
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || dpeditable == null)
				return;

			controlPoints.Clear();

			Vector3 offset = dpeditable.GetEditableOffset();
			Matrix4x4 lwMat = bh.transform.localToWorldMatrix;
			Matrix4x4 wlMat = bh.transform.worldToLocalMatrix;

			foreach (var dpoint in dpeditable.GetDragPoints())
			{
				Vector3 dpos = dpoint.Vertex.ToUnityVector3();
				dpos.z += dpoint.CalcHeight;
				dpos += offset;
				dpos = lwMat.MultiplyPoint(dpos);
				EditorGUI.BeginChangeCheck();
				dpos = Handles.PositionHandle(dpos, Quaternion.identity);
				DragPointData transformedData = new DragPointData(dpoint);
				transformedData.Vertex = dpos.ToVertex3D();
				controlPoints.Add(transformedData);
				if (EditorGUI.EndChangeCheck())
				{
					//Set Meshdirty to true there so it'll trigger again after Undo
					if (editable != null)
						editable.MeshDirty = true;
					Undo.RecordObject(bh, "Change DragPoint Position to " + dpos.ToString());
					dpos = wlMat.MultiplyPoint(dpos);
					dpos -= offset;
					dpos.z -= dpoint.CalcHeight;
					dpoint.Vertex = dpos.ToVertex3D();
				}
			}

			if (controlPoints.Count > 3)
			{
				var cross = controlPoints[1].Vertex.Clone().Sub(controlPoints[0].Vertex).Cross(controlPoints[2].Vertex.Clone().Sub(controlPoints[0].Vertex));
				var areaSq = cross.LengthSq();
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(controlPoints.ToArray(), dpeditable.PointsAreLooping(), areaSq * 0.000001f);

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
}
