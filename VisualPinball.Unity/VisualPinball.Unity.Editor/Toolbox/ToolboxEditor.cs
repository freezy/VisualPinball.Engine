using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.VPT.Table;
using Component = UnityEngine.Component;

namespace VisualPinball.Unity.Editor.Toolbox
{
	public class ToolboxEditor : EditorWindow
	{
		private static Table Table => TableBehavior == null ? null : TableBehavior.Item;
		private static TableBehavior TableBehavior => FindObjectOfType<TableBehavior>();

		[MenuItem("Visual Pinball/Toolbox", false, 100)]
		public static void ShowWindow()
		{
			GetWindow<ToolboxEditor>("Visual Pinball Toolbox");
		}

		private void OnGUI()
		{
			if (GUILayout.Button("New Table")) {
				var existingTable = FindObjectOfType<TableBehavior>();
				if (existingTable == null) {
					const string tableName = "Table1";
					var rootGameObj = new GameObject();
					var table = new Table(new TableData {Name = tableName});
					var converter = rootGameObj.AddComponent<VpxConverter>();
					converter.Convert(tableName, table);
					DestroyImmediate(converter);
					Selection.activeGameObject = rootGameObj;
					Undo.RegisterCreatedObjectUndo(rootGameObj, "New Table");

				} else {
					EditorUtility.DisplayDialog("Visual Pinball",
						"Sorry, cannot add multiple tables, and there already is " +
						existingTable.name, "Close");
				}
			}

			if (GUILayout.Button("Wall")) {
				var table = Table;
				var surfaceData = new SurfaceData(
					table.GetNewName<Surface>("Wall"),
					new[] {
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f + 50f),
						new DragPointData(table.Width / 2f + 50f, table.Height / 2f + 50f),
						new DragPointData(table.Width / 2f + 50f, table.Height / 2f - 50f)
					}
				);

				var surface = new Surface(surfaceData);
				table.Add(surface, true);
				Selection.activeGameObject = CreateRenderable(table, surface);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Wall");
			}

			if (GUILayout.Button("Gate")) {
				var table = Table;
				var gateData = new GateData(table.GetNewName<Gate>("Gate"), table.Width / 2f, table.Height / 2f);
				var gate = new Gate(gateData);
				table.Add(gate, true);
				Selection.activeGameObject = CreateRenderable(table, gate);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Gate");
			}

			if (GUILayout.Button("Ramp")) {
				var table = Table;
				var rampData = new RampData(table.GetNewName<Ramp>("Ramp"), new[] {
					new DragPointData(table.Width / 2f, table.Height / 2f + 200f) { HasAutoTexture = false, IsSmooth = true },
					new DragPointData(table.Width / 2f, table.Height / 2f - 200f) { HasAutoTexture = false, IsSmooth = true }
				}) {
					HeightTop = 50f,
					HeightBottom = 0f,
					WidthTop = 60f,
					WidthBottom = 75f
				};
				var ramp = new Ramp(rampData);
				table.Add(ramp, true);
				Selection.activeGameObject = CreateRenderable(table, ramp);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Ramp");
			}

			if (GUILayout.Button("Flipper")) {
				var table = Table;
				var flipperData = new FlipperData(table.GetNewName<Flipper>("Flipper"), table.Width / 2f, table.Height / 2f);
				var flipper = new Flipper(flipperData);
				table.Add(flipper, true);
				Selection.activeGameObject = CreateRenderable(table, flipper);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Flipper");
			}

			if (GUILayout.Button("Plunger")) {
				var table = Table;
				var plungerData = new PlungerData(table.GetNewName<Plunger>("Plunger"), table.Width / 2f, table.Height / 2f);
				var plunger = new Plunger(plungerData);
				table.Add(plunger, true);
				Selection.activeGameObject = CreateRenderable(table, plunger);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Plunger");
			}

			if (GUILayout.Button("Bumper")) {
				var table = Table;
				var bumperData = new BumperData(table.GetNewName<Bumper>("Bumper"), table.Width / 2f, table.Height / 2f);
				var bumper = new Bumper(bumperData);
				table.Add(bumper, true);
				Selection.activeGameObject = CreateRenderable(table, bumper);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Bumper");
			}

