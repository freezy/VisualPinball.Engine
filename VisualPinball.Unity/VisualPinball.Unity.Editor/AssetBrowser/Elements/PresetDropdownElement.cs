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
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[UxmlElement]
	public partial class PresetDropdownElement : VisualElement
	{
		private readonly DropdownField _dropdown;
		private readonly ObjectField _objectPicker;

		[UxmlAttribute("label")]
		private string Label { set => _dropdown.label = value; }

		[UxmlAttribute("binding-path")]
		private string BindingPath { set => _objectPicker.bindingPath = value; }

		[UxmlAttribute("tooltip")]
		private string Tooltip { set => _dropdown.tooltip = value; }

		[UxmlAttribute("preset-path")]
		private string PresetPath {
			set {
				if (string.IsNullOrEmpty(value)) {
					return;
				}
				_presetPath = value;
				var presets = Directory.GetFiles(_presetPath).Where(p => !p.Contains(".meta"));
				_presets = presets.Select(filename => (Preset)AssetDatabase.LoadAssetAtPath(filename, typeof(Preset))).ToList();
				_dropdown.choices = new List<string> { "<Default>" };
				_dropdown.choices.AddRange(_presets.Select(p => p.name));
			}
		}

		private string _presetPath;
		private string _defaultPresetName;
		private List<Preset> _presets;

		public PresetDropdownElement()
		{
			_objectPicker = new ObjectField {
				objectType = typeof(Preset),
				style = {
					display = DisplayStyle.None
				}
			};
			_dropdown = new DropdownField();
			_dropdown.RegisterValueChangedCallback(OnDropdownValueChanged);

			Add(_objectPicker);
			Add(_dropdown);
		}

		public void SetValue(Preset preset)
		{
			if (preset == null) {
				_dropdown.index = 0;
			} else {
				_dropdown.value = preset.name;
			}
		}

		private void OnDropdownValueChanged(ChangeEvent<string> evt)
		{
			if (evt.newValue == evt.previousValue) {
				return;
			}
			_objectPicker.value = _presets.FirstOrDefault(p => p.name == evt.newValue);
		}
	}
}
