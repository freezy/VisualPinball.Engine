using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.VPT
{
	public enum DragPointExposition
	{
		None = 0,
		Smooth = 1,
		SlingShot = 2,
		Texture = 4,
	}

	public interface IDragPointsEditable
	{
		bool DragPointEditEnabled { get; set; }
		DragPointData[] GetDragPoints();
		void SetDragPoints(DragPointData[] dpoints);
		Vector3 GetEditableOffset();
		Vector3 GetDragPointOffset(float ratio);
		bool PointsAreLooping();
		DragPointExposition GetDragPointExposition();
	}
}
