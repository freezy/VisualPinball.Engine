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

		[Test]
		public void ShouldAddAndRemoveKnotsWithoutRemovingTheMinimumRoute()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				component.SetRailCount(0, 3);
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
	}
}
