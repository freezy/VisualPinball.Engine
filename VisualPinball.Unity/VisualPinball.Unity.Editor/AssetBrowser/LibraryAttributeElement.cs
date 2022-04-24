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
		private readonly AssetData _assetData;
		private readonly LibraryAttribute _attribute;

		private readonly Label _nameElement;
		private readonly VisualElement _valuesElement;
		private readonly VisualElement _displayElement;
		private readonly VisualElement _editElement;
		private readonly TextField _nameEditElement;
		private readonly TextField _valuesEditElement;

		private bool _isEditing;

		public LibraryAttributeElement(AssetData data, LibraryAttribute attribute)
		{
			_assetData = data;
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
			_nameEditElement = ui.Q<TextField>("attribute-name-edit");
			_valuesEditElement = ui.Q<TextField>("attribute-value-edit");

			ui.Q<Button>("okButton").RegisterCallback<MouseUpEvent>(_ => CompleteEdit(true, _nameEditElement.value, _valuesEditElement.value));
			ui.Q<Button>("cancelButton").RegisterCallback<MouseUpEvent>(_ => CompleteEdit(false));

			_displayElement.RegisterCallback<MouseDownEvent>(OnMouseDown);
			_nameEditElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
			_valuesEditElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

			Update();

			// right-click menu
			_displayElement.AddManipulator(new ContextualMenuManipulator(AddContextMenu));
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
					_valuesElement.Add(new Label(value)); // todo make each of those clickable
				}
			}
			_nameElement.text = _attribute.Key;
		}

		private void StartEditing()
		{
			_nameEditElement.value = _attribute.Key;
			_valuesEditElement.value = _attribute.Value;
			_nameEditElement.Focus();
			_nameEditElement.SelectAll();
		}

		public void CompleteEdit(bool success, string newName = null, string newValue = null)
		{
			if (success) {
				_attribute.Key = newName;
				_attribute.Value = newValue;
				_assetData.Update();
				Update();
			}
			ToggleEdit();
		}

		private void OnKeyDown(KeyDownEvent evt)
		{
			switch (evt.keyCode) {
				case KeyCode.Return or KeyCode.KeypadEnter:
					CompleteEdit(true, _nameEditElement.value, _valuesEditElement.value);
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
			if (_assetData.Asset.Attributes.Contains(_attribute)) {
				_assetData.Asset.Attributes.Remove(_attribute);
				_assetData.Update();
				parent.Remove(this);
			}
		}
	}
}
