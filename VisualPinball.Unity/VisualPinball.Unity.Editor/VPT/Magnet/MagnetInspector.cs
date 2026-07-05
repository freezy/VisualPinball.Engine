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
	[CustomEditor(typeof(MagnetComponent))]
	public class MagnetInspector : ItemInspector
	{
		private SerializedProperty _radiusProperty;
		private SerializedProperty _strengthProperty;
		private SerializedProperty _forceProfileProperty;
		private SerializedProperty _grabBallProperty;
		private SerializedProperty _grabRadiusProperty;
		private SerializedProperty _heightRangeProperty;
		private SerializedProperty _isEnabledOnStartProperty;
		private SerializedProperty _drawDebugForcesProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_radiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.Radius));
			_strengthProperty = serializedObject.FindProperty(nameof(MagnetComponent.Strength));
			_forceProfileProperty = serializedObject.FindProperty(nameof(MagnetComponent.ForceProfile));
			_grabBallProperty = serializedObject.FindProperty(nameof(MagnetComponent.GrabBall));
			_grabRadiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.GrabRadius));
			_heightRangeProperty = serializedObject.FindProperty(nameof(MagnetComponent.HeightRange));
			_isEnabledOnStartProperty = serializedObject.FindProperty(nameof(MagnetComponent.IsEnabledOnStart));
			_drawDebugForcesProperty = serializedObject.FindProperty(nameof(MagnetComponent.DrawDebugForces));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();
			OnPreInspectorGUI();

			PropertyField(_radiusProperty);
			PropertyField(_heightRangeProperty);
			PropertyField(_strengthProperty);
			PropertyField(_forceProfileProperty);

			EditorGUILayout.Space(8f);
			PropertyField(_grabBallProperty);
			if (_grabBallProperty.boolValue) {
				PropertyField(_grabRadiusProperty);
			}

			EditorGUILayout.Space(8f);
			PropertyField(_isEnabledOnStartProperty);
			PropertyField(_drawDebugForcesProperty);

			base.OnInspectorGUI();
			EndEditing();
		}
	}
}
