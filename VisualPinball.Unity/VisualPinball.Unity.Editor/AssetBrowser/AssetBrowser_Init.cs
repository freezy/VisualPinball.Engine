﻿// Visual Pinball Engine
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

using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowser
	{
		private ToolbarButton _refreshButton;
		private ToolbarSearchField _queryInput;

		private LibraryCategoryView _categoryView;
		private VisualElement _libraryList;
		private Label _noLibrariesLabel;

		private VisualElement _gridContent;
		private Label _dragErrorLabelLeft;
		private VisualElement _dragErrorContainerLeft;
		private Label _dragErrorLabel;
		private VisualElement _dragErrorContainer;
		private AssetDetailsElement _detailsElement;
		private Label _statusLabel;
		private Slider _sizeSlider;

		private VisualTreeAsset _assetTree;

		public string DragErrorLeft {
			get => _dragErrorContainerLeft.ClassListContains("hidden") ? null : _dragErrorLabelLeft.text;
			set {
				if (value == null) {
					if (!_dragErrorContainerLeft.ClassListContains("hidden")) {
						_dragErrorContainerLeft.AddToClassList("hidden");
					}
					return;
				}

				_dragErrorContainerLeft.RemoveFromClassList("hidden");
				_dragErrorLabelLeft.text = value;
			}
		}

		private string DragError {
			get => _dragErrorContainer.ClassListContains("hidden") ? null : _dragErrorLabel.text;
			set {
				if (value == null) {
					if (!_dragErrorContainer.ClassListContains("hidden")) {
						_dragErrorContainer.AddToClassList("hidden");
					}
					return;
				}

				_dragErrorContainer.RemoveFromClassList("hidden");
				_dragErrorLabel.text = value;
			}
		}

		/// <summary>
		/// Setup the UI. Data is already set up at this point. We'll just trigger a refresh once the UI is set up.
		/// </summary>
		public void CreateGUI()
		{
			// import UXML
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowser.uxml");
			visualTree.CloneTree(rootVisualElement);
			_assetTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAssetElement.uxml");

			var ui = rootVisualElement;

			// import style sheet
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowser.uss");
			ui.styleSheets.Add(styleSheet);

			// libraries
			_libraryList = ui.Q<VisualElement>("libraryList");
			_noLibrariesLabel = ui.Q<Label>("noLibraries");

			_categoryView = ui.Q<LibraryCategoryView>();
			_gridContent = ui.Q<VisualElement>("gridContent");
			_detailsElement = ui.Q<AssetDetailsElement>();

			_statusLabel = ui.Q<Label>("bottomLabel");
			_sizeSlider = ui.Q<Slider>("sizeSlider");
			_sizeSlider.value = _thumbnailSize;
			_sizeSlider.RegisterValueChangedCallback(OnThumbSizeChanged);

			_refreshButton = ui.Q<ToolbarButton>("refreshButton");
			_refreshButton.clicked += Refresh;

			_queryInput = ui.Q<ToolbarSearchField>("queryInput");
			_queryInput.RegisterValueChangedCallback(OnSearchQueryChanged);

			_dragErrorContainer = ui.Q<VisualElement>("dragErrorContainer");
			_dragErrorLabel = ui.Q<Label>("dragError");

			_dragErrorContainerLeft = ui.Q<VisualElement>("dragErrorContainerLeft");
			_dragErrorLabelLeft = ui.Q<Label>("dragErrorLeft");

			_gridContent.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_gridContent.RegisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_gridContent.RegisterCallback<DragEnterEvent>(OnDragEnterEvent);
			_gridContent.RegisterCallback<DragLeaveEvent>(OnDragLeaveEvent);
			_gridContent.RegisterCallback<PointerUpEvent>(OnEmptyClicked);

			ui.panel.visualTree.userData = this; // children need access to this. if there's another way of getting the panel's owner object, let me know!

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
				assetLibrary.OnChange -= OnLibraryChanged;
			}
		}

		private VisualElement NewItem(AssetResult result)
		{
			var obj = result.Asset.Object;
			var tex = AssetPreview.GetAssetPreview(obj);
			var item = new VisualElement();
			_assetTree.CloneTree(item);
			item.Q<LibraryAssetElement>().Result = result;
			item.Q<Image>("thumbnail").image = tex;
			item.Q<Label>("label").text = result.Asset.Name;
			item.RegisterCallback<MouseUpEvent>(evt => OnAssetClicked(evt, item));
			item.Q<LibraryAssetElement>().RegisterDrag(this);
			item.AddManipulator(new ContextualMenuManipulator(AddAssetContextMenu));
			return item;
		}

		private VisualElement NewAssetLibrary(AssetLibrary lib)
		{
			var item = new VisualElement();
			var toggle = new Toggle();
			var label = new Label(lib.Name);

			item.AddToClassList("library-item");
			item.style.flexDirection = FlexDirection.Row;
			item.Add(toggle);
			item.Add(label);
			if (lib.IsLocked) {
				var icon = new Image {
					image = EditorGUIUtility.IconContent("InspectorLock").image
				};
				item.Add(icon);
			}

			toggle.value = lib.IsActive;
			toggle.RegisterValueChangedCallback(evt => OnLibraryToggled(lib, evt.newValue));
			label.RegisterCallback<MouseDownEvent>(evt => toggle.value = !toggle.value);
			return item;
		}
	}
}
