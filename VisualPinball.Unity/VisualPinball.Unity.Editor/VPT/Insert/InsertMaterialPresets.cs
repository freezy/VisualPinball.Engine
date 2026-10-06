// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Finds, saves and links the look presets: how an insert is moulded and
	/// behaves optically, every numeric setting of the hybrid body but Max
	/// Brightness, which is the colour's, and the virtual lamps, which are the
	/// shape's. A body material follows the look it is linked to, as it
	/// follows its colour preset, and the link is a tag in the material.
	/// </summary>
	public static class InsertMaterialPresets
	{
		private const string LuminosityProperty = "_InsertLuminosity";
		private const string LinkTag = "VPEInsertLookPreset";
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

		/// <summary>Whether the material is a hybrid body a look can be linked to.</summary>
		public static bool CanLink(Material material) => material != null && material.HasProperty(LuminosityProperty);

		/// <summary>The look the material follows, or null.</summary>
		public static InsertMaterialPreset LinkedPreset(Material material)
		{
			var guid = material != null ? material.GetTag(LinkTag, false) : null;
			return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<InsertMaterialPreset>(AssetDatabase.GUIDToAssetPath(guid));
		}

		/// <summary>How many of its look's values the material has changed; zero when it follows none.</summary>
		public static int Edited(Material material)
		{
			var preset = LinkedPreset(material);
			return preset != null ? preset.Edited(material) : 0;
		}

		/// <summary>
		/// Links the insert's body material to the look, undoably, taking its
		/// values at once, or unlinks it when the preset is null. Returns how
		/// many values were set.
		/// </summary>
		public static int Apply(InsertMaterialPreset preset, InsertComponent insert)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			return CanLink(body) ? Link(body, preset) : 0;
		}

		/// <summary>
		/// Links the material to the look, undoably, and takes its values at
		/// once, or unlinks it when the preset is null, keeping the values it
		/// has. The material is saved, since that's where the link is looked
		/// for. Returns how many values were set.
		/// </summary>
		public static int Link(Material material, InsertMaterialPreset preset)
		{
			Undo.RecordObject(material, preset != null ? "Link Insert Look Preset" : "Unlink Insert Look Preset");
			material.SetOverrideTag(LinkTag, preset != null ? Guid(preset) : string.Empty);
			var count = preset != null ? preset.Apply(material, null) : 0;
			// A body linked to a color preset keeps that preset's Max Brightness.
			InsertColorPresets.Refresh(material);
			EditorUtility.SetDirty(material);
			AssetDatabase.SaveAssetIfDirty(material);
			return count;
		}

		/// <summary>
		/// Links the materials to the look, or unlinks them for null. Linking
		/// takes the preset's values, so when <paramref name="mayCopy"/> and a
		/// material has values of its own, the user is asked whether to copy
		/// them into the preset first, which then passes them on to every
		/// material linked to it. Returns false when the user cancelled.
		/// </summary>
		public static bool LinkAsking(IReadOnlyList<Material> materials, InsertMaterialPreset preset, bool mayCopy, string whose)
		{
			if (preset != null && mayCopy && materials.Count == 1 && preset.Edited(materials[0]) > 0) {
				var canCopy = !InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(preset));
				var message = $"{whose} look differs from {preset.name}'s in {preset.Edited(materials[0])} values. Linking gives it the preset's";
				// 0: the preset's, 1: cancel, 2: copy them to the preset.
				var choice = canCopy
					? EditorUtility.DisplayDialogComplex("Link Insert Look Preset", message + ", unless its own are copied to the preset first, which then passes them on to every material linked to it.",
						$"Use {preset.name}'s", "Cancel", $"Copy to {preset.name}")
					: EditorUtility.DisplayDialog("Link Insert Look Preset", message + ". The preset can't be written to, so its values can't be replaced.", $"Use {preset.name}'s", "Cancel") ? 0 : 1;
				if (choice == 1) {
					return false;
				}
				if (choice == 2) {
					Capture(preset, materials[0]);
				}
			}
			foreach (var material in materials) {
				Link(material, preset);
			}
			return true;
		}

		/// <summary>Gives the material its look's values again, if it follows one.</summary>
		public static void Refresh(Material material)
		{
			var preset = LinkedPreset(material);
			if (preset != null && preset.Edited(material) > 0) {
				preset.Apply(material, null);
				EditorUtility.SetDirty(material);
			}
		}

		/// <summary>
		/// Every material linked to the look. The link is a tag in the material
		/// file, so the files are searched for it before anything is loaded.
		/// </summary>
		public static List<Material> LinkedMaterials(InsertMaterialPreset preset)
		{
			var linked = new List<Material>();
			var guid = Guid(preset);
			if (string.IsNullOrEmpty(guid)) {
				return linked;
			}
			foreach (var path in AssetDatabase.FindAssets("t:Material").Select(AssetDatabase.GUIDToAssetPath).Distinct()) {
				if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				string text;
				try {
					text = File.ReadAllText(FileUtil.GetPhysicalPath(path));
				} catch (IOException) {
					continue;
				}
				if (text.IndexOf(LinkTag, StringComparison.Ordinal) < 0 || text.IndexOf(guid, StringComparison.Ordinal) < 0) {
					continue;
				}
				var material = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (material != null && material.GetTag(LinkTag, false) == guid) {
					linked.Add(material);
				}
			}
			// What's on disk may be behind: an undone unlink restores the link in
			// memory only, until the material is saved.
			foreach (var material in Resources.FindObjectsOfTypeAll<Material>()) {
				if (EditorUtility.IsDirty(material) && EditorUtility.IsPersistent(material) && material.GetTag(LinkTag, false) == guid && !linked.Contains(material)) {
					linked.Add(material);
				}
			}
			return linked;
		}

		/// <summary>
		/// Writes the look into the materials linked to it that can be written
		/// to, and returns how many changed. Materials in a read-only package
		/// are left as they are. An undoable change goes with the preset's
		/// own, to be saved with it; otherwise the materials are saved at once.
		/// </summary>
		public static int Sync(InsertMaterialPreset preset, IEnumerable<Material> linked, bool undoable)
		{
			var changed = 0;
			foreach (var material in linked) {
				if (material == null || InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(material)) || preset.Edited(material) == 0) {
					continue;
				}
				if (undoable) {
					Undo.RecordObject(material, "Change Insert Look Preset");
				}
				preset.Apply(material, null);
				InsertColorPresets.Refresh(material);
				EditorUtility.SetDirty(material);
				if (!undoable) {
					AssetDatabase.SaveAssetIfDirty(material);
				}
				changed++;
			}
			return changed;
		}

		/// <summary>
		/// Takes the body's look into the preset, undoably, and passes it on to
		/// every material linked to it.
		/// </summary>
		public static void Capture(InsertMaterialPreset preset, Material body)
		{
			Undo.RecordObject(preset, "Copy Insert Look to Preset");
			preset.Capture(body, null);
			EditorUtility.SetDirty(preset);
			Sync(preset, LinkedMaterials(preset), true);
		}

		private static string Guid(Object asset)
			=> AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long _) ? guid : null;
	}
}
