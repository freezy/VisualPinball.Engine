// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightInsertMeshComponent)), CanEditMultipleObjects]
	public class LightInsertMeshInspector : MeshInspector<LightData, LightComponent, LightInsertMeshComponent>, IDragPointsInspector
	{
		public Transform Transform => null; // not needed for lights, IDragpointInspector will probably be removed.

		private SerializedProperty _insertHeightProperty;
		private SerializedProperty _positionZProperty;

		public bool DragPointsActive => true;

		protected override void OnEnable()
		{
			base.OnEnable();

			DragPointsHelper = new DragPointsInspectorHelper(MeshComponent.MainComponent, this);
			DragPointsHelper.OnEnable();

			_insertHeightProperty = serializedObject.FindProperty(nameof(LightInsertMeshComponent.InsertHeight));
			_positionZProperty = serializedObject.FindProperty(nameof(LightInsertMeshComponent.PositionZ));
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

			PropertyField(_insertHeightProperty, rebuildMesh: true);
			PropertyField(_positionZProperty, updateTransforms: true);

			DragPointsHelper.OnInspectorGUI(this);

			base.OnInspectorGUI();

			EndEditing();
		}

		private void OnSceneGUI()
		{
			DragPointsHelper.OnSceneGUI(this);
		}

		#region Dragpoint Tooling

		public DragPointData[] DragPoints { get => MeshComponent.DragPoints; set => MeshComponent.DragPoints = value; }

		public bool PointsAreLooping => true;
		public IEnumerable<DragPointExposure> DragPointExposition => new[] { DragPointExposure.Smooth, DragPointExposure.Texture };
		public DragPointTransformType HandleType => DragPointTransformType.TwoD;
		public DragPointsInspectorHelper DragPointsHelper { get; private set; }
		public float ZOffset => MeshComponent.PositionZ;
		public float[] TopBottomZ => null;
		public void SetDragPointPosition(DragPointData dragPoint, Vertex3D value, int numSelectedDragPoints,
			float[] topBottomZ) => dragPoint.Center = value;

		#endregion
	}
}
