// Visual Pinball Engine
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

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The visual element for read-only presentation of a tag. See also <see cref="AssetTagPropertyDrawer"/>
	/// for write-enabled presentation.
	/// </summary>
	public class AssetTagElement : VisualElement
	{
		private readonly AssetTag _tag;
		private AssetBrowser _browser;

		public AssetTagElement(AssetTag tag)
		{
			_tag = tag;

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetTagElement.uss");
			styleSheets.Add(styleSheet);

			RegisterCallback<AttachToPanelEvent>(OnAttached);
		}

		private void OnAttached(AttachToPanelEvent evt)
		{
			_browser = panel.visualTree.userData as AssetBrowser;
			Bind();
		}

		private void Bind()
		{
			var tag = new ToolbarToggle {
				text = _tag.TagName,
				value = _browser != null && _browser.Query.HasTag(_tag.TagName)
			};
			tag.RegisterValueChangedCallback(evt => OnToggle(_tag.TagName, evt.newValue));

			Add(tag);
		}

		private void OnToggle(string tagName, bool isToggled)
		{
			if (!isToggled) {
				_browser.FilterByTag(tagName, true);

			} else {
				_browser.FilterByTag(tagName);
			}
		}
	}
}
