using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.VPT
{
	public interface IDragPointsEditable
	{
		DragPointData[] GetDragPoints();
		Vector3 GetEditableOffset();
	}
}
