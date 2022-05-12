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

using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class LibraryCategoryRenameElement : VisualElement
	{
		public new class UxmlFactory : UxmlFactory<LibraryCategoryRenameElement, UxmlTraits> { }

		public LibraryCategoryElement Category;

		private readonly TextField _textField;
		private readonly Label _lockedNotice;

		public LibraryCategoryRenameElement()
		{
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryRenameElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryCategoryRenameElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_textField = ui.Q<TextField>();
			_textField.RegisterCallback<KeyDownEvent>(OnKeyDown);
			_lockedNotice = ui.Q<Label>("locked-notice");

			ui.Q<Button>("okButton").RegisterCallback<MouseUpEvent>(_ => Category.CompleteRename(true, _textField.value));
			ui.Q<Button>("cancelButton").RegisterCallback<MouseUpEvent>(_ => Category.CompleteRename(false));
		}

		public void StartEditing()
		{
			_textField.value = Category.Name;
			_textField.Focus();
			_textField.SelectAll();

			if (Category.HasLockedLibraries && Category.UnlockedLibraries.Length > 0) {
				var unlockedLibraries = Category.UnlockedLibraries;
				_lockedNotice.RemoveFromClassList("hidden");
				_lockedNotice.text = unlockedLibraries.Length switch {
					1 => $"Changes will only be applied to <i>{unlockedLibraries.First().Name}</i>.",
					> 1 => "Changes will only be applied to <i>" + string.Join("</i>, <i>", unlockedLibraries[..^1].Select(c => c.Name)) + $"</i> and <i>{unlockedLibraries.Last().Name}</i>.",
					_ => _lockedNotice.text
				};
			} else {
				if (!_lockedNotice.ClassListContains("hidden")) {
					_lockedNotice.AddToClassList("hidden");
				}
			}
		}

		private void OnKeyDown(KeyDownEvent evt)
		{
			if (Category == null) {
				return;
			}

			switch (evt.keyCode) {
				case KeyCode.Return or KeyCode.KeypadEnter:
					Category.CompleteRename(true, _textField.value);
					break;

				case KeyCode.Escape:
					Category.CompleteRename(false);
					evt.StopImmediatePropagation();
					break;
			}
		}
	}
}
