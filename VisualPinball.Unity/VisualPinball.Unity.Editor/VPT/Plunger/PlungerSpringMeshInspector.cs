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
	[CustomEditor(typeof(PlungerSpringMeshAuthoring)), CanEditMultipleObjects]
	public class PlungerSpringMeshInspector : ItemMeshInspector<Plunger, PlungerData, PlungerAuthoring, PlungerSpringMeshAuthoring>
	{
		private SerializedProperty _springDiamProperty;
		private SerializedProperty _springGaugeProperty;
		private SerializedProperty _springLoopsProperty;
		private SerializedProperty _springEndLoopsProperty;

		protected override void OnEnable()
		{
			base.OnEnable();
			_springDiamProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshAuthoring.SpringDiam));
			_springGaugeProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshAuthoring.SpringGauge));
			_springLoopsProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshAuthoring.SpringLoops));
			_springEndLoopsProperty = serializedObject.FindProperty(nameof(PlungerSpringMeshAuthoring.SpringEndLoops));

		}
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_springDiamProperty, "Spring Diameter", true);
			PropertyField(_springGaugeProperty, "Spring Gauge", true);
			PropertyField(_springLoopsProperty, "Loops", true);
			PropertyField(_springEndLoopsProperty, "End Loops", true);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
