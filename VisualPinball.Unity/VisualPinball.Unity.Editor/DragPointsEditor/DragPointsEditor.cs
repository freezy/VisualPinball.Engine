using UnityEngine;
using UnityEditor;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.Extensions;
using System.Collections.Generic;

namespace VisualPinball.Unity.Editor.DragPoints
{
	public abstract class DragPointsEditor : UnityEditor.Editor
	{
		List<Vector3> dpoints = new List<Vector3>();

		public virtual void OnSceneGUI()
		{
			IDragPointsEditable editable = target as IDragPointsEditable;
			Behaviour bh = target as Behaviour;

			if (bh == null || editable == null)
				return;

			dpoints.Clear();

			Vector3 offset = editable.GetEditableOffset();

			foreach (var dpoint in editable.GetDragPoints())
			{
				Vector3 dpos = MathExtensions.ToUnityVector3(dpoint.Vertex);
				dpos.z += dpoint.CalcHeight;
				dpos += offset;
				dpos = bh.transform.localToWorldMatrix.MultiplyPoint(dpos);
				dpos = Handles.PositionHandle(dpos, Quaternion.identity);
				dpoints.Add(dpos);
				dpos = bh.transform.worldToLocalMatrix.MultiplyPoint(dpos);
				dpos -= offset;
				dpos.z -= dpoint.CalcHeight;
				dpoint.Vertex = MathExtensions.FromUnityVector3(dpos);
			}
		}
	}

	[CustomEditor(typeof(VPT.Ramp.RampBehavior))]
	public class RampDragPointsEditor : DragPointsEditor { }

	[CustomEditor(typeof(VPT.Rubber.RubberBehavior))]
	public class RubberDragPointsEditor : DragPointsEditor { }

	[CustomEditor(typeof(VPT.Trigger.TriggerBehavior))]
	public class TriggerDragPointsEditor : DragPointsEditor { }
}
