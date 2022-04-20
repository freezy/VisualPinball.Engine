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

using System.Collections.Generic;
using System.IO;
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
		private AssetDetailsElement _detailsElement;
		private Label _bottomLabel;
		private Slider _sizeSlider;

		private VisualTreeAsset _assetTree;

		/// <summary>
		/// Setup the UI. Data is already set up at this point. We'll just trigger a refresh once the UI is set up.
		/// </summary>
		public void CreateGUI()
		{
			Debug.Log("CREATING ASSET BROWSER GUI...");

			// import UXML
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uxml");
			visualTree.CloneTree(rootVisualElement);
			_assetTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAssetElement.uxml");

			var ui = rootVisualElement;

			// import style sheet
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uss");
			ui.styleSheets.Add(styleSheet);

			// libraries
			_libraryList = ui.Q<VisualElement>("libraryList");

			// active library dropdown
			var activeLibraryContainer = ui.Q<VisualElement>("activeLibrary");
			_activeLibraryDropdown = new DropdownField(new List<string> { "none" }, 0, OnActiveLibraryChanged) {
				tooltip = "The library that is currently being edited."
			};
			activeLibraryContainer.Add(_activeLibraryDropdown);

			_categoryView = ui.Q<LibraryCategoryView>();
			_gridContent = ui.Q<VisualElement>("gridContent");
			_detailsElement = ui.Q<AssetDetailsElement>();

			_bottomLabel = ui.Q<Label>("bottomLabel");
			_sizeSlider = ui.Q<Slider>("sizeSlider");
			_sizeSlider.value = _thumbnailSize;
			_sizeSlider.RegisterValueChangedCallback(OnThumbSizeChanged);

			_refreshButton = ui.Q<ToolbarButton>("refreshButton");
			_refreshButton.clicked += Refresh;

			_queryInput = ui.Q<ToolbarSearchField>("queryInput");
			_queryInput.RegisterValueChangedCallback(OnSearchQueryChanged);

			_gridContent.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_gridContent.RegisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_gridContent.RegisterCallback<PointerUpEvent>(evt => OnEmptyClicked(evt));

			Refresh();
		}

		private void OnDestroy()
		{
			_sizeSlider.UnregisterValueChangedCallback(OnThumbSizeChanged);
			_queryInput.UnregisterValueChangedCallback(OnSearchQueryChanged);

			_gridContent.UnregisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_gridContent.UnregisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_refreshButton.clicked -= Refresh;

			foreach (var assetLibrary in Libraries) {
				assetLibrary.Dispose();
			}

			Debug.Log("ASSET BROWSER UNLOADED.");
		}

		private VisualElement NewItem(AssetData data)
		{
			var obj = AssetDatabase.LoadAssetAtPath(data.Asset.Path, AssetLibrary.TypeByName(data.Asset.Type));
			var tex = AssetPreview.GetAssetPreview(obj);
			var item = new VisualElement();
			_assetTree.CloneTree(item);
			item.Q<LibraryAssetElement>().Data = data;
			item.Q<Image>("thumbnail").image = tex;
			item.Q<Label>("label").text = Path.GetFileNameWithoutExtension(data.Asset.Path);
			item.RegisterCallback<MouseUpEvent>(evt => OnItemClicked(evt, item));
			item.Q<LibraryAssetElement>().RegisterDrag(this);
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
	}
}
