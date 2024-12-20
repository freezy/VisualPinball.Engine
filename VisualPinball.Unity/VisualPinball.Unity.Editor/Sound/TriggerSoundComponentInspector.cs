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

using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TriggerSoundComponent)), CanEditMultipleObjects]
	public class TriggerSoundComponentInspector : SoundComponentInspector
	{
		[SerializeField]
		private VisualTreeAsset inspectorXml;

		public override VisualElement CreateInspectorGUI()
		{
			var root = base.CreateInspectorGUI();
			var inspectorUi = inspectorXml.Instantiate();
			root.Add(inspectorUi);
			var triggerDropdown = root.Q<DropdownField>("trigger-id");
			var stopTriggerDropdown = root.Q<DropdownField>("stop-trigger-id");
			var hasStopTriggerField = root.Q<PropertyField>("has-stop-trigger");
			var availableTriggers = GetAvailableTriggers();
			var triggerIdProp = serializedObject.FindProperty("_triggerId");
			var stopTriggerIdProp = serializedObject.FindProperty("_stopTriggerId");
			ConfigureDropdown(triggerDropdown, triggerIdProp, availableTriggers);
			ConfigureDropdown(stopTriggerDropdown, stopTriggerIdProp, availableTriggers);
			hasStopTriggerField.RegisterValueChangeCallback(
				e => stopTriggerDropdown.style.display = e.changedProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None);

			serializedObject.ApplyModifiedProperties();
			return root;
		}

		protected override void AddHelpBoxes(VisualElement container)
		{
			base.AddHelpBoxes(container);
			NoTriggersHelpBox(container);
		}

		private void NoTriggersHelpBox(VisualElement container)
		{
			if (GetAvailableTriggers().Count == 0)
				container.Add(new HelpBox("There are no triggers to choose from", HelpBoxMessageType.Info));
		}

		private Dictionary<string, string> GetAvailableTriggers()
		{
			if (target != null &&
				target is Component &&
				(target as Component).TryGetComponent<ISoundEmitter>(out var emitter)) {
				return emitter.AvailableTriggers.ToDictionary(i => i.Id, i => i.Name);
			}
			return new();
		}
	}
}
