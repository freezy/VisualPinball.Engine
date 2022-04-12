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
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What's rendered on the right panel.
	/// </summary>
	public class AssetDetailsElement : VisualElement
	{
		private readonly Label _noSelectionElement;
		private readonly VisualElement _detailsElement;
		private readonly Label _titleElement;

		public LibraryAsset Asset {
			get => _asset;
			set {
				if (_asset == value) {
					return;
				}
				// toggle empty label
				if (value != null && _asset == null) {
					_noSelectionElement.AddToClassList("hidden");
					_detailsElement.RemoveFromClassList("hidden");
				}
				if (value == null && _asset != null) {
					_noSelectionElement.RemoveFromClassList("hidden");
					_detailsElement.AddToClassList("hidden");
				}
				_asset = value;
				if (value != null) {
					UpdateDetails();
				}
			}
		}

		private LibraryAsset _asset;

		public new class UxmlFactory : UxmlFactory<AssetDetailsElement, UxmlTraits> { }

		public AssetDetailsElement()
		{
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetDetailsElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetDetailsElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_noSelectionElement = ui.Q<Label>("nothing-selected");
			_detailsElement = ui.Q<VisualElement>("details");
			_titleElement = ui.Q<Label>("title");
		}

		private void UpdateDetails()
		{
			var obj = _asset.LoadAsset();
			_titleElement.text = obj.name;
		}
	}
}
