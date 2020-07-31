using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.VPT.Table;

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
					NextName(table.Surfaces, "Wall"),
					new[] {
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f + 50f),
						new DragPointData(table.Width / 2f + 50f, table.Height / 2f + 50f),
						new DragPointData(table.Width / 2f + 50f, table.Height / 2f - 50f)
					}
				);

				var surface = new Surface(surfaceData);
				table.Surfaces[surface.Name] = surface;
				Selection.activeGameObject = CreateRenderable(table, surface);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Wall");
			}

			if (GUILayout.Button("Gate")) {
				var table = Table;
				var gateData = new GateData(NextName(table.Gates, "Gate"), table.Width / 2f, table.Height / 2f);
				var gate = new Gate(gateData);
				table.Gates[gate.Name] = gate;
				Selection.activeGameObject = CreateRenderable(table, gate);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Gate");
			}

			if (GUILayout.Button("Ramp")) {
				var table = Table;
				var rampData = new RampData(NextName(table.Ramps, "Ramp"), new[] {
					new DragPointData(table.Width / 2f, table.Height / 2f + 200f) { HasAutoTexture = false, IsSmooth = true },
					new DragPointData(table.Width / 2f, table.Height / 2f - 200f) { HasAutoTexture = false, IsSmooth = true }
				}) {
					HeightTop = 50f,
					HeightBottom = 0f,
					WidthTop = 60f,
					WidthBottom = 75f
				};
				var ramp = new Ramp(rampData);
				table.Ramps[ramp.Name] = ramp;
				Selection.activeGameObject = CreateRenderable(table, ramp);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Ramp");
			}

			if (GUILayout.Button("Flipper")) {
				var table = Table;
				var flipperData = new FlipperData(NextName(table.Flippers, "Flipper"), table.Width / 2f, table.Height / 2f);
				var flipper = new Flipper(flipperData);
				table.Flippers[flipper.Name] = flipper;
				Selection.activeGameObject = CreateRenderable(table, flipper);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Flipper");
			}

			if (GUILayout.Button("Plunger")) {
				var table = Table;
				var plungerData = new PlungerData(NextName(table.Plungers, "Plunger"), table.Width / 2f, table.Height / 2f);
				var plunger = new Plunger(plungerData);
				table.Plungers[plunger.Name] = plunger;
				Selection.activeGameObject = CreateRenderable(table, plunger);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Plunger");
			}

			if (GUILayout.Button("Bumper")) {
				var table = Table;
				var bumperData = new BumperData(NextName(table.Bumpers, "Bumper"), table.Width / 2f, table.Height / 2f);
				var bumper = new Bumper(bumperData);
				table.Bumpers[bumper.Name] = bumper;
				Selection.activeGameObject = CreateRenderable(table, bumper);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Bumper");
			}

			if (GUILayout.Button("Spinner")) {
				var table = Table;
				var spinnerData = new SpinnerData(NextName(table.Spinners, "Spinner"), table.Width / 2f, table.Height / 2f);
				var spinner = new Spinner(spinnerData);
				table.Spinners[spinner.Name] = spinner;
				Selection.activeGameObject = CreateRenderable(table, spinner);
				Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, "New Spinner");
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

		private static string NextName<T>(IReadOnlyDictionary<string, T> existingNames, string prefix)
		{
			var n = 0;
			do {
				var elementName = $"{prefix}{++n}";
				if (!existingNames.ContainsKey(elementName)) {
					return elementName;
				}
			} while (true);
		}
	}
}
