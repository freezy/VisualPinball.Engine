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
			   || !Mathf.Approximately(light.FadeSpeedUp, preset.FadeSpeedUp)
			   || !Mathf.Approximately(light.FadeSpeedDown, preset.FadeSpeedDown)
			   || light.ExtraBulbs != null && light.ExtraBulbs.Any(bulb => bulb != null && bulb.Color != preset.Color);

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
					light.Color = preset.Color;
					light.FadeSpeedUp = preset.FadeSpeedUp;
					light.FadeSpeedDown = preset.FadeSpeedDown;
					foreach (var bulb in light.ExtraBulbs ?? System.Array.Empty<InsertLightComponent.Bulb>()) {
						if (bulb != null) {
							bulb.Color = preset.Color;
						}
					}
				}
				light.Lamp = preset != null ? preset.LampName : string.Empty;
				light.Apply();
				EditorUtility.SetDirty(light);
				PrefabUtility.RecordPrefabInstancePropertyModifications(light);
			}
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
