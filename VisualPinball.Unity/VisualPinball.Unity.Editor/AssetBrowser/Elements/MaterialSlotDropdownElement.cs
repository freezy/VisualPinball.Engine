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
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class MaterialSlotDropdownElement : VisualElement
	{
		private readonly DropdownField _dropdown;
		private readonly IntegerField _integerField;

		public new class UxmlFactory : UxmlFactory<MaterialSlotDropdownElement, UxmlTraits> { }

		public new class UxmlTraits : VisualElement.UxmlTraits
		{
			private readonly UxmlStringAttributeDescription _label = new() { name = "label" };
			private readonly UxmlStringAttributeDescription _bindingPath = new() { name = "binding-path" };
			private readonly UxmlStringAttributeDescription _tooltip = new() { name = "tooltip" };

			public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
			{
				get { yield break; }
			}

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
			{
				base.Init(ve, bag, cc);
				var ate = ve as MaterialSlotDropdownElement;

				ate!.Label = _label.GetValueFromBag(bag, cc);
				ate!.BindingPath = _bindingPath.GetValueFromBag(bag, cc);
				ate!.Tooltip = _tooltip.GetValueFromBag(bag, cc);
			}
		}

		public string Label { get => _dropdown.label; set => _dropdown.label = value; }
		public string BindingPath { get => _integerField.bindingPath; set => _integerField.bindingPath = value; }
		public string Value { get => _dropdown.value; set => _dropdown.value = value; }
		public string Tooltip { get => _dropdown.tooltip; set => _dropdown.tooltip = value; }

		private readonly List<Material> _materials = new();

		public MaterialSlotDropdownElement()
		{
			_integerField = new IntegerField {
				style = {
					display = DisplayStyle.None
				}
			};
			_dropdown = new DropdownField();
			_dropdown.RegisterValueChangedCallback(OnDropdownValueChanged);

			Add(_integerField);
			Add(_dropdown);
		}

		public void SetObject(GameObject obj)
		{
			var renderer = obj.GetComponent<Renderer>();
			if (!renderer) {
				return;
			}

			_materials.Clear();
			_materials.AddRange(renderer.sharedMaterials);

			_dropdown.choices = _materials.Select((mat, slot) => SlotName(slot, mat)).ToList();
			if (_integerField.value >= 0 && _integerField.value < _materials.Count) {
				_dropdown.SetValueWithoutNotify(SlotName(_integerField.value, _materials[_integerField.value]));
			}
		}

		private static string SlotName(int slot, Material material)
		{
			var materialName = material ? material.name : "<empty>";
			return $"[Slot {slot}] {materialName}";
		}

		public void SetValue(int slot)
		{
			if (slot < 0 || slot >= _materials.Count) {
				return;
			}

			_dropdown.SetValueWithoutNotify(SlotName(slot, _materials[slot]));
			_integerField.value = slot;
		}

		private void OnDropdownValueChanged(ChangeEvent<string> evt)
		{
			_integerField.value = _dropdown.index;
		}
	}
}
