using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.VPT
{
	// DragPointExposition
	// Exposed drag points feature, will enable/disable some DragPointItemInspector expositions & management
	// Will also change some rendering on the CentralCurve (Slingshot segments)
	public enum DragPointExposition
	{
		Smooth,		
		SlingShot,		
		Texture,		// expose AutoTexure and texture coords.
	}

	// IDragPointsEditable interface has to be implemented by ItemBehaviors which needs some drag-points edition support
	public interface IDragPointsEditable
	{
		bool DragPointEditEnabled { get; set; }			// switched by the DragPointsItemInspector while enabling/disabling edition.
		DragPointData[] GetDragPoints();				// returns internal drag-points data to the inspector (these DragPointData will be directly accessed by the inspector).
		void SetDragPoints(DragPointData[] dpoints);	// update internal drag-points data with a new set from the inspector (used while adding/removing drag-points).
		Vector3 GetEditableOffset();					// returns a global offset applyied on all drag-points.
		Vector3 GetDragPointOffset(float ratio);		// returns a per drag-point offset regarding the ratio along the curve.
		bool PointsAreLooping();						//	tells the inspector if the drag-points are looping or not.
		DragPointExposition[] GetDragPointExposition();	//	returns exposed drag-points features (see DragPointExposition enum)
		ItemDataTransformType GetHandleType();			// returns the applied constrains to drag-points position edition.
	}
}
