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
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Shoots an insert's inspector, or its body material's, in the states the
	/// documentation shows: its sections, the top of the component with the
	/// buttons to fork it, and the bake section of a reshaped insert. The
	/// insert is the one named in "target.txt". An asset library it comes from
	/// is locked for the shots, in memory only, and unlocked again afterwards.
	/// </summary>
	public static class AutomationInspectorShots
	{
		private static readonly string[] Sections = { "Geometry", "Bake", "Cover", "Preset" };

		private static string Folder => Automation.Folder("inspector");

		private static IEnumerable<AssetLibrary> Libraries() => AssetDatabase.FindAssets("t:" + nameof(AssetLibrary))
			.Select(guid => AssetDatabase.LoadAssetAtPath<AssetLibrary>(AssetDatabase.GUIDToAssetPath(guid)))
			.Where(library => library != null);

		private static AssetLibrary LibraryOf(string path) => Libraries()
			.FirstOrDefault(library => !string.IsNullOrEmpty(library.LibraryRoot) && path.StartsWith(library.LibraryRoot.TrimEnd('/') + "/"));

		private static InsertComponent Target(string folder)
		{
			var name = Automation.Input(folder, "target.txt");
			return Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include).First(insert => insert.name == name);
		}

		/// <summary>Lists the asset libraries and the inserts in the scene, with where their body materials come from, into "list.txt".</summary>
		[MenuItem(Automation.Menu + "Inspector/List")]
		public static void List()
		{
			var sb = new StringBuilder();
			foreach (var library in Libraries()) {
				sb.AppendLine($"library {library.name} root {library.LibraryRoot} locked {library.IsLocked}");
			}
			foreach (var insert in Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include).OrderBy(i => i.name)) {
				var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
				var path = body != null ? AssetDatabase.GetAssetPath(body) : "";
				sb.AppendLine($"{insert.name} | {path} | library {LibraryOf(path)?.name} | wearing {InsertColorPresets.Wearing(body).Length} | prefab {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(insert.gameObject)} | needs editing {InsertBaker.NeedsEditing(insert)}");
			}
			File.WriteAllText(Path.Combine(Folder, "list.txt"), sb.ToString());
		}

		/// <summary>The Presets section into "insert-presets.png", and the body material into "material.png".</summary>
		[MenuItem(Automation.Menu + "Inspector/Shoot Presets")]
		public static void ShootPresets()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Target(folder);
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			Run(folder, insert, "Preset", new[] {
				new AutomationShots.Shot("insert-presets", insert, 340f, 640f, 1f),
				new AutomationShots.Shot("material", body, 560f, 520f, 1f),
			});
		}

		/// <summary>The Normal Bake section into "insert-bake.png".</summary>
		[MenuItem(Automation.Menu + "Inspector/Shoot Bake")]
		public static void ShootBake()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Target(folder);
			Run(folder, insert, "Bake", new[] { new AutomationShots.Shot("insert-bake", insert, 340f, 900f, 1f) });
		}

		/// <summary>
		/// The Normal Bake section of the insert reshaped, at three times the
		/// size, into "insert-bake-reshaped.png". The insert is made shallower
		/// for the shot and put back afterwards.
		/// </summary>
		[MenuItem(Automation.Menu + "Inspector/Shoot Bake Reshaped")]
		public static void ShootBakeReshaped()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Target(folder);
			var restore = Reshape(insert);
			Run(folder, insert, "Bake", new[] { new AutomationShots.Shot("insert-bake-reshaped", insert, 300f, 540f, 2f, 24f) }, restore);
		}

		/// <summary>
		/// The top of the component down to the end of the Normal Bake section
		/// of the insert reshaped, at three times the size, into
		/// "insert-bake-reshaped-tall.png": two windows, scrolled and stitched,
		/// as the screen is shorter than that.
		/// </summary>
		[MenuItem(Automation.Menu + "Inspector/Shoot Bake Reshaped Tall")]
		public static void ShootBakeReshapedTall()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			const float scroll = 400f;
			var folder = Folder;
			var insert = Target(folder);
			var restore = Reshape(insert);
			Run(folder, insert, "Bake", new[] {
				new AutomationShots.Shot("insert-bake-reshaped-a", insert, 300f, 540f, 2f),
				new AutomationShots.Shot("insert-bake-reshaped-b", insert, 300f, 540f, 2f, scroll),
			}, () => {
				restore();
				Stitch(folder, "insert-bake-reshaped-a", "insert-bake-reshaped-b", scroll * 2f * EditorGUIUtility.pixelsPerPoint, "insert-bake-reshaped-tall");
			});
		}

		/// <summary>The top of the component, title bar included, with every section folded, into "insert-top.png".</summary>
		[MenuItem(Automation.Menu + "Inspector/Shoot Top")]
		public static void ShootTop()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Target(folder);
			Run(folder, insert, null, new[] { new AutomationShots.Shot("insert-top", insert, 340f, 230f, 2f, titlebar: true) });
		}

		/// <summary>
		/// The Plastic part of the body material into "material-plastic.png",
		/// with nothing selected, so a library material offers to select the
		/// inserts wearing it.
		/// </summary>
		[MenuItem(Automation.Menu + "Inspector/Shoot Material")]
		public static void ShootMaterial()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Target(folder);
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			Run(folder, insert, null, new[] { new AutomationShots.Shot("material-plastic", body, 560f, 180f, 1f, 186f) }, select: false);
		}

		/// <summary>
		/// Takes the shots with only the given section of the insert unfolded,
		/// its body material unfolded, the insert's asset library locked and
		/// the insert selected, or nothing; puts all of it back afterwards.
		/// </summary>
		private static void Run(string folder, InsertComponent insert, string section, IEnumerable<AutomationShots.Shot> shots, Action after = null, bool select = true)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			var library = body != null ? LibraryOf(AssetDatabase.GetAssetPath(body)) : null;
			var locked = library != null && !library.IsLocked ? library : null;
			if (locked != null) {
				locked.IsLocked = true;
			}
			InsertBaker.ForgetLibraries();
			foreach (var key in Sections) {
				SessionState.SetBool("VisualPinball.Insert." + key, key == section);
			}
			if (body != null) {
				InternalEditorUtility.SetIsInspectorExpanded(body, true);
			}
			var selection = Selection.objects;
			Selection.objects = select ? new Object[] { insert.gameObject } : new Object[0];
			AutomationShots.Run(folder, shots, () => {
				if (locked != null) {
					locked.IsLocked = false;
				}
				InsertBaker.ForgetLibraries();
				foreach (var key in Sections) {
					SessionState.EraseBool("VisualPinball.Insert." + key);
				}
				Selection.objects = selection;
				after?.Invoke();
			});
		}

		/// <summary>Makes the insert shallower, so its bake no longer fits, and returns what puts it back.</summary>
		private static Action Reshape(InsertComponent insert)
		{
			var serialized = new SerializedObject(insert);
			var depth = serialized.FindProperty("_depth");
			var overridden = depth.prefabOverride;
			var before = depth.floatValue;
			depth.floatValue = before * 0.75f;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			insert.RebuildMesh();
			return () => {
				var back = new SerializedObject(insert);
				var property = back.FindProperty("_depth");
				if (overridden || !PrefabUtility.IsPartOfPrefabInstance(insert)) {
					property.floatValue = before;
					back.ApplyModifiedPropertiesWithoutUndo();
				} else {
					PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
				}
				insert.RebuildMesh();
			};
		}

		/// <summary>Writes the top of the first shot, down to where the second one starts, above the second one.</summary>
		private static void Stitch(string folder, string first, string second, float scrolled, string result)
		{
			var top = new Texture2D(2, 2);
			var bottom = new Texture2D(2, 2);
			try {
				top.LoadImage(File.ReadAllBytes(Path.Combine(folder, first + ".png")));
				bottom.LoadImage(File.ReadAllBytes(Path.Combine(folder, second + ".png")));
				var cut = Mathf.Min(Mathf.RoundToInt(scrolled), top.height);
				var width = top.width;
				var tall = new Texture2D(width, cut + bottom.height, TextureFormat.RGB24, false);
				try {
					// Textures count rows from the bottom.
					tall.SetPixels(0, 0, width, bottom.height, bottom.GetPixels(0, 0, width, bottom.height));
					tall.SetPixels(0, bottom.height, width, cut, top.GetPixels(0, top.height - cut, width, cut));
					tall.Apply();
					File.WriteAllBytes(Path.Combine(folder, result + ".png"), tall.EncodeToPNG());
				} finally {
					Object.DestroyImmediate(tall);
				}
			} finally {
				Object.DestroyImmediate(top);
				Object.DestroyImmediate(bottom);
			}
		}
	}
}
