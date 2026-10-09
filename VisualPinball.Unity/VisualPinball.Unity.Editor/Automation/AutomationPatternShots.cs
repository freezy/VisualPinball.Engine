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
	/// Shoots the pattern part of the Geometry section once per pattern, and
	/// exports each pattern's preview wireframe as text, for the Patterns page
	/// of the documentation. Every pattern is shown on a copy of the insert
	/// named "Insert, Patterns" in the scene.
	/// </summary>
	public static class AutomationPatternShots
	{
		private const float Width = 340f, Height = 640f;
		private const string Source = "Insert, Patterns";

		private static readonly string[] Patterns = { "none", "starburst", "ribbed", "rings", "gem", "engraved-line", "stippled" };
		private static readonly string[] Sections = { "Geometry", "Bake", "Cover", "Preset" };

		private static string Folder => Automation.Folder("patterns");

		private static InsertComponent SourceInsert => Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(i => i.name == Source);

		private static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

		/// <summary>Lists the source insert's settings into "list.txt".</summary>
		[MenuItem(Automation.Menu + "Patterns/List")]
		public static void List()
		{
			var insert = SourceInsert;
			var sb = new StringBuilder();
			sb.AppendLine($"{insert.name} active {insert.gameObject.activeInHierarchy} bake fresh {insert.IsBakeFresh} showing {insert.IsShowingGeometry}");
			var it = new SerializedObject(insert).GetIterator();
			for (var enter = true; it.NextVisible(enter); enter = false) {
				sb.AppendLine($"{it.propertyPath} = {Value(it)}");
			}
			File.WriteAllText(Path.Combine(Folder, "list.txt"), sb.ToString());
		}

		private static string Value(SerializedProperty p) => p.propertyType switch {
			SerializedPropertyType.Float => F(p.floatValue),
			SerializedPropertyType.Integer => p.intValue.ToString(),
			SerializedPropertyType.Boolean => p.boolValue.ToString(),
			SerializedPropertyType.Enum => p.enumDisplayNames.ElementAtOrDefault(p.enumValueIndex),
			SerializedPropertyType.Vector2 => p.vector2Value.ToString(),
			SerializedPropertyType.ObjectReference => p.objectReferenceValue != null ? p.objectReferenceValue.name : "null",
			_ => p.propertyType.ToString(),
		};

		/// <summary>A plain copy of the source with only the given pattern switched on.</summary>
		private static InsertComponent Copy(string pattern, List<GameObject> temps)
		{
			var source = SourceInsert;
			var go = new GameObject(source.name + " " + pattern);
			go.SetActive(false);
			var copy = go.AddComponent<InsertComponent>();
			EditorUtility.CopySerialized(source, copy);
			go.SetActive(true);
			temps.Add(go);
			var so = new SerializedObject(copy);
			so.FindProperty("_starburst").boolValue = pattern == "starburst";
			so.FindProperty("_ribbed").boolValue = pattern == "ribbed";
			so.FindProperty("_ringed").boolValue = pattern == "rings";
			so.FindProperty("_gem").boolValue = pattern == "gem";
			so.FindProperty("_flashGroove").boolValue = pattern == "engraved-line";
			so.FindProperty("_stippled").boolValue = pattern == "stippled";
			// The engraved line follows a flash or a custom outline only.
			if (pattern == "engraved-line") {
				so.FindProperty("_shape").enumValueIndex = (int)InsertShape.Flash;
			}
			so.ApplyModifiedPropertiesWithoutUndo();
			copy.RebuildMesh();
			return copy;
		}

		private static void Destroy(List<GameObject> temps)
		{
			foreach (var go in temps.Where(go => go != null)) {
				Object.DestroyImmediate(go);
			}
			temps.Clear();
		}

		/// <summary>
		/// Exports each pattern's wireframe from above into "wire-{pattern}.txt":
		/// "o x z" outline points, "t" triangles and "e" preview edges, in insert space.
		/// </summary>
		[MenuItem(Automation.Menu + "Patterns/Export Wireframes")]
		public static void Export()
		{
			var folder = Folder;
			var log = new StringBuilder();
			var temps = new List<GameObject>();
			try {
				foreach (var pattern in Patterns) {
					var insert = Copy(pattern, temps);
					var mesh = new Mesh();
					var layout = new InsertMeshLayout();
					InsertMeshGenerator.Generate(insert.Outline, insert.Body, mesh, layout);
					var sb = new StringBuilder();
					foreach (var p in insert.Outline.Points) {
						sb.AppendLine($"o {F(p.Position.x)} {F(p.Position.y)}");
					}
					var v = mesh.vertices;
					var t = mesh.GetTriangles(0);
					for (var i = 0; i < t.Length; i += 3) {
						sb.AppendLine($"t {F(v[t[i]].x)} {F(v[t[i]].z)} {F(v[t[i + 1]].x)} {F(v[t[i + 1]].z)} {F(v[t[i + 2]].x)} {F(v[t[i + 2]].z)}");
					}
					var e = layout.PreviewEdges ?? new Vector3[0];
					for (var i = 0; i + 1 < e.Length; i += 2) {
						sb.AppendLine($"e {F(e[i].x)} {F(e[i].z)} {F(e[i + 1].x)} {F(e[i + 1].z)}");
					}
					File.WriteAllText(Path.Combine(folder, $"wire-{pattern}.txt"), sb.ToString());
					log.AppendLine($"{pattern}: {t.Length / 3} tris, {e.Length / 2} edges, warnings: {string.Join(" | ", insert.Warnings)} error: {insert.Error}");
					Object.DestroyImmediate(mesh);
				}
			} finally {
				Destroy(temps);
			}
			File.WriteAllText(Path.Combine(folder, "export.txt"), log.ToString());
		}

		/// <summary>
		/// The same wireframes into "wire3d-{pattern}.txt", projected through the
		/// Scene view's camera onto the source insert's place, each backed off in
		/// proportion to its size, so a longer one isn't seen from closer up.
		/// </summary>
		[MenuItem(Automation.Menu + "Patterns/Export Wireframes From Scene View")]
		public static void ExportFromSceneView()
		{
			var folder = Folder;
			var source = SourceInsert;
			var cam = SceneView.lastActiveSceneView.camera;
			var model = source.transform.localToWorldMatrix;
			var aspect = cam.aspect;
			var sourceBounds = source.GetComponent<MeshFilter>().sharedMesh.bounds;
			var sourceCenter = model.MultiplyPoint3x4(sourceBounds.center);
			var ratio = Vector3.Distance(cam.transform.position, sourceCenter) / model.MultiplyVector(sourceBounds.extents).magnitude;
			var projection = Matrix4x4.Perspective(cam.fieldOfView, aspect, 0.0001f, 10f);
			Matrix4x4 vp = default;
			Vector2 P(Vector3 v) { var c = vp * new Vector4(v.x, v.y, v.z, 1f); return new Vector2(c.x / c.w * aspect, c.y / c.w); }
			var log = new StringBuilder($"camera {cam.transform.position} {cam.transform.eulerAngles} ortho {cam.orthographic} fov {cam.fieldOfView} aspect {aspect}\n");
			var temps = new List<GameObject>();
			try {
				foreach (var pattern in Patterns) {
					var insert = Copy(pattern, temps);
					var mesh = new Mesh();
					var layout = new InsertMeshLayout();
					InsertMeshGenerator.Generate(insert.Outline, insert.Body, mesh, layout);
					mesh.RecalculateBounds();
					// The flash turned so its +Z points away from the camera: the bolt as the preview shows it from above, tilted back.
					var m = pattern == "engraved-line" ? model * Matrix4x4.Rotate(Quaternion.Euler(0f, cam.transform.eulerAngles.y - source.transform.eulerAngles.y, 0f)) : model;
					var center = m.MultiplyPoint3x4(mesh.bounds.center);
					var distance = ratio * model.MultiplyVector(sourceBounds.extents).magnitude * (mesh.bounds.extents.magnitude / sourceBounds.extents.magnitude);
					var eye = center - cam.transform.forward * distance;
					var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(eye, cam.transform.rotation, Vector3.one).inverse;
					vp = projection * view * m;
					var sb = new StringBuilder();
					var v = mesh.vertices;
					var t = mesh.GetTriangles(0);
					for (var i = 0; i < t.Length; i += 3) {
						Vector2 a = P(v[t[i]]), b = P(v[t[i + 1]]), c = P(v[t[i + 2]]);
						sb.AppendLine($"t {F(a.x)} {F(a.y)} {F(b.x)} {F(b.y)} {F(c.x)} {F(c.y)}");
					}
					var e = layout.PreviewEdges ?? new Vector3[0];
					for (var i = 0; i + 1 < e.Length; i += 2) {
						Vector2 a = P(e[i]), b = P(e[i + 1]);
						sb.AppendLine($"e {F(a.x)} {F(a.y)} {F(b.x)} {F(b.y)}");
					}
					File.WriteAllText(Path.Combine(folder, $"wire3d-{pattern}.txt"), sb.ToString());
					Object.DestroyImmediate(mesh);
				}
			} finally {
				Destroy(temps);
			}
			File.WriteAllText(Path.Combine(folder, "export3d.txt"), log.ToString());
		}

		/// <summary>
		/// Shoots the Geometry section of each pattern's copy into
		/// "section-{pattern}.png", scrolled down by the points in "offset.txt"
		/// to where the patterns are.
		/// </summary>
		[MenuItem(Automation.Menu + "Patterns/Shoot")]
		public static void Shoot()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			foreach (var key in Sections) {
				SessionState.SetBool("VisualPinball.Insert." + key, key == "Geometry");
			}
			var offset = float.Parse(Automation.Input(folder, "offset.txt"), CultureInfo.InvariantCulture);
			var temps = new List<GameObject>();
			// The flash has five more outline rows than the rectangle.
			var shots = Patterns.Select(pattern => new AutomationShots.Shot($"section-{pattern}", Copy(pattern, temps), Width, Height,
				offset: pattern == "engraved-line" ? offset + 100f : offset)).ToArray();
			AutomationShots.Run(folder, shots, () => {
				Destroy(temps);
				foreach (var key in Sections) {
					SessionState.EraseBool("VisualPinball.Insert." + key);
				}
			});
		}
	}
}
