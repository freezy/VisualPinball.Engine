// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
	public class SwitchSoundInspector : BinaryEventSoundInspector
	{
		[SerializeField]
		private VisualTreeAsset switchSoundInspectorXml;

		public override VisualElement CreateInspectorGUI()
		{
			var root = base.CreateInspectorGUI();
			var inspectorUi = switchSoundInspectorXml.Instantiate();
			root.Add(inspectorUi);
			var switchNameDropdown = root.Q<DropdownField>("switch-name");
			var switchNameProp = serializedObject.FindProperty(nameof(SwitchSoundComponent.SwitchName));
			var availableSwitches = GetAvailableSwitches();
			ConfigureDropdown(switchNameDropdown, switchNameProp, availableSwitches);
			return root;
		}

		private Dictionary<string, string> GetAvailableSwitches()
		{
			if (
				target != null
				&& target is Component t
				&& t.TryGetComponent<ISwitchDeviceComponent>(out var switchDevice)
			)
			{
				return switchDevice.AvailableSwitches.ToDictionary(
					i => i.Id,
					i => string.IsNullOrWhiteSpace(i.Description) ? i.Id : i.Description
				);
			}
			return new Dictionary<string, string>();
		}
	}
}
