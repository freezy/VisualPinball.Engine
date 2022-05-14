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

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class KeyValueElement : VisualElement
	{
		private readonly AssetResult _assetResult;
		private readonly LibraryKeyValue _keyValue;

		private readonly Label _nameElement;
		private readonly Label _linkElement;
		private readonly VisualElement _valuesElement;
		private readonly VisualElement _displayElement;
		private readonly VisualElement _displayLinkElement;
		private readonly VisualElement _editElement;
		private readonly SearchSuggest _nameEditElement;
		private readonly SearchSuggest _valuesEditElement;

		private readonly bool _isLink;
		private bool _isEditing;
		private AssetBrowserX _browser;

		private VisualElement DisplayContainer => _isLink ? _displayLinkElement : _displayElement;
		private Label DisplayElement => _isLink ? _linkElement : _nameElement;

		public KeyValueElement(AssetResult result, LibraryKeyValue keyValue, bool isLink)
		{
			_assetResult = result;
			_keyValue = keyValue;

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/KeyValueElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/KeyValueElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_displayElement = ui.Q<VisualElement>("display");
			_displayLinkElement = ui.Q<VisualElement>("display-link");
			_editElement = ui.Q<VisualElement>("edit");
			_nameElement = ui.Q<Label>("attribute-name");
			_linkElement = ui.Q<Label>("attribute-link");
			_valuesElement = ui.Q<VisualElement>("attribute-values");
			_nameEditElement = ui.Q<SearchSuggest>("attribute-name-edit");
			_valuesEditElement = ui.Q<SearchSuggest>("attribute-value-edit");

			ui.Q<Button>("okButton").RegisterCallback<MouseUpEvent>(_ => CompleteEdit(true, _nameEditElement.Value, _valuesEditElement.Value));
			ui.Q<Button>("cancelButton").RegisterCallback<MouseUpEvent>(_ => CompleteEdit(false));

			_isLink = isLink;
			DisplayContainer.RemoveFromClassList("hidden");
			DisplayContainer.RegisterCallback<MouseDownEvent>(OnNameClicked);

			if (!_assetResult.Library.IsLocked) {
				_nameEditElement.RegisterKeyDownCallback(evt => OnKeyDown(evt, _nameEditElement));
				_valuesEditElement.RegisterKeyDownCallback(evt => OnKeyDown(evt, _valuesEditElement));
				if (!_isLink) {
					_valuesEditElement.IsMultiValue = true;
				}

				// right-click menu
				DisplayContainer.AddManipulator(new ContextualMenuManipulator(AddContextMenu));
			}

			Update();
			RegisterCallback<AttachToPanelEvent>(OnAttached);
		}

		private void OnAttached(AttachToPanelEvent evt)
		{
			_browser = panel.visualTree.userData as AssetBrowserX;
			_valuesEditElement.RegisterCallback<FocusInEvent>(OnAttributeValueFocus);
		}

		private void OnNameClicked(MouseDownEvent evt)
		{
			// if it's a link, open it on first left click
			if (_isLink && evt.button == 0 && evt.clickCount == 1) {
				OpenLink(_keyValue.Value);
			}

			// on double click and lib isn't locked, toggle edit.
			if (!_assetResult.Library.IsLocked && evt.button == 0 && evt.clickCount == 2) {
				ToggleEdit();
			}
		}

		public void ToggleEdit(DropdownMenuAction act = null)
		{
			if (_isEditing) {
				DisplayContainer.RemoveFromClassList("hidden");
				_editElement.AddToClassList("hidden");

			} else {
				DisplayContainer.AddToClassList("hidden");
				_editElement.RemoveFromClassList("hidden");
				StartEditing();
			}

			_isEditing = !_isEditing;
		}

		private void Update()
		{
			if (!string.IsNullOrEmpty(_keyValue.Value)) {
				_valuesElement.Clear();
				if (_isLink) {
					var label = new Label(_keyValue.Value);
					if (IsValidLink(_keyValue.Value)) {
						label.RegisterCallback<MouseDownEvent>(_ => OpenLink(_keyValue.Value));
					} else {
						label.AddToClassList("non-clickable");
					}
					_valuesElement.Add(label);

				} else {
					var values = _keyValue.Value.Split(',').Select(s => s.Trim());

					foreach (var value in values) {
						var label = new Label(value);
						label.RegisterCallback<MouseDownEvent>(_ => _browser.FilterByAttribute(_keyValue.Key, value));
						_valuesElement.Add(label);
					}
				}
			}
			DisplayElement.text = _keyValue.Key;
		}


		private void StartEditing()
		{
			_nameEditElement.Value = _keyValue.Key;
			_nameEditElement.SuggestOptions = _browser!.Query.AttributeNames;
			_valuesEditElement.Value = _keyValue.Value;
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
				_keyValue.Key = newName;
				_keyValue.Value = newValue;
				_assetResult.Save();
				Update();
			}
			ToggleEdit();
		}

		private static bool IsValidLink(string link) => Uri.TryCreate(link, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

		private static void OpenLink(string link) => Application.OpenURL(link);

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
			if (_assetResult.Asset.Attributes.Contains(_keyValue)) {
				_assetResult.Asset.Attributes.Remove(_keyValue);
				_assetResult.Save();
				parent.Remove(this);
			}
		}
	}
}
