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
		public readonly (AssetLibrary, LibraryCategory)[] Categories;

		public readonly bool IsCreateButton;
		public string Name => _label.text;

		private readonly LibraryCategoryView _libraryCategoryView;
		private readonly VisualElement _ui;
		private readonly Label _label;
		private readonly LibraryCategoryRenameElement _renameElement;

		private bool _isRenaming;

		private const string ClassSelected = "selected";

		/// <summary>
		/// Construct as normal category
		/// </summary>
		/// <param name="libraryCategoryView">Reference to parent</param>
		/// <param name="categories">Category of each library</param>
		public LibraryCategoryElement(LibraryCategoryView libraryCategoryView, IEnumerable<(AssetLibrary, LibraryCategory)> categories)
			: this(libraryCategoryView, categories, false) { }

		/// <summary>
		/// Construct as "add new" entry
		/// </summary>
		public LibraryCategoryElement(LibraryCategoryView libraryCategoryView)
			: this(libraryCategoryView, null, true) { }

		private LibraryCategoryElement(LibraryCategoryView libraryCategoryView, IEnumerable<(AssetLibrary, LibraryCategory)> categories, bool isCreateButton)
		{
			_libraryCategoryView = libraryCategoryView;
			Categories = categories?.ToArray();
			IsCreateButton = isCreateButton;

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryElement.uxml");
			var ui = visualTree.CloneTree();
			Add(ui);

			_ui = ui.Q<VisualElement>(null, "library-category-element");
			_label = _ui.Q<Label>();
			_label.text = !isCreateButton ? Categories!.First().Item2.Name : "Add New";
			_renameElement = ui.Q<LibraryCategoryRenameElement>();
			_renameElement.Category = this;

			RegisterCallback<PointerUpEvent>(OnPointerUp);

			// right-click menu
			this.AddManipulator(new ContextualMenuManipulator(AddContextMenu));
		}

		public void ToggleRename(DropdownMenuAction act = null)
		{
			if (_isRenaming) {
				_label.RemoveFromClassList("hidden");
				_renameElement.AddToClassList("hidden");

			} else {
				_label.AddToClassList("hidden");
				_renameElement.RemoveFromClassList("hidden");
				_renameElement.StartEditing();
			}

			_isRenaming = !_isRenaming;
		}

		public void CompleteRename(bool success, string newName = null)
		{
			if (success) {
				_label.text = newName;
				foreach (var (lib, category) in Categories) {
					lib.RenameCategory(category, newName);
				}
			}
			ToggleRename();
		}

		private void AddContextMenu(ContextualMenuPopulateEvent evt)
		{
			evt.menu.AppendAction("Rename", ToggleRename);
			evt.menu.AppendAction("Delete", Delete);
		}


		private void Delete(DropdownMenuAction obj)
		{
			foreach (var (lib, category) in Categories) {
				if (lib.NumAssetsWithCategory(category) == 0) {
					lib.DeleteCategory(category);
				}
			}
			_libraryCategoryView.Refresh();
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (evt.button != 0) {
				return;
			}
			if (IsCreateButton) {
				_libraryCategoryView.Create();
			}
		}
	}
}
