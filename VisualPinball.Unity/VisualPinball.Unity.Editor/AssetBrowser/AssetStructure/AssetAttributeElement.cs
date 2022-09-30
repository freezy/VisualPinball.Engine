// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The visual element for read-only presentation of an attribute. See also <see cref="AssetAttributePropertyDrawer"/>
	/// for write-enabled presentation.
	/// </summary>
	public class AssetAttributeElement : VisualElement
	{
		private readonly AssetAttribute _attribute;
		private AssetBrowser _browser;

		public AssetAttributeElement(AssetAttribute attribute)
		{
			_attribute = attribute;

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetAttributeElement.uss");
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
			var row = new VisualElement { name = "attribute-row" };
			row.Add(new Label(_attribute.Key));

			var values = new VisualElement { name = "attribute-values" };
			foreach (var attrText in _attribute.Value.Split(",").Select(s => s.Trim())) {
				var attr = new ToolbarToggle {
					text = attrText,
					value = _browser.Query.HasAttribute(_attribute.Key, attrText)
				};
				attr.RegisterValueChangedCallback(evt => OnToggle(_attribute.Key, attrText, evt.newValue));

				values.Add(attr);
			}
			row.Add(values);

			Add(row);
		}

		private void OnToggle(string key, string value, bool isToggled)
		{
			if (!isToggled) {
				_browser.FilterByAttribute(key, value, true);

			} else {
				_browser.FilterByAttribute(key, value);
			}
		}

	}
}
