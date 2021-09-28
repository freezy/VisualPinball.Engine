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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SlingshotComponent))]
	public class SlingshotInspector : ItemInspector
	{
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _rubberOnProperty;
		private SerializedProperty _rubberOffProperty;
		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_surfaceProperty = serializedObject.FindProperty(nameof(SlingshotComponent.SlingshotSurface));
			_rubberOnProperty = serializedObject.FindProperty(nameof(SlingshotComponent.RubberOn));
			_rubberOffProperty = serializedObject.FindProperty(nameof(SlingshotComponent.RubberOff));
		}

		public override void OnInspectorGUI()
		{
			// if (HasErrors()) {
			// 	return;
			// }

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_surfaceProperty, "Collider Surface");
			PropertyField(_rubberOffProperty, "Rubber Off",  true);
			PropertyField(_rubberOnProperty, "Rubber On",  true);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
