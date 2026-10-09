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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Shoots the Geometry section of an insert per shape, for the Geometry
	/// page of the documentation. The inserts come from the "Insert Showcase"
	/// in the scene, or from anywhere in it, as listed in "picks.txt".
	/// </summary>
	public static class AutomationGeometryShots
	{
		private const float Width = 340f, Height = 640f;
		private const string Showcase = "Insert Showcase";

		private static readonly string[] Sections = { "Geometry", "Bake", "Cover", "Preset" };

		private static string Folder => Automation.Folder("geometry");

		private static IEnumerable<InsertComponent> All => Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);

		private static InsertShape ShapeOf(InsertComponent insert) => (InsertShape)new SerializedObject(insert).FindProperty("_shape").enumValueIndex;

		/// <summary>Lists the inserts in the scene with their shapes, into "list.txt", to pick from.</summary>
		[MenuItem(Automation.Menu + "Geometry/List")]
		public static void List()
		{
			var sb = new StringBuilder();
			foreach (var insert in All.OrderBy(i => ShapeOf(i).ToString())) {
				var parent = insert.transform.parent != null ? insert.transform.parent.name + "/" : "";
				sb.AppendLine($"{ShapeOf(insert)} | {parent}{insert.name} | active {insert.gameObject.activeInHierarchy}");
			}
			File.WriteAllText(Path.Combine(Folder, "list.txt"), sb.ToString());
		}

		/// <summary>
		/// Shoots the inserts listed in "picks.txt", one per line as
		/// "file | insert name | offset", the offset in points being optional.
		/// "temp banana" stands for a default banana, which the showcase lacks.
		/// </summary>
		[MenuItem(Automation.Menu + "Geometry/Shoot")]
		public static void Shoot()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			foreach (var key in Sections) {
				SessionState.SetBool("VisualPinball.Insert." + key, key == "Geometry");
			}
			var root = GameObject.Find(Showcase);
			var showcase = root != null ? root.GetComponentsInChildren<InsertComponent>(true) : new InsertComponent[0];
			var picks = File.ReadAllLines(Path.Combine(folder, "picks.txt")).Where(line => line.Contains('|')).Select(line => line.Split('|')).ToArray();
			var temps = new List<GameObject>();
			var shots = new List<AutomationShots.Shot>();
			foreach (var pick in picks) {
				var name = pick[1].Trim();
				var source = showcase.FirstOrDefault(i => i.name == name) ?? All.FirstOrDefault(i => i.name == name);
				var insert = source != null ? PlainCopy(source, temps) : name == "temp banana" ? Banana(temps) : null;
				if (insert == null) {
					continue;
				}
				RoundHoles(insert);
				var offset = pick.Length > 2 ? float.Parse(pick[2].Trim(), CultureInfo.InvariantCulture) : 0f;
				shots.Add(new AutomationShots.Shot(pick[0].Trim(), insert, Width, Height, offset: offset));
			}
			AutomationShots.Run(folder, shots, () => {
				foreach (var temp in temps.Where(temp => temp != null)) {
					Object.DestroyImmediate(temp);
				}
				foreach (var key in Sections) {
					SessionState.EraseBool("VisualPinball.Insert." + key);
				}
			});
		}

		/// <summary>A plain copy of the insert: a prefab instance's overrides would show as bold labels and blue bars.</summary>
		private static InsertComponent PlainCopy(InsertComponent source, List<GameObject> temps)
		{
			var go = new GameObject(source.name);
			go.SetActive(false);
			var copy = go.AddComponent<InsertComponent>();
			EditorUtility.CopySerialized(source, copy);
			go.SetActive(true);
			temps.Add(go);
			return copy;
		}

		/// <summary>A default banana with a starburst.</summary>
		private static InsertComponent Banana(List<GameObject> temps)
		{
			var go = new GameObject("Insert, Banana");
			temps.Add(go);
			var insert = go.AddComponent<InsertComponent>();
			var so = new SerializedObject(insert);
			so.FindProperty("_shape").enumValueIndex = (int)InsertShape.Banana;
			so.FindProperty("_starburst").boolValue = true;
			so.FindProperty("_starburstBursts").intValue = 52;
			so.FindProperty("_starburstCenter").vector2Value = new Vector2(0f, 21.85f);
			so.FindProperty("_starburstOffset").floatValue = 1.33f;
			so.ApplyModifiedPropertiesWithoutUndo();
			return insert;
		}

		/// <summary>Rounds the holes to two decimals, which fit the narrow fields, and unfolds them.</summary>
		private static void RoundHoles(InsertComponent insert)
		{
			var so = new SerializedObject(insert);
			var holes = so.FindProperty("_holes");
			holes.isExpanded = true;
			for (var i = 0; i < holes.arraySize; i++) {
				var hole = holes.GetArrayElementAtIndex(i);
				var center = hole.FindPropertyRelative("Center");
				var radius = hole.FindPropertyRelative("Radius");
				center.vector2Value = new Vector2(Mathf.Round(center.vector2Value.x * 100f) / 100f, Mathf.Round(center.vector2Value.y * 100f) / 100f);
				radius.floatValue = Mathf.Round(radius.floatValue * 100f) / 100f;
			}
			so.ApplyModifiedPropertiesWithoutUndo();
			insert.RebuildMesh();
		}
	}
}
