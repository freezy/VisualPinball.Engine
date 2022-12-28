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
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class ObjectDropdownElement : VisualElement
	{
		private readonly DropdownField _dropdown;
		private readonly ObjectField _objectPicker;

		#region Uxml

		public new class UxmlFactory : UxmlFactory<ObjectDropdownElement, UxmlTraits> { }

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
				var ate = ve as ObjectDropdownElement;

				ate!.Label = _label.GetValueFromBag(bag, cc);
				ate!.BindingPath = _bindingPath.GetValueFromBag(bag, cc);
				ate!.Tooltip = _tooltip.GetValueFromBag(bag, cc);
			}
		}

		#endregion

		private string Label { set => _dropdown.label = value; }
		private string BindingPath { set => _objectPicker.bindingPath = value; }
		private string Tooltip { set => _dropdown.tooltip = value; }

		public Object Value { get => _objectPicker.value; set => SetValue(value); }
		public bool HasValue => _objectPicker.value as GameObject != null;

		private readonly List<GameObject> _objects = new();
		private Action<Object> _onValueChanged;

		public ObjectDropdownElement()
		{
			_objectPicker = new ObjectField {
				objectType = typeof(GameObject),
				style = {
					display = DisplayStyle.None
				}
			};
			_dropdown = new DropdownField();
			_dropdown.RegisterValueChangedCallback(OnDropdownValueChanged);

			Add(_objectPicker);
			Add(_dropdown);
		}

		public void RegisterValueChangedCallback(Action<Object> onValueChanged)
		{
			_onValueChanged = onValueChanged;
		}

		public void AddObjectsToDropdown<T>(Object parentObj, bool includeInactive = false) where T : Component
		{
			if (parentObj is not GameObject parentGo) {
				return;
			}
			_objects.Clear();
			_objects.AddRange(parentGo.GetComponentsInChildren<T>(includeInactive).Select(c => c.gameObject));
			_dropdown.choices = _objects.Select(go => go.name).ToList();
			if (_objectPicker.value) {
				_dropdown.SetValueWithoutNotify(_objectPicker.value.name);
			}
		}

		public void SetValue(Object obj)
		{
			if (obj == null) {
				_dropdown.SetValueWithoutNotify(null);
				_objectPicker.value = null;
				return;
			}
			var selectedGo = _objects.FirstOrDefault(go => go.name == obj.name);
			if (selectedGo != null) {
				_dropdown.SetValueWithoutNotify(obj.name);
				_objectPicker.value = selectedGo;
			}
		}

		private void OnDropdownValueChanged(ChangeEvent<string> evt)
		{
			var selectedGo = _objects.FirstOrDefault(go => go.name == evt.newValue);
			if (selectedGo != null) {
				_objectPicker.value = selectedGo;
				_onValueChanged?.Invoke(selectedGo);
			}
		}
	}
}
