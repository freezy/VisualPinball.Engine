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

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	[Flags]
	internal enum DragPointSplineExposure
	{
		None = 0,
		Smooth = 1 << 0,
		Slingshot = 1 << 1,
		Texture = 1 << 2,
	}

	public static class DragPointSplineInspectorGUI
	{
		public static void OnInspectorGUI(DragPointSplineComponent component,
			bool showEditButton = true)
		{
			EditorGUILayout.Space(10f);
			EditorGUILayout.LabelField("Spline", EditorStyles.boldLabel);
			if (showEditButton) {
				using (new EditorGUI.DisabledScope(!IsEditable(component))) {
					if (GUILayout.Button("Edit Spline")) {
						EditSpline(component);
					}
				}
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Center Origin")) {
				CenterOrigin(component);
			}
			if (GUILayout.Button("Reverse")) {
				Reverse(component);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Flip X")) {
				Flip(component, FlipAxis.X);
			}
			if (GUILayout.Button("Flip Y")) {
				Flip(component, FlipAxis.Y);
			}
			using (new EditorGUI.DisabledScope(component.Owner?.SplinePlanar ?? true)) {
				if (GUILayout.Button("Flip Z")) {
					Flip(component, FlipAxis.Z);
				}
			}
			EditorGUILayout.EndHorizontal();
		}

		public static void EditSpline(DragPointSplineComponent component)
		{
			Selection.activeGameObject = component.Container.gameObject;
			ToolManager.SetActiveContext<SplineToolContext>();
			ToolManager.SetActiveTool<SplineMoveTool>();
			SceneView.RepaintAll();
		}

		public static void Flip(DragPointSplineComponent component, FlipAxis axis)
		{
			var dragPoints = component.DragPoints;
			if (dragPoints.Length == 0) {
				return;
			}

			RecordUndo(component, $"Flip {axis} Drag Points");
			var center = dragPoints.Aggregate(Vertex3D.Zero,
				(current, dragPoint) => current + dragPoint.Center) / dragPoints.Length;
			foreach (var dragPoint in dragPoints) {
				switch (axis) {
					case FlipAxis.X:
						dragPoint.Center.X = center.X + center.X - dragPoint.Center.X;
						break;
					case FlipAxis.Y:
						dragPoint.Center.Y = center.Y + center.Y - dragPoint.Center.Y;
						break;
					case FlipAxis.Z:
						dragPoint.Center.Z *= -1f;
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
				}
			}

			if (component.Owner?.SplineClosed == true) {
				ReverseInPlace(dragPoints);
			}
			Apply(component, dragPoints);
		}

		public static void Reverse(DragPointSplineComponent component)
		{
			var dragPoints = component.DragPoints;
			if (dragPoints.Length < 2) {
				return;
			}

			RecordUndo(component, "Reverse Drag Points");
			ReverseInPlace(dragPoints);
			Apply(component, dragPoints);
		}

		public static void CenterOrigin(DragPointSplineComponent component)
		{
			var owner = component.Owner;
			var dragPoints = component.DragPoints;
			if (owner == null || dragPoints.Length == 0) {
				return;
			}

			RecordUndo(component, $"Center pivot point of {owner.SplineOwner.name}");
			var center = dragPoints.Aggregate(Vertex3D.Zero,
				(current, dragPoint) => current + dragPoint.Center) / dragPoints.Length;
			var centerUnity = center.ToUnityVector3();
			var ownerTransform = owner.SplineTransform;
			ownerTransform.Translate(centerUnity.TranslateToWorld(ownerTransform)
				- ownerTransform.position);
			foreach (var dragPoint in dragPoints) {
				dragPoint.Center -= center;
			}
			Apply(component, dragPoints);
		}

		internal static DragPointSplineExposure GetExposure(DragPointSplineComponent component)
		{
			switch (component.Owner) {
				case RubberComponent _: return DragPointSplineExposure.Smooth;
				case MetalWireGuideComponent _: return DragPointSplineExposure.Smooth;
				case TriggerComponent _: return DragPointSplineExposure.Smooth
					| DragPointSplineExposure.Slingshot;
				case RampComponent _: return DragPointSplineExposure.Smooth
					| DragPointSplineExposure.Slingshot;
				case SurfaceComponent _: return DragPointSplineExposure.Smooth
					| DragPointSplineExposure.Slingshot | DragPointSplineExposure.Texture;
				case LightInsertMeshComponent _: return DragPointSplineExposure.Smooth
					| DragPointSplineExposure.Texture;
				default: return DragPointSplineExposure.None;
			}
		}

		private static bool IsEditable(DragPointSplineComponent component)
		{
			if (component.Owner is TriggerComponent trigger) {
				var mesh = trigger.GetComponent<TriggerMeshComponent>();
				return !mesh || !mesh.IsCircle;
			}
			return component.Owner != null;
		}

		private static void ReverseInPlace(DragPointData[] dragPoints)
		{
			Array.Reverse(dragPoints, 1, dragPoints.Length - 1);
			var slingshots = dragPoints.Select(dragPoint => dragPoint.IsSlingshot).ToArray();
			for (var i = 0; i < dragPoints.Length; i++) {
				dragPoints[i].IsSlingshot = slingshots[(i + 1) % slingshots.Length];
			}
		}

		private static void Apply(DragPointSplineComponent component,
			DragPointData[] dragPoints)
		{
			component.SetDragPoints(dragPoints);
			component.Owner?.RebuildSplineMeshes();
			EditorUtility.SetDirty(component);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			SceneView.RepaintAll();
		}

		private static void RecordUndo(DragPointSplineComponent component, string name)
		{
			var owner = component.Owner;
			var objects = new List<UnityEngine.Object> { component, component.Container };
			if (owner != null) {
				objects.Add(owner.SplineOwner);
				objects.Add(owner.SplineTransform);
			}
			Undo.RecordObjects(objects.Distinct().ToArray(), name);
		}
	}

	[CustomEditor(typeof(DragPointSplineComponent))]
	public class DragPointSplineComponentInspector : UnityEditor.Editor
	{
		private readonly List<SelectableKnot> _selectedKnots = new();

		public override void OnInspectorGUI()
		{
			var component = (DragPointSplineComponent)target;
			var owner = component.Owner;
			if (owner == null) {
				EditorGUILayout.HelpBox("This spline has no drag-point owner.", MessageType.Warning);
				return;
			}

			EditorGUILayout.LabelField("Owner", owner.SplineOwner.name);
			if (GUILayout.Button("Select Owner")) {
				Selection.activeObject = owner.SplineOwner;
			}
			DragPointSplineInspectorGUI.OnInspectorGUI(component, false);
			DrawSelectedMetadata(component);
		}

		private void DrawSelectedMetadata(DragPointSplineComponent component)
		{
			_selectedKnots.Clear();
			SplineSelection.GetElements(new SplineInfo(component.Container, 0), _selectedKnots);
			EditorGUILayout.Space(10f);
			EditorGUILayout.LabelField("Selected Knots", EditorStyles.boldLabel);
			if (_selectedKnots.Count == 0) {
				EditorGUILayout.HelpBox("Select one or more knots in the Scene view.",
					MessageType.Info);
				return;
			}

			var exposure = DragPointSplineInspectorGUI.GetExposure(component);
			if ((exposure & DragPointSplineExposure.Smooth) != 0) {
				DrawBoolean(component, "Smooth", metadata => metadata.IsSmooth,
					(metadata, value) => metadata.IsSmooth = value);
			}
			if ((exposure & DragPointSplineExposure.Slingshot) != 0) {
				DrawBoolean(component, "Slingshot", metadata => metadata.IsSlingshot,
					(metadata, value) => metadata.IsSlingshot = value);
			}
			if ((exposure & DragPointSplineExposure.Texture) != 0) {
				DrawBoolean(component, "Auto Texture", metadata => metadata.HasAutoTexture,
					(metadata, value) => metadata.HasAutoTexture = value);
				DrawFloat(component, "Texture Coordinate", metadata => metadata.TextureCoord,
					(metadata, value) => metadata.TextureCoord = value);
			}
		}

		private void DrawBoolean(DragPointSplineComponent component, string label,
			Func<DragPointMetadata, bool> getter, Action<DragPointMetadata, bool> setter)
		{
			var first = getter(component.Metadata[_selectedKnots[0].KnotIndex]);
			EditorGUI.showMixedValue = _selectedKnots.Any(knot =>
				getter(component.Metadata[knot.KnotIndex]) != first);
			EditorGUI.BeginChangeCheck();
			var value = EditorGUILayout.Toggle(label, first);
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()) {
				UpdateMetadata(component, label, metadata => setter(metadata, value));
			}
		}

		private void DrawFloat(DragPointSplineComponent component, string label,
			Func<DragPointMetadata, float> getter, Action<DragPointMetadata, float> setter)
		{
			var first = getter(component.Metadata[_selectedKnots[0].KnotIndex]);
			EditorGUI.showMixedValue = _selectedKnots.Any(knot =>
				math.abs(getter(component.Metadata[knot.KnotIndex]) - first) > 1e-6f);
			EditorGUI.BeginChangeCheck();
			var value = EditorGUILayout.FloatField(label, first);
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()) {
				UpdateMetadata(component, label, metadata => setter(metadata, value));
			}
		}

		private void UpdateMetadata(DragPointSplineComponent component, string label,
			Action<DragPointMetadata> update)
		{
			Undo.RecordObjects(new UnityEngine.Object[] { component, component.Container },
				$"Change Drag Point {label}");
			foreach (var knot in _selectedKnots) {
				update(component.Metadata[knot.KnotIndex]);
			}
			component.NotifyMetadataChanged();
			EditorUtility.SetDirty(component);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component);
		}
	}

	[InitializeOnLoad]
	internal static class DragPointSplineSceneView
	{
		private const int SegmentResolution = 24;

		static DragPointSplineSceneView()
		{
			SceneView.duringSceneGui += OnSceneGUI;
		}

		private static void OnSceneGUI(SceneView _)
		{
			var component = Selection.activeGameObject
				? Selection.activeGameObject.GetComponent<DragPointSplineComponent>()
				: null;
			if (!component || !component.Container || component.Container.Spline.Count == 0) {
				return;
			}

			var spline = component.Container.Spline;
			var matrix = component.Container.transform.localToWorldMatrix;
			var segmentCount = spline.Closed ? spline.Count : spline.Count - 1;
			for (var segment = 0; segment < segmentCount; segment++) {
				var points = new Vector3[SegmentResolution + 1];
				var curve = spline.GetCurve(segment);
				for (var sample = 0; sample <= SegmentResolution; sample++) {
					var point = CurveUtility.EvaluatePosition(curve,
						sample / (float)SegmentResolution);
					points[sample] = matrix.MultiplyPoint3x4(new Vector3(point.x, point.y, point.z));
				}
				Handles.color = component.Metadata[segment].IsSlingshot
					? new UnityEngine.Color(1f, 0.15f, 0.1f, 0.9f)
					: new UnityEngine.Color(0.1f, 0.45f, 1f, 0.65f);
				Handles.DrawAAPolyLine(4f, points);
			}

			var style = new GUIStyle(EditorStyles.boldLabel) {
				normal = { textColor = UnityEngine.Color.white },
				alignment = TextAnchor.MiddleCenter,
			};
			for (var i = 0; i < spline.Count; i++) {
				var position = spline[i].Position;
				var world = matrix.MultiplyPoint3x4(new Vector3(position.x, position.y, position.z));
				Handles.Label(world, i.ToString(), style);
			}
		}
	}
}
