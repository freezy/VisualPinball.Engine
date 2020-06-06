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
		private List<DragPointData> _controlPoints = new List<DragPointData>();
		private List<Vector3> _pathPoints = new List<Vector3>();

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

		public void OnSceneGUI(Object target)
		{
			IEditableItemBehavior editable = target as IEditableItemBehavior;
			IDragPointsEditable dpeditable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || dpeditable == null || !dpeditable.DragPointEditEnabled)
				return;

			_controlPoints.Clear();

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
				_controlPoints.Add(transformedData);
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

			if (_controlPoints.Count > 3)
			{
				var cross = _controlPoints[1].Vertex.Clone().Sub(_controlPoints[0].Vertex).Cross(_controlPoints[2].Vertex.Clone().Sub(_controlPoints[0].Vertex));
				var areaSq = cross.LengthSq();
				var vVertex = DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(_controlPoints.ToArray(), dpeditable.PointsAreLooping(), areaSq * 0.000001f);

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
			}

		}
	}
}
