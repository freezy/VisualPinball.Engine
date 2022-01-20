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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class LabelThumbnailElement : ThumbnailElement
	{
		private PinballLabel _label = null;

		private static Dictionary<EThumbnailSize, Dimension> _commonDimensions = new Dictionary<EThumbnailSize, Dimension>() {
			{ EThumbnailSize.Small, new Dimension(){ Offset = new Vector2(3.0f, 3.0f), Height = GUI.skin.label.lineHeight * 0.75f } },
			{ EThumbnailSize.Normal, new Dimension(){ Offset = new Vector2(3.0f, 3.0f), Height = GUI.skin.label.lineHeight } },
			{ EThumbnailSize.Large, new Dimension(){ Offset = new Vector2(3.0f, 3.0f), Height = GUI.skin.label.lineHeight * 1.5f } },
		};

		public override Dictionary<EThumbnailSize, Dimension> CommonDimensions => _commonDimensions;

		public LabelThumbnailElement(PinballLabel label) : base()
		{
			_label = label;
		}

		public override string Name => string.Empty;

		public override void DrawHoverContainer(Rect rect)
		{
		}

		public override Vector2 GetHoverContainerSize() => Vector2.zero;

		public override float GetWidth(EThumbnailSize thumbSize, GUIStyle style)
		{
			return style.CalcSize(new GUIContent(_label.FullLabel)).x + style.padding.horizontal;
		}

		public override void OnGUI(Rect rect, GUIStyle style)
		{
			GUI.Label(rect, _label.FullLabel, style);
		}
	}
}
