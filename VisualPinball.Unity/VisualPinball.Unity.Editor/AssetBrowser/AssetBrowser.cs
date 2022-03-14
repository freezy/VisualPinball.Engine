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
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class AssetBrowser : EditorWindow
	{
		private VisualElement _rightPane;

		[SerializeField]
		private int selectedIndex = -1;


		[MenuItem("Visual Pinball/Asset Browser")]
		public static void Init()
		{
			var wnd = GetWindow<AssetBrowser>("Asset Browser");

			// Limit size of the window
			wnd.minSize = new Vector2(450, 200);
			wnd.maxSize = new Vector2(1920, 720);
		}

		public void CreateGUI()
		{
			// Get a list of all sprites in the project
			var allObjectGuids = AssetDatabase.FindAssets("t:Texture", new [] {
				"Packages/org.visualpinball.unity.assetlibrary/Assets"
			});

			var allObjects = allObjectGuids.Select(guid => AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(guid))).ToList();

			// Create a two-pane view with the left pane being fixed with
			var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);

			// Add the view to the visual tree by adding it as a child to the root element
			rootVisualElement.Add(splitView);

			// A TwoPaneSplitView always needs exactly two child elements
			var leftPane = new ListView();
			splitView.Add(leftPane);

			_rightPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
			splitView.Add(_rightPane);

			// Initialize the list view with all sprites' names
			leftPane.makeItem = () => new Label();
			leftPane.bindItem = (item, index) => { (item as Label)!.text = allObjects[index].name; };
			leftPane.itemsSource = allObjects;
			leftPane.onSelectionChange += OnSpriteSelectionChange;
			leftPane.onSelectionChange += _ => { selectedIndex = leftPane.selectedIndex; };
			leftPane.selectedIndex = selectedIndex;
		}

		private void OnSpriteSelectionChange(IEnumerable<object> selectedItems)
		{
			// Clear all previous content from the pane
			_rightPane.Clear();

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
			_rightPane.Add(spriteImage);
		}
	}
}
