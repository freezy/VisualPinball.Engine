using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class PrefabThumbnailView : ThumbnailView<PrefabThumbnailElement>
	{
		public GameObject SelectedPrefab => SelectedItem?.Prefab;

		public PrefabThumbnailView(IEnumerable<PrefabThumbnailElement> data) : base(data) { }

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

		protected override void OnGUIToolbar() 
		{
		}

		protected override bool MatchLabelFilter(PrefabThumbnailElement item, string labelFilter)
		{
			var labels = AssetDatabase.GetLabels(item.Prefab);
			return labels.Any(L => L.Contains(labelFilter, StringComparison.InvariantCultureIgnoreCase));
		}

	}
}
