// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Finds the lamp presets and puts their bulb onto insert lights.
	/// </summary>
	public static class InsertLampPresets
	{
		private static InsertLampPreset[] _presets;

		static InsertLampPresets()
		{
			EditorApplication.projectChanged += () => _presets = null;
		}

		public static InsertLampPreset[] Find()
		{
			return _presets ??= AssetDatabase.FindAssets("t:" + nameof(InsertLampPreset))
				.Select(guid => AssetDatabase.LoadAssetAtPath<InsertLampPreset>(AssetDatabase.GUIDToAssetPath(guid)))
				.Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.LampName))
				.OrderBy(preset => preset.Order)
				.ThenBy(preset => preset.LampName)
				.ToArray();
		}

		/// <summary>The preset the light follows, or null when it follows none or its preset is gone.</summary>
		public static InsertLampPreset Followed(InsertLightComponent light)
			=> string.IsNullOrEmpty(light.Lamp) ? null : Find().FirstOrDefault(preset => preset.LampName == light.Lamp);

		/// <summary>Whether the light's bulb is not the preset's any more.</summary>
		public static bool Differs(InsertLightComponent light, InsertLampPreset preset)
			=> light.Color != preset.Color
			   || !Mathf.Approximately(light.Output, preset.Output)
			   || !Mathf.Approximately(light.FadeSpeedUp, preset.FadeSpeedUp)
			   || !Mathf.Approximately(light.FadeSpeedDown, preset.FadeSpeedDown)
			   || light.ExtraBulbs != null && light.ExtraBulbs.Any(bulb => bulb != null && bulb.Color != preset.Color);

		/// <summary>The lights that have their preset's bulb, with that preset.</summary>
		public static (InsertLightComponent Light, InsertLampPreset Preset)[] Matching(IEnumerable<InsertLightComponent> lights)
			=> lights.Select(light => (light, Followed(light))).Where(entry => entry.Item2 != null && !Differs(entry.light, entry.Item2)).ToArray();

		/// <summary>
		/// Lets go of the preset on the lights that had its bulb and no longer
		/// do: a bulb edited away from its preset is a custom one, and says so.
		/// A light whose preset changed under it isn't in the list, and keeps
		/// following it until it's reset.
		/// </summary>
		public static void UnfollowEdited(IEnumerable<(InsertLightComponent Light, InsertLampPreset Preset)> matched)
			=> Apply(null, matched.Where(entry => entry.Light != null && Differs(entry.Light, entry.Preset)).Select(entry => entry.Light).ToArray());

		/// <summary>
		/// Gives the lights the preset's bulb, undoably, and has them follow it. All
		/// of a light's bulbs take the colour: they are the same kind of lamp.
		/// A null preset leaves the lights as they are, following nothing.
		/// </summary>
		public static void Apply(InsertLampPreset preset, IReadOnlyList<InsertLightComponent> lights)
		{
			if (lights.Count == 0) {
				return;
			}
			Undo.RecordObjects(lights.Cast<Object>().ToArray(), preset != null ? "Apply Insert Lamp Preset" : "Unfollow Insert Lamp Preset");
			foreach (var light in lights) {
				if (preset != null) {
					// While the game runs, an RGB lamp's colour is the game's: it takes the output and fades only.
					if (!light.IsColorControlledByApi) {
						light.Color = preset.Color;
					}
					light.Output = preset.Output;
					light.FadeSpeedUp = preset.FadeSpeedUp;
					light.FadeSpeedDown = preset.FadeSpeedDown;
					if (!light.AreExtraBulbColorsControlledByApi) {
						foreach (var bulb in light.ExtraBulbs ?? System.Array.Empty<InsertLightComponent.Bulb>()) {
							if (bulb != null) {
								bulb.Color = preset.Color;
							}
						}
					}
				}
				light.Lamp = preset != null ? preset.LampName : string.Empty;
				light.Apply();
				EditorUtility.SetDirty(light);
				PrefabUtility.RecordPrefabInstancePropertyModifications(light);
			}
		}

		/// <summary>
		/// The lamp every insert light of the table follows, or null when they
		/// follow none or not all the same. <paramref name="mixed"/> tells the
		/// two apart.
		/// </summary>
		public static InsertLampPreset TableLamp(TableComponent table, out bool mixed)
		{
			var names = table.GetComponentsInChildren<InsertLightComponent>(true).Select(light => light.Lamp).Distinct().ToArray();
			mixed = names.Length > 1;
			return names.Length == 1 ? Find().FirstOrDefault(preset => preset.LampName == names[0]) : null;
		}

		/// <summary>
		/// Puts the bulb under every insert of the table, and gives the table's
		/// other lamps its fades: a Light under the playfield is the same kind of
		/// bulb, with a colour of its own. An RGB LED is still an LED: it takes the
		/// output and fades, and the game's colour replaces the preset's while it
		/// runs. Flashers keep theirs, being another bulb altogether. Works while
		/// the game runs in the editor, too: fades are
		/// read live, and nothing here pauses the game.
		/// </summary>
		/// <returns>How many insert lights and how many other lamps took the preset</returns>
		public static (int Inserts, int Lamps) ApplyToTable(InsertLampPreset preset, TableComponent table)
		{
			var inserts = table.GetComponentsInChildren<InsertLightComponent>(true);
			Apply(preset, inserts);
			if (preset == null) {
				return (inserts.Length, 0);
			}
			var theirs = new HashSet<ILampDeviceComponent>(table.MappingConfig.Lamps
				.Where(mapping => mapping.Device != null && mapping.IsCoil)
				.Select(mapping => mapping.Device));
			var lamps = table.GetComponentsInChildren<LightComponent>(true).Where(light => !theirs.Contains(light)).ToArray();
			if (lamps.Length > 0) {
				Undo.RecordObjects(lamps.Cast<Object>().ToArray(), "Apply Insert Lamp Preset");
				foreach (var light in lamps) {
					light.FadeSpeedUp = preset.FadeSpeedUp;
					light.FadeSpeedDown = preset.FadeSpeedDown;
					EditorUtility.SetDirty(light);
					PrefabUtility.RecordPrefabInstancePropertyModifications(light);
				}
			}
			return (inserts.Length, lamps.Length);
		}

		/// <summary>Every insert light of the table the light is on, or of its scene without a table.</summary>
		public static InsertLightComponent[] Siblings(InsertLightComponent light)
		{
			var table = light.GetComponentInParent<TableComponent>(true);
			return table != null
				? table.GetComponentsInChildren<InsertLightComponent>(true)
				: light.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InsertLightComponent>(true)).ToArray();
		}
	}
}
