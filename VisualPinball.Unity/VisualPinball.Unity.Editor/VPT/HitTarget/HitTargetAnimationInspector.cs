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
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(HitTargetAnimationAuthoring)), CanEditMultipleObjects]
	public class HitTargetAnimationInspector : ItemAnimationInspector<HitTargetData, HitTargetAuthoring, HitTargetAnimationAuthoring>
	{
		private SerializedProperty _speedProperty;
		private SerializedProperty _maxAngleProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_speedProperty = serializedObject.FindProperty(nameof(HitTargetAnimationAuthoring.Speed));
			_maxAngleProperty = serializedObject.FindProperty(nameof(HitTargetAnimationAuthoring.MaxAngle));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_speedProperty, updateTransforms: true);
			PropertyField(_maxAngleProperty, updateTransforms: true);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
