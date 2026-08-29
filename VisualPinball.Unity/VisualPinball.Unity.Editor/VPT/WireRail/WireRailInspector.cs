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
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(WireRailComponent))]
	public class WireRailInspector : UnityEditor.Editor
	{
		private static readonly string[] ThirdRailSides = { "Left", "Right" };
		private static readonly Color TransitionCurveColor = new(0.05f, 0.75f, 1f, 1f);
		private static SplineContainer _pendingSplineEdit;
		private readonly WireRailCrossSectionEditor _crossSectionEditor = new();

		[MenuItem("GameObject/Pinball/Wire Rail", false, 11)]
		private static void CreateWireRailGameObject(MenuCommand menuCommand)
		{
			var undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Create Wire Rail");
			var parent = menuCommand.context as GameObject;
			parent ??= Selection.activeGameObject;
			var gameObject = new GameObject {
				name = GameObjectUtility.GetUniqueNameForSibling(
					parent ? parent.transform : null, "Wire Rail"),
			};
			StageUtility.PlaceGameObjectInCurrentStage(gameObject);
			GameObjectUtility.SetParentAndAlign(gameObject, parent);
			gameObject.AddComponent<WireRailComponent>();
			Undo.RegisterCreatedObjectUndo(gameObject, "Create Wire Rail");
			Undo.CollapseUndoOperations(undoGroup);

			Selection.activeGameObject = gameObject;
			EditorGUIUtility.PingObject(gameObject);
		}

		public override void OnInspectorGUI()
		{
			var component = (WireRailComponent)target;
			if (Event.current.type == EventType.Layout) {
				component.SynchronizeSegments();
			}
			var container = component.SplineContainer;
			if (!container) {
				EditorGUILayout.HelpBox(
					"The generated Wire Rail Spline child is missing.", MessageType.Error);
				if (GUILayout.Button("Recreate Wire Rail Spline")) {
					Undo.RecordObject(component, "Recreate Wire Rail Spline");
					container = component.EnsureSplineContainerExists();
					component.SynchronizeSegments();
					component.RebuildGeneratedMeshes();
					EditorUtility.SetDirty(component);
				}
				return;
			}

			EditorGUILayout.HelpBox(
				"Spline knots and rail offsets use VPX units. The default route points along +Y; "
				+ "rail offsets are X (lateral) and Z (vertical). Knot rotations control the "
				+ "cross-section orientation when the route turns through 3D.",
				MessageType.Info);

			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.ObjectField("Spline", container, typeof(SplineContainer), true);
			}
			EditorGUILayout.Space(3f);
			var editButtonStyle = new GUIStyle(GUI.skin.button) {
				fontStyle = FontStyle.Bold,
			};
			if (GUILayout.Button("Edit Spline in Scene View", editButtonStyle,
					GUILayout.Height(30f))) {
				EditSpline(container);
			}
			EditorGUILayout.HelpBox(
				"While editing, double-click the spline to add a knot or double-click a knot "
				+ "to remove it.", MessageType.None);

			if (container.Splines.Count > 1) {
				EditorGUILayout.HelpBox(
					"This first wire-rail slice uses the first spline only. Remove additional splines "
					+ "from the container before authoring segment layouts.", MessageType.Warning);
			}

			DrawGenerationSettings(component);

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Segments", EditorStyles.boldLabel);
			if (component.Segments.Count == 0) {
				EditorGUILayout.HelpBox("Add at least two spline knots to create a segment.",
					MessageType.Warning);
				return;
			}

			for (var segmentIndex = 0; segmentIndex < component.Segments.Count; segmentIndex++) {
				DrawSegment(component, segmentIndex);
				if (component.GetNextSegmentIndex(segmentIndex) >= 0) {
					DrawConnection(component, segmentIndex);
				}
			}
		}

		private void DrawGenerationSettings(WireRailComponent component)
		{
			serializedObject.Update();
			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Render Geometry", EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_renderMaterial"),
				new GUIContent("Material"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_wireDiameter"),
				new GUIContent("New Wire Diameter",
					"Diameter assigned when a new wire layout is created, in VPX units."));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_radialSegments"),
				new GUIContent("Tube Sides"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_renderSamplesPerSegment"),
				new GUIContent("Minimum Samples Per Segment",
					"Base longitudinal detail. Sharper wire bends receive extra rings automatically."));

			EditorGUILayout.Space(4f);
			EditorGUILayout.LabelField("Ball Channel Collider", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_referenceBallDiameter"),
				new GUIContent("Ball Diameter", "Reference ball diameter in VPX units."));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_colliderSamplesPerSegment"),
				new GUIContent("Samples Per Segment"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_showColliderPreview"),
				new GUIContent("Show Collider Preview"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_physicsMaterial"),
				new GUIContent("Physics Material"));
			var overwritePhysics = serializedObject.FindProperty("_overwritePhysics");
			EditorGUILayout.PropertyField(overwritePhysics, new GUIContent("Overwrite Physics"));
			if (overwritePhysics.boolValue) {
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_elasticity"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_elasticityFalloff"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_friction"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_scatter"));
			}
			var settingsChanged = EditorGUI.EndChangeCheck();
			serializedObject.ApplyModifiedProperties();
			if (settingsChanged) {
				component.RebuildGeneratedMeshes();
				SceneView.RepaintAll();
			}

			if (!string.IsNullOrEmpty(component.GenerationError)) {
				EditorGUILayout.HelpBox(component.GenerationError, MessageType.Error);
			}
			var renderMesh = component.RenderMesh;
			var colliderMesh = component.ColliderMesh;
			if (renderMesh && colliderMesh) {
				EditorGUILayout.LabelField("Generated",
					$"{renderMesh.vertexCount} render vertices, "
					+ $"{colliderMesh.triangles.Length / 3} collider triangles");
			}
			if (GUILayout.Button("Rebuild Geometry")) {
				component.RebuildGeneratedMeshes();
				SceneView.RepaintAll();
			}
		}

		private void DrawSegment(WireRailComponent component, int segmentIndex)
		{
			var segment = component.Segments[segmentIndex];
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField($"Segment {segmentIndex + 1}  (Knot {segmentIndex} → "
				+ $"{(segmentIndex + 1) % component.SplineContainer.Spline.Count})",
				EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			var railCount = EditorGUILayout.DelayedIntField(new GUIContent("Rail Count",
				"Changing the count applies the ball-clearance default layout for that count."),
				segment.RailCount);
			if (GUILayout.Button("−", GUILayout.Width(28f))) {
				railCount = math.max(1, segment.RailCount - 1);
			}
			if (GUILayout.Button("+", GUILayout.Width(28f))) {
				railCount = segment.RailCount + 1;
			}
			EditorGUILayout.EndHorizontal();
			if (railCount != segment.RailCount) {
				Edit(component, "Change Wire Rail Count",
					() => component.SetRailCount(segmentIndex, math.max(1, railCount)));
				segment = component.Segments[segmentIndex];
			}

			if (segment.RailCount == 3) {
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(new GUIContent("Third Rail",
					"Choose whether the third rail starts at middle-left or middle-right."));
				var side = (WireRailThirdRailSide)GUILayout.Toolbar(
					(int)segment.ThirdRailSide, ThirdRailSides);
				EditorGUILayout.EndHorizontal();
				if (side != segment.ThirdRailSide) {
					Edit(component, "Change Third Wire Rail Side",
						() => component.SetThirdRailSide(segmentIndex, side));
					segment = component.Segments[segmentIndex];
				}
			}

			_crossSectionEditor.Draw(component, segmentIndex);

			if (GUILayout.Button("Reset Layout")) {
				Edit(component, "Reset Wire Rail Layout",
					() => component.ResetSegmentLayout(segmentIndex));
			}
			EditorGUILayout.EndVertical();
		}

		private static void DrawConnection(WireRailComponent component, int segmentIndex)
		{
			var nextSegmentIndex = component.GetNextSegmentIndex(segmentIndex);
			var segment = component.Segments[segmentIndex];
			var nextSegment = component.Segments[nextSegmentIndex];
			var connection = segment.ConnectionToNext;
			var wireCount = math.min(segment.RailCount, nextSegment.RailCount);

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField(
				$"Connection: Segment {segmentIndex + 1} → Segment {nextSegmentIndex + 1}",
				EditorStyles.boldLabel);
			EditorGUILayout.LabelField(
				"Configure each matching wire as it passes through this knot.",
				EditorStyles.wordWrappedMiniLabel);

			for (var wireIndex = 0; wireIndex < wireCount; wireIndex++) {
				EditorGUILayout.BeginVertical(GUI.skin.box);
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField($"Wire {wireIndex + 1}", EditorStyles.boldLabel,
					GUILayout.Width(48f));
				var continuous = connection.IsWireContinuous(wireIndex);
				var toggled = EditorGUILayout.ToggleLeft(new GUIContent("Continuous",
					"Blend this wire's position and diameter across the knot instead of "
					+ "ending and restarting it."), continuous, GUILayout.Width(88f));
				if (toggled != continuous) {
					var capturedWireIndex = wireIndex;
					Edit(component, "Change Wire Rail Continuity",
						() => component.SetWireContinuous(segmentIndex,
							capturedWireIndex, toggled));
					connection = component.Segments[segmentIndex].ConnectionToNext;
				}

				if (toggled) {
					var currentWeight = connection.GetWireWeight(wireIndex);
					var weight = EditorGUILayout.Slider(new GUIContent("Weight",
						"0 keeps the junction at this segment's position, 1 keeps it at the "
						+ "next segment's position, and 0.5 meets halfway."),
						currentWeight, 0f, 1f);
					if (!Mathf.Approximately(weight, currentWeight)) {
						var capturedWireIndex = wireIndex;
						Edit(component, "Change Wire Rail Blend Weight",
							() => component.SetWireConnectionWeight(segmentIndex,
								capturedWireIndex, weight));
						connection = component.Segments[segmentIndex].ConnectionToNext;
					}
				}
				EditorGUILayout.EndHorizontal();

				if (toggled) {
					EditorGUI.BeginChangeCheck();
					var editableCurve = CloneCurve(connection.GetWireCurve(wireIndex));
					var curve = EditorGUILayout.CurveField(new GUIContent("Transition Curve",
						"Shapes how this wire moves through both adjoining segments. The curve "
						+ "is evaluated from 0 to 1 on each segment."),
						editableCurve, TransitionCurveColor,
						new Rect(0f, 0f, 1f, 1f));
					if (EditorGUI.EndChangeCheck()) {
						var capturedWireIndex = wireIndex;
						Edit(component, "Change Wire Rail Transition Curve",
							() => component.SetWireTransitionCurve(segmentIndex,
								capturedWireIndex, curve));
						connection = component.Segments[segmentIndex].ConnectionToNext;
					}
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.LabelField("Weight: 0 = this segment   •   0.5 = halfway   •   1 = next segment",
				EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.EndVertical();
		}

		private static AnimationCurve CloneCurve(AnimationCurve source)
		{
			if (source == null) {
				return AnimationCurve.Linear(0f, 0f, 1f, 1f);
			}
			return new AnimationCurve(source.keys) {
				preWrapMode = source.preWrapMode,
				postWrapMode = source.postWrapMode,
			};
		}

		private static void Edit(WireRailComponent component, string undoName, Action edit)
		{
			Undo.RecordObject(component, undoName);
			edit();
			EditorUtility.SetDirty(component);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			SceneView.RepaintAll();
		}

		internal static void EditSpline(SplineContainer container)
		{
			if (!container) {
				return;
			}
			_pendingSplineEdit = container;
			Selection.activeGameObject = container.gameObject;
			EditorApplication.update -= ActivateSplineTool;
			EditorApplication.update += ActivateSplineTool;
		}

		private static void ActivateSplineTool()
		{
			EditorApplication.update -= ActivateSplineTool;
			var container = _pendingSplineEdit;
			_pendingSplineEdit = null;
			if (!container || Selection.activeGameObject != container.gameObject) {
				return;
			}

			ActiveEditorTracker.sharedTracker.ForceRebuild();
			ToolManager.SetActiveContext<SplineToolContext>();
			ToolManager.SetActiveTool<SplineMoveTool>();
			SceneView.RepaintAll();
		}
	}

	[InitializeOnLoad]
	internal static class WireRailScenePreview
	{
		private const int SamplesPerSegment = 24;
		private static readonly Color[] RailColors = {
			new(0.05f, 0.75f, 1f, 0.95f),
			new(1f, 0.55f, 0.05f, 0.95f),
			new(0.45f, 1f, 0.2f, 0.95f),
			new(1f, 0.2f, 0.65f, 0.95f),
			new(0.65f, 0.35f, 1f, 0.95f),
		};

		static WireRailScenePreview()
		{
			SceneView.duringSceneGui += OnSceneGUI;
		}

		private static void OnSceneGUI(SceneView _)
		{
			var selected = Selection.activeGameObject;
			var component = selected ? selected.GetComponent<WireRailComponent>() : null;
			component ??= selected ? selected.GetComponentInParent<WireRailComponent>() : null;
			if (!component) {
				return;
			}

			var container = component.SplineContainer;
			if (!container) {
				return;
			}
			component.SynchronizeSegments();
			DrawEditPanel(container);
			var spline = container.Spline;
			if (spline == null || spline.Count < 2) {
				return;
			}
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			var evaluationContext = new WireRailPathEvaluationContext();

			for (var segmentIndex = 0; segmentIndex < component.Segments.Count; segmentIndex++) {
				var segment = component.Segments[segmentIndex];
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					var points = new Vector3[SamplesPerSegment + 1];
					for (var sampleIndex = 0; sampleIndex <= SamplesPerSegment; sampleIndex++) {
						var curveT = sampleIndex / (float)SamplesPerSegment;
						points[sampleIndex] = WireRailSplineGeometry.TryEvaluateRailPosition(spline,
							component.Segments, evaluationContext, segmentIndex, railIndex, curveT,
							out var position)
							? container.transform.TransformPoint((Vector3)position)
							: container.transform.position;
					}
					Handles.color = RailColors[railIndex % RailColors.Length];
					Handles.DrawAAPolyLine(3f, points);
				}

				var spinePoints = new Vector3[SamplesPerSegment + 1];
				for (var sampleIndex = 0; sampleIndex <= SamplesPerSegment; sampleIndex++) {
					var curveT = sampleIndex / (float)SamplesPerSegment;
					spinePoints[sampleIndex] = EvaluateWorldPosition(container, spline,
						segmentIndex, curveT, Vector2.zero);
				}
				var previousZTest = Handles.zTest;
				Handles.zTest = CompareFunction.Always;
				Handles.color = new Color(0.02f, 0.02f, 0.02f, 0.95f);
				Handles.DrawAAPolyLine(editing ? 11f : 8f, spinePoints);
				Handles.color = editing
					? new Color(1f, 0.78f, 0.05f, 1f)
					: new Color(0.9f, 0.95f, 1f, 1f);
				Handles.DrawAAPolyLine(editing ? 5f : 4f, spinePoints);
				Handles.zTest = previousZTest;

				Handles.color = Color.white;
				var labelPosition = EvaluateWorldPosition(container, spline, segmentIndex,
					0.5f, Vector2.zero);
				Handles.Label(labelPosition, $"{segmentIndex + 1}: {segment.RailCount} rail"
					+ (segment.RailCount == 1 ? string.Empty : "s"), EditorStyles.boldLabel);
			}

			if (component.ShowColliderPreview) {
				DrawColliderPreview(component.ColliderMesh, container.transform);
			}
		}

		private static void DrawEditPanel(SplineContainer container)
		{
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			Handles.BeginGUI();
			GUILayout.BeginArea(new Rect(55f, 42f, 250f, editing ? 86f : 58f),
				GUIContent.none, GUI.skin.window);
			using (new EditorGUI.DisabledScope(editing)) {
				if (GUILayout.Button(editing ? "Editing Wire Rail Spline"
						: "Edit Wire Rail Spline", GUILayout.Height(26f))) {
					WireRailInspector.EditSpline(container);
				}
			}
			if (editing) {
				GUILayout.Label("Double-click line: add knot\nDouble-click knot: remove",
					EditorStyles.miniLabel);
			}
			GUILayout.EndArea();
			Handles.EndGUI();
		}

		private static void DrawColliderPreview(Mesh mesh, Transform meshTransform)
		{
			if (!mesh || mesh.vertexCount == 0) {
				return;
			}
			var vertices = mesh.vertices;
			var indices = mesh.triangles;
			var edges = new HashSet<ulong>();
			Handles.color = new Color(1f, 0.9f, 0.05f, 0.9f);
			for (var i = 0; i < indices.Length; i += 3) {
				DrawEdge(indices[i], indices[i + 1]);
				DrawEdge(indices[i + 1], indices[i + 2]);
				DrawEdge(indices[i + 2], indices[i]);
			}

			void DrawEdge(int first, int second)
			{
				var min = (uint)math.min(first, second);
				var max = (uint)math.max(first, second);
				var key = ((ulong)min << 32) | max;
				if (!edges.Add(key)) {
					return;
				}
				Handles.DrawLine(meshTransform.TransformPoint(vertices[first]),
					meshTransform.TransformPoint(vertices[second]), 2f);
			}
		}

		private static Vector3 EvaluateWorldPosition(SplineContainer container, Spline spline,
			int segmentIndex, float curveT, Vector2 offset)
		{
			var knotT = segmentIndex + math.saturate(curveT);
			var normalizedT = SplineUtility.GetNormalizedInterpolation(spline, knotT,
				PathIndexUnit.Knot);
			if (!spline.Evaluate(normalizedT, out var position, out var tangent, out var up)) {
				return container.transform.position;
			}

			tangent = math.normalizesafe(tangent, new float3(0f, 1f, 0f));
			up -= tangent * math.dot(up, tangent);
			up = math.normalizesafe(up, new float3(0f, 0f, 1f));
			var right = math.normalizesafe(math.cross(tangent, up), new float3(1f, 0f, 0f));
			up = math.normalizesafe(math.cross(right, tangent), new float3(0f, 0f, 1f));
			var offsetPosition = position + right * offset.x + up * offset.y;
			return container.transform.TransformPoint(new Vector3(offsetPosition.x,
				offsetPosition.y, offsetPosition.z));
		}
	}
}
