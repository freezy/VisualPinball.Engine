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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TriggerComponent)), CanEditMultipleObjects]
	public class TriggerInspector : DragPointsItemInspector<TriggerData, TriggerComponent>
	{
		private SerializedProperty _positionProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _surfaceProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(TriggerComponent.Position));
			_rotationProperty = serializedObject.FindProperty(nameof(TriggerComponent.Rotation));
			_surfaceProperty = serializedObject.FindProperty(nameof(TriggerComponent._surface));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_rotationProperty, updateTransforms: true);
			PropertyField(_surfaceProperty, updateTransforms: true);

			base.OnInspectorGUI();

			EndEditing();
		}

		#region Dragpoint Tooling

		public override Vector3 EditableOffset => new Vector3(-MainComponent.Position.x, -MainComponent.Position.y, 0.0f);
		public override Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public override bool PointsAreLooping => true;
		public override IEnumerable<DragPointExposure> DragPointExposition => new[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public override ItemDataTransformType HandleType => ItemDataTransformType.TwoD;

		#endregion
	}
}
