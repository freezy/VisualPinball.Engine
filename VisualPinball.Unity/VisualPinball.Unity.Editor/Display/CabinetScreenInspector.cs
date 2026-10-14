// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CabinetScreenComponent)), CanEditMultipleObjects]
	public class CabinetScreenInspector : ItemInspector
	{
		private SerializedProperty _roleProperty;
		private SerializedProperty _customRoleProperty;
		private SerializedProperty _targetProperty;
		private SerializedProperty _materialIndexProperty;
		private SerializedProperty _textureProperty;
		private SerializedProperty _hideRootProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_roleProperty = serializedObject.FindProperty(nameof(CabinetScreenComponent.Role));
			_customRoleProperty = serializedObject.FindProperty(nameof(CabinetScreenComponent.CustomRole));
			_targetProperty = serializedObject.FindProperty(nameof(CabinetScreenComponent.Target));
			_materialIndexProperty = serializedObject.FindProperty(nameof(CabinetScreenComponent.MaterialIndex));
			_textureProperty = serializedObject.FindProperty(nameof(CabinetScreenComponent.TextureProperty));
			_hideRootProperty = serializedObject.FindProperty(nameof(CabinetScreenComponent.HideRoot));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			PropertyField(_roleProperty);
			if (_roleProperty.hasMultipleDifferentValues || _roleProperty.enumValueIndex == (int)CabinetScreenRole.Custom) {
				PropertyField(_customRoleProperty, "Custom Role");
			}
			PropertyField(_targetProperty);
			PropertyField(_materialIndexProperty, "Material Slot");
			PropertyField(_textureProperty, "Texture Property");
			PropertyField(_hideRootProperty, "Hide When Unused");

			if (!serializedObject.isEditingMultipleObjects && target is CabinetScreenComponent screen) {
				Validate(screen);
			}

			base.OnInspectorGUI();

			EndEditing();
		}

		private static void Validate(CabinetScreenComponent screen)
		{
			var renderer = screen.TargetRenderer;
			if (renderer == null) {
				EditorGUILayout.HelpBox("Set a target renderer, or add this component to a GameObject with a renderer.", MessageType.Warning);
				return;
			}
			var materials = renderer.sharedMaterials;
			if (screen.MaterialIndex < 0 || screen.MaterialIndex >= materials.Length) {
				EditorGUILayout.HelpBox($"\"{renderer.name}\" has no material slot {screen.MaterialIndex}.", MessageType.Warning);
				return;
			}
			var property = CabinetScreenBinder.TextureProperty(materials[screen.MaterialIndex], screen.TextureProperty);
			if (property == null) {
				EditorGUILayout.HelpBox("The material has no texture property to show the screen in.", MessageType.Warning);
				return;
			}
			if (screen.Role == CabinetScreenRole.Custom && string.IsNullOrWhiteSpace(screen.CustomRole)) {
				EditorGUILayout.HelpBox("Set the role name the plugin publishes the screen with.", MessageType.Warning);
				return;
			}
			EditorGUILayout.HelpBox($"The screen is shown in {property}.", MessageType.None);
		}
	}
}
