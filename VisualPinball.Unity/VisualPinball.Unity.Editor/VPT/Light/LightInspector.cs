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
	[CustomEditor(typeof(LightAuthoring)), CanEditMultipleObjects]
	public class LightInspector : ItemMainInspector<Light, LightData, LightAuthoring>
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

			_positionProperty = serializedObject.FindProperty(nameof(LightAuthoring.Position));
			_surfaceProperty = serializedObject.FindProperty(nameof(LightAuthoring.Surface));
			_bulbSizeProperty = serializedObject.FindProperty(nameof(LightAuthoring.BulbSize));

			_stateProperty = serializedObject.FindProperty(nameof(LightAuthoring.State));
			_blinkPatternProperty = serializedObject.FindProperty(nameof(LightAuthoring.BlinkPattern));
			_blinkIntervalProperty = serializedObject.FindProperty(nameof(LightAuthoring.BlinkInterval));
			_fadeSpeedUpProperty = serializedObject.FindProperty(nameof(LightAuthoring.FadeSpeedUp));
			_fadeSpeedDownProperty = serializedObject.FindProperty(nameof(LightAuthoring.FadeSpeedDown));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_surfaceProperty, updateTransforms: true);
			PropertyField(_bulbSizeProperty, "Bulb Mesh Size", updateTransforms: true);

			if (_foldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutState, "State")) {
				DropDownProperty("State", _blinkPatternProperty, LightStateLabels, LightStateValues);
				PropertyField(_blinkPatternProperty);
				PropertyField(_blinkIntervalProperty);
				PropertyField(_fadeSpeedUpProperty);
				PropertyField(_fadeSpeedDownProperty);
			}

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
