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
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerAuthoring)), CanEditMultipleObjects]
	public class KickerInspector : ItemMainInspector<KickerData, KickerAuthoring>
	{
		private SerializedProperty _positionProperty;
		private SerializedProperty _radiusProperty;
		private SerializedProperty _orientationProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _coilsProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(KickerAuthoring.Position));
			_radiusProperty = serializedObject.FindProperty(nameof(KickerAuthoring.Radius));
			_orientationProperty = serializedObject.FindProperty(nameof(KickerAuthoring.Orientation));
			_surfaceProperty = serializedObject.FindProperty(nameof(KickerAuthoring._surface));
			_coilsProperty = serializedObject.FindProperty(nameof(KickerAuthoring.Coils));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_radiusProperty, updateTransforms: true);

			if (MainComponent.KickerType == KickerType.KickerCup ||
			    MainComponent.KickerType == KickerType.KickerWilliams) {
				PropertyField(_orientationProperty, updateTransforms: true);
			}
			PropertyField(_surfaceProperty, updateTransforms: true);

			PropertyField(_coilsProperty);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
