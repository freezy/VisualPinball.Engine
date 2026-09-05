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

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class WireRailSplineHandlesTests
	{
		[Test]
		public void ShouldCreateWireRailFromTheGameObjectMenu()
		{
			var parent = new GameObject("Wire Rail Menu Parent");
			try {
				var create = typeof(WireRailInspector).GetMethod("CreateWireRailGameObject",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(create, Is.Not.Null);
				var menuItem = create.GetCustomAttribute<MenuItem>();
				Assert.That(menuItem, Is.Not.Null);
				Assert.That(menuItem.menuItem, Is.EqualTo("GameObject/Pinball/Wire Rail"));

				create.Invoke(null, new object[] { new MenuCommand(parent) });
				var created = Selection.activeGameObject;
				Assert.That(created, Is.Not.Null);
				Assert.That(created.name, Is.EqualTo("Wire Rail"));
				Assert.That(created.transform.parent, Is.SameAs(parent.transform));
				Assert.That(created.transform.localPosition, Is.EqualTo(Vector3.zero));
				Assert.That(created.GetComponent<WireRailComponent>(), Is.Not.Null);

				Undo.PerformUndo();
				Assert.That(created == null, Is.True);
			} finally {
				Selection.activeGameObject = null;
				Undo.ClearAll();
				Object.DestroyImmediate(parent);
			}
		}

		[UnityTest]
		public IEnumerator ShouldSelectTheGeneratedSplineWhenEditingStarts()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var editSpline = typeof(WireRailInspector).GetMethod("EditSpline",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(editSpline, Is.Not.Null);

				editSpline.Invoke(null, new object[] { component.SplineContainer });
				yield return null;

				Assert.That(Selection.activeGameObject,
					Is.SameAs(component.SplineContainer.gameObject));
			} finally {
				ToolManager.SetActiveContext<GameObjectToolContext>();
				Selection.activeGameObject = null;
				Object.DestroyImmediate(gameObject);
			}
		}

		[UnityTest]
		public IEnumerator ShouldKeepTheSplineMoveGizmoVisibleWhileEditingKnots()
		{
			var gameObject = new GameObject("Wire Rail");
			var toolsWereHidden = Tools.hidden;
			try {
				Tools.hidden = false;
				var component = gameObject.AddComponent<WireRailComponent>();
				Selection.activeGameObject = component.SplineContainer.gameObject;
				ActiveEditorTracker.sharedTracker.ForceRebuild();
				yield return null;

				var splineEditorAssembly = Assembly.Load("Unity.Splines.Editor");
				var contextType = splineEditorAssembly.GetType(
					"UnityEditor.Splines.SplineToolContext");
				var moveToolType = splineEditorAssembly.GetType(
					"UnityEditor.Splines.SplineMoveTool");
				Assert.That(contextType, Is.Not.Null);
				Assert.That(moveToolType, Is.Not.Null);
				Assert.That(Selection.activeGameObject,
					Is.SameAs(component.SplineContainer.gameObject));
				ToolManager.SetActiveContext(contextType);
				ToolManager.SetActiveTool(moveToolType);
				yield return null;

				Assert.That(ToolManager.activeToolType, Is.EqualTo(moveToolType));
				Assert.That(Tools.hidden, Is.False,
					"The spline move tool needs to draw its standard axis handles.");
			} finally {
				ToolManager.SetActiveContext<GameObjectToolContext>();
				Selection.activeGameObject = null;
				Tools.hidden = toolsWereHidden;
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldUndoAndRedoACompleteWireDragAsOneOperation()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				Undo.ClearAll();
				var component = gameObject.AddComponent<WireRailComponent>();
				var initialOffset = component.Segments[0].GetRailOffset(0);
				var intermediateOffset = initialOffset + new Vector2(5f, 3f);
				var finalOffset = initialOffset + new Vector2(12f, 8f);
				var editorType = typeof(WireRailInspector).Assembly.GetType(
					"VisualPinball.Unity.Editor.WireRailCrossSectionEditor");
				var beginDragUndo = editorType?.GetMethod("BeginDragUndo",
					BindingFlags.Static | BindingFlags.NonPublic);
				var endDragUndo = editorType?.GetMethod("EndDragUndo",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(beginDragUndo, Is.Not.Null);
				Assert.That(endDragUndo, Is.Not.Null);

				var undoGroup = (int)beginDragUndo.Invoke(null, new object[] { component });
				component.SetWireProperties(0, new[] { 0 },
					new[] { intermediateOffset });
				component.SetWireProperties(0, new[] { 0 }, new[] { finalOffset });
				endDragUndo.Invoke(null, new object[] { undoGroup });

				Assert.That(component.Segments[0].GetRailOffset(0),
					Is.EqualTo(finalOffset));
				Undo.PerformUndo();
				Assert.That(component.Segments[0].GetRailOffset(0),
					Is.EqualTo(initialOffset));
				Undo.PerformRedo();
				Assert.That(component.Segments[0].GetRailOffset(0),
					Is.EqualTo(finalOffset));
			} finally {
				Undo.ClearAll();
				Object.DestroyImmediate(gameObject);
			}
		}

		[UnityTest]
		public IEnumerator ShouldUndoAndRedoADirectSplineKnotDrag()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				Undo.ClearAll();
				var component = gameObject.AddComponent<WireRailComponent>();
				// Creating the hidden spline child registers its own Undo operation. A direct
				// knot drag starts from an already-authored object, so isolate that gesture.
				Undo.ClearAll();
				var container = component.SplineContainer;
				var handles = typeof(WireRailInspector).Assembly.GetType(
					"VisualPinball.Unity.Editor.WireRailSplineHandles");
				var moveKnot = handles?.GetMethod("MoveKnot",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(moveKnot, Is.Not.Null);

				var splineEditorAssembly = Assembly.Load("Unity.Splines.Editor");
				var knotType = splineEditorAssembly.GetType(
					"UnityEditor.Splines.SelectableKnot");
				Assert.That(knotType, Is.Not.Null);
				var selectableKnot = System.Activator.CreateInstance(knotType,
					new object[] { new SplineInfo(container, 0), 0 });
				var positionProperty = knotType.GetProperty("Position");
				Assert.That(positionProperty, Is.Not.Null);
				var initialPosition = (float3)positionProperty.GetValue(selectableKnot);
				var finalPosition = initialPosition + new float3(17f, 23f, 11f);

				moveKnot.Invoke(null,
					new[] { (object)component, selectableKnot, finalPosition });

				Assert.That(math.distance((float3)positionProperty.GetValue(selectableKnot),
					finalPosition),
					Is.LessThan(0.001f));
				yield return null;
				Undo.PerformUndo();
				yield return null;
				Assert.That(math.distance(
					(float3)positionProperty.GetValue(selectableKnot), initialPosition),
					Is.LessThan(0.001f));
				Undo.PerformRedo();
				yield return null;
				Assert.That(math.distance(
					(float3)positionProperty.GetValue(selectableKnot), finalPosition),
					Is.LessThan(0.001f));
			} finally {
				Undo.ClearAll();
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldGradeKnotHeightsByHorizontalDistanceAndSupportUndo()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;
				container.Spline = new Spline(new[] {
					new float3(0f, 0f, 10f),
					new float3(100f, 0f, -40f),
					new float3(100f, 300f, 400f),
					new float3(500f, 300f, 110f),
				}, TangentMode.Linear);
				Undo.ClearAll();

				Assert.That(GradeSplineHeights(component, 0, 3), Is.True);
				Assert.That(container.Spline[0].Position.z, Is.EqualTo(10f).Within(0.001f));
				Assert.That(container.Spline[1].Position.z, Is.EqualTo(22.5f).Within(0.001f));
				Assert.That(container.Spline[2].Position.z, Is.EqualTo(60f).Within(0.001f));
				Assert.That(container.Spline[3].Position.z, Is.EqualTo(110f).Within(0.001f));

				Undo.PerformUndo();
				Assert.That(container.Spline[1].Position.z, Is.EqualTo(-40f).Within(0.001f));
				Assert.That(container.Spline[2].Position.z, Is.EqualTo(400f).Within(0.001f));
				Undo.PerformRedo();
				Assert.That(container.Spline[1].Position.z, Is.EqualTo(22.5f).Within(0.001f));
				Assert.That(container.Spline[2].Position.z, Is.EqualTo(60f).Within(0.001f));
			} finally {
				Undo.ClearAll();
				Object.DestroyImmediate(gameObject);
			}
		}

		[TestCase(TangentMode.AutoSmooth, TangentMode.Continuous)]
		[TestCase(TangentMode.Broken, TangentMode.Broken)]
		[TestCase(TangentMode.Continuous, TangentMode.Continuous)]
		[TestCase(TangentMode.Mirrored, TangentMode.Mirrored)]
		public void ShouldGradeBezierTangentsWithoutChangingThePlanView(
			TangentMode initialMode, TangentMode expectedMode)
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;
				var rotation = quaternion.EulerXYZ(math.radians(new float3(23f, -17f, 31f)));
				var inverseRotation = math.inverse(rotation);
				var spline = new Spline();
				spline.Add(new BezierKnot(new float3(0f, 0f, 5f),
					math.rotate(inverseRotation, new float3(-40f, -20f, 0f)),
					math.rotate(inverseRotation, new float3(90f, 25f, 0f)), rotation),
					TangentMode.Broken);
				spline.Add(new BezierKnot(new float3(180f, 130f, -70f),
					math.rotate(inverseRotation, new float3(-65f, -55f, 0f)),
					math.rotate(inverseRotation, new float3(55f, 85f, 0f)), rotation),
					TangentMode.Broken);
				spline.Add(new BezierKnot(new float3(360f, 310f, 125f),
					math.rotate(inverseRotation, new float3(-80f, -35f, 0f)),
					math.rotate(inverseRotation, new float3(30f, 20f, 0f)), rotation),
					TangentMode.Broken);
				for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
					var mainTangent = knotIndex == spline.Count - 1
						? BezierTangent.In : BezierTangent.Out;
					spline.SetTangentMode(knotIndex, initialMode, mainTangent);
				}
				container.Spline = spline;
				var originalCurves = new[] { spline.GetCurve(0), spline.GetCurve(1) };
				var planarLength = CalculatePlanarLength(originalCurves[0])
					+ CalculatePlanarLength(originalCurves[1]);
				var expectedGrade = 120f / planarLength;

				Assert.That(GradeSplineHeights(component, 0, 2), Is.True);

				for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
					Assert.That(spline.GetTangentMode(knotIndex), Is.EqualTo(expectedMode));
				}
				for (var segmentIndex = 0; segmentIndex < originalCurves.Length;
					segmentIndex++) {
					var original = originalCurves[segmentIndex];
					var graded = spline.GetCurve(segmentIndex);
					AssertPlanarPoint(graded.P0, original.P0);
					AssertPlanarPoint(graded.P1, original.P1);
					AssertPlanarPoint(graded.P2, original.P2);
					AssertPlanarPoint(graded.P3, original.P3);
					var outgoing = graded.P1 - graded.P0;
					var incoming = graded.P2 - graded.P3;
					Assert.That(outgoing.z,
						Is.EqualTo(expectedGrade * math.length(outgoing.xy)).Within(0.001f));
					Assert.That(incoming.z,
						Is.EqualTo(-expectedGrade * math.length(incoming.xy)).Within(0.001f));
				}
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldOfferHeightGradingForZeroOrTwoSelectedKnotsOnly()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;
				container.Spline = new Spline(new[] {
					new float3(0f, 0f, 0f),
					new float3(100f, 0f, 0f),
					new float3(200f, 0f, 0f),
					new float3(300f, 0f, 0f),
				}, TangentMode.Linear);
				ClearSplineSelection();
				Assert.That(TryGetGradeSplineRange(container, out var startKnot,
					out var endKnot, out var selectedCount), Is.True);
				Assert.That(selectedCount, Is.Zero);
				Assert.That(startKnot, Is.Zero);
				Assert.That(endKnot, Is.EqualTo(3));

				SetSplineSelection(container, 3);
				Assert.That(TryGetGradeSplineRange(container, out _, out _,
					out selectedCount), Is.False);
				Assert.That(selectedCount, Is.EqualTo(1));

				AddSplineSelection(container, 1);
				Assert.That(TryGetGradeSplineRange(container, out startKnot,
					out endKnot, out selectedCount), Is.True);
				Assert.That(selectedCount, Is.EqualTo(2));
				Assert.That(startKnot, Is.EqualTo(1));
				Assert.That(endKnot, Is.EqualTo(3));

				AddSplineSelection(container, 0);
				Assert.That(TryGetGradeSplineRange(container, out _, out _,
					out selectedCount), Is.False);
				Assert.That(selectedCount, Is.EqualTo(3));
			} finally {
				ClearSplineSelection();
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldGradeOnlyBetweenTwoSelectedKnots()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;
				var spline = new Spline();
				var positions = new[] {
					new float3(0f, 0f, -100f),
					new float3(100f, 0f, 10f),
					new float3(200f, 0f, 900f),
					new float3(500f, 0f, 110f),
					new float3(700f, 0f, 500f),
				};
				for (var knotIndex = 0; knotIndex < positions.Length; knotIndex++) {
					spline.Add(new BezierKnot(positions[knotIndex],
						new float3(-30f, 0f, 0f), new float3(30f, 0f, 0f)),
						TangentMode.Broken);
				}
				spline.SetTangentMode(1, TangentMode.Continuous, BezierTangent.Out);
				spline.SetTangentMode(2, TangentMode.AutoSmooth, BezierTangent.Out);
				spline.SetTangentMode(3, TangentMode.Mirrored, BezierTangent.In);
				container.Spline = spline;
				var precedingCurve = spline.GetCurve(0);
				var followingCurve = spline.GetCurve(3);
				SetSplineSelection(container, 3);
				AddSplineSelection(container, 1);

				Assert.That(TryGetGradeSplineRange(container, out var startKnot,
					out var endKnot, out var selectedCount), Is.True);
				Assert.That(selectedCount, Is.EqualTo(2));
				Assert.That(GradeSplineHeights(component, startKnot, endKnot), Is.True);

				Assert.That(spline[0].Position.z, Is.EqualTo(-100f).Within(0.001f));
				Assert.That(spline[1].Position.z, Is.EqualTo(10f).Within(0.001f));
				Assert.That(spline[2].Position.z, Is.EqualTo(35f).Within(0.001f));
				Assert.That(spline[3].Position.z, Is.EqualTo(110f).Within(0.001f));
				Assert.That(spline[4].Position.z, Is.EqualTo(500f).Within(0.001f));
				AssertCurve(spline.GetCurve(0), precedingCurve);
				AssertCurve(spline.GetCurve(3), followingCurve);
				Assert.That(spline.GetTangentMode(1), Is.EqualTo(TangentMode.Broken));
				Assert.That(spline.GetTangentMode(2), Is.EqualTo(TangentMode.Continuous));
				Assert.That(spline.GetTangentMode(3), Is.EqualTo(TangentMode.Broken));
			} finally {
				ClearSplineSelection();
				Object.DestroyImmediate(gameObject);
			}
		}

		[TestCase(TangentMode.AutoSmooth)]
		[TestCase(TangentMode.Continuous)]
		[TestCase(TangentMode.Mirrored)]
		public void ShouldKeepBoundaryTangentModesWhenIntervalGradeIsANoOp(
			TangentMode boundaryMode)
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var spline = new Spline(new[] {
					new float3(0f, 0f, 20f),
					new float3(100f, 0f, 20f),
					new float3(200f, 0f, 20f),
					new float3(300f, 0f, 20f),
					new float3(400f, 0f, 20f),
				}, TangentMode.AutoSmooth);
				spline.SetTangentMode(1, boundaryMode, BezierTangent.Out);
				spline.SetTangentMode(3, boundaryMode, BezierTangent.In);
				component.SplineContainer.Spline = spline;
				var precedingCurve = spline.GetCurve(0);
				var followingCurve = spline.GetCurve(3);

				Assert.That(GradeSplineHeights(component, 1, 3), Is.True);

				Assert.That(spline.GetTangentMode(1), Is.EqualTo(boundaryMode));
				Assert.That(spline.GetTangentMode(3), Is.EqualTo(boundaryMode));
				AssertCurve(spline.GetCurve(0), precedingCurve);
				AssertCurve(spline.GetCurve(3), followingCurve);
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldAddAndRemoveKnotsWithoutRemovingTheMinimumRoute()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				component.SetRailCount(3);
				var handles = typeof(WireRailInspector).Assembly.GetType(
					"VisualPinball.Unity.Editor.WireRailSplineHandles");
				var insert = handles?.GetMethod("InsertKnot",
					BindingFlags.Static | BindingFlags.NonPublic);
				var remove = handles?.GetMethod("RemoveKnot",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(insert, Is.Not.Null);
				Assert.That(remove, Is.Not.Null);

				Assert.That(insert.Invoke(null, new object[] { component, 0, 0.5f }),
					Is.True);
				Assert.That(component.SplineContainer.Spline.Count, Is.EqualTo(3));
				Assert.That(component.Segments, Has.Count.EqualTo(1));
				Assert.That(component.Segments[0].RailCount, Is.EqualTo(3));

				Assert.That(remove.Invoke(null, new object[] { component, 1 }), Is.True);
				Assert.That(component.SplineContainer.Spline.Count, Is.EqualTo(2));
				Assert.That(component.Segments, Has.Count.EqualTo(1));
				Assert.That(remove.Invoke(null, new object[] { component, 0 }), Is.False);
				Assert.That(component.SplineContainer.Spline.Count, Is.EqualTo(2));
			} finally {
				Undo.ClearAll();
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldAppendAKnotOnTheClosingCurveWithoutMovingTheOrigin()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(400f, 500f, 0f)), TangentMode.AutoSmooth);
				spline.Add(new BezierKnot(new float3(400f, 0f, 0f)), TangentMode.AutoSmooth);
				spline.Closed = true;
				component.AddLayout(200f);
				var origin = spline[0].Position;
				var handles = typeof(WireRailInspector).Assembly.GetType(
					"VisualPinball.Unity.Editor.WireRailSplineHandles");
				var insert = handles?.GetMethod("InsertKnot",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(insert, Is.Not.Null);

				Assert.That(insert.Invoke(null, new object[] { component, 3, 0.5f }), Is.True);

				Assert.That(spline.Count, Is.EqualTo(5));
				Assert.That(math.distance(spline[0].Position, origin), Is.LessThan(0.001f),
					"the route origin, and with it distance zero, stays where it was");
				Assert.That(spline[4].Position.x, Is.EqualTo(200f).Within(1f),
					"the new knot sits on the closing curve, appended after the last knot");
				Assert.That(component.Segments[1].Distance, Is.EqualTo(200f).Within(0.001f));
			} finally {
				Undo.ClearAll();
				Object.DestroyImmediate(gameObject);
			}
		}

		[TestCase(TangentMode.AutoSmooth, false)]
		[TestCase(TangentMode.Linear, false)]
		[TestCase(TangentMode.Broken, true)]
		[TestCase(TangentMode.Continuous, true)]
		[TestCase(TangentMode.Mirrored, true)]
		public void ShouldOnlyDrawTangentGripsForEditableBezierModes(TangentMode mode,
			bool expected)
		{
			var handles = typeof(WireRailInspector).Assembly.GetType(
				"VisualPinball.Unity.Editor.WireRailSplineHandles");
			var hasEditableTangents = handles?.GetMethod("HasEditableTangents",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(hasEditableTangents, Is.Not.Null);
			Assert.That(hasEditableTangents.Invoke(null, new object[] { mode }),
				Is.EqualTo(expected));
		}

		[TestCase(TangentMode.Broken)]
		[TestCase(TangentMode.Continuous)]
		[TestCase(TangentMode.Mirrored)]
		public void ShouldMoveBezierTangentsDirectlyToTheDraggedPosition(TangentMode mode)
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;
				container.transform.localScale = new Vector3(1.6f, 0.7f, 1.25f);
				var spline = container.Spline;
				spline.SetTangentMode(0, mode, BezierTangent.Out);
				var splineEditorAssembly = Assembly.Load("Unity.Splines.Editor");
				var knotType = splineEditorAssembly.GetType(
					"UnityEditor.Splines.SelectableKnot");
				var splineInfo = new SplineInfo(container, 0);
				var knot = System.Activator.CreateInstance(knotType,
					new object[] { splineInfo, 0 });
				var tangent = knotType.GetProperty("TangentOut")?.GetValue(knot);
				var knotPosition = (float3)knotType.GetProperty("Position")?.GetValue(knot);
				var target = knotPosition + new float3(23f, 31f, 17f);

				var handles = typeof(WireRailInspector).Assembly.GetType(
					"VisualPinball.Unity.Editor.WireRailSplineHandles");
				var applyTangentPosition = handles?.GetMethod("ApplyTangentPosition",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(applyTangentPosition, Is.Not.Null);
				applyTangentPosition.Invoke(null, new object[] { tangent, target });

				var tangentType = tangent.GetType();
				var position = (float3)tangentType.GetProperty("Position")?.GetValue(tangent);
				Assert.That(math.distance(position, target), Is.LessThan(0.01f));
				if (mode == TangentMode.Mirrored) {
					var localDirection = (float3)tangentType.GetProperty("LocalDirection")
						?.GetValue(tangent);
					var opposite = tangentType.GetProperty("OppositeTangent")?.GetValue(tangent);
					var oppositeDirection = (float3)tangentType.GetProperty("LocalDirection")
						?.GetValue(opposite);
					Assert.That(math.length(localDirection),
						Is.EqualTo(math.length(oppositeDirection))
						.Within(0.001f));
				}
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		private static float CalculatePlanarLength(BezierCurve curve)
		{
			curve.P0.z = 0f;
			curve.P1.z = 0f;
			curve.P2.z = 0f;
			curve.P3.z = 0f;
			return CurveUtility.CalculateLength(curve, 64);
		}

		private static bool GradeSplineHeights(WireRailComponent component,
			int startKnotIndex, int endKnotIndex)
		{
			var gradeSplineHeights = typeof(WireRailInspector).GetMethod(
				"GradeSplineHeights", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(gradeSplineHeights, Is.Not.Null);
			return (bool)gradeSplineHeights.Invoke(null,
				new object[] { component, startKnotIndex, endKnotIndex });
		}

		private static bool TryGetGradeSplineRange(SplineContainer container,
			out int startKnotIndex, out int endKnotIndex, out int selectedKnotCount)
		{
			var getGradeSplineRange = typeof(WireRailInspector).GetMethod(
				"TryGetGradeSplineRange", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(getGradeSplineRange, Is.Not.Null);
			var arguments = new object[] { container, 0, 0, 0 };
			var result = (bool)getGradeSplineRange.Invoke(null, arguments);
			startKnotIndex = (int)arguments[1];
			endKnotIndex = (int)arguments[2];
			selectedKnotCount = (int)arguments[3];
			return result;
		}

		private static void ClearSplineSelection()
		{
			var selectionType = Assembly.Load("Unity.Splines.Editor").GetType(
				"UnityEditor.Splines.SplineSelection");
			var clear = selectionType?.GetMethod("Clear", BindingFlags.Static
				| BindingFlags.Public);
			Assert.That(clear, Is.Not.Null);
			clear.Invoke(null, null);
		}

		private static void SetSplineSelection(SplineContainer container, int knotIndex)
			=> ChangeSplineSelection("Set", container, knotIndex);

		private static void AddSplineSelection(SplineContainer container, int knotIndex)
			=> ChangeSplineSelection("Add", container, knotIndex);

		private static void ChangeSplineSelection(string methodName,
			SplineContainer container, int knotIndex)
		{
			var splineEditorAssembly = Assembly.Load("Unity.Splines.Editor");
			var knotType = splineEditorAssembly.GetType(
				"UnityEditor.Splines.SelectableKnot");
			var selectionType = splineEditorAssembly.GetType(
				"UnityEditor.Splines.SplineSelection");
			Assert.That(knotType, Is.Not.Null);
			Assert.That(selectionType, Is.Not.Null);
			var knot = System.Activator.CreateInstance(knotType,
				new object[] { new SplineInfo(container, 0), knotIndex });
			MethodInfo selectionMethod = null;
			foreach (var method in selectionType.GetMethods(BindingFlags.Static
				| BindingFlags.Public)) {
				if (method.Name == methodName && method.IsGenericMethodDefinition
					&& method.GetParameters().Length == 1) {
					selectionMethod = method.MakeGenericMethod(knotType);
					break;
				}
			}
			Assert.That(selectionMethod, Is.Not.Null);
			selectionMethod.Invoke(null, new[] { knot });
		}

		private static void AssertPlanarPoint(float3 actual, float3 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
		}

		private static void AssertCurve(BezierCurve actual, BezierCurve expected)
		{
			Assert.That(math.distance(actual.P0, expected.P0), Is.LessThan(0.001f));
			Assert.That(math.distance(actual.P1, expected.P1), Is.LessThan(0.001f));
			Assert.That(math.distance(actual.P2, expected.P2), Is.LessThan(0.001f));
			Assert.That(math.distance(actual.P3, expected.P3), Is.LessThan(0.001f));
		}
	}
}
