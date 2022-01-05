using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class AssetsThumbnailView : ThumbnailView<AssetThumbnailElement>
	{
		public Object SelectedAsset => SelectedItem?.Asset;

		public AssetsThumbnailView(IEnumerable<AssetThumbnailElement> data) : base(data) { }

		protected override void InitCommonStyles()
		{
			if (!_commonStyles.Inited) {
				_commonStyles.DefaultStyle = new GUIStyle("button");
				_commonStyles.SelectedStyle = new GUIStyle(_commonStyles.DefaultStyle);
				_commonStyles.SelectedStyle.active.background = TextureExtensions.CreatePixelTexture(new Color(0.5f, .5f, .8f));
				_commonStyles.SelectedStyle.normal = _commonStyles.SelectedStyle.active;
				_commonStyles.SelectedStyle.hover = _commonStyles.SelectedStyle.active;
				_commonStyles.SelectedStyle.focused = _commonStyles.SelectedStyle.active;
				_commonStyles.NameStyle = new GUIStyle("label");
				_commonStyles.HoverStyle = new GUIStyle();
				_commonStyles.HoverStyle.normal.background = TextureExtensions.CreatePixelTexture(new Color(.25f, .25f, .25f));
			}
		}

		protected override void OnGUIToolbarBegin() 
		{

		}

		protected override void OnGUIToolbarEnd()
		{
		}

		protected override bool MatchLabelFilter(AssetThumbnailElement item, string labelFilter)
		{
			var labels = AssetDatabase.GetLabels(item.Asset);
			return labels.Any(L => L.Contains(labelFilter, StringComparison.InvariantCultureIgnoreCase));
		}

	}
}
