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
using UnityEditor;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A category element groups categories with the same name of multiple libraries.
	/// It's also what's rendered.
	/// </summary>
	public class LibraryCategoryElement : VisualElement
	{
		public new class UxmlFactory : UxmlFactory<LibraryCategoryElement, UxmlTraits> { }

		public readonly (AssetLibrary, LibraryCategory)[] Categories;

		public readonly bool IsCreateButton;

		private readonly VisualElement _ui;
		private readonly Label _label;

		private const string ClassSelected = "selected";

		/// <summary>
		/// Construct as normal category
		/// </summary>
		/// <param name="categories">Category of each library</param>
		public LibraryCategoryElement(IEnumerable<(AssetLibrary, LibraryCategory)> categories) : this(categories, false)
		{
		}

		/// <summary>
		/// Construct as "add new" entry
		/// </summary>
		public LibraryCategoryElement() : this(null, true)
		{
		}

		private LibraryCategoryElement(IEnumerable<(AssetLibrary, LibraryCategory)> categories, bool isCreateButton)
		{
			Categories = categories?.ToArray();
			IsCreateButton = isCreateButton;

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryElement.uxml");
			var ui = visualTree.CloneTree();
			Add(ui);

			_ui = ui.Q<VisualElement>(null, "library-category-element");
			_label = _ui.Q<Label>();
			_label.text = !isCreateButton ? Categories!.First().Item2.Name : "Add New";

		}
	}
}
