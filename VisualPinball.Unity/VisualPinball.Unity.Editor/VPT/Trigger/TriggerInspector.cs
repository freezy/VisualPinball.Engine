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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TriggerComponent)), CanEditMultipleObjects]
	public class TriggerInspector : MainInspector<TriggerData, TriggerComponent>, IDragPointsInspector
	{

		private SerializedProperty _positionProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _surfaceProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			DragPointsHelper = new DragPointsInspectorHelper(MainComponent, this);
			DragPointsHelper.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(TriggerComponent.Position));
			_rotationProperty = serializedObject.FindProperty(nameof(TriggerComponent.Rotation));
			_surfaceProperty = serializedObject.FindProperty(nameof(TriggerComponent._surface));
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			DragPointsHelper.OnDisable();
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

			DragPointsHelper.OnInspectorGUI(this);

			base.OnInspectorGUI();

			EndEditing();
		}

		private void OnSceneGUI()
		{
			DragPointsHelper.OnSceneGUI(this);
		}

		#region Dragpoint Tooling

		public bool DragPointsActive {
			get {
				var meshComp = MainComponent.GetComponent<TriggerMeshComponent>();
				return !meshComp || !meshComp.IsCircle;
			}
		}

		public DragPointData[] DragPoints { get => MainComponent.DragPoints; set => MainComponent.DragPoints = value; }
		public Vector3 EditableOffset => new Vector3(-MainComponent.Position.x, -MainComponent.Position.y, 0.0f);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping => true;
		public IEnumerable<DragPointExposure> DragPointExposition => new[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public ItemDataTransformType HandleType => ItemDataTransformType.TwoD;
		public DragPointsInspectorHelper DragPointsHelper { get; private set; }

		#endregion
	}
}
