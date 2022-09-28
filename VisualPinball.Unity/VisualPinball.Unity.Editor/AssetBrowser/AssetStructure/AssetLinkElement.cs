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

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The visual element for read-only presentation of a link. See also <see cref="AssetLinkPropertyDrawer"/>
	/// for write-enabled presentation.
	/// </summary>
	public class AssetLinkElement : VisualElement
	{
		public AssetLinkElement(AssetLink link)
		{
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetLinkElement.uss");
			styleSheets.Add(styleSheet);

			var linkEl = new Label(link.Name);
			linkEl.RegisterCallback<MouseDownEvent>(ev => OpenLink(link.Url));

			Add(linkEl);
		}

		private static void OpenLink(string link) => Application.OpenURL(link);

	}
}
