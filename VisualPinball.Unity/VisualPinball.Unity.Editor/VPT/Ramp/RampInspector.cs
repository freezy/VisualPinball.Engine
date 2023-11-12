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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RampComponent)), CanEditMultipleObjects]
	public class RampInspector : MainInspector<RampData, RampComponent>, IDragPointsInspector
	{
		public Transform Transform => MainComponent.transform;

		private bool _foldoutGeometry = true;

		private static readonly string[] RampTypeLabels = {
			"Flat",
			"1 Wire",
			"2 Wire",
			"3 Wire Left",
			"3 Wire Right",
			"4 Wire",
		};
		private static readonly int[] RampTypeValues = {
			RampType.RampTypeFlat,
			RampType.RampType1Wire,
			RampType.RampType2Wire,
			RampType.RampType3WireLeft,
			RampType.RampType3WireRight,
			RampType.RampType4Wire,
		};
		private static readonly string[] RampImageAlignmentLabels = {
			"World",
			"Wrap",
		};
		private static readonly int[] RampImageAlignmentValues = {
			RampImageAlignment.ImageModeWorld,
			RampImageAlignment.ImageModeWrap,
		};

		private SerializedProperty _typeProperty;
		private SerializedProperty _heightTopProperty;
		private SerializedProperty _heightBottomProperty;
		private SerializedProperty _imageAlignmentProperty;
		private SerializedProperty _leftWallHeightVisibleProperty;
		private SerializedProperty _rightWallHeightVisibleProperty;
		private SerializedProperty _widthBottomProperty;
		private SerializedProperty _widthTopProperty;
		private SerializedProperty _wireDiameterProperty;
		private SerializedProperty _wireDistanceXProperty;
		private SerializedProperty _wireDistanceYProperty;


		protected override void OnEnable()
		{
			base.OnEnable();

			DragPointsHelper = new DragPointsInspectorHelper(MainComponent, this);
			DragPointsHelper.OnEnable();

			_heightBottomProperty = serializedObject.FindProperty(nameof(RampComponent._heightBottom));
			_heightTopProperty = serializedObject.FindProperty(nameof(RampComponent._heightTop));
			_imageAlignmentProperty = serializedObject.FindProperty(nameof(RampComponent._imageAlignment));
			_leftWallHeightVisibleProperty = serializedObject.FindProperty(nameof(RampComponent._leftWallHeightVisible));
			_typeProperty = serializedObject.FindProperty(nameof(RampComponent._type));
			_rightWallHeightVisibleProperty = serializedObject.FindProperty(nameof(RampComponent._rightWallHeightVisible));
			_widthBottomProperty = serializedObject.FindProperty(nameof(RampComponent._widthBottom));
			_widthTopProperty = serializedObject.FindProperty(nameof(RampComponent._widthTop));
			_wireDiameterProperty = serializedObject.FindProperty(nameof(RampComponent._wireDiameter));
			_wireDistanceXProperty = serializedObject.FindProperty(nameof(RampComponent._wireDistanceX));
			_wireDistanceYProperty = serializedObject.FindProperty(nameof(RampComponent._wireDistanceY));
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

			DropDownProperty("Type", _typeProperty, RampTypeLabels, RampTypeValues, true, true);
			DropDownProperty("Image Mode", _imageAlignmentProperty, RampImageAlignmentLabels, RampImageAlignmentValues, true);

			if (_foldoutGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGeometry, "Geometry")) {
				PropertyField(_heightTopProperty, "Top Height", true);
				PropertyField(_heightBottomProperty, "Bottom Height", true);

				EditorGUILayout.Space(10);
				PropertyField(_widthTopProperty, "Top Width", true);
				PropertyField(_widthBottomProperty, "Bottom Width", true);

				EditorGUILayout.Space(10);

				if (MainComponent.IsWireRamp) {
					EditorGUILayout.LabelField("Wire Ramp");
					EditorGUI.indentLevel++;
					PropertyField(_wireDiameterProperty, "Diameter", true);
					PropertyField(_wireDistanceXProperty, "Distance X", true);
					PropertyField(_wireDistanceYProperty, "Distance Y", true);
					EditorGUI.indentLevel--;

				} else {
					EditorGUILayout.LabelField("Wall Mesh");
					EditorGUI.indentLevel++;
					PropertyField(_leftWallHeightVisibleProperty, "Left Wall", true, updateVisibility: true);
					PropertyField(_rightWallHeightVisibleProperty, "Right Wall", true, updateVisibility: true);
					EditorGUI.indentLevel--;
				}
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			DragPointsHelper.OnInspectorGUI(this);

			base.OnInspectorGUI();

			EndEditing();
		}

		private void OnSceneGUI()
		{
			DragPointsHelper.OnSceneGUI(this);
		}

		#region Dragpoint Tooling

		public bool DragPointsActive => true;
		public DragPointData[] DragPoints { get => MainComponent.DragPoints; set => MainComponent.DragPoints = value; }
		public bool PointsAreLooping => false;
		public IEnumerable<DragPointExposure> DragPointExposition => new[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public ItemDataTransformType HandleType => ItemDataTransformType.ThreeD;
		public DragPointsInspectorHelper DragPointsHelper { get; private set; }
		public float ZOffset => 0f;
		public float[] TopBottomZ => new[] { MainComponent._heightBottom, MainComponent._heightTop };

		public void SetDragPointPosition(DragPointData dragPoint, Vertex3D value, int numSelectedDragPoints, float[] topBottomZ)
		{
			var isFirst = MainComponent.DragPoints[0].Id == dragPoint.Id;
			var isLast = MainComponent.DragPoints[^1].Id == dragPoint.Id;
			var zDiff = value.Z - dragPoint.Center.Z;
			
			if (isFirst && numSelectedDragPoints == 1) {
				MainComponent._heightBottom = topBottomZ[0] + zDiff;
				dragPoint.Center.X = value.X;
				dragPoint.Center.Y = value.Y;

			} else if (isLast && numSelectedDragPoints == 1) {
				MainComponent._heightTop = topBottomZ[1] + zDiff;
				dragPoint.Center.X = value.X;
				dragPoint.Center.Y = value.Y;
				
			} else {
				dragPoint.Center = value;
			}
		}

		#endregion
	}
}
