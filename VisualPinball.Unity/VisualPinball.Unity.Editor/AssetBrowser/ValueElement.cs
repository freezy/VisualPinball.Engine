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

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class ValueElement : VisualElement
	{
		private readonly AssetResult _assetResult;

		private string _value;
		private readonly SerializableHashSet<string> _values;

		private readonly Label _nameElement;
		private readonly VisualElement _displayElement;
		private readonly VisualElement _editElement;
		private readonly SearchSuggest _nameEditElement;

		private bool _isEditing;
		private AssetBrowserX _browser;

		public ValueElement(AssetResult result, string value, SerializableHashSet<string> values)
		{
			_assetResult = result;
			_value = value;
			_values = values;

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/ValueElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/ValueElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_displayElement = ui.Q<VisualElement>("display");
			_editElement = ui.Q<VisualElement>("edit");
			_nameElement = ui.Q<Label>("attribute-name");
			_nameEditElement = ui.Q<SearchSuggest>("attribute-name-edit");

			ui.Q<Button>("okButton").RegisterCallback<MouseUpEvent>(_ => CompleteEdit(true, _nameEditElement.Value));
			ui.Q<Button>("cancelButton").RegisterCallback<MouseUpEvent>(_ => ToggleEdit());

			_displayElement.RegisterCallback<MouseDownEvent>(OnNameClicked);

			if (!_assetResult.Library.IsLocked) {
				_nameEditElement.RegisterKeyDownCallback(evt => OnKeyDown(evt, _nameEditElement));

				// right-click menu
				_displayElement.AddManipulator(new ContextualMenuManipulator(AddContextMenu));
			}

			Update();
			RegisterCallback<AttachToPanelEvent>(OnAttached);
		}

		private void OnAttached(AttachToPanelEvent evt)
		{
			_browser = panel.visualTree.userData as AssetBrowserX;
		}

		private void OnNameClicked(MouseDownEvent evt)
		{
			// if it's a link, apply filter
			if (evt.button == 0 && evt.clickCount == 1) {
				//OpenLink(_keyValue.Value);
			}
		}

		public void ToggleEdit(DropdownMenuAction act = null)
		{
			if (_isEditing) {
				_displayElement.RemoveFromClassList("hidden");
				_editElement.AddToClassList("hidden");

			} else {
				_displayElement.AddToClassList("hidden");
				_editElement.RemoveFromClassList("hidden");
				StartEditing();
			}

			_isEditing = !_isEditing;
		}

		private void Update()
		{
			_nameElement.text = _value;
		}


		private void StartEditing()
		{
			_nameEditElement.Value = _value;
			_nameEditElement.SuggestOptions = _browser!.Query.TagNames;
			_nameEditElement.Focus();
			_nameEditElement.SelectAll();
		}

		private void CompleteEdit(bool success, string newName)
		{
			if (success) {
				// first, remove the old name
				_values.Remove(_value);
				if (!_values.Contains(newName)) {
					_values.Add(newName);
				}
				_value = newName;
				_assetResult.Save();
				Update();
			}
			ToggleEdit();
		}

		private void OnKeyDown(KeyDownEvent evt, SearchSuggest ss)
		{
			if (ss.PopupVisible) {
				return;
			}
			switch (evt.keyCode) {
				case KeyCode.Return or KeyCode.KeypadEnter:
					CompleteEdit(true, _nameEditElement.Value);
					break;

				case KeyCode.Escape:
					ToggleEdit();
					evt.StopImmediatePropagation();
					break;
			}
		}

		private void AddContextMenu(ContextualMenuPopulateEvent evt)
		{
			evt.menu.AppendAction("Edit", ToggleEdit);
			evt.menu.AppendAction("Delete", Delete);
		}

		private void Delete(DropdownMenuAction obj)
		{
			if (_values.Contains(_value)) {
				_values.Remove(_value);
				_assetResult.Save();
				parent.Remove(this);
			}
		}
	}
}
