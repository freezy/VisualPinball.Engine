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
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class LibraryAttributeElement : VisualElement
	{
		private readonly LibraryAttribute _attribute;

		private bool _isRenaming;

		public LibraryAttributeElement(LibraryAttribute attribute)
		{
			_attribute = attribute;

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAttributeElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAttributeElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			// right-click menu
			this.AddManipulator(new ContextualMenuManipulator(AddContextMenu));
		}

		public void ToggleEdit(DropdownMenuAction act = null)
		{
			// if (_isRenaming) {
			// 	_ui.RemoveFromClassList("hidden");
			// 	_renameElement.AddToClassList("hidden");
			//
			// } else {
			// 	_ui.AddToClassList("hidden");
			// 	_renameElement.RemoveFromClassList("hidden");
			// 	_renameElement.StartEditing();
			// }

			_isRenaming = !_isRenaming;
		}

		public void CompleteRename(bool success, string newName = null)
		{
			// if (success) {
			// 	_label.text = newName;
			// 	foreach (var (lib, category) in Categories) {
			// 		lib.RenameCategory(category, newName);
			// 	}
			// 	_libraryCategoryView.Refresh();
			// }
			// ToggleRename();
		}

		private void AddContextMenu(ContextualMenuPopulateEvent evt)
		{
			evt.menu.AppendAction("Rename", ToggleEdit);
			evt.menu.AppendAction("Delete", Delete);
		}

		private void Delete(DropdownMenuAction obj)
		{
			// foreach (var (lib, category) in Categories) {
			// 	if (lib.NumAssetsWithCategory(category) == 0) {
			// 		lib.DeleteCategory(category);
			// 	}
			// }
			// _libraryCategoryView.Refresh();
		}

	}
}
