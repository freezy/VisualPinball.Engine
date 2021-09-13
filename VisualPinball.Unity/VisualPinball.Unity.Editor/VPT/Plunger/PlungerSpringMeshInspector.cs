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
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlungerSpringMeshComponent)), CanEditMultipleObjects]
	public class PlungerSpringMeshInspector : MeshInspector<PlungerData, PlungerComponent, PlungerSpringMeshComponent>
	{
		private SerializedProperty _springDiamProperty;
		private SerializedProperty _springGaugeProperty;
		private SerializedProperty _springLoopsProperty;
		private SerializedProperty _springEndLoopsProperty;

		protected override void OnEnable()
		{
			base.OnEnable();
			_springDiamProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshComponent.SpringDiam));
			_springGaugeProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshComponent.SpringGauge));
			_springLoopsProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshComponent.SpringLoops));
			_springEndLoopsProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshComponent.SpringEndLoops));

		}
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_springDiamProperty, "Spring Diameter", true);
			PropertyField(_springGaugeProperty, "Spring Gauge", true);
			PropertyField(_springLoopsProperty, "Loops", true);
			PropertyField(_springEndLoopsProperty, "End Loops", true);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
