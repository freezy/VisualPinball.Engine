// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Finds, saves and applies insert material presets: numeric settings
	/// for the hybrid body, without changing colors, maps, or the cover.
	/// </summary>
	public static class InsertMaterialPresets
	{
		private const string LuminosityProperty = "_InsertLuminosity";
		private static InsertMaterialPreset[] _presets;

		static InsertMaterialPresets()
		{
			EditorApplication.projectChanged += () => _presets = null;
		}

		/// <summary>Every preset in the project and its packages, by name.</summary>
		public static InsertMaterialPreset[] Find()
		{
			return _presets ??= AssetDatabase.FindAssets("t:" + nameof(InsertMaterialPreset))
				.Select(guid => AssetDatabase.LoadAssetAtPath<InsertMaterialPreset>(AssetDatabase.GUIDToAssetPath(guid)))
				.Where(preset => preset != null)
				.OrderBy(preset => preset.name)
				.ToArray();
		}

		/// <summary>Whether the insert wears a material a preset can be put on.</summary>
		public static bool HasStyledMaterial(InsertComponent insert)
		{
			var material = insert.GetComponent<MeshRenderer>().sharedMaterial;
			return material != null && material.HasProperty(LuminosityProperty);
		}

		/// <summary>
		/// Applies the preset to the insert's body material, undoably, and
		/// returns how many values were set.
		/// </summary>
		public static int Apply(InsertMaterialPreset preset, InsertComponent insert)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			if (body == null || !body.HasProperty(LuminosityProperty)) {
				return 0;
			}
			Undo.RecordObject(body, "Apply Insert Material Preset");
			var count = preset.Apply(body, null);
			EditorUtility.SetDirty(body);
			return count;
		}

		/// <summary>
		/// Saves the insert's current body settings as a new preset, asking where, and
		/// returns it, or null when the user cancelled.
		/// </summary>
		public static InsertMaterialPreset SaveAs(InsertComponent insert)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			if (body == null) {
				return null;
			}
			var path = EditorUtility.SaveFilePanelInProject("Save Insert Material Preset", body.name + " Preset", "asset",
				"Where to save this insert's numeric body settings.");
			if (string.IsNullOrEmpty(path)) {
				return null;
			}
			return Save(body, null, path);
		}

		/// <summary>Saves the body's numeric settings as a preset asset at that path.</summary>
		public static InsertMaterialPreset Save(Material body, Material cover, string path)
		{
			var preset = ScriptableObject.CreateInstance<InsertMaterialPreset>();
			preset.Capture(body, cover);
			var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
			if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder)) {
				Directory.CreateDirectory(folder);
				AssetDatabase.Refresh();
			}
			AssetDatabase.CreateAsset(preset, path);
			_presets = null;
			AssetDatabase.SaveAssets();
			return AssetDatabase.LoadAssetAtPath<InsertMaterialPreset>(path);
		}

		/// <summary>The names a popup shows for the presets, with a line for none.</summary>
		public static string[] Labels(IReadOnlyList<InsertMaterialPreset> presets)
		{
			var labels = new string[presets.Count + 1];
			labels[0] = "Choose a preset";
			for (var i = 0; i < presets.Count; i++) {
				labels[i + 1] = presets[i].name;
			}
			return labels;
		}
	}
}
