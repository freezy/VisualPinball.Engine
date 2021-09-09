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
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberComponent)), CanEditMultipleObjects]
	public class RubberInspector : DragPointsItemInspector<RubberData, RubberComponent>
	{
		private SerializedProperty _heightProperty;
		private SerializedProperty _hitHeightProperty;
		private SerializedProperty _thicknessProperty;
		private SerializedProperty _rotationProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_heightProperty = serializedObject.FindProperty(nameof(RubberComponent._height));
			_hitHeightProperty = serializedObject.FindProperty(nameof(RubberComponent._hitHeight));
			_thicknessProperty = serializedObject.FindProperty(nameof(RubberComponent._thickness));
			_rotationProperty = serializedObject.FindProperty(nameof(RubberComponent.Rotation));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_rotationProperty, rebuildMesh: true);
			PropertyField(_heightProperty, rebuildMesh: true);
			PropertyField(_hitHeightProperty, rebuildMesh: true);
			PropertyField(_thicknessProperty, rebuildMesh: true);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}

		#region Dragpoint Tooling

		public override Vector3 EditableOffset => new Vector3(0.0f, 0.0f, MainComponent._hitHeight);
		public override Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public override bool PointsAreLooping => true;
		public override IEnumerable<DragPointExposure> DragPointExposition => new[] { DragPointExposure.Smooth };
		public override ItemDataTransformType HandleType => ItemDataTransformType.TwoD;

		#endregion
	}
}
