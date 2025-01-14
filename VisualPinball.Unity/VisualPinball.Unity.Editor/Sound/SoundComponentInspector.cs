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

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SoundComponent), editorForChildClasses: true), CanEditMultipleObjects]
	public class SoundComponentInspector : UnityEditor.Editor
	{
		public override VisualElement CreateInspectorGUI()
		{
			var container = new VisualElement();
			AddHelpBoxes(container);
			InspectorElement.FillDefaultInspector(container, serializedObject, this);
			return container;
		}

		protected virtual void AddHelpBoxes(VisualElement container)
		{
			MissingComponentHelpBox(container);
			InvalidSoundAssetHelpBox(container);
			InfiniteLoopHelpBox(container);
		}

		protected void InvalidSoundAssetHelpBox(VisualElement container)
		{
			var helpBox = new HelpBox(
				"The selected sound asset is invalid. Make sure it has at least one audio clip.",
				HelpBoxMessageType.Warning);
			container.Add(helpBox);
			var soundAssetProp = serializedObject.FindProperty(nameof(SoundComponent._soundAsset));
			UpdateVisibility(soundAssetProp);
			helpBox.TrackPropertyValue(soundAssetProp, UpdateVisibility);

			void UpdateVisibility(SerializedProperty prop)
			{
				var soundAsset = prop.objectReferenceValue as SoundAsset;
				if (soundAsset == null || soundAsset.IsValid()) {
					helpBox.style.display = DisplayStyle.None;

				} else {
					helpBox.style.display = DisplayStyle.Flex;
				}
			}
		}

		protected void MissingComponentHelpBox(VisualElement container)
		{
			if (target != null && target is SoundComponent) {
				var soundComp = target as SoundComponent;
				var requiredType = soundComp.GetRequiredType();
				if (requiredType != null && !soundComp.TryGetComponent(requiredType, out _)) {
					container.Add(new HelpBox($"This component needs a component of type {requiredType.Name} on the same game object to work.",
						HelpBoxMessageType.Error));
				}
			}
		}

		private bool AllTargetsSupportLoopingSoundAssets()
		{
			foreach (var t in targets) {
				if (t == null) {
					continue;
				}

				if (t is not SoundComponent) {
					continue;
				}

				if (!(t as SoundComponent).SupportsLoopingSoundAssets()) {
					return false;
				}
			}
			return true;
		}

		protected void InfiniteLoopHelpBox(VisualElement container)
		{
			var soundAssetProp = serializedObject.FindProperty("_soundAsset");
			var helpBox = new HelpBox("The assigned sound asset loops, but this component " +
				"provides no mechanism to stop it or is not configured to do so. Either assign" +
				" a sound asset that does not loop or (if possible) configure this component to" +
				" stop the sound.", HelpBoxMessageType.Warning);
			container.Add(helpBox);
			UpdateVisbility(serializedObject);
			helpBox.TrackSerializedObjectValue(serializedObject, UpdateVisbility);

			void UpdateVisbility(SerializedObject obj)
			{
				var prop = obj.FindProperty(nameof(SoundComponent._soundAsset));
				var soundAsset = prop.objectReferenceValue as SoundAsset;
				if (soundAsset && soundAsset.Loop && !AllTargetsSupportLoopingSoundAssets()) {
					helpBox.style.display = DisplayStyle.Flex;
				} else {
					helpBox.style.display = DisplayStyle.None;
				}
			}
		}

		protected static void ConfigureDropdown(DropdownField dropdown, SerializedProperty boundProp, Dictionary<string, string> idsToDisplayNames)
		{
			var displayNamesToIds = idsToDisplayNames.ToDictionary(i => i.Value, i => i.Key);
			dropdown.choices = displayNamesToIds.Keys.ToList();
			UpdateDropdown(boundProp);
			dropdown.RegisterValueChangedCallback(e => UpdateProperty(dropdown));
			dropdown.TrackPropertyValue(boundProp, UpdateDropdown);

			void UpdateProperty(DropdownField dd)
			{
				var displayName = dd.value;
				boundProp.stringValue = displayNamesToIds.GetValueOrDefault(displayName);
				boundProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
			}

			void UpdateDropdown(SerializedProperty property)
			{
				var id = property.stringValue;
				if (idsToDisplayNames.TryGetValue(id, out var displayName)) {
					dropdown.value = displayName;

				} else if (string.IsNullOrEmpty(id) && idsToDisplayNames.Count > 0) {
					dropdown.value = idsToDisplayNames.First().Value;
					property.stringValue = idsToDisplayNames.First().Key;
					property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
				}
			}
		}
	}
}
