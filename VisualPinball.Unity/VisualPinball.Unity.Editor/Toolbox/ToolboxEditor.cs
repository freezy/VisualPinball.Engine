using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
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
					var table = new Table(new TableData { Name = tableName});
					var converter = rootGameObj.AddComponent<VpxConverter>();
					converter.Convert(tableName, table);
					DestroyImmediate(converter);

				} else {
					EditorUtility.DisplayDialog("Visual Pinball", "Sorry, cannot add multiple tables, and there already is " +
					                            existingTable.name, "Close");
				}
			}

			if (GUILayout.Button("Wall")) {
				var table = Table;
				var tb = TableBehavior;
				var surfaceData = new SurfaceData {
					Name = NextName(table.Surfaces, "Wall"),
					DragPoints = new [] {
						new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
						new DragPointData( table.Width / 2f - 50f, table.Height / 2f + 50f),
						new DragPointData( table.Width / 2f + 50f, table.Height / 2f + 50f),
						new DragPointData( table.Width / 2f + 50f, table.Height / 2f - 50f)
					}
				};

				var surface = new Surface(surfaceData);
				table.Surfaces[surface.Name] = surface;
				Selection.activeGameObject = CreateRenderable(table, tb, surface);
			}
		}

		private static GameObject CreateRenderable(Table table, TableBehavior tb, IRenderable renderable)
		{
			var rog = renderable.GetRenderObjects(table, Origin.Original, true);
			return VpxConverter.ConvertRenderObjects(renderable, rog, GetOrCreateParent(tb, rog), tb);
		}

		private static GameObject GetOrCreateParent(Component tb, RenderObjectGroup rog)
		{
			var parent = tb.gameObject.transform.Find(rog.Parent)?.gameObject;
			if (parent == null) {
				parent = new GameObject(rog.Parent);
				parent.transform.parent = tb.gameObject.transform;
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
