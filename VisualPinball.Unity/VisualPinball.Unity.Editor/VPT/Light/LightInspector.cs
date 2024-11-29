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

// ReSharper disable AssignmentInConditionalExpression

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightComponent)), CanEditMultipleObjects]
	public class LightInspector : MainInspector<LightData, LightComponent>
	{
		private bool _foldoutState = true;

		private static readonly string[] LightStateLabels = { "Off", "On", "Blinking" };
		private static readonly int[] LightStateValues = { LightStatus.LightStateOff, LightStatus.LightStateOn, LightStatus.LightStateBlinking };

		private SerializedProperty _bulbSizeProperty;
		private SerializedProperty _stateProperty;
		private SerializedProperty _blinkPatternProperty;
		private SerializedProperty _blinkIntervalProperty;
		private SerializedProperty _fadeSpeedUpProperty;
		private SerializedProperty _fadeSpeedDownProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

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

			GUILayout.Space(10);
			if (GUILayout.Button("Select Light Source")) {
				Selection.objects = MainComponent
					.GetComponentsInChildren<UnityEngine.Light>()
					.Select(l => l.gameObject as Object)
					.ToArray();
			}
		}
	}
}
