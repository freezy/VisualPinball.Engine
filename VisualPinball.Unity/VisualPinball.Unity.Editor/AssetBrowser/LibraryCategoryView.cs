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
using System.Linq;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class LibraryCategoryView : VisualElement
	{
		public new class UxmlFactory : UxmlFactory<LibraryCategoryView, UxmlTraits> { }


		private AssetBrowserX _browser;
		private readonly VisualElement _container = new();
		private readonly HashSet<LibraryCategoryElement> _selectedCategoryElements = new();
		private readonly Dictionary<AssetLibrary, List<LibraryCategory>> _selectedCategories = new();

		public LibraryCategoryView()
		{
			var scrollView = new ScrollView();
			var button = new Button(Create) {
				text = "New Category"
			};
			Add(scrollView);
			scrollView.Add(_container);
			Add(button);
		}

		public void Refresh(AssetBrowserX browser = null)
		{
			if (browser != null) {
				_browser = browser;
			}
			_container.Clear();

			var categories = _browser.Libraries
				.SelectMany(lib => lib.GetCategories().Select(c => (lib, c)))
				.GroupBy(t => t.Item2.Name, (_, g) => g);

			foreach (var cat in categories) {
				var categoryElement = new LibraryCategoryElement(this, cat);
				_container.Add(categoryElement);
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
				case true when _selectedCategoryElements.Count == 1:
					categoryElement.IsSelected = false;
					_selectedCategoryElements.Clear();
					break;

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

			foreach (var selectedCategoryElement in _selectedCategoryElements) {
				_selectedCategories.Clear();
				foreach (var (lib, category) in selectedCategoryElement.Categories) {
					if (!_selectedCategories.ContainsKey(lib)) {
						_selectedCategories[lib] = new List<LibraryCategory>();
					}
					_selectedCategories[lib].Add(category);
				}
			}
			Query();
		}

		private void Query()
		{

		}

		private void Create()
		{
			var category = _browser.ActiveLibrary.AddCategory("New Category");
			var categoryElement = new LibraryCategoryElement(this, new []{(_browser.ActiveLibrary, category)});
			_container.Add(categoryElement);
			categoryElement.ToggleRename();
		}
	}
}
