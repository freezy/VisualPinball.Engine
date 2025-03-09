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

// ReSharper disable InconsistentNaming

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SoundAsset)), CanEditMultipleObjects]
	public class SoundAssetInspector : UnityEditor.Editor
	{
		[SerializeField]
		private VisualTreeAsset _soundAssetInspectorAsset;

		public override VisualElement CreateInspectorGUI()
		{
			return _soundAssetInspectorAsset.Instantiate();
		}

		private void OnDisable()
		{
			RemoveNullClips();
		}

		private void RemoveNullClips()
		{
			var clipsProp = serializedObject.FindProperty(nameof(SoundAsset.Clips));
			for (var i = clipsProp.arraySize -1; i >= 0; i--) {
				if (clipsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
					clipsProp.DeleteArrayElementAtIndex(i);
			}
			serializedObject.ApplyModifiedPropertiesWithoutUndo();
		}

	}
}
