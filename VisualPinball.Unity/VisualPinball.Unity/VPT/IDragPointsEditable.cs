using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Visual Pinball keeps the same data for drag points across all game
	/// items. However, not all properties are used in every game item. <p/>
	///
	/// This defines the data sets that can be used in a given game item, and
	/// thus how they are exposed in the editor.
	/// </summary>
	public enum DragPointExposure
	{
		/// <summary>
		/// A drag point can be set to "smooth".
		/// </summary>
		Smooth,

		/// <summary>
		/// A drag point can be set as "slingshot".
		/// </summary>
		SlingShot,

		/// <summary>
		/// A drag point can be set as "auto texture".
		/// </summary>
		Texture
	}

	/// <summary>
	/// Abstraction for authoring components that support drag points.
	/// </summary>
	public interface IDragPointsEditable
	{
		/// <summary>
		/// Toggled by the inspector while enabling/disabling edition.
		/// </summary>
		///
		/// <remarks>
		/// The goal of this sitting on the authoring component is because like
		/// that it's individually retained, whereas keeping it in the inspector
		/// would reset it every time the inspector gets destroyed.
		/// </remarks>
		bool DragPointEditEnabled { get; set; }

		/// <summary>
		/// Returns the game item's drag point data.
		/// </summary>
		DragPointData[] GetDragPoints();

		/// <summary>
		/// Updates the game item's drag points (used while adding/removing drag points).
		/// </summary>
		void SetDragPoints(DragPointData[] dragPoints);

		/// <summary>
		/// Returns the global offset applied on all drag points.
		/// </summary>
		Vector3 GetEditableOffset();

		/// <summary>
		/// Returns the height offset regarding a given position along the curve.
		/// </summary>
		/// <param name="ratio">Position on the curve, from 0.0 to 1.0.</param>
		/// <returns></returns>
		Vector3 GetDragPointOffset(float ratio);

		/// <summary>
		/// Returns whether the drag points are looping or not.
		/// </summary>
		bool PointsAreLooping();

		/// <summary>
		/// Returns exposed drag points features
		/// </summary>
		IEnumerable<DragPointExposure> GetDragPointExposition();

		/// <summary>
		/// Returns the applied constrains to drag points position edition.
		/// </summary>
		/// <returns></returns>
		ItemDataTransformType GetHandleType();
	}
}
