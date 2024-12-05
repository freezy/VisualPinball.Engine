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

using System;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(MechSound))]
	public class MechSoundDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var container = new VisualElement();
			var treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
				"Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Sound/SoundDrawer.uxml");
			treeAsset.CloneTree(container);
			var triggerDropdown = container.Q<DropdownField>("trigger-id");
			var stopTriggerDropdown = container.Q<DropdownField>("stop-trigger-id");
			var hasStopTriggerToggle = container.Q<Toggle>("has-stop-trigger");
			var availableTriggers = GetAvailableTriggers(property);
			if (availableTriggers.Length > 0) {
				var triggerIdProp = property.FindPropertyRelative("TriggerId");
				var stopTriggerIdProp = property.FindPropertyRelative("StopTriggerId");				
				ConfigureTriggerDropdown(triggerIdProp, triggerDropdown, availableTriggers);
				ConfigureTriggerDropdown(stopTriggerIdProp, stopTriggerDropdown, availableTriggers);
				hasStopTriggerToggle.RegisterValueChangedCallback(
					e => stopTriggerDropdown.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None);
				var hasStopTriggerProp = property.FindPropertyRelative("HasStopTrigger");
				InfiniteLoopHelpBox(property, container, hasStopTriggerToggle, hasStopTriggerProp);
			} else {
				AddNoTriggersHelpBox(container, triggerDropdown, stopTriggerDropdown, hasStopTriggerToggle);
			}
			InvalidSoundAssetHelpBox(property, container);
			property.serializedObject.ApplyModifiedProperties();
			return container;
		}

        private static void ConfigureTriggerDropdown(SerializedProperty triggerIdProp, DropdownField triggerDropdown, SoundTrigger[] availableTriggers)
        {
            var availableTriggerNames = availableTriggers.Select(t => t.Name).ToList();
            triggerDropdown.choices = availableTriggerNames;

            var isSelectedTriggerValid = availableTriggers.Any(t => t.Id == triggerIdProp.stringValue);
            if (isSelectedTriggerValid) {
                triggerDropdown.value = availableTriggers.First(t => t.Id == triggerIdProp.stringValue).Name;
            } else {
                triggerDropdown.value = availableTriggerNames[0];
                triggerIdProp.stringValue = availableTriggers[0].Id;
            }

            triggerDropdown.RegisterValueChangedCallback(
                e => {
					triggerIdProp.stringValue = availableTriggers.FirstOrDefault(t => t.Name == e.newValue).Id;
					triggerIdProp.serializedObject.ApplyModifiedProperties();
				});
        }

		private static void AddNoTriggersHelpBox(VisualElement container, DropdownField triggerDropdown, DropdownField stopTriggerDropdown, Toggle hasStopTriggerToggle)
		{
			container.Insert(0, new HelpBox("There are no triggers to choose from", HelpBoxMessageType.Info));
			triggerDropdown.style.display = DisplayStyle.None;
			stopTriggerDropdown.style.display = DisplayStyle.None;
			hasStopTriggerToggle.style.display = DisplayStyle.None;
		}

		private static void InfiniteLoopHelpBox(SerializedProperty rootProp, VisualElement container, Toggle hasStopTriggerToggle, SerializedProperty hasStopTriggerProp)
		{
			var soundAssetProp = rootProp.FindPropertyRelative("Sound");
			var infiniteLoopHelpBox = new HelpBox("The selected sound asset loops and no stop trigger is set, so the sound will loop forever once started.", HelpBoxMessageType.Warning);
			UpdateVisbility();
			container.Insert(0, infiniteLoopHelpBox);
			var soundAssetField = container.Q<ObjectField>("sound-asset");
			soundAssetField.RegisterValueChangedCallback(
				e => UpdateVisbility());
			hasStopTriggerToggle.RegisterValueChangedCallback(
				e => UpdateVisbility());

			void UpdateVisbility()
			{
				var soundAsset = soundAssetProp.objectReferenceValue as SoundAsset;
				if (soundAsset && soundAsset.Loop && !hasStopTriggerProp.boolValue)
					infiniteLoopHelpBox.style.display = DisplayStyle.Flex;
				else
					infiniteLoopHelpBox.style.display = DisplayStyle.None;
			}
		}

		private static void InvalidSoundAssetHelpBox(SerializedProperty rootProp, VisualElement container)
		{
			var soundAssetProp = rootProp.FindPropertyRelative("Sound");
			var infiniteLoopHelpBox = new HelpBox("The selected sound asset is invalid. Make sure it has at least one audio clip", HelpBoxMessageType.Warning);
			UpdateVisibility();
			container.Insert(0, infiniteLoopHelpBox);
			var soundAssetField = container.Q<ObjectField>("sound-asset");
			soundAssetField.RegisterValueChangedCallback(
				e => UpdateVisibility());

			void UpdateVisibility()
			{
				var soundAsset = soundAssetProp.objectReferenceValue as SoundAsset;
				if (soundAsset == null || soundAsset.IsValid())
					infiniteLoopHelpBox.style.display = DisplayStyle.None;
				else
					infiniteLoopHelpBox.style.display = DisplayStyle.Flex;
			}
		}

		private static SoundTrigger[] GetAvailableTriggers(SerializedProperty property)
		{
			var mechSoundsComponent = (MechSoundsComponent)property.serializedObject.targetObject;
			if (mechSoundsComponent.TryGetComponent<ISoundEmitter>(out var emitter))
				return emitter.AvailableTriggers;
			else
				return Array.Empty<SoundTrigger>();
		}
	}
}
