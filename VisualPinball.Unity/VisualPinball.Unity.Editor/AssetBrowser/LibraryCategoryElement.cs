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
		public readonly (AssetLibrary, AssetCategory)[] Categories;

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
		private readonly Image _folderIcon;
		private readonly Label _label;
		private readonly Image _lockIcon;
		private readonly LibraryCategoryRenameElement _renameElement;

		private int NumAssets => Categories.Select(c => c.Item1.NumAssetsWithCategory(c.Item2)).Sum();
		private bool AllLibrariesLocked => Categories.Count(c => !c.Item1.IsLocked) == 0;
		public bool HasLockedLibraries => Categories.Any(c => c.Item1.IsLocked);
		public AssetLibrary[] UnlockedLibraries => Categories.Where(c => !c.Item1.IsLocked).Select(c => c.Item1).ToArray();

		private bool _isSelected;
		private bool _isRenaming;

		private const string ClassSelected = "unity-collection-view__item--selected";
		private const string ClassDrag = "library-category-element--dragover";

		/// <summary>
		/// Construct as normal category
		/// </summary>
		/// <param name="libraryCategoryView">Reference to parent</param>
		/// <param name="categories">Category of each library</param>
		public LibraryCategoryElement(LibraryCategoryView libraryCategoryView, IEnumerable<(AssetLibrary, AssetCategory)> categories)
		{
			_libraryCategoryView = libraryCategoryView;
			Categories = categories.ToArray();

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_ui = ui.Q<VisualElement>(null, "library-category-element");
			_folderIcon = _ui.Q<Image>("icon-folder");
			_label = _ui.Q<Label>();
			_label.text = Categories!.First().Item2.Name;
			_renameElement = ui.Q<LibraryCategoryRenameElement>();
			_renameElement.Category = this;

			_ui.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_ui.RegisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_ui.RegisterCallback<DragEnterEvent>(OnDragEnterEvent);
			_ui.RegisterCallback<DragLeaveEvent>(OnDragLeaveEvent);

			UpdateIcon();
			RegisterCallback<PointerUpEvent>(OnPointerUp);

			// if at least one lib is unlocked, enable right-click menu
			if (!AllLibrariesLocked) {
				this.AddManipulator(new ContextualMenuManipulator(AddContextMenu));
			}
		}

		public void ToggleRename(DropdownMenuAction act = null)
		{
			if (_isRenaming) {
				_ui.RemoveFromClassList("hidden");
				_renameElement.AddToClassList("hidden");

			} else {
				_ui.AddToClassList("hidden");
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
					if (!lib.IsLocked) {
						lib.RenameCategory(category, newName);
					}
				}
				_libraryCategoryView.Refresh();
			}
			ToggleRename();
		}

		private void AddContextMenu(ContextualMenuPopulateEvent evt)
		{
			evt.menu.AppendAction("Rename", ToggleRename);

			// count assets
			var num = 0;
			foreach (var (lib, category) in Categories) {
				num += lib.NumAssetsWithCategory(category);
			}
			if (num == 0) {
				evt.menu.AppendAction("Delete", Delete);
			}
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

		private void OnDragEnterEvent(DragEnterEvent evt)
		{
			if (AssetBrowser.IsDraggingExistingAssets || AssetBrowser.IsDraggingNewAssets) {
				AddToClassList(ClassDrag);
			} else {
				return;
			}
			_libraryCategoryView.DragError = null;

			// drag from asset panel
			if (DragAndDrop.GetGenericData("assets") is HashSet<AssetResult> data) {
				foreach (var d in data) {
					if (d.Asset.Library.IsLocked) {
						_libraryCategoryView.DragError = "Access Error. At least one of the assets you're dragging is part of a locked library.";
						break;
					}
				}
				return;
			}

			// drag from outside
			foreach (var path in DragAndDrop.paths) {
				var assetLibrary = _libraryCategoryView.GetLibraryByPath(path);
				if (assetLibrary == null) {
					_libraryCategoryView.DragError = "Unknown library. Your assets must be under the root of a library, and at least one of the assets you're dragging is not.";
					break;
				}

				if (assetLibrary.IsLocked) {
					_libraryCategoryView.DragError = "Access Error. The library you're trying to add assets to is locked.";
					break;
				}
			}
		}

		private static void OnDragUpdatedEvent(DragUpdatedEvent evt)
		{
			DragAndDrop.visualMode = AssetBrowser.IsDraggingExistingAssets || AssetBrowser.IsDraggingNewAssets
				? DragAndDropVisualMode.Move
				: DragAndDropVisualMode.Rejected;
		}

		private void OnDragPerformEvent(DragPerformEvent evt)
		{
			RemoveFromClassList(ClassDrag);
			if (_libraryCategoryView.DragError != null) {
				_libraryCategoryView.DragError = null;
				AssetBrowser.StopDraggingAssets();
				return;
			}

			DragAndDrop.AcceptDrag();

			// drop from asset panel
			if (DragAndDrop.GetGenericData("assets") is HashSet<AssetResult> data) {
				foreach (var d in data) {
					var category = Categories.FirstOrDefault(i => i.Item1 == d.Asset.Library).Item2 ?? d.Asset.Library.AddCategory(Name);
					d.Asset.Library.SetCategory(d.Asset, category);
				}
				_libraryCategoryView.Refresh();
				AssetBrowser.StopDraggingAssets();
				return;
			}

			// drop from outside
			if (AssetBrowser.IsDraggingNewAssets) {
				_libraryCategoryView.AddAssets(DragAndDrop.paths, assetLibrary => Categories.FirstOrDefault(i => i.Item1 == assetLibrary).Item2 ??assetLibrary.AddCategory(Name));
			}
		}

		private void OnDragLeaveEvent(DragLeaveEvent evt)
		{
			RemoveFromClassList(ClassDrag);
			_libraryCategoryView.DragError = null;
		}

		private void UpdateIcon()
		{
			var iconName = _isSelected ? "d_FolderOpened Icon" : NumAssets > 0 ? "d_Folder Icon" : "d_FolderEmpty Icon";
			_folderIcon.image = EditorGUIUtility.IconContent(iconName).image;
		}
	}
}
