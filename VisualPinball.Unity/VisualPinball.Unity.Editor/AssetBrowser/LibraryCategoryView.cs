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

		public List<LibraryCategoryElement> Elements = new();

		private readonly VisualElement _list = new();
		private AssetBrowserX _browser;

		public LibraryCategoryView()
		{
			var scrollView = new ScrollView();
			var button = new Button(Create) {
				text = "New Category"
			};
			Add(scrollView);
			scrollView.Add(_list);
			Add(button);
		}

		public void Refresh(AssetBrowserX browser = null)
		{
			if (browser != null) {
				_browser = browser;
			}
			Elements.Clear();
			_list.Clear();

			var categories = _browser.Libraries
				.SelectMany(lib => lib.GetCategories().Select(c => (lib, c)))
				.GroupBy(t => t.Item2.Name, (_, g) => g);

			foreach (var cat in categories) {
				var categoryElement = new LibraryCategoryElement(this, cat);
				Elements.Add(categoryElement);
				_list.Add(categoryElement);
			}
		}

		private void Create()
		{
			var category = _browser.ActiveLibrary.AddCategory("New Category");
			var categoryElement = new LibraryCategoryElement(this, new []{(_browser.ActiveLibrary, category)});
			_list.Add(categoryElement);
			categoryElement.ToggleRename();
		}
	}
}
