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
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Shoots where the insert presets are set, for the Presets page of the
	/// documentation: the Insert and Insert Light components and the body
	/// material of the insert whose name contains what "target.txt" says, the
	/// three tabs of the Insert Presets window, and the Lamp Manager of the
	/// insert's table. Also shoots the toolbox.
	/// </summary>
	public static class AutomationPresetShots
	{
		private static readonly string[] InsertSections = { "Geometry", "Bake", "Cover", "Preset" };
		private static readonly string[] LightSections = { "Lamp", "State", "Spill" };

		private static string Folder => Automation.Folder("presets");

		[MenuItem(Automation.Menu + "Presets/Shoot")]
		public static void Shoot()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var name = Automation.Input(folder, "target.txt");
			var insert = Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(i => i.name.Contains(name));
			var light = insert.GetComponent<InsertLightComponent>();
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			var table = insert.GetComponentInParent<TableComponent>();
			foreach (var key in InsertSections) {
				SessionState.SetBool("VisualPinball.Insert." + key, key == "Preset");
			}
			foreach (var key in LightSections) {
				SessionState.SetBool("VisualPinball.InsertLight." + key, key == "Lamp");
			}
			// One value of the material is edited for its shot, so it shows the mark of an edited value, and put back after it.
			var smoothness = body.GetFloat("_Smoothness");
			var smoothnessInherited = body.parent != null && !body.IsPropertyOverriden("_Smoothness");
			var undoGroup = 0;
			AutomationShots.Run(folder, new[] {
				// Unscaled: the preset buttons wrap against the window's width.
				new AutomationShots.Shot("inspector-insert", insert, 280f, 360f, 1f),
				new AutomationShots.Shot("inspector-light", light, 560f, 230f),
				AutomationShots.Shot.Then(() => body.SetFloat("_Smoothness", smoothness + 0.1f)),
				// HDRP's material blocks lay out against the window's width, not the scaled area: unscaled, at the display's own scale.
				new AutomationShots.Shot("inspector-material", body, 560f, 415f, 1f),
				AutomationShots.Shot.Then(() => {
					// A variant that followed its parent keeps following it.
					if (smoothnessInherited) {
						body.RevertPropertyOverride("_Smoothness");
					} else {
						body.SetFloat("_Smoothness", smoothness);
					}
				}),
				AutomationShots.Shot.Window("inspector-preset-window-lamp", () => PresetWindow(InsertPresetWindow.Kind.Lamp)),
				AutomationShots.Shot.Window("inspector-preset-window-color", () => PresetWindow(InsertPresetWindow.Kind.Color)),
				AutomationShots.Shot.Window("inspector-preset-window-look", () => PresetWindow(InsertPresetWindow.Kind.Look)),
				// The table's inserts without a lamp preset follow Incandescent for the shot, undone after it.
				AutomationShots.Shot.Then(() => undoGroup = FollowIncandescent()),
				AutomationShots.Shot.Window("inspector-lamp-manager", () => LampManagerWindow(table)),
				AutomationShots.Shot.Then(() => Undo.RevertAllDownToGroup(undoGroup)),
			}, () => {
				foreach (var key in InsertSections) {
					SessionState.EraseBool("VisualPinball.Insert." + key);
				}
				foreach (var key in LightSections) {
					SessionState.EraseBool("VisualPinball.InsertLight." + key);
				}
			});
		}

		/// <summary>The toolbox of the table in the scene into "toolbox.png".</summary>
		[MenuItem(Automation.Menu + "Toolbox/Shoot")]
		public static void ShootToolbox()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			AutomationShots.Run(Automation.Folder("toolbox"), new[] {
				AutomationShots.Shot.Window("toolbox", () => {
					var toolbox = ScriptableObject.CreateInstance<ToolboxEditor>();
					var host = AutomationShotWindow.Host(toolbox, "Toolbox", 220f, 1200f);
					// Out of sight, the window never gets the focus it finds its table on.
					WithTable(toolbox, Object.FindFirstObjectByType<TableComponent>());
					return host;
				}),
			});
		}

		/// <summary>Gives a table window the table, which it keeps in a field that SetTable reads.</summary>
		private static void WithTable(LockingTableEditorWindow window, TableComponent table)
		{
			if (table == null) {
				Debug.LogWarning("Automation: there is no table in the open scenes.");
				return;
			}
			typeof(LockingTableEditorWindow).GetField("TableComponent", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(window, table);
			typeof(LockingTableEditorWindow).GetMethod("SetTable", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(window, new object[] { table });
		}

		private static EditorWindow PresetWindow(InsertPresetWindow.Kind kind)
		{
			var window = ScriptableObject.CreateInstance<InsertPresetWindow>();
			typeof(InsertPresetWindow).GetField("_kind", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(window, kind);
			return AutomationShotWindow.Host(window, "Insert Presets", 720f, 420f);
		}

		/// <summary>The Lamp Manager of the table, with its lamp settings open, wide enough for the toolbar and the settings row.</summary>
		private static EditorWindow LampManagerWindow(TableComponent table)
		{
			typeof(LampManager).GetField("_lampSettings", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
			var window = ScriptableObject.CreateInstance<LampManager>();
			var host = AutomationShotWindow.Host(window, "Lamp Manager", 1050f, 150f);
			WithTable(window, table);
			return host;
		}

		/// <summary>Gives every insert light without a lamp preset the incandescent one, as one undo group, and returns it.</summary>
		private static int FollowIncandescent()
		{
			Undo.IncrementCurrentGroup();
			var group = Undo.GetCurrentGroup();
			var incandescent = InsertLampPresets.Find().First(preset => preset.LampName == "Incandescent");
			var loose = Object.FindObjectsByType<InsertLightComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				.Where(light => light.Lamp != incandescent.LampName)
				.ToArray();
			InsertLampPresets.Apply(incandescent, loose);
			return group;
		}
	}
}
