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
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BumperColliderAuthoring)), CanEditMultipleObjects]
	public class BumperColliderInspector : ItemColliderInspector<BumperData, BumperAuthoring, BumperColliderAuthoring>
	{
		private SerializedProperty _hitEventProperty;
		private SerializedProperty _thresholdProperty;
		private SerializedProperty _forceProperty;
		private SerializedProperty _scatterProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_thresholdProperty = serializedObject.FindProperty(nameof(BumperColliderAuthoring.Threshold));
			_forceProperty = serializedObject.FindProperty(nameof(BumperColliderAuthoring.Force));
			_scatterProperty = serializedObject.FindProperty(nameof(BumperColliderAuthoring.Scatter));
			_hitEventProperty = serializedObject.FindProperty(nameof(BumperColliderAuthoring.HitEvent));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			PropertyField(_hitEventProperty, "Has Hit Event");
			PropertyField(_forceProperty);
			PropertyField(_thresholdProperty, "Hit Threshold");
			PropertyField(_scatterProperty, "Scatter Angle");

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
