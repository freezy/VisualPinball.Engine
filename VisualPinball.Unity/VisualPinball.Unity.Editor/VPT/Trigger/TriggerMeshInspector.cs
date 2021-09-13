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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TriggerMeshComponent)), CanEditMultipleObjects]
	public class TriggerMeshInspector : ItemMeshInspector<TriggerData, TriggerComponent, TriggerMeshComponent>
	{
		private static readonly string[] TriggerShapeLabels = {
			"Button",
			"Star",
			"Wire A",
			"Wire B",
			"Wire C",
			"Wire D",
		};
		private static readonly int[] TriggerShapeValues = {
			TriggerShape.TriggerButton,
			TriggerShape.TriggerStar,
			TriggerShape.TriggerWireA,
			TriggerShape.TriggerWireB,
			TriggerShape.TriggerWireC,
			TriggerShape.TriggerWireD,
		};

		private SerializedProperty _wireThicknessProperty;
		private SerializedProperty _shapeProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_shapeProperty = serializedObject.FindProperty(nameof(TriggerMeshComponent.Shape));
			_wireThicknessProperty = serializedObject.FindProperty(nameof(TriggerMeshComponent.WireThickness));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			DropDownProperty("Shape", _shapeProperty, TriggerShapeLabels, TriggerShapeValues, true, true);
			if (!MeshComponent.IsCircle) {
				PropertyField(_wireThicknessProperty, rebuildMesh: true);
			}

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
