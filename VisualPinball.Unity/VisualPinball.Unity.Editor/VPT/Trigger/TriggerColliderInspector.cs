﻿// Visual Pinball Engine
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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TriggerColliderComponent)), CanEditMultipleObjects]
	public class TriggerColliderInspector : ItemColliderInspector<TriggerData, TriggerComponent, TriggerColliderComponent>
	{
		private SerializedProperty _hitHeightProperty;
		private SerializedProperty _hitCircleRadiusProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_hitHeightProperty = serializedObject.FindProperty(nameof(TriggerColliderComponent.HitHeight));
			_hitCircleRadiusProperty = serializedObject.FindProperty(nameof(TriggerColliderComponent.HitCircleRadius));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_hitHeightProperty, updateColliders: true);
			var meshComponent = (target as TriggerColliderComponent)!.GetComponent<TriggerMeshComponent>();
			if (meshComponent && meshComponent.IsCircle) {
				PropertyField(_hitCircleRadiusProperty, updateColliders: true);
			}

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
