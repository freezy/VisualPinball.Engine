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
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerColliderAuthoring)), CanEditMultipleObjects]
	public class KickerColliderInspector : ItemColliderInspector<KickerData, KickerAuthoring, KickerColliderAuthoring>
	{
		private SerializedProperty _hitAccuracyProperty;
		private SerializedProperty _hitHeightProperty;
		private SerializedProperty _scatterProperty;
		private SerializedProperty _fallThroughProperty;
		private SerializedProperty _legacyModeProperty;
		private SerializedProperty _ejectAngleProperty;
		private SerializedProperty _ejectSpeedProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_hitAccuracyProperty = serializedObject.FindProperty(nameof(KickerColliderAuthoring.HitAccuracy));
			_hitHeightProperty = serializedObject.FindProperty(nameof(KickerColliderAuthoring.HitHeight));
			_scatterProperty = serializedObject.FindProperty(nameof(KickerColliderAuthoring.Scatter));
			_fallThroughProperty = serializedObject.FindProperty(nameof(KickerColliderAuthoring.FallThrough));
			_legacyModeProperty = serializedObject.FindProperty(nameof(KickerColliderAuthoring.LegacyMode));
			_ejectAngleProperty = serializedObject.FindProperty(nameof(KickerColliderAuthoring.EjectAngle));
			_ejectSpeedProperty = serializedObject.FindProperty(nameof(KickerColliderAuthoring.EjectSpeed));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			PropertyField(_hitAccuracyProperty);
			PropertyField(_hitHeightProperty);
			PropertyField(_scatterProperty, "Scatter Angle");
			PropertyField(_fallThroughProperty, "Falltrough");
			PropertyField(_legacyModeProperty, "Legacy Mode");

			EditorGUILayout.Space(20f);
			PropertyField(_ejectSpeedProperty);
			PropertyField(_ejectAngleProperty);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}

