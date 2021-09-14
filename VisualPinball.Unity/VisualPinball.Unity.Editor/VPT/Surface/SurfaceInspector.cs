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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceComponent)), CanEditMultipleObjects]
	public class SurfaceInspector : MainInspector<SurfaceData, SurfaceComponent>, IDragPointsInspector
	{
		private DragPointsInspectorHelper _dragPointsInspectorHelper;

		private SerializedProperty _heightTopProperty;
		private SerializedProperty _heightBottomProperty;

		public bool DragPointsActive => true;

		protected override void OnEnable()
		{
			base.OnEnable();

			_dragPointsInspectorHelper = new DragPointsInspectorHelper(MainComponent, this);
			_dragPointsInspectorHelper.OnEnable();

			_heightTopProperty = serializedObject.FindProperty(nameof(SurfaceComponent.HeightTop));
			_heightBottomProperty = serializedObject.FindProperty(nameof(SurfaceComponent.HeightBottom));
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

			PropertyField(_heightTopProperty, "Top Height", true, onChanged: () => {
				WalkChildren(PlayfieldComponent.transform, UpdateSurfaceReferences);
			});
			PropertyField(_heightBottomProperty, "Bottom Height", true);

			_dragPointsInspectorHelper.OnInspectorGUI(this);

			base.OnInspectorGUI();

			EndEditing();
		}

		private void OnSceneGUI()
		{
			_dragPointsInspectorHelper.OnSceneGUI(this);
		}

		#region Dragpoint Tooling

		public DragPointData[] DragPoints { get => MainComponent.DragPoints; set => MainComponent.DragPoints = value; }
		public Vector3 EditableOffset => new Vector3(0.0f, 0.0f, MainComponent.HeightTop + MainComponent.PlayfieldHeight);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping => true;
		public IEnumerable<DragPointExposure> DragPointExposition => new[] { DragPointExposure.Smooth, DragPointExposure.SlingShot, DragPointExposure.Texture };
		public ItemDataTransformType HandleType => ItemDataTransformType.TwoD;

		#endregion
	}
}
