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

using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A non-generic interface for DragPointsItemInspector we can use for all
	/// types of items.
	/// </summary>
	public interface IDragPointsItemInspector
	{
		/// <summary>
		/// Catmull Curve Handler
		/// </summary>
		DragPointsHandler DragPointsHandler { get; }

		/// <summary>
		/// Returns a reference to the drag point data for a given control ID.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point</param>
		/// <returns>Drag point data or null if no linked data.</returns>
		DragPointData GetDragPoint(int controlId);

		/// <summary>
		/// Sets an UNDO point before the next operation.
		/// </summary>
		/// <param name="message">Message to appear in the UNDO menu</param>
		void PrepareUndo(string message);

		/// <summary>
		/// Returns true if the game item is locked.
		/// </summary>
		/// <returns>True if game item is locked, false otherwise.</returns>
		bool IsItemLocked();

		/// <summary>
		/// Returns whether this game item has a given drag point exposure.
		/// </summary>
		/// <param name="exposure">Exposure to check</param>
		/// <returns>True if exposed, false otherwise.</returns>
		bool HasDragPointExposure(DragPointExposure exposure);

		/// <summary>
		/// Removes a drag point of a given control ID.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point to remove.</param>
		void RemoveDragPoint(int controlId);

		/// <summary>
		/// Copies the position of a drag point.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point</param>
		void CopyDragPoint(int controlId);

		/// <summary>
		/// Sets the position of a previously copied drag point to another drag point.
		/// </summary>
		/// <param name="controlId">Control ID of the drag point to which the new position is applied.</param>
		void PasteDragPoint(int controlId);

		/// <summary>
		/// Adds a new drag point at the traveller's current position.
		/// </summary>
		void AddDragPointOnTraveller();

		/// <summary>
		/// Flips all drag points on a given axis.
		/// </summary>
		/// <param name="flipAxis">Axis to flip on</param>
		void FlipDragPoints(FlipAxis flipAxis);
	}
}
