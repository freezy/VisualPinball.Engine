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

using System.Collections.Generic;
using Unity.Mathematics;
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
		private SerializedProperty _starburstBursts;
		private SerializedProperty _starburstCenter;
		private SerializedProperty _starburstOffset;
		private SerializedProperty _holes;
		private SerializedProperty _holeSegments;
		private SerializedProperty _holeRadius;

		/// <summary>How many holes the "Around" preset spreads out, remembered while the inspector is open.</summary>
		private int _aroundCount = 5;

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
			_starburstBursts = serializedObject.FindProperty("_starburstBursts");
			_starburstCenter = serializedObject.FindProperty("_starburstCenter");
			_starburstOffset = serializedObject.FindProperty("_starburstOffset");
			_holes = serializedObject.FindProperty("_holes");
			_holeSegments = serializedObject.FindProperty("_holeSegments");
			_holeRadius = serializedObject.FindProperty("_holeRadius");
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

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Starburst", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_starburstBursts, new GUIContent("Bursts"));
			if (_starburstBursts.intValue >= InsertStarburst.MinBursts) {
				EditorGUILayout.PropertyField(_starburstCenter, new GUIContent("Center (VPX)"));
				EditorGUILayout.PropertyField(_starburstOffset, new GUIContent("Offset (VPX)"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Holes", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_holeSegments, new GUIContent("Circle Segments"));
			EditorGUILayout.PropertyField(_holeRadius, new GUIContent("New Hole Radius (VPX)"));
			DrawHolePresets();
			EditorGUILayout.PropertyField(_holes, new GUIContent("Holes"), true);

			serializedObject.ApplyModifiedProperties();

			DrawStatus();
		}

		/// <summary>
		/// Buttons that lay holes out along the raised floor's edge, replacing
		/// whatever holes were there. Each hole can be moved afterwards.
		/// </summary>
		private void DrawHolePresets()
		{
			var insert = (InsertComponent)target;
			var outline = insert.Outline;
			var inset = outline?.ClampInset(_channelWidth.floatValue) ?? 0f;

			using (new EditorGUI.DisabledScope(outline == null || targets.Length > 1)) {
				using (new EditorGUILayout.HorizontalScope()) {
					if (GUILayout.Button("Corners")) {
						SetHoles(InsertHoles.AtCorners(outline, inset));
					}
					if (GUILayout.Button("Corners + Sides")) {
						SetHoles(InsertHoles.AtCornersAndSides(outline, inset));
					}
				}
				using (new EditorGUILayout.HorizontalScope()) {
					_aroundCount = EditorGUILayout.IntSlider(_aroundCount, 1, 64);
					if (GUILayout.Button("Around", GUILayout.Width(80f))) {
						SetHoles(InsertHoles.Around(outline, inset, _aroundCount));
					}
				}
			}
			// A new hole goes in the middle of this insert's outline, which means
			// nothing to another insert, so it is one at a time like the presets.
			using (new EditorGUI.DisabledScope(targets.Length > 1))
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button("Add Hole")) {
					var index = _holes.arraySize;
					_holes.InsertArrayElementAtIndex(index);
					var hole = _holes.GetArrayElementAtIndex(index);
					hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value =
						outline != null ? new Vector2(outline.Centroid.x, outline.Centroid.y) : Vector2.zero;
					hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue = _holeRadius.floatValue;
				}
				if (GUILayout.Button("Clear")) {
					_holes.ClearArray();
				}
			}
		}

		private void SetHoles(List<Vector2> centers)
		{
			_holes.ClearArray();
			for (var i = 0; i < centers.Count; i++) {
				_holes.InsertArrayElementAtIndex(i);
				var hole = _holes.GetArrayElementAtIndex(i);
				hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value = centers[i];
				hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue = _holeRadius.floatValue;
			}
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

			DrawHoleHandles(insert);
		}

		/// <summary>
		/// A handle on each hole's centre that drags it across the insert's
		/// plane, and one on its rim that sets its radius.
		/// </summary>
		/// <remarks>
		/// Works on the component itself: the serialized object is off limits in
		/// scene GUI. Every change is recorded for undo first.
		/// </remarks>
		private static void DrawHoleHandles(InsertComponent insert)
		{
			var transform = insert.transform;
			var matrix = transform.localToWorldMatrix;
			var up = transform.TransformDirection(InsertMeshGenerator.ToLocal(float2.zero, 1f)).normalized;
			var across = transform.TransformDirection(InsertMeshGenerator.ToLocal(new float2(1f, 0f))).normalized;
			var along = transform.TransformDirection(InsertMeshGenerator.ToLocal(new float2(0f, 1f))).normalized;

			for (var i = 0; i < insert.Holes.Count; i++) {
				var hole = insert.Holes[i];
				var center = new float2(hole.Center.x, hole.Center.y);

				var world = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center));
				var rim = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center + new float2(hole.Radius, 0f)));
				var scale = HandleUtility.GetHandleSize(world);

				Handles.color = new Color(1f, 0.6f, 0.1f);
				Handles.DrawWireDisc(world, up, Vector3.Distance(world, rim));

				EditorGUI.BeginChangeCheck();
				var moved = Handles.Slider2D(world, up, across, along, scale * 0.06f, Handles.CircleHandleCap, Vector2.zero);
				if (EditorGUI.EndChangeCheck()) {
					var plan = InsertMeshGenerator.FromLocal(transform.InverseTransformPoint(moved));
					SetHole(insert, i, new InsertHole(new Vector2(plan.x, plan.y), hole.Radius), "Move Hole");
					continue;
				}

				EditorGUI.BeginChangeCheck();
				var stretched = Handles.Slider(rim, rim - world, scale * 0.04f, Handles.DotHandleCap, 0f);
				if (EditorGUI.EndChangeCheck()) {
					var plan = InsertMeshGenerator.FromLocal(transform.InverseTransformPoint(stretched));
					var radius = math.max(0.01f, math.distance(plan, center));
					SetHole(insert, i, new InsertHole(hole.Center, radius), "Resize Hole");
				}
			}
		}

		private static void SetHole(InsertComponent insert, int index, InsertHole hole, string action)
		{
			Undo.RecordObject(insert, action);
			insert.SetHole(index, hole);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
		}
	}
}
