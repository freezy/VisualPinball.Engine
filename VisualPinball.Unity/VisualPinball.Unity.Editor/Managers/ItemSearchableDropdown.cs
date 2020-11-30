// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class ItemSearchableDropdown<T> : AdvancedDropdown where T : class, IIdentifiableItemAuthoring
	{
		private readonly string _title;

		private readonly TableAuthoring _tableAuthoring;

		private readonly Action<T> _onElementSelected;

		public ItemSearchableDropdown(AdvancedDropdownState state, TableAuthoring tableAuthoring, string title, Action<T> onElementSelected) : base(state)
		{
			_onElementSelected = onElementSelected;
			_tableAuthoring = tableAuthoring;
			minimumSize = new Vector2(200, 300);
			_title = title;
		}

		protected override AdvancedDropdownItem BuildRoot()
		{
			var node = new AdvancedDropdownItem(_title);
			var elements = _tableAuthoring.GetComponentsInChildren<T>();
			foreach (var element in elements) {
				node.AddChild(new ElementDropdownItem<T>(element));
			}
			return node;
		}

		protected override void ItemSelected(AdvancedDropdownItem item)
		{
			var elementItem = (ElementDropdownItem<T>) item;
			_onElementSelected?.Invoke(elementItem.Item);
		}

		private class ElementDropdownItem<TItem> : AdvancedDropdownItem where TItem : class, IIdentifiableItemAuthoring
		{
			public readonly TItem Item;

			public ElementDropdownItem(TItem element) : base(element.Name)
			{
				Item = element;
				icon = Icons.ByComponent(element, color: IconColor.Gray, size: IconSize.Small);
			}
		}
	}
}
