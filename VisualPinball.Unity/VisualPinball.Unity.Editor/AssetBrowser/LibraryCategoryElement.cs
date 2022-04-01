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

		public string Name => _label.text;
		public bool IsSelected {
			get => _isSelected;
			set {
				switch (value) {
					case true when !_isSelected:
						AddToClassList(ClassSelected);
						break;
					case false when _isSelected:
						RemoveFromClassList(ClassSelected);
						break;
				}
				_isSelected = value;
			}
		}

		private readonly LibraryCategoryView _libraryCategoryView;
		private readonly VisualElement _ui;
		private readonly Image _icon;
		private readonly Label _label;
		private readonly LibraryCategoryRenameElement _renameElement;

		private int NumAssets => Categories.Select(c => c.Item1.NumAssetsWithCategory(c.Item2)).Sum();

		private bool _isSelected;
		private bool _isRenaming;

		private const string ClassSelected = "unity-collection-view__item--selected";

		/// <summary>
		/// Construct as normal category
		/// </summary>
		/// <param name="libraryCategoryView">Reference to parent</param>
		/// <param name="categories">Category of each library</param>
		public LibraryCategoryElement(LibraryCategoryView libraryCategoryView, IEnumerable<(AssetLibrary, LibraryCategory)> categories)
		{
			_libraryCategoryView = libraryCategoryView;
			Categories = categories?.ToArray();

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_ui = ui.Q<VisualElement>(null, "library-category-element");
			_icon = _ui.Q<Image>();
			_label = _ui.Q<Label>();
			_label.text = Categories!.First().Item2.Name;
			_renameElement = ui.Q<LibraryCategoryRenameElement>();
			_renameElement.Category = this;

			UpdateIcon();
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
			if (evt.button != 0) { // only handle left mouse click
				return;
			}
			_libraryCategoryView.OnCategoryClicked(this, evt.ctrlKey);
		}

		private void UpdateIcon()
		{
			var iconName = _isSelected ? "d_FolderOpened Icon" : NumAssets > 0 ? "d_Folder Icon" : "d_FolderEmpty Icon";
			//iconName = "_Help";
			_icon.image = EditorGUIUtility.IconContent(iconName).image;
		}
	}
}
