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
		private SerializedProperty _magnetTypeProperty;
		private SerializedProperty _forceProfileProperty;
		private SerializedProperty _coilRiseTimeProperty;
		private SerializedProperty _coilFallTimeProperty;
		private SerializedProperty _poleRadiusProperty;
		private SerializedProperty _grabBallProperty;
		private SerializedProperty _grabRadiusProperty;
		private SerializedProperty _heightRangeProperty;
		private SerializedProperty _isEnabledOnStartProperty;
		private SerializedProperty _isKinematicProperty;
		private SerializedProperty _drawDebugForcesProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_radiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.Radius));
			_strengthProperty = serializedObject.FindProperty(nameof(MagnetComponent.Strength));
			_magnetTypeProperty = serializedObject.FindProperty(nameof(MagnetComponent.MagnetType));
			_forceProfileProperty = serializedObject.FindProperty(nameof(MagnetComponent.ForceProfile));
			_coilRiseTimeProperty = serializedObject.FindProperty(nameof(MagnetComponent.CoilRiseTime));
			_coilFallTimeProperty = serializedObject.FindProperty(nameof(MagnetComponent.CoilFallTime));
			_poleRadiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.PoleRadius));
			_grabBallProperty = serializedObject.FindProperty(nameof(MagnetComponent.GrabBall));
			_grabRadiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.GrabRadius));
			_heightRangeProperty = serializedObject.FindProperty(nameof(MagnetComponent.HeightRange));
			_isEnabledOnStartProperty = serializedObject.FindProperty(nameof(MagnetComponent.IsEnabledOnStart));
			_isKinematicProperty = serializedObject.FindProperty(nameof(MagnetComponent.IsKinematic));
			_drawDebugForcesProperty = serializedObject.FindProperty(nameof(MagnetComponent.DrawDebugForces));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();
			OnPreInspectorGUI();

			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				PropertyField(_magnetTypeProperty);
			}
			var isSpatial = _magnetTypeProperty.enumValueIndex == (int)MagnetType.Spatial;

			PropertyField(_radiusProperty);
			if (!isSpatial) {
				PropertyField(_heightRangeProperty);
			}
			PropertyField(_strengthProperty);
			if (!isSpatial) {
				PropertyField(_forceProfileProperty);
			}
			var usesPhysicalResponse = isSpatial || _forceProfileProperty.enumValueIndex == (int)MagnetForceProfile.Physical;
			if (usesPhysicalResponse) {
				PropertyField(_poleRadiusProperty);
				PropertyField(_coilRiseTimeProperty);
				PropertyField(_coilFallTimeProperty);
			}

			EditorGUILayout.Space(8f);
			PropertyField(_grabBallProperty);
			if (_grabBallProperty.boolValue) {
				PropertyField(_grabRadiusProperty);
			}

			EditorGUILayout.Space(8f);
			PropertyField(_isEnabledOnStartProperty);
			// kinematic registration is fixed at startup; toggling during play would silently do nothing
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				PropertyField(_isKinematicProperty);
			}
			PropertyField(_drawDebugForcesProperty);

			base.OnInspectorGUI();
			EndEditing();
		}
	}
}
