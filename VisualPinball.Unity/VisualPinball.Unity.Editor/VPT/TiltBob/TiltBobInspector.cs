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
	[CustomEditor(typeof(TiltBobComponent)), CanEditMultipleObjects]
	public class TiltBobInspector : ItemInspector
	{
		private SerializedProperty _plumbDampingProperty;
		private SerializedProperty _plumbThresholdAngleProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_plumbDampingProperty = serializedObject.FindProperty(nameof(TiltBobComponent.PlumbDamping));
			_plumbThresholdAngleProperty = serializedObject.FindProperty(nameof(TiltBobComponent.PlumbThresholdAngle));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_plumbDampingProperty, "Plumb Damping");
			PropertyField(_plumbThresholdAngleProperty, "Plumb Threshold Angle");

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
