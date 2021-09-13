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
	[CustomEditor(typeof(PlungerRodMeshComponent)), CanEditMultipleObjects]
	public class PlungerRodMeshInspector : MeshInspector<PlungerData, PlungerComponent, PlungerRodMeshComponent>
	{
		private SerializedProperty _rodDiamPullProperty;
		private SerializedProperty _tipShapeProperty;
		private SerializedProperty _ringGapProperty;
		private SerializedProperty _ringDiamProperty;
		private SerializedProperty _ringWidthProperty;

		protected override void OnEnable()
		{
			base.OnEnable();
			_rodDiamPullProperty = serializedObject.FindProperty(nameof(PlungerRodMeshComponent.RodDiam));
			_tipShapeProperty = serializedObject.FindProperty(nameof(PlungerRodMeshComponent.TipShape));
			_ringGapProperty = serializedObject.FindProperty(nameof(PlungerRodMeshComponent.RingGap));
			_ringDiamProperty = serializedObject.FindProperty(nameof(PlungerRodMeshComponent.RingDiam));
			_ringWidthProperty = serializedObject.FindProperty(nameof(PlungerRodMeshComponent.RingWidth));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_rodDiamPullProperty, "Rod Diameter", true);
			PropertyField(_tipShapeProperty, "Tip Shape", true);
			PropertyField(_ringGapProperty, "Ring Gap", true);
			PropertyField(_ringDiamProperty, "Ring Diameter", true);
			PropertyField(_ringWidthProperty, "Ring Width", true);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
