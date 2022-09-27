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
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class LibraryCategoryView : VisualElement
	{
		public new class UxmlFactory : UxmlFactory<LibraryCategoryView, UxmlTraits> { }

		public int NumSelectedCategories => _selectedCategoryElements.Count;
		public int NumCategories;

		public string DragError {
			get => _browser.DragErrorLeft;
			set => _browser.DragErrorLeft = value;
		}

		public AssetLibrary GetLibraryByPath(string path) => _browser.GetLibraryByPath(path);

		public void AddAssets(IEnumerable<string> paths, Func<AssetLibrary, AssetCategory> getCategory) => _browser.AddAssets(paths, getCategory);

		private AssetBrowser _browser;
		private AssetLibrary _activeLibrary;

		private readonly VisualElement _container;
		private readonly HashSet<LibraryCategoryElement> _selectedCategoryElements = new();
		private readonly Dictionary<AssetLibrary, List<AssetCategory>> _selectedCategories = new();
		private readonly DropdownField _activeLibraryDropdown;
		private readonly Label _noCategoriesLabel;
		private readonly VisualElement _addContainer;

		public LibraryCategoryView()
		{
			// import UXML
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryView.uxml");
			var ui = visualTree.CloneTree();

			// import style sheet
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryView.uss");
			ui.styleSheets.Add(styleSheet);

			Add(ui);

			_container = ui.Q<VisualElement>("container");
			_noCategoriesLabel = ui.Q<Label>("noCategories");
			_addContainer = ui.Q<VisualElement>("addContainer");
			ui.Q<Button>("add").clickable = new Clickable(Create);

			// active library dropdown
			var activeLibraryContainer = ui.Q<VisualElement>("activeLibrary");
			_activeLibraryDropdown = new DropdownField(new List<string> { "none" }, 0, OnActiveLibraryChanged) {
				tooltip = "The library in which the new category will be created."
			};
			activeLibraryContainer.Add(_activeLibraryDropdown);
		}

		private string OnActiveLibraryChanged(string libraryName)
		{
			if (_browser == null || _browser.Libraries == null) {
				return libraryName;
			}
			var library = _browser.Libraries.FirstOrDefault(l => l.Name == libraryName);
			if (library == null) {
				return libraryName;
			}
			_activeLibrary = library;
			_browser.ActiveLibraryForCategories = _activeLibrary.Name;
			return libraryName;
		}

		public void Refresh(AssetBrowser browser = null)
		{
			if (browser != null) {
				_browser = browser;
			}

			// remember selection
			var selectedCategoryNames = new HashSet<string>(_container.Children()
				.Select(c => c as LibraryCategoryElement)
				.Where(c => c!.IsSelected)
				.Select(c => c!.Name));

			// update categories
			_container.Clear();
			var categories = _browser.Libraries
				.Where(lib => lib.IsActive)
				.SelectMany(lib => lib.GetCategories().Select(c => (lib, c)))
				.OrderBy(tuple => tuple.c.Name)
				.GroupBy(t => t.Item2.Name, (_, g) => g);

			// update elements
			_selectedCategoryElements.Clear();
			NumCategories = 0;
			foreach (var cat in categories) {
				var categoryElement = new LibraryCategoryElement(this, cat);
				_container.Add(categoryElement);
				if (selectedCategoryNames.Contains(categoryElement.Name)) {
					categoryElement.IsSelected = true;
					_selectedCategoryElements.Add(categoryElement);
				}
				NumCategories++;
			}

			// re-apply selection
			BuildSelectedCategories();
			_browser.OnCategoriesUpdated(_selectedCategories);

			// show/hide "no categories"
			if (NumCategories > 0) {
				_noCategoriesLabel.AddToClassList("hidden");
			} else {
				_noCategoriesLabel.RemoveFromClassList("hidden");
			}

			// update libraries dropdown
			var writableLibraries = _browser.Libraries.Where(lib => !lib.IsLocked && lib.IsActive).ToArray();
			_activeLibraryDropdown.choices = writableLibraries.Select(l => l.Name).ToList();
			if (_activeLibrary != null && (_activeLibrary.IsLocked || !_activeLibrary.IsActive)) {
				_activeLibrary = null;
			}
			if (_activeLibrary != null && writableLibraries.Length > 0) {
				_activeLibraryDropdown.index = System.Math.Max(0, _activeLibraryDropdown.choices.IndexOf(_activeLibrary.Name));
			}

			// if active library isn't set, try to match it by name
			if (_activeLibrary == null && writableLibraries.Length > 0) {
				var activeLibrary = writableLibraries.FirstOrDefault(l => l.Name == _browser.ActiveLibraryForCategories);
				if (activeLibrary != null) {
					_activeLibrary = activeLibrary;
					_activeLibraryDropdown.index = System.Math.Max(0, _activeLibraryDropdown.choices.IndexOf(_activeLibrary.Name));
				}
			}

			// if active library cannot be determined, fall back to first available library.
			if (_activeLibrary == null && writableLibraries.Length > 0) {
				_activeLibrary = writableLibraries.First();
				_activeLibraryDropdown.index = System.Math.Max(0, _activeLibraryDropdown.choices.IndexOf(_activeLibrary.Name));
			}

			// show/hide add button
			if (_activeLibrary != null) {
				_addContainer.RemoveFromClassList("hidden");
			} else {
				_addContainer.AddToClassList("hidden");
			}
		}

		public void OnCategoryClicked(LibraryCategoryElement categoryElement, bool ctrlPressed)
		{
			switch (categoryElement.IsSelected) {

				// if not selected, select it.
				case false: {
					// clear if ctrl not pressed
					if (!ctrlPressed) {
						foreach (var selectedElement in _selectedCategoryElements) {
							selectedElement.IsSelected = false;
						}
						_selectedCategoryElements.Clear();
					}
					_selectedCategoryElements.Add(categoryElement);
					categoryElement.IsSelected = true;
					break;
				}

				// if it's the only one and it's already selected, de-select (= select all)
				case true when _selectedCategoryElements.Count == 1: {
					categoryElement.IsSelected = false;
					_selectedCategoryElements.Clear();
					break;
				}

				// if it's already selected but not the only one, make it the only one (or de-select, if ctrl pressed)
				case true when _selectedCategoryElements.Count > 1: {
					if (!ctrlPressed) {
						foreach (var selectedElement in _selectedCategoryElements) {
							if (selectedElement != categoryElement) {
								selectedElement.IsSelected = false;
							}
						}
						_selectedCategoryElements.Clear();
						_selectedCategoryElements.Add(categoryElement);

					} else {
						categoryElement.IsSelected = false;
						_selectedCategoryElements.Remove(categoryElement);
					}
					break;
				}
			}

			BuildSelectedCategories();
			_browser.OnCategoriesUpdated(_selectedCategories);
		}

		private void BuildSelectedCategories()
		{
			_selectedCategories.Clear();
			foreach (var selectedCategoryElement in _selectedCategoryElements) {
				foreach (var (lib, category) in selectedCategoryElement.Categories) {
					if (!_selectedCategories.ContainsKey(lib)) {
						_selectedCategories[lib] = new List<AssetCategory>();
					}
					_selectedCategories[lib].Add(category);
				}
			}
		}

		private void Create()
		{
			if (_activeLibrary.IsLocked) {
				throw new InvalidOperationException($"Library {_activeLibrary.Name} is locked.");
			}
			var category = _activeLibrary.AddCategory("New Category");
			var categoryElement = new LibraryCategoryElement(this, new []{(_activeLibrary, category)});
			_container.Add(categoryElement);
			categoryElement.ToggleRename();
		}

		public AssetCategory GetOrCreateSelected(AssetLibrary assetLibrary)
		{
			if (_selectedCategories.ContainsKey(assetLibrary)) {
				return _selectedCategories[assetLibrary].First();
			}
			var selectedElement = _selectedCategoryElements.First();
			var addedCategory = assetLibrary.AddCategory(selectedElement.Name);
			_selectedCategories[assetLibrary] = new List<AssetCategory> { addedCategory };
			return addedCategory;
		}
	}
}
