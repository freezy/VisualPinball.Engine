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

// ReSharper disable InconsistentNaming

using System.Linq;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SoundComponent)), CanEditMultipleObjects]
	public class SoundComponentInspector : UnityEditor.Editor
	{
		[SerializeField]
		private VisualTreeAsset inspectorXml;

		public override VisualElement CreateInspectorGUI()
		{
			var container = new VisualElement();
			var comp = target as SoundComponent;
			if (!comp!.TryGetComponent<ISoundEmitter>(out var _)) {
				container.Add(new HelpBox("Cannot find sound emitter. This component only works with a sound emitter on the same GameObject.", HelpBoxMessageType.Warning));
				return container;
			}

			var inspectorUi = inspectorXml.Instantiate();
			container.Add(inspectorUi);

			var triggerDropdown = container.Q<DropdownField>("trigger-id");
			var stopTriggerDropdown = container.Q<DropdownField>("stop-trigger-id");
			var hasStopTriggerField = container.Q<PropertyField>("has-stop-trigger");
			var availableTriggers = GetAvailableTriggers();
			if (availableTriggers.Length > 0) {
				var triggerIdProp = serializedObject.FindProperty("_triggerId");
				var stopTriggerIdProp = serializedObject.FindProperty("_stopTriggerId");
				ConfigureTriggerDropdown(triggerIdProp, triggerDropdown, availableTriggers);
				ConfigureTriggerDropdown(stopTriggerIdProp, stopTriggerDropdown, availableTriggers);
				hasStopTriggerField.RegisterValueChangeCallback(
					e => stopTriggerDropdown.style.display = e.changedProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None);
				var hasStopTriggerProp = serializedObject.FindProperty("_hasStopTrigger");
				InfiniteLoopHelpBox(container, hasStopTriggerField, hasStopTriggerProp);
			} else {
				AddNoTriggersHelpBox(container, triggerDropdown, stopTriggerDropdown, hasStopTriggerField);
			}

			InvalidSoundAssetHelpBox(container);
			serializedObject.ApplyModifiedProperties();
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

		private static void AddNoTriggersHelpBox(VisualElement container, DropdownField triggerDropdown, DropdownField stopTriggerDropdown, PropertyField hasStopTriggerField)
		{
			container.Insert(0, new HelpBox("There are no triggers to choose from", HelpBoxMessageType.Info));
			triggerDropdown.style.display = DisplayStyle.None;
			stopTriggerDropdown.style.display = DisplayStyle.None;
			hasStopTriggerField.style.display = DisplayStyle.None;
		}

		private void InfiniteLoopHelpBox(VisualElement container, PropertyField hasStopTriggerField, SerializedProperty hasStopTriggerProp)
		{
			var soundAssetProp = serializedObject.FindProperty("_soundAsset");
			var infiniteLoopHelpBox = new HelpBox("The selected sound asset loops and no stop trigger is set, so the sound will loop forever once started.", HelpBoxMessageType.Warning);
			UpdateVisbility();
			container.Insert(0, infiniteLoopHelpBox);
			var soundAssetField = container.Q<ObjectField>("sound-asset");
			soundAssetField.RegisterValueChangedCallback(e => UpdateVisbility());
			hasStopTriggerField.RegisterValueChangeCallback(e => UpdateVisbility());

			void UpdateVisbility()
			{
				var soundAsset = soundAssetProp.objectReferenceValue as SoundAsset;
				if (soundAsset && soundAsset.Loop && !hasStopTriggerProp.boolValue)
					infiniteLoopHelpBox.style.display = DisplayStyle.Flex;
				else
					infiniteLoopHelpBox.style.display = DisplayStyle.None;
			}
		}

		private void InvalidSoundAssetHelpBox(VisualElement container)
		{
			var soundAssetProp = serializedObject.FindProperty("_soundAsset");
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

		private SoundTrigger[] GetAvailableTriggers()
		{
			if (target is Component && (target as Component).TryGetComponent<ISoundEmitter>(out var emitter))
				return emitter.AvailableTriggers;
			return Array.Empty<SoundTrigger>();
		}
	}
}
