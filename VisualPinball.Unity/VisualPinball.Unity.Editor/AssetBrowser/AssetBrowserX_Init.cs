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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowserX
	{
		public void CreateGUI()
		{
			// import UXML
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uxml");
			visualTree.CloneTree(rootVisualElement);

			var ui = rootVisualElement;

			// import style sheet
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uss");
			ui.styleSheets.Add(styleSheet);

			InitLeftPane(ui.Q<ListView>("leftPane"));
			_rightPane = ui.Q<ScrollView>("rightPane");

			_refreshButton = ui.Q<ToolbarButton>("refreshButton");
			Debug.Log("Got refresh button: " + _refreshButton);
			_refreshButton.clicked += Refresh;
		}


		private void OnDestroy()
		{
			_refreshButton.clicked -= Refresh;
		}

		private void InitLeftPane(BaseVerticalCollectionView leftPane)
		{
			var allObjectGuids = AssetDatabase.FindAssets("t:Texture", new [] {
				"Packages/org.visualpinball.unity.assetlibrary/Assets"
			});
			var allObjects = allObjectGuids.Select(guid => AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(guid))).ToList();

			leftPane.makeItem = () => new Label();
			leftPane.bindItem = (item, index) => {
				(item as Label)!.text = allObjects[index].name;
			};
			leftPane.itemsSource = allObjects;
			leftPane.onSelectionChange += OnSpriteSelectionChange;
			leftPane.onSelectionChange += _ => {
				selectedIndex = leftPane.selectedIndex;
			};
			leftPane.selectedIndex = selectedIndex;

			leftPane.RefreshItems();
		}

		private void OnSpriteSelectionChange(IEnumerable<object> selectedItems)
		{
			// Clear all previous content from the pane
			_rightPane?.Clear();

			// Get the selected sprite
			var selectedTexture = selectedItems.First() as Texture;
			if (selectedTexture == null) {
				return;
			}

			// Add a new Image control and display the sprite
			var spriteImage = new Image {
				scaleMode = ScaleMode.ScaleToFit,
				image = selectedTexture
			};

			// Add the Image control to the right-hand pane
			_rightPane?.Add(spriteImage);
		}
	}
}
