// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// An editable drag point in Unity's editor. <p/>
	///
	/// The inspector manages adding/removing control points and updates the
	/// data of IDragPointEditable.
	///
	/// ControlPoint also manages the ControlId used by Unity's Handles system
	/// and the points that make the path in the scene view.
	/// </summary>
	public class ControlPoint
	{
		public const float ScreenRadius = 0.25f;

		/// <summary>
		/// Reference to the drag point data
		/// </summary>
		public DragPointData DragPoint;

		/// <summary>
		/// Position in world space
		/// </summary>
		public Vector3 WorldPos = Vector3.zero;

		/// <summary>
		/// Position in local space
		/// </summary>
		public Vector3 ScrPos = Vector3.zero;

		/// <summary>
		/// Currently selected or not?
		/// </summary>
		public bool IsSelected = false;

		/// <summary>
		/// Unity's <a href="https://docs.unity3d.com/ScriptReference/GUIUtility.GetControlID.html">ControlID</a>.
		/// </summary>
		public readonly int ControlId;

		/// <summary>
		/// Index of the drag point within the game item's drag point array.
		/// </summary>
		public readonly int Index;

		/// <summary>
		/// Relative position on the curve, from 0.0 to 1.0.
		/// </summary>
		public readonly float IndexRatio;

		public ControlPoint(DragPointData dp, int controlId, int idx, float indexRatio)
		{
			DragPoint = dp;
			ControlId = controlId;
			Index = idx;
			IndexRatio = indexRatio;
		}

		public void UpdateDragPoint(IDragPointsEditable editable, Transform transform)
		{
			var dragpointPos = transform.worldToLocalMatrix.MultiplyPoint(WorldPos);
			dragpointPos -= editable.GetEditableOffset();
			dragpointPos -= editable.GetDragPointOffset(IndexRatio);
			DragPoint.Center = dragpointPos.ToVertex3D();
		}
	}
}
