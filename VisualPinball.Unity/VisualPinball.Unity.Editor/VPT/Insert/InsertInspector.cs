// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(InsertComponent))]
	[CanEditMultipleObjects]
	public class InsertInspector : UnityEditor.Editor
	{
		private SerializedProperty _shape;
		private SerializedProperty _width;
		private SerializedProperty _height;
		private SerializedProperty _triangleBase;
		private SerializedProperty _triangleApex;
		private SerializedProperty _rectangleCorner;
		private SerializedProperty _sideSegments;
		private SerializedProperty _depth;
		private SerializedProperty _channelWidth;
		private SerializedProperty _channelDepth;

		private void OnEnable()
		{
			_shape = serializedObject.FindProperty("_shape");
			_width = serializedObject.FindProperty("_width");
			_height = serializedObject.FindProperty("_height");
			_triangleBase = serializedObject.FindProperty("_triangleBase");
			_triangleApex = serializedObject.FindProperty("_triangleApex");
			_rectangleCorner = serializedObject.FindProperty("_rectangleCorner");
			_sideSegments = serializedObject.FindProperty("_sideSegments");
			_depth = serializedObject.FindProperty("_depth");
			_channelWidth = serializedObject.FindProperty("_channelWidth");
			_channelDepth = serializedObject.FindProperty("_channelDepth");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_shape);
			EditorGUILayout.PropertyField(_width, new GUIContent("Width (VPX)"));
			EditorGUILayout.PropertyField(_height, new GUIContent("Height (VPX)"));

			switch ((InsertShape)_shape.enumValueIndex) {
				case InsertShape.Triangle:
					EditorGUILayout.PropertyField(_triangleBase, new GUIContent("Base Corners"), true);
					EditorGUILayout.PropertyField(_triangleApex, new GUIContent("Apex Corner"), true);
					break;

				case InsertShape.Rectangle:
					EditorGUILayout.PropertyField(_rectangleCorner, new GUIContent("Corners"), true);
					break;
			}

			EditorGUILayout.PropertyField(_sideSegments);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_depth, new GUIContent("Depth (VPX)"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Channel", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_channelWidth, new GUIContent("Width (VPX)"));
			EditorGUILayout.PropertyField(_channelDepth, new GUIContent("Depth (VPX)"));

			serializedObject.ApplyModifiedProperties();

			DrawStatus();
		}

		private void DrawStatus()
		{
			var insert = (InsertComponent)target;
			if (insert.Error != null) {
				EditorGUILayout.HelpBox($"{insert.Error}\n\nShowing the last mesh that worked.", MessageType.Error);
			}

			foreach (var warning in insert.Warnings) {
				EditorGUILayout.HelpBox(warning, MessageType.Warning);
			}

			if (insert.Outline != null) {
				// The real-world size is the quickest way to catch a value that was
				// typed in the wrong unit.
				var inch = 1f / InsertShapes.VpxUnitsPerInch;
				var millimetre = InsertShapes.MetresPerInch * 1000f * inch;
				EditorGUILayout.HelpBox(
					$"{insert.Outline.Width * inch:0.###} x {insert.Outline.Height * inch:0.###} in " +
					$"({insert.Outline.Width * millimetre:0.#} x {insert.Outline.Height * millimetre:0.#} mm)\n" +
					$"{insert.Outline.Points.Count} outline vertices, {insert.Outline.Perimeter:0.#} VPX units around.",
					MessageType.None);
			}
		}

		private void OnSceneGUI()
		{
			var insert = (InsertComponent)target;
			var outline = insert.Outline;
			if (outline == null) {
				return;
			}

			var matrix = insert.transform.localToWorldMatrix;
			var points = new Vector3[outline.Points.Count + 1];
			for (var i = 0; i < outline.Points.Count; i++) {
				points[i] = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(outline.Points[i].Position));
			}
			points[^1] = points[0];

			Handles.color = Color.cyan;
			Handles.DrawPolyLine(points);

			// Corner samples and straight-run subdivisions are told apart on sight,
			// which is the quickest way to see what a segment count actually did.
			var size = HandleUtility.GetHandleSize(points[0]) * 0.02f;
			for (var i = 0; i < outline.Points.Count; i++) {
				Handles.color = outline.Points[i].Kind switch {
					InsertOutlinePointKind.Arc => Color.yellow,
					InsertOutlinePointKind.Side => Color.grey,
					_ => Color.red,
				};
				Handles.DotHandleCap(0, points[i], Quaternion.identity, size, EventType.Repaint);
			}
		}
	}
}
