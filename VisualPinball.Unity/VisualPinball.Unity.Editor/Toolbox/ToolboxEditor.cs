using System;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
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

namespace VisualPinball.Unity.Editor.Toolbox
{
	public class ToolboxEditor : EditorWindow
	{
		private static TableBehavior TableBehavior => FindObjectOfType<TableBehavior>();

		private static Table Table {
			get {
				var tb = TableBehavior;
				return tb == null ? null : tb.Item;
			}
		}

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

			if (TableBehavior == null) {
				GUI.enabled = false;
			}

			if (GUILayout.Button("Wall")) {
				CreateItem(Surface.GetDefault, "New Wall");
			}

			if (GUILayout.Button("Gate")) {
				CreateItem(Gate.GetDefault, "New Gate");
			}

			if (GUILayout.Button("Ramp")) {
				CreateItem(Ramp.GetDefault, "New Ramp");
			}

			if (GUILayout.Button("Flipper")) {
				CreateItem(Flipper.GetDefault, "New Flipper");
			}

			if (GUILayout.Button("Plunger")) {
				CreateItem(Plunger.GetDefault, "New Plunger");
			}

			if (GUILayout.Button("Bumper")) {
				CreateItem(Bumper.GetDefault, "New Bumper");
			}

			if (GUILayout.Button("Spinner")) {
				CreateItem(Spinner.GetDefault, "New Spinner");
			}

			if (GUILayout.Button("Trigger")) {
				CreateItem(Trigger.GetDefault, "New Trigger");
			}

			if (GUILayout.Button("Kicker")) {
				CreateItem(Kicker.GetDefault, "New Kicker");
			}

			if (GUILayout.Button("Target")) {
				CreateItem(HitTarget.GetDefault, "New Target");
			}

			if (GUILayout.Button("Rubber")) {
				CreateItem(Rubber.GetDefault, "New Rubber");
			}

			GUI.enabled = true;
		}

		private static void CreateItem<TItem>(Func<Table, TItem> create, string actionName) where TItem : IItem
		{
			var table = Table;
			var item = create(table);
			table.Add(item, true);
			Selection.activeGameObject = CreateRenderable(table, item as IRenderable);
			Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, actionName);
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
