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
	public class LibraryAttributeElement : VisualElement
	{
		private readonly AssetResult _assetResult;
		private readonly LibraryAttribute _attribute;

		private readonly Label _nameElement;
		private readonly VisualElement _valuesElement;
		private readonly VisualElement _displayElement;
		private readonly VisualElement _editElement;
		private readonly SearchSuggest _nameEditElement;
		private readonly SearchSuggest _valuesEditElement;

		private bool _isEditing;
		private AssetBrowserX _browser;

		public LibraryAttributeElement(AssetResult result, LibraryAttribute attribute)
		{
			_assetResult = result;
			_attribute = attribute;

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAttributeElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAttributeElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_displayElement = ui.Q<VisualElement>("display");
			_editElement = ui.Q<VisualElement>("edit");
			_nameElement = ui.Q<Label>("attribute-name");
			_valuesElement = ui.Q<VisualElement>("attribute-values");
			_nameEditElement = ui.Q<SearchSuggest>("attribute-name-edit");
			_valuesEditElement = ui.Q<SearchSuggest>("attribute-value-edit");

			ui.Q<Button>("okButton").RegisterCallback<MouseUpEvent>(_ => CompleteEdit(true, _nameEditElement.Value, _valuesEditElement.Value));
			ui.Q<Button>("cancelButton").RegisterCallback<MouseUpEvent>(_ => CompleteEdit(false));

			if (!_assetResult.Library.IsLocked) {
				_displayElement.RegisterCallback<MouseDownEvent>(OnMouseDown);
				_nameEditElement.RegisterKeyDownCallback(evt => OnKeyDown(evt, _nameEditElement));
				_valuesEditElement.RegisterKeyDownCallback(evt => OnKeyDown(evt, _valuesEditElement));
				_valuesEditElement.IsMultiValue = true;

				// right-click menu
				_displayElement.AddManipulator(new ContextualMenuManipulator(AddContextMenu));
			}

			Update();
			RegisterCallback<AttachToPanelEvent>(OnAttached);
		}

		private void OnAttached(AttachToPanelEvent evt)
		{
			_browser = panel.visualTree.userData as AssetBrowserX;
			_valuesEditElement.RegisterCallback<FocusInEvent>(OnAttributeValueFocus);
		}

		private void OnMouseDown(MouseDownEvent evt)
		{
			if (evt.clickCount == 2) {
				ToggleEdit();
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
			if (!string.IsNullOrEmpty(_attribute.Value)) {
				var values = _attribute.Value.Split(',').Select(s => s.Trim());
				_valuesElement.Clear();
				foreach (var value in values) {
					var label = new Label(value);
					label.RegisterCallback<MouseDownEvent>(_ => _browser.FilterByAttribute(_attribute.Key, value));
					_valuesElement.Add(label);
				}
			}
			_nameElement.text = _attribute.Key;
		}

		private void StartEditing()
		{
			_nameEditElement.Value = _attribute.Key;
			_nameEditElement.SuggestOptions = _browser!.Query.AttributeNames;
			_valuesEditElement.Value = _attribute.Value;
			_nameEditElement.Focus();
			_nameEditElement.SelectAll();
		}

		private void OnAttributeValueFocus(FocusInEvent focusInEvent)
		{
			_valuesEditElement.SuggestOptions = _browser!.Query.AttributeValues(_nameEditElement.Value);
		}

		public void CompleteEdit(bool success, string newName = null, string newValue = null)
		{
			if (success) {
				_attribute.Key = newName;
				_attribute.Value = newValue;
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
					CompleteEdit(true, _nameEditElement.Value, _valuesEditElement.Value);
					break;

				case KeyCode.Escape:
					CompleteEdit(false);
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
			if (_assetResult.Asset.Attributes.Contains(_attribute)) {
				_assetResult.Asset.Attributes.Remove(_attribute);
				_assetResult.Save();
				parent.Remove(this);
			}
		}
	}
}
