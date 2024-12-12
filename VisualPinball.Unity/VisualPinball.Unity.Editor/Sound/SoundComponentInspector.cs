using System.Linq;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VisualPinball.Unity;
using UnityEditor.UIElements;

[CustomEditor(typeof(SoundComponent)), CanEditMultipleObjects]
public class SoundComponentInspector : Editor
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

		inspectorXml.CloneTree(container);

		var triggerDropdown = container.Q<DropdownField>("trigger-id");
		var stopTriggerDropdown = container.Q<DropdownField>("stop-trigger-id");
		var hasStopTriggerToggle = container.Q<Toggle>("has-stop-trigger");
		var availableTriggers = GetAvailableTriggers();
		if (availableTriggers.Length > 0) {
			var triggerIdProp = serializedObject.FindProperty("_triggerId");
			var stopTriggerIdProp = serializedObject.FindProperty("_stopTriggerId");
			ConfigureTriggerDropdown(triggerIdProp, triggerDropdown, availableTriggers);
			ConfigureTriggerDropdown(stopTriggerIdProp, stopTriggerDropdown, availableTriggers);
			hasStopTriggerToggle.RegisterValueChangedCallback(
				e => stopTriggerDropdown.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None);
			var hasStopTriggerProp = serializedObject.FindProperty("_hasStopTrigger");
			InfiniteLoopHelpBox(container, hasStopTriggerToggle, hasStopTriggerProp);
		} else {
			AddNoTriggersHelpBox(container, triggerDropdown, stopTriggerDropdown, hasStopTriggerToggle);
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

	private static void AddNoTriggersHelpBox(VisualElement container, DropdownField triggerDropdown, DropdownField stopTriggerDropdown, Toggle hasStopTriggerToggle)
	{
		container.Insert(0, new HelpBox("There are no triggers to choose from", HelpBoxMessageType.Info));
		triggerDropdown.style.display = DisplayStyle.None;
		stopTriggerDropdown.style.display = DisplayStyle.None;
		hasStopTriggerToggle.style.display = DisplayStyle.None;
	}

	private void InfiniteLoopHelpBox(VisualElement container, Toggle hasStopTriggerToggle, SerializedProperty hasStopTriggerProp)
	{
		var soundAssetProp = serializedObject.FindProperty("_soundAsset");
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
