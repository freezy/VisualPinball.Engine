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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class AssetsThumbnailView : ThumbnailView<AssetThumbnailElement>
	{
		public Object SelectedAsset => SelectedItem?.Asset;

		public AssetsThumbnailView(IEnumerable<AssetThumbnailElement> data) : base(data) { }

		public LabelsHandler LabelsHandler
		{
			set {
				_filteredLabelsView.LabelsHandler = value;
			}
		}

		private LabelsThumbnailView _filteredLabelsView = new LabelsThumbnailView(null);

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

		protected override bool MatchLabelFilter(AssetThumbnailElement item, string labelFilter)
		{
			var labels = AssetDatabase.GetLabels(item.Asset);
			return labels.Any(L => L.Contains(labelFilter, StringComparison.InvariantCultureIgnoreCase));
		}

		protected override bool MatchTypeFilter(AssetThumbnailElement item, string typeFilter)
		{
			var prefabType = PrefabUtility.GetPrefabAssetType(item.Asset);
			if (prefabType != PrefabAssetType.NotAPrefab && typeFilter.Equals("prefab", StringComparison.InvariantCultureIgnoreCase)) {
				return true;
			} 
			return (item.Asset.GetType().Name.Contains(typeFilter, StringComparison.InvariantCultureIgnoreCase));
		}

		protected override void OnGUIToolbarEnd(Rect r)
		{
			base.OnGUIToolbarEnd(r);

		}
	}
}
