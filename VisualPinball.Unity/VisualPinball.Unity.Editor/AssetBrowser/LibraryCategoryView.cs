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

		private readonly VisualElement _container = new();

		public LibraryCategoryView()
		{
			var scrollView = new ScrollView();
			Add(scrollView);
			scrollView.Add(_container);
		}

		public void Refresh(IEnumerable<AssetLibrary> libraries)
		{
			Elements.Clear();
			_container.Clear();

			var categories = libraries
				.SelectMany(lib => lib.GetCategories().Select(c => (lib, c)))
				.GroupBy(t => t.Item2.Name, (_, g) => g);


			foreach (var cat in categories) {
				var categoryElement = new LibraryCategoryElement(this, cat);
				Elements.Add(categoryElement);
				_container.Add(categoryElement);
			}
			_container.Add(new LibraryCategoryElement(this)); // that's the "add" entry
		}

		public void Create()
		{

		}
	}
}
