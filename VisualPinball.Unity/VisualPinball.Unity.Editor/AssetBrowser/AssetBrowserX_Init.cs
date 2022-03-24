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
		private ToolbarButton _refreshButton;
		private ToolbarSearchField _queryInput;

		private LibraryCategoryView _categoryView;
		private VisualElement _libraryList;
		private DropdownField _activeLibraryDropdown;

		private VisualElement _gridContent;
		private Label _bottomLabel;
		private Slider _sizeSlider;

		private static readonly Dictionary<string, Type> Types = new();
		private VisualTreeAsset _itemTree;

		public void CreateGUI()
		{
			// import UXML
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uxml");
			visualTree.CloneTree(rootVisualElement);
			_itemTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserItem.uxml");

			var ui = rootVisualElement;

			// import style sheet
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uss");
			ui.styleSheets.Add(styleSheet);

			_libraryList = ui.Q<VisualElement>("libraryList");
			_activeLibraryDropdown = ui.Q<DropdownField>("activeLibrary");
			_categoryView = ui.Q<LibraryCategoryView>();
			_gridContent = ui.Q<VisualElement>("gridContent");

			_bottomLabel = ui.Q<Label>("bottomLabel");
			_sizeSlider = ui.Q<Slider>("sizeSlider");
			_sizeSlider.value = _thumbnailSize;
			_sizeSlider.RegisterValueChangedCallback(OnThumbSizeChanged);

			_refreshButton = ui.Q<ToolbarButton>("refreshButton");
			_refreshButton.clicked += Setup;

			_queryInput = ui.Q<ToolbarSearchField>("queryInput");
			_queryInput.RegisterValueChangedCallback(OnSearchQueryChanged);

			_gridContent.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_gridContent.RegisterCallback<DragPerformEvent>(OnDragPerformEvent);
		}

		private void OnDestroy()
		{
			_sizeSlider.UnregisterValueChangedCallback(OnThumbSizeChanged);
			_queryInput.UnregisterValueChangedCallback(OnSearchQueryChanged);

			_gridContent.UnregisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_gridContent.UnregisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_refreshButton.clicked -= Setup;

			foreach (var assetLibrary in _libraries) {
				assetLibrary.Dispose();
			}
		}

		private VisualElement NewItem(Texture image, string label)
		{
			var item = new VisualElement();
			_itemTree.CloneTree(item);
			item.Q<Image>("thumbnail").image = image;
			item.Q<Label>("label").text = label;
			item.RegisterCallback<MouseUpEvent>(_ => OnItemClicked(item));
			return item;
		}

		private VisualElement NewAssetLibrary(AssetLibrary lib)
		{
			var toggle = new Toggle(lib.Name);
			var item = new VisualElement();
			item.AddToClassList("library-item");
			item.style.flexDirection = FlexDirection.Row;
			item.Add(toggle);

			toggle.value = true;
			toggle.RegisterValueChangedCallback(evt => OnLibraryToggled(lib, evt.newValue));
			return item;
		}

		private static Type TypeByName(string name)
		{
			if (Types.ContainsKey(name)) {
				return Types[name];
			}
			Types[name] = AppDomain.CurrentDomain.GetAssemblies().Reverse().Select(assembly => assembly.GetType(name)).FirstOrDefault(tt => tt != null);
			return Types[name];
		}
	}
}