			if (GUILayout.Button("Spinner")) {
				var table = Table;
				var spinnerData = new SpinnerData(table.GetNewName<Spinner>("Spinner"), table.Width / 2f, table.Height / 2f);
				var spinner = new Spinner(spinnerData);
				table.Add(spinner, true);
				Selection.activeGameObject = CreateRenderable(table, spinner);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Spinner");
			}

			if (GUILayout.Button("Trigger")) {
				var table = Table;
				var triggerData = new TriggerData(table.GetNewName<Trigger>("Trigger"), table.Width / 2f, table.Height / 2f)
				{
					DragPoints = new[] {
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f + 50f),
						new DragPointData(table.Width / 2f + 50f, table.Height / 2f + 50f),
						new DragPointData(table.Width / 2f + 50f, table.Height / 2f - 50f)
					}
				};
				var trigger = new Trigger(triggerData);
				table.Add(trigger, true);
				Selection.activeGameObject = CreateRenderable(table, trigger);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Trigger");
			}

			if (GUILayout.Button("Kicker")) {
				var table = Table;
				var kickerData = new KickerData(table.GetNewName<Kicker>("Kicker"), table.Width / 2f, table.Height / 2f);
				var kicker = new Kicker(kickerData);
				table.Add(kicker, true);
				Selection.activeGameObject = CreateRenderable(table, kicker);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Kicker");
			}

			if (GUILayout.Button("Target")) {
				var table = Table;
				var hitTargetData = new HitTargetData(table.GetNewName<HitTarget>("Target"), table.Width / 2f, table.Height / 2f);
				var hitTarget = new HitTarget(hitTargetData);
				table.Add(hitTarget, true);
				Selection.activeGameObject = CreateRenderable(table, hitTarget);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Target");
			}

			if (GUILayout.Button("Rubber")) {
				var table = Table;
				var rubberData = new RubberData(table.GetNewName<Rubber>("Rubber")) {
					DragPoints = new[] {
						new DragPointData(table.Width / 2f, table.Height / 2f - 50f) {IsSmooth = true },
						new DragPointData(table.Width / 2f - 50f * Mathf.Cos(Mathf.PI / 4), table.Height / 2f - 50f * Mathf.Sin(Mathf.PI / 4)) {IsSmooth = true },
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f) {IsSmooth = true },
						new DragPointData(table.Width / 2f - 50f * Mathf.Cos(Mathf.PI / 4), table.Height / 2f + 50f * Mathf.Sin(Mathf.PI / 4)) {IsSmooth = true },
						new DragPointData(table.Width / 2f, table.Height / 2f + 50f) {IsSmooth = true },
						new DragPointData(table.Width / 2f + 50f * Mathf.Cos(Mathf.PI / 4), table.Height / 2f + 50f * Mathf.Sin(Mathf.PI / 4)) {IsSmooth = true },
						new DragPointData(table.Width / 2f + 50f, table.Height / 2f) {IsSmooth = true },
						new DragPointData(table.Width / 2f + 50f * Mathf.Cos(Mathf.PI / 4), table.Height / 2f - 50f * Mathf.Sin(Mathf.PI / 4)) {IsSmooth = true },
					}
				};
				var rubber = new Rubber(rubberData);
				table.Add(rubber, true);
				Selection.activeGameObject = CreateRenderable(table, rubber);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Rubber");
			}
		}

		private static GameObject CreateRenderable(Table table, IRenderable renderable)
		{
			var tb = TableBehavior;
			var rog = renderable.GetRenderObjects(table, Origin.Original, false);
			return VpxConverter.ConvertRenderObjects(renderable, rog, GetOrCreateParent(tb, rog), tb);
		}

		private static GameObject GetOrCreateParent(Component tb, RenderObjectGroup rog)
		{
			var parent = tb.gameObject.transform.Find(rog.Parent)?.gameObject;
			if (parent == null) {
				parent = new GameObject(rog.Parent);
				parent.transform.parent = tb.gameObject.transform;
				parent.transform.localPosition = Vector3.zero;
				parent.transform.localRotation = Quaternion.identity;
				parent.transform.localScale = Vector3.one;
			}

			return parent;
		}
	}
}
