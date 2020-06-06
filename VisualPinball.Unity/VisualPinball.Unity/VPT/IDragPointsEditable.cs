using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.VPT
{
	public interface IDragPointsEditable
	{
		bool DragPointEditEnabled { get; set; }
		DragPointData[] GetDragPoints();
		Vector3 GetEditableOffset();
		bool PointsAreLooping();
	}
}
