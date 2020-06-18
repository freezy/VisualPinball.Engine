using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.VPT
{
	public interface IDragPointsEditable
	{
		bool DragPointEditEnabled { get; set; }
		DragPointData[] GetDragPoints();
		void SetDragPoints(DragPointData[] dpoints);
		Vector3 GetEditableOffset();
		Vector3 GetDragPointOffset(float ratio);
		bool PointsAreLooping();
	}
}
