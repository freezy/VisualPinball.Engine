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
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SwitchSoundComponent)), CanEditMultipleObjects]
	public class SwitchSoundComponentInspector : SoundComponentInspector
	{
		[SerializeField]
		private VisualTreeAsset inspectorXml;

		public override VisualElement CreateInspectorGUI()
		{
			var root = base.CreateInspectorGUI();
			var inspectorUi = inspectorXml.Instantiate();
			root.Add(inspectorUi);
			var switchNameDropdown = root.Q<DropdownField>("switch-name");
			var switchNameProp = serializedObject.FindProperty(nameof(SwitchSoundComponent._switchName));
			var availableSwitches = GetAvailableSwitches();
			ConfigureDropdown(switchNameDropdown, switchNameProp, availableSwitches);
			return root;
		}

		private Dictionary<string, string> GetAvailableSwitches()
		{
			var targetComponent = target as Component;
			if (targetComponent != null && targetComponent.TryGetComponent<ISwitchDeviceComponent>(out var switchDevice)) {
				return switchDevice.AvailableSwitches.ToDictionary(
					i => i.Id,
					i => string.IsNullOrWhiteSpace(i.Description) ? i.Id : i.Description
				);
			}
			return new Dictionary<string, string>();
		}
	}
}
