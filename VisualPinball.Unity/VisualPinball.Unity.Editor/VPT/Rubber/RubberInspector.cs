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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberComponent)), CanEditMultipleObjects]
	public class RubberInspector : MainInspector<RubberData, RubberComponent>, IDragPointsInspector
	{
		private DragPointsInspectorHelper _dragPointsInspectorHelper;

		private SerializedProperty _heightProperty;
		private SerializedProperty _thicknessProperty;
		private SerializedProperty _rotationProperty;

		protected override void OnEnable()
		{
			base.OnEnable();


			_dragPointsInspectorHelper = new DragPointsInspectorHelper(MainComponent, this);
			_dragPointsInspectorHelper.OnEnable();

			_heightProperty = serializedObject.FindProperty(nameof(RubberComponent._height));
			_thicknessProperty = serializedObject.FindProperty(nameof(RubberComponent._thickness));
			_rotationProperty = serializedObject.FindProperty(nameof(RubberComponent.Rotation));
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			_dragPointsInspectorHelper.OnDisable();
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
			PropertyField(_thicknessProperty, rebuildMesh: true);

			_dragPointsInspectorHelper.OnInspectorGUI(this);

			base.OnInspectorGUI();

			EndEditing();
		}

		private void OnSceneGUI()
		{
			_dragPointsInspectorHelper.OnSceneGUI(this);
		}

		#region Dragpoint Tooling

		public bool DragPointsActive => true;
		public DragPointData[] DragPoints { get => MainComponent.DragPoints; set => MainComponent.DragPoints = value; }
		public Vector3 EditableOffset => new Vector3(0.0f, 0.0f, MainComponent._height);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping => true;
		public IEnumerable<DragPointExposure> DragPointExposition => new[] { DragPointExposure.Smooth };
		public ItemDataTransformType HandleType => ItemDataTransformType.TwoD;

		#endregion
	}
}
