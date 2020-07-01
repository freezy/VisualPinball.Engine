using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor.Handle
{
	/// An editable drag point in Unity's editor. <p/>
	///
	/// The inspector manages adding/removing control points and updates the
	/// data of IDragPointEditable.
	///
	/// ControlPoint also handles the ControlId used by Unity's Handles system
	/// and
	/// Controlpoint will keep the curve segment points starting from it
	public class ControlPoint
	{
		public const float ScreenRadius = 0.25f;

		/// <summary>
		/// Reference to the drag point data
		/// </summary>
		public DragPointData DragPoint;

		/// <summary>
		/// Points that render the curve in the scene view
		/// </summary>
		public List<Vector3> PathPoints = new List<Vector3>();

		public Vector3 WorldPos = Vector3.zero;
		public Vector3 ScrPos = Vector3.zero;
		public bool IsSelected = false;
		public readonly int ControlId;
		public readonly int Index;
		public readonly float IndexRatio;

		public ControlPoint(DragPointData dp, int controlId, int idx, float indexRatio)
		{
			DragPoint = dp;
			ControlId = controlId;
			Index = idx;
			IndexRatio = indexRatio;
		}
	}
}
