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

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BallRollingSoundComponent)), CanEditMultipleObjects]
	public class BallRollingSoundInspector : UnityEditor.Editor
	{
		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();

			AddNoAssetHelpBox(root);
			AddNotLoopingHelpBox(root);
			AddNoPlayerHelpBox(root);

			InspectorElement.FillDefaultInspector(root, serializedObject, this);

			return root;
		}

		private void AddNoAssetHelpBox(VisualElement root)
		{
			var helpBox = new HelpBox(
				"Assign a SoundEffectAsset with a looping audio clip to enable ball rolling sounds.",
				HelpBoxMessageType.Warning
			);

			var assetProp = serializedObject.FindProperty(
				nameof(BallRollingSoundComponent.RollingSoundAsset)
			);

			UpdateVisibility(assetProp);
			helpBox.TrackPropertyValue(assetProp, UpdateVisibility);
			root.Add(helpBox);

			void UpdateVisibility(SerializedProperty prop)
			{
				var hasAsset = prop.objectReferenceValue != null;
				helpBox.style.display = hasAsset ? DisplayStyle.None : DisplayStyle.Flex;
			}
		}

		private void AddNotLoopingHelpBox(VisualElement root)
		{
			var helpBox = new HelpBox(
				"The assigned SoundEffectAsset does not have its Loop flag enabled. " +
				"The rolling sound will stop after the first playback.",
				HelpBoxMessageType.Warning
			);

			var assetProp = serializedObject.FindProperty(
				nameof(BallRollingSoundComponent.RollingSoundAsset)
			);

			UpdateVisibility(assetProp);
			helpBox.TrackPropertyValue(assetProp, UpdateVisibility);
			root.Add(helpBox);

			void UpdateVisibility(SerializedProperty prop)
			{
				if (prop.objectReferenceValue is SoundEffectAsset asset && !asset.Loop) {
					helpBox.style.display = DisplayStyle.Flex;
				} else {
					helpBox.style.display = DisplayStyle.None;
				}
			}
		}

		private void AddNoPlayerHelpBox(VisualElement root)
		{
			if (target is not BallRollingSoundComponent comp)
				return;

			var player = comp.GetComponentInChildren<Player>();
			if (player == null) {
				root.Add(new HelpBox(
					"No Player component found as a child. " +
					"Make sure this component is placed on the table root GameObject.",
					HelpBoxMessageType.Error
				));
			}
		}
	}
}
