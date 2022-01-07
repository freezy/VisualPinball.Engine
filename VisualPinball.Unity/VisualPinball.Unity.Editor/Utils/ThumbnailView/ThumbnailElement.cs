// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public enum EThumbnailSize
	{
		Small,
		Normal,
		Large
	}

	public abstract class ThumbnailElement
    {
		/// <summary>
		/// This Unique Id is automatically set & increment on each <see cref="ThumbnailElement"/> CTor so you don't need to worry about its unicity
		/// </summary>
		/// <remarks>
		/// Could be useful when constructing an array of TreeElemnt in a delegate for instance, without having to manage a local counter
		/// Of course, this Id can be overridden afterward
		/// </remarks>
		private static int UniqueId = math.abs(typeof(ThumbnailElement).GUID.ToString().GetHashCode());

		/// <summary>
		/// Unique ID of this <see cref="ThumbnailElement"/> within the <see cref="ThumbnailView"/> structure
		/// </summary>
		public int Id { get; set; }

		/// <summary>
		/// The element's name
		/// </summary>
		public abstract string Name { get; }

		/// <summary>
		/// Is the element selectable
		/// </summary>
		public bool Selectable { get; set; } = true;

		/// <summary>
		/// Draw the main thumbnail area for this element.
		/// </summary>
		/// <param name="rect">
		/// The rect provided by the calling <see cref="ThumbnailView"/>, the draw method has to adapt to this rect.
		/// </param>
		public abstract void OnGUI(Rect rect, GUIStyle style);

		/// <summary>
		/// Return the element width using the provided style.
		/// </summary>
		/// <param name="style">A style used to calcule the element width</param>
		/// <param name="thumbSize">A thumbnail size enum to calculate the element width</param>
		/// <returns>The element width</returns>
		public abstract float GetWidth(EThumbnailSize thumbSize, GUIStyle style);

		/// <summary>
		/// Returns the size of the hovering container for this element.
		/// </summary>
		/// <returns>The size of the hovering container in pixels</returns>
		public abstract Vector2 GetHoverContainerSize();

		/// <summary>
		/// Draw the hovering container for this element.
		/// </summary>
		/// <param name="rect">
		/// The rect provided by the calling <see cref="ThumbnailView"/>, the draw method has to adapt to this rect.
		/// </param>
		/// <remarks>
		/// The rect width & height has been evaluated by the <see cref="ThumbnailView"/> using <see cref="GetHoverContainerSize"/>.
		/// </remarks>
		public abstract void DrawHoverContainer(Rect rect);

		public struct Dimension
		{
			public Vector2 Offset;
			public float Height;
		}

		private static Dictionary<EThumbnailSize, Dimension> _commonDimensions = new Dictionary<EThumbnailSize, Dimension>() {
			{ EThumbnailSize.Small, new Dimension(){ Offset = Vector2.zero, Height = 50 } },
			{ EThumbnailSize.Normal, new Dimension(){ Offset = Vector2.zero, Height = 100 } },
			{ EThumbnailSize.Large, new Dimension(){ Offset = Vector2.zero, Height = 150 } }
		};

		public virtual Dictionary<EThumbnailSize, Dimension> CommonDimensions => _commonDimensions;


		public ThumbnailElement()
		{
			Id = UniqueId++;
		}

		public ThumbnailElement(int id)
		{
			Id = id;
		}

	}
}
