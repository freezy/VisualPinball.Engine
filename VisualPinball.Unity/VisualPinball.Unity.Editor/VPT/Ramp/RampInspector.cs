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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RampAuthoring))]
	public class RampInspector : DragPointsItemInspector<Ramp, RampData, RampAuthoring>
	{
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

			_heightBottomProperty = serializedObject.FindProperty(nameof(RampAuthoring.HeightBottom));
			_heightTopProperty = serializedObject.FindProperty(nameof(RampAuthoring.HeightTop));
			_imageAlignmentProperty = serializedObject.FindProperty(nameof(RampAuthoring.ImageAlignment));
			_leftWallHeightVisibleProperty = serializedObject.FindProperty(nameof(RampAuthoring.LeftWallHeightVisible));
			_typeProperty = serializedObject.FindProperty(nameof(RampAuthoring.Type));
			_rightWallHeightVisibleProperty = serializedObject.FindProperty(nameof(RampAuthoring.RightWallHeightVisible));
			_widthBottomProperty = serializedObject.FindProperty(nameof(RampAuthoring.WidthBottom));
			_widthTopProperty = serializedObject.FindProperty(nameof(RampAuthoring.WidthTop));
			_wireDiameterProperty = serializedObject.FindProperty(nameof(RampAuthoring.WireDiameter));
			_wireDistanceXProperty = serializedObject.FindProperty(nameof(RampAuthoring.WireDistanceX));
			_wireDistanceYProperty = serializedObject.FindProperty(nameof(RampAuthoring.WireDistanceY));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

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

				if (ItemAuthoring.IsWireRamp) {
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

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}

		#region Dragpoint Tooling

		public override Vector3 EditableOffset => new Vector3(0.0f, 0.0f, Data.HeightBottom);
		public override Vector3 GetDragPointOffset(float ratio) => new Vector3(0.0f, 0.0f, (Data.HeightTop - Data.HeightBottom) * ratio);
		public override bool PointsAreLooping => false;
		public override IEnumerable<DragPointExposure> DragPointExposition => new DragPointExposure[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public override ItemDataTransformType HandleType => ItemDataTransformType.ThreeD;

		#endregion
	}
}
