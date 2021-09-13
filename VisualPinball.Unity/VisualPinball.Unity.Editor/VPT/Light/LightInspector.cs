// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightComponent)), CanEditMultipleObjects]
	public class LightInspector : ItemMainInspector<LightData, LightComponent>
	{
		private bool _foldoutState = true;

		private static readonly string[] LightStateLabels = { "Off", "On", "Blinking" };
		private static readonly int[] LightStateValues = { LightStatus.LightStateOff, LightStatus.LightStateOn, LightStatus.LightStateBlinking };

		private SerializedProperty _positionProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _bulbSizeProperty;
		private SerializedProperty _stateProperty;
		private SerializedProperty _blinkPatternProperty;
		private SerializedProperty _blinkIntervalProperty;
		private SerializedProperty _fadeSpeedUpProperty;
		private SerializedProperty _fadeSpeedDownProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(LightComponent.Position));
			_surfaceProperty = serializedObject.FindProperty(nameof(LightComponent._surface));
			_bulbSizeProperty = serializedObject.FindProperty(nameof(LightComponent.BulbSize));

			_stateProperty = serializedObject.FindProperty(nameof(LightComponent.State));
			_blinkPatternProperty = serializedObject.FindProperty(nameof(LightComponent.BlinkPattern));
			_blinkIntervalProperty = serializedObject.FindProperty(nameof(LightComponent.BlinkInterval));
			_fadeSpeedUpProperty = serializedObject.FindProperty(nameof(LightComponent.FadeSpeedUp));
			_fadeSpeedDownProperty = serializedObject.FindProperty(nameof(LightComponent.FadeSpeedDown));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_surfaceProperty, updateTransforms: true);
			PropertyField(_bulbSizeProperty, "Bulb Mesh Size", updateTransforms: true);

			if (_foldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutState, "State")) {
				DropDownProperty("State", _stateProperty, LightStateLabels, LightStateValues);
				PropertyField(_blinkPatternProperty);
				PropertyField(_blinkIntervalProperty);
				PropertyField(_fadeSpeedUpProperty);
				PropertyField(_fadeSpeedDownProperty);
			}

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
