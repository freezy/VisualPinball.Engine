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
	[CustomEditor(typeof(PlungerRodMeshAuthoring)), CanEditMultipleObjects]
	public class PlungerRodMeshInspector : ItemMeshInspector<Plunger, PlungerData, PlungerAuthoring, PlungerRodMeshAuthoring>
	{
		private SerializedProperty _rodDiamPullProperty;
		private SerializedProperty _tipShapeProperty;
		private SerializedProperty _ringGapProperty;
		private SerializedProperty _ringDiamProperty;
		private SerializedProperty _ringWidthProperty;

		protected override void OnEnable()
		{
			base.OnEnable();
			_rodDiamPullProperty = serializedObject.FindProperty(nameof(PlungerRodMeshAuthoring.RodDiam));
			_tipShapeProperty = serializedObject.FindProperty(nameof(PlungerRodMeshAuthoring.TipShape));
			_ringGapProperty = serializedObject.FindProperty(nameof(PlungerRodMeshAuthoring.RingGap));
			_ringDiamProperty = serializedObject.FindProperty(nameof(PlungerRodMeshAuthoring.RingDiam));
			_ringWidthProperty = serializedObject.FindProperty(nameof(PlungerRodMeshAuthoring.RingWidth));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_rodDiamPullProperty, "Rod Diameter", true);
			PropertyField(_tipShapeProperty, "Tip Shape", true);
			PropertyField(_ringGapProperty, "Ring Gap", true);
			PropertyField(_ringDiamProperty, "Ring Diameter", true);
			PropertyField(_ringWidthProperty, "Ring Width", true);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
