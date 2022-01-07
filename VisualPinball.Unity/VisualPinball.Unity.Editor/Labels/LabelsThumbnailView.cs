using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class LabelsThumbnailView : ThumbnailView<LabelThumbnailElement>
	{
		GUIStyle _labelButtonStyle = null;

		public LabelsThumbnailView(IEnumerable<LabelThumbnailElement> data) : base(data) 
		{
			ShowToolbar = false;
		}

		protected override void InitCommonStyles()
		{
			if (!_commonStyles.Inited) {
				_commonStyles.DefaultStyle = new GUIStyle(GUI.skin.FindStyle("AssetLabel"));
				_commonStyles.SelectedStyle = new GUIStyle(_commonStyles.DefaultStyle);
				_commonStyles.SelectedStyle.active.textColor = Color.white;
				_commonStyles.SelectedStyle.normal = _commonStyles.SelectedStyle.active;
				_commonStyles.SelectedStyle.hover = _commonStyles.SelectedStyle.active;
				_commonStyles.SelectedStyle.focused = _commonStyles.SelectedStyle.active;
				_commonStyles.NameStyle = new GUIStyle("label");
				_commonStyles.HoverStyle = new GUIStyle();
				_commonStyles.HoverStyle.normal.background = TextureExtensions.CreatePixelTexture(new Color(.25f, .25f, .25f));
			}
		}

	}
}
