// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[UxmlElement]
	public partial class MaterialSlotDropdownElement : VisualElement
	{
		private readonly DropdownField _dropdown;
		private readonly IntegerField _integerField;

		[UxmlAttribute("label")]
		public string Label { get => _dropdown.label; set => _dropdown.label = value; }

		[UxmlAttribute("binding-path")]
		public string BindingPath { get => _integerField.bindingPath; set => _integerField.bindingPath = value; }

		[UxmlAttribute("tooltip")]
		public string Tooltip { get => _dropdown.tooltip; set => _dropdown.tooltip = value; }

		public string Value { get => _dropdown.value; set => _dropdown.value = value; }

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

		public void PopulateChoices(GameObject obj)
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
