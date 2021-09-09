// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class ObjectReferencePicker<T> where T: class, IIdentifiableItemComponent
	{
		private AdvancedDropdownState _itemPickDropdownState;

		public IconColor IconColor = IconColor.Gray;

		private readonly string _pickerTitle;
		private readonly TableComponent _tableComp;
		private readonly bool _showIcon;

		public ObjectReferencePicker(string pickerTitle, TableComponent tableComp, bool showIcon)
		{
			_pickerTitle = pickerTitle;
			_tableComp = tableComp;
			_showIcon = showIcon;
		}

		public void Render(Rect pos, T currentObj, Action<T> onUpdated)
		{
			// render content
			var content = currentObj == null
				? new GUIContent("None")
				: _showIcon
					? new GUIContent(currentObj.name, Icons.ByComponent(currentObj, IconSize.Small, IconColor))
					: new GUIContent(currentObj.name);

			// render drawer
			var id = GUIUtility.GetControlID(FocusType.Keyboard, pos);
			var objectFieldButton = GUI.skin.GetStyle("ObjectFieldButton");
			var suffixButtonPos = new Rect(pos.xMax - 19f, pos.y + 1, 19f, pos.height - 2);
			EditorGUIUtility.SetIconSize(new Vector2(12f, 12f));

			// handle click
			if (Event.current.type == EventType.MouseDown && pos.Contains(Event.current.mousePosition)) {

				if (currentObj != null && !suffixButtonPos.Contains(Event.current.mousePosition)) {
					// click on ping
					var mb = currentObj as MonoBehaviour;
					if (mb) {
						EditorGUIUtility.PingObject(mb.gameObject);
					}

				} else {
					// click on picker
					_itemPickDropdownState ??= new AdvancedDropdownState();
					var dropdown = new ItemSearchableDropdown<T>(
						_itemPickDropdownState,
						_tableComp,
						_pickerTitle,
						onUpdated
					);
					dropdown.Show(pos);
				}
			}

			if (Event.current.type == EventType.Repaint) {
				EditorStyles.objectField.Draw(pos, content, id, DragAndDrop.activeControlID == id, pos.Contains(Event.current.mousePosition));
				objectFieldButton.Draw(suffixButtonPos, GUIContent.none, id, DragAndDrop.activeControlID == id, suffixButtonPos.Contains(Event.current.mousePosition));
			}
		}
	}

	public class ItemSearchableDropdown<T> : AdvancedDropdown where T : class, IIdentifiableItemComponent
	{
		private readonly string _title;

		private readonly TableComponent _tableComponent;

		private readonly Action<T> _onElementSelected;

		public ItemSearchableDropdown(AdvancedDropdownState state, TableComponent tableComponent, string title, Action<T> onElementSelected) : base(state)
		{
			_onElementSelected = onElementSelected;
			_tableComponent = tableComponent;
			minimumSize = new Vector2(200, 300);
			_title = title;
		}

		protected override AdvancedDropdownItem BuildRoot()
		{
			var node = new AdvancedDropdownItem(_title);
			var elements = _tableComponent.GetComponentsInChildren<T>();
			node.AddChild(new ElementDropdownItem<T>(null));
			foreach (var element in elements) {
				node.AddChild(new ElementDropdownItem<T>(element));
			}
			return node;
		}

		protected override void ItemSelected(AdvancedDropdownItem item)
		{
			var elementItem = (ElementDropdownItem<T>) item;
			_onElementSelected?.Invoke(elementItem?.Item);
		}

		private class ElementDropdownItem<TItem> : AdvancedDropdownItem where TItem : class, IIdentifiableItemComponent
		{
			public readonly TItem Item;

			public ElementDropdownItem(TItem element) : base(element == null ? "None" : element.name)
			{
				if (element != null) {
					Item = element;
					icon = Icons.ByComponent(element, color: IconColor.Gray, size: IconSize.Small);
				}
			}
		}
	}
}
