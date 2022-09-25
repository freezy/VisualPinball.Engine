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
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class ObjectDropdownElement : VisualElement
	{
		private readonly DropdownField _dropdown;
		private readonly ObjectField _objectPicker;

		public new class UxmlFactory : UxmlFactory<ObjectDropdownElement, UxmlTraits> { }

		public new class UxmlTraits : VisualElement.UxmlTraits
		{
			private readonly UxmlStringAttributeDescription _label = new() { name = "label" };
			private readonly UxmlStringAttributeDescription _value = new() { name = "value" };
			private readonly UxmlStringAttributeDescription _bindingPath = new() { name = "binding-path" };
			private readonly UxmlStringAttributeDescription _tooltip = new() { name = "tootip" };

			public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
			{
				get { yield break; }
			}

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
			{
				base.Init(ve, bag, cc);
				var ate = ve as ObjectDropdownElement;

				ate!.Label = _label.GetValueFromBag(bag, cc);
				ate!.Value = _value.GetValueFromBag(bag, cc);
				ate!.BindingPath = _bindingPath.GetValueFromBag(bag, cc);
				ate!.Tooltip = _tooltip.GetValueFromBag(bag, cc);
			}
		}

		public string Label { get => _dropdown.label; set => _dropdown.label = value; }
		public string BindingPath { get => _objectPicker.bindingPath; set => _objectPicker.bindingPath = value; }
		public string Value { get => _dropdown.value; set => _dropdown.value = value; }
		public string Tooltip { get => _dropdown.tooltip; set => _dropdown.tooltip = value; }

		public ObjectDropdownElement()
		{
			_objectPicker = new ObjectField();
			_dropdown = new DropdownField();

			Add(_objectPicker);
			Add(_dropdown);
		}
	}
}
