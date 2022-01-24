﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(DropTargetAnimationComponent)), CanEditMultipleObjects]
	public class DropTargetAnimationInspector : AnimationInspector<HitTargetData, DropTargetComponent, DropTargetAnimationComponent>
	{
		private SerializedProperty _isDroppedProperty;
		private SerializedProperty _speedProperty;
		private SerializedProperty _raiseDelayProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_isDroppedProperty = serializedObject.FindProperty(nameof(DropTargetAnimationComponent.IsDropped));
			_speedProperty = serializedObject.FindProperty(nameof(DropTargetAnimationComponent.Speed));
			_raiseDelayProperty = serializedObject.FindProperty(nameof(DropTargetAnimationComponent.RaiseDelay));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_isDroppedProperty, updateTransforms: true);
			PropertyField(_speedProperty);
			PropertyField(_raiseDelayProperty);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
