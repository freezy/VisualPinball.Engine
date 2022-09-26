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
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class PresetDropdownElement : VisualElement
	{
		private readonly DropdownField _dropdown;
		private readonly ObjectField _objectPicker;

		public new class UxmlFactory : UxmlFactory<PresetDropdownElement, UxmlTraits> { }

		public new class UxmlTraits : VisualElement.UxmlTraits
		{
			private readonly UxmlStringAttributeDescription _label = new() { name = "label" };
			private readonly UxmlStringAttributeDescription _bindingPath = new() { name = "binding-path" };
			private readonly UxmlStringAttributeDescription _tooltip = new() { name = "tootip" };
			private readonly UxmlStringAttributeDescription _presetPath = new() { name = "preset-path" };
			private readonly UxmlStringAttributeDescription _defaultPreset = new() { name = "default-preset" };

			public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
			{
				get { yield break; }
			}

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
			{
				base.Init(ve, bag, cc);
				var el = ve as PresetDropdownElement;

				el!.Label = _label.GetValueFromBag(bag, cc);
				el!.BindingPath = _bindingPath.GetValueFromBag(bag, cc);
				el!.Tooltip = _tooltip.GetValueFromBag(bag, cc);
				el!.PresetPath = _presetPath.GetValueFromBag(bag, cc);
				el!.DefaultPreset = _defaultPreset.GetValueFromBag(bag, cc);
			}
		}

		private string Label { set => _dropdown.label = value; }
		private string BindingPath { set => _objectPicker.bindingPath = value; }
		private string Tooltip { set => _dropdown.tooltip = value; }

		private string PresetPath {
			get => _presetPath;
			set {
				_presetPath = value;
				if (string.IsNullOrEmpty(value)) {
					return;
				}
				var presets = Directory.GetFiles(_presetPath).Where(p => !p.Contains(".meta"));
				_presets = presets.Select(filename => (Preset)AssetDatabase.LoadAssetAtPath(filename, typeof(Preset))).ToList();
				_dropdown.choices = _presets.Select(p => p.name).ToList();
			}
		}
		private string DefaultPreset {
			set {
				_defaultPresetName = value;
				if (_presets != null) {
					_defaultPreset = _presets.FirstOrDefault(p => p.name == _defaultPresetName);
				} else {
					Debug.LogWarning("Presets is not set, ignoring default preset.");
				}
			}
		}

		private string _presetPath;
		private string _defaultPresetName;
		private Preset _defaultPreset;
		private List<Preset> _presets;

		public PresetDropdownElement()
		{
			_objectPicker = new ObjectField {
				objectType = typeof(Preset),
				style = {
					display = DisplayStyle.None
				}
			};
			_objectPicker.RegisterValueChangedCallback(OnObjectPickerSet);
			_dropdown = new DropdownField();
			_dropdown.RegisterValueChangedCallback(OnDropdownValueChanged);

			Add(_objectPicker);
			Add(_dropdown);
		}

		private void OnObjectPickerSet(ChangeEvent<Object> evt)
		{
			if (evt.newValue == evt.previousValue) {
				return;
			}
			
			if (evt.newValue == null) {
				if (_defaultPreset != null) {
					_objectPicker.value = _defaultPreset;
					_dropdown.index = _presets.IndexOf(_defaultPreset);
					Debug.Log("Set thumb cam to default.");

				} else {
					Debug.LogWarning("Default preset not set.");
				}
			} else {
				_dropdown.index = _presets.IndexOf(evt.newValue as Preset);
			}
		}

		private void OnDropdownValueChanged(ChangeEvent<string> evt)
		{
			if (evt.newValue == evt.previousValue) {
				return;
			}
			var selectedPreset = _presets.FirstOrDefault(p => p.name == evt.newValue);
			if (selectedPreset != null) {
				_objectPicker.value = selectedPreset; // should also be SetValueWithoutNotify, but that doesn't apply the value. maybe a bug, to test with 2022.x
			} else {
				Debug.LogWarning($"Cannot find new preset {evt.newValue} in loaded presets.");
			}
		}
	}
}
