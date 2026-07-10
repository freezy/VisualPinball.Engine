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

using UnityEditor;
using VisualPinball.Engine.VPT.MetalWireGuide;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MetalWireGuideComponent)), CanEditMultipleObjects]
	public class MetalWireGuideInspector : MainInspector<MetalWireGuideData, MetalWireGuideComponent>
	{
		private SerializedProperty _heightProperty;
		private SerializedProperty _thicknessProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _bendradiusProperty;
		private SerializedProperty _standheightProperty;

		protected override void OnEnable()
		{
			base.OnEnable();


			_heightProperty = serializedObject.FindProperty(nameof(MetalWireGuideComponent._height));
			_thicknessProperty = serializedObject.FindProperty(nameof(MetalWireGuideComponent._thickness));
			_rotationProperty = serializedObject.FindProperty(nameof(MetalWireGuideComponent.Rotation));
			_bendradiusProperty = serializedObject.FindProperty(nameof(MetalWireGuideComponent._bendradius));
			_standheightProperty = serializedObject.FindProperty(nameof(MetalWireGuideComponent._standheight));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_rotationProperty, rebuildMesh: true);
			PropertyField(_heightProperty, rebuildMesh: true);
			PropertyField(_standheightProperty, rebuildMesh: true);
			PropertyField(_thicknessProperty, rebuildMesh: true);
			PropertyField(_bendradiusProperty, rebuildMesh: true);

			DragPointSplineInspectorGUI.OnInspectorGUI(MainComponent.DragPointSpline);

			base.OnInspectorGUI();

			EndEditing();
		}

	}
}
