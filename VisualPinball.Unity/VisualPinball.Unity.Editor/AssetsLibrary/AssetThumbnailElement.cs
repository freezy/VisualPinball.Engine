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
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A <see cref="ThumbnailElement"/> dedicated for displaying assets
	/// </summary>
	public class AssetThumbnailElement : ThumbnailElement
	{
		private Object _asset = null;
		public Object Asset => _asset;

		private static Dictionary<EThumbnailSize, Dimension> _commonDimensions = new Dictionary<EThumbnailSize, Dimension>() {
			{ EThumbnailSize.Small, new Dimension(){ Offset = new Vector2(3.0f, 3.0f), Height = 64 } },
			{ EThumbnailSize.Normal, new Dimension(){ Offset = new Vector2(3.0f, 3.0f), Height = 128 } },
			{ EThumbnailSize.Large, new Dimension(){ Offset = new Vector2(3.0f, 3.0f), Height = 196 } },
		};

		public override Dictionary<EThumbnailSize, Dimension> CommonDimensions => _commonDimensions;

		public AssetThumbnailElement(Object asset) : base()
		{
			_asset = asset;
		}

		public override string Name => _asset?.name;

		public override float GetWidth(EThumbnailSize thumbSize, GUIStyle style) => CommonDimensions[thumbSize].Height;

		public override void OnGUI(Rect rect, GUIStyle style)
		{
			var boxRect = new Rect(rect.x, rect.y, rect.width, rect.height);
			var assetPreview = AssetPreview.GetAssetPreview(_asset);
			GUI.Box(boxRect, new GUIContent(), style);
			boxRect.x += style.border.left;
			boxRect.y += style.border.top;
			boxRect.width -= style.border.horizontal;
			boxRect.height -= style.border.vertical;
			GUI.DrawTexture(boxRect, assetPreview, ScaleMode.StretchToFill);
		}

		public override void DrawHoverContainer(Rect rect)
		{
		}

		public override Vector2 GetHoverContainerSize()
		{
			return new Vector2(100, 100);
		}

	}
}
