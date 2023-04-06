﻿// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using System.Collections.Generic;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Editor;

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
	/// Abstraction inspectors that support drag points.
	/// </summary>
	public interface IDragPointsInspector
	{
		/// <summary>
		/// Access to the drag point data
		/// </summary>
		DragPointData[] DragPoints { get; set; }

		/// <summary>
		/// Whether to render drag points at all. Because sometimes we don't want to, like for trigger buttons.
		/// </summary>
		bool DragPointsActive { get; }

		/// <summary>
		/// Returns whether the drag points are looping or not.
		/// </summary>
		bool PointsAreLooping { get; }

		/// <summary>
		/// Returns exposed drag points features
		/// </summary>
		IEnumerable<DragPointExposure> DragPointExposition { get; }

		/// <summary>
		/// Returns the applied constrains to drag points position edition.
		/// </summary>
		/// <returns></returns>
		ItemDataTransformType HandleType { get; }

		DragPointsInspectorHelper DragPointsHelper { get; }
	}
}
