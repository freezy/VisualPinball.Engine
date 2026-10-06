// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>Finds or makes a body and cover material in a chosen insert color.</summary>
	public static class InsertColorPresets
	{
		private static InsertColorPreset[] _presets;

		static InsertColorPresets()
		{
			EditorApplication.projectChanged += () => _presets = null;
		}

		public static InsertColorPreset[] Find()
		{
			return _presets ??= AssetDatabase.FindAssets("t:" + nameof(InsertColorPreset))
				.Select(guid => AssetDatabase.LoadAssetAtPath<InsertColorPreset>(AssetDatabase.GUIDToAssetPath(guid)))
				.Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.ColorName))
				.OrderBy(preset => preset.Order)
				.ThenBy(preset => preset.ColorName)
				.ToArray();
		}

		public static bool CanApply(InsertComponent insert)
		{
			var renderer = insert.GetComponent<MeshRenderer>();
			return renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_InsertLuminosity")
				&& renderer.sharedMaterial.HasProperty("_BaseColor")
				&& renderer.sharedMaterial.HasProperty("_EmissiveColor");
		}

		/// <summary>Assigns existing color materials, or creates new ones beside their source materials, linked to the preset.</summary>
		/// <param name="preset">The color to apply</param>
		/// <param name="insert">The insert to apply it to</param>
		/// <param name="toPackage">Whether new materials go next to their source when that's in a package that can be written to, instead of into the project</param>
		public static bool Apply(InsertColorPreset preset, InsertComponent insert, bool toPackage = false)
		{
			if (preset == null || !CanApply(insert)) {
				return false;
			}
			var renderer = insert.GetComponent<MeshRenderer>();
			var sourceBody = renderer.sharedMaterial;
			var sourceCover = insert.CoverMaterial;
			var created = new List<Material>();
			try {
				var body = Resolve(sourceBody, preset, false, created, toPackage);
				var cover = sourceCover != null ? Resolve(sourceCover, preset, true, created, toPackage) : null;
				if (body == sourceBody && cover == sourceCover) {
					return false;
				}
				Undo.RecordObjects(new Object[] { insert, renderer }, "Apply Insert Color Preset");
				renderer.sharedMaterial = body;
				if (cover != sourceCover) {
					insert.SetCoverMaterial(cover);
				}
				PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
				PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
				AssetDatabase.SaveAssets();
				return true;
			} catch {
				foreach (var material in created) {
					var path = AssetDatabase.GetAssetPath(material);
					if (!string.IsNullOrEmpty(path)) {
						AssetDatabase.DeleteAsset(path);
					}
				}
				throw;
			}
		}

		private static Material Resolve(Material source, InsertColorPreset preset, bool cover, List<Material> created, bool toPackage)
		{
			if (HasColor(source.name, preset.ColorName)) {
				return source;
			}
			var sourcePath = AssetDatabase.GetAssetPath(source);
			var folder = toPackage && IsWritablePackage(sourcePath)
				? Path.GetDirectoryName(sourcePath)?.Replace('\\', '/')
				: WritableFolder(sourcePath);
			var name = NameFor(source.name, preset.ColorName);
			var existing = FindExisting(source, sourcePath, folder, name, preset.ColorName);
			if (existing != null) {
				return existing;
			}
			EnsureFolder(folder);
			var material = new Material(source) { name = name };
			if (!HasKnownColor(source.name)) {
				material.parent = source;
			}
			Write(material, preset, cover);
			// A material made for a color follows that color's preset from then on.
			material.SetOverrideTag(LinkTag, Guid(preset));
			var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + FileName(name) + ".mat");
			AssetDatabase.CreateAsset(material, path);
			created.Add(material);
			return material;
		}

		#region Project copies

		/// <summary>Whether the material is an asset that can't be written to, like the installed asset library's.</summary>
		public static bool IsReadOnly(Material material)
			=> material != null && InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(material));

		/// <summary>
		/// Whether the insert's material can't be changed: its asset can't be
		/// written to, or it comes with the insert's prefab from a locked asset
		/// library, wherever the material itself lives.
		/// </summary>
		public static bool IsReadOnly(InsertComponent insert, Material material)
		{
			if (material == null) {
				return false;
			}
			if (IsReadOnly(material)) {
				return true;
			}
			var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(insert.gameObject);
			if (source == null || InsertBaker.LockedLibrary(AssetDatabase.GetAssetPath(source)) == null) {
				return false;
			}
			var renderer = source.GetComponent<MeshRenderer>();
			var sourceInsert = source.GetComponent<InsertComponent>();
			return renderer != null && renderer.sharedMaterial == material || sourceInsert != null && sourceInsert.CoverMaterial == material;
		}

		/// <summary>Whether the insert's body or cover material can't be changed.</summary>
		public static bool HasReadOnlyMaterial(InsertComponent insert)
			=> IsReadOnly(insert, insert.GetComponent<MeshRenderer>().sharedMaterial) || IsReadOnly(insert, insert.CoverMaterial);

		/// <summary>The inserts in the open scenes that wear the material, as body or cover.</summary>
		public static InsertComponent[] Wearing(Material material)
			=> material == null ? Array.Empty<InsertComponent>() : Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include)
				.Where(insert => insert.GetComponent<MeshRenderer>().sharedMaterial == material || insert.CoverMaterial == material)
				.ToArray();

		/// <summary>
		/// The inserts in the open scenes that wear any of the given inserts'
		/// read-only materials, the given ones included: those a copy for all
		/// of them would go to.
		/// </summary>
		public static InsertComponent[] SharingReadOnlyMaterials(IReadOnlyCollection<InsertComponent> inserts)
		{
			var materials = inserts
				.SelectMany(insert => new[] { insert.GetComponent<MeshRenderer>().sharedMaterial, insert.CoverMaterial }.Where(material => IsReadOnly(insert, material)))
				.Distinct();
			return inserts.Concat(materials.SelectMany(Wearing)).Distinct().ToArray();
		}

		/// <summary>
		/// Gives the inserts copies of their read-only body and cover materials,
		/// in the project, so they can be changed. A copy is a variant of the
		/// package's material: it keeps following it, and its color preset,
		/// until a value is changed. Inserts wearing the same material get the
		/// same copy, and a copy made before is used again.
		/// </summary>
		/// <returns>How many inserts got a copy</returns>
		public static int CopyToProject(IEnumerable<InsertComponent> inserts)
		{
			var copies = new Dictionary<Material, Material>();
			var changed = 0;
			Undo.IncrementCurrentGroup();
			var group = Undo.GetCurrentGroup();
			foreach (var insert in inserts) {
				var renderer = insert.GetComponent<MeshRenderer>();
				var body = renderer.sharedMaterial;
				var cover = insert.CoverMaterial;
				var bodyCopy = IsReadOnly(insert, body) ? Copy(body, copies) : body;
				var coverCopy = IsReadOnly(insert, cover) ? Copy(cover, copies) : cover;
				if (bodyCopy == body && coverCopy == cover) {
					continue;
				}
				Undo.RecordObjects(new Object[] { insert, renderer }, "Copy Insert Materials to Project");
				renderer.sharedMaterial = bodyCopy;
				if (coverCopy != cover) {
					insert.SetCoverMaterial(coverCopy);
				}
				PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
				PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
				changed++;
			}
			Undo.SetCurrentGroupName("Copy Insert Materials to Project");
			Undo.CollapseUndoOperations(group);
			AssetDatabase.SaveAssets();
			return changed;
		}

		/// <summary>
		/// The project's copy of a material: a variant of it, at the same path
		/// under Assets as it has in its package.
		/// </summary>
		public static Material ProjectCopy(Material source)
		{
			var folder = WritableFolder(AssetDatabase.GetAssetPath(source));
			var path = folder + "/" + FileName(source.name) + ".mat";
			var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (existing != null && existing.parent == source) {
				return existing;
			}
			EnsureFolder(folder);
			// A copy given its parent, so nothing counts as overridden until it's changed.
			var copy = new Material(source) { parent = source, name = source.name };
			AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(path));
			return copy;
		}

		private static Material Copy(Material source, Dictionary<Material, Material> copies)
		{
			if (!copies.TryGetValue(source, out var copy)) {
				copy = ProjectCopy(source);
				copies[source] = copy;
			}
			return copy;
		}

		#endregion

		#region Linking

		/// <summary>
		/// The material tag naming the preset a material follows. It lives in the
		/// material, so it travels with it, and the preset needn't know about it.
		/// </summary>
		private const string LinkTag = "VPEInsertColorPreset";

		/// <summary>The preset the material is linked to, or null.</summary>
		public static InsertColorPreset LinkedPreset(Material material)
		{
			var guid = material != null ? material.GetTag(LinkTag, false) : null;
			return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<InsertColorPreset>(AssetDatabase.GUIDToAssetPath(guid));
		}

		/// <summary>Whether the material takes a preset's body colors, or its cover color.</summary>
		public static bool CanLink(Material material, bool cover)
			=> material != null && (cover ? material.HasProperty("_HazeColor") : material.HasProperty("_InsertLuminosity"));

		/// <summary>
		/// Links the material to the preset, undoably, and takes its colors at
		/// once, or unlinks it when the preset is null. The material is saved,
		/// since that's where the link is looked for.
		/// </summary>
		public static void Link(Material material, InsertColorPreset preset, bool cover)
		{
			Undo.RecordObject(material, preset != null ? "Link Insert Color Preset" : "Unlink Insert Color Preset");
			material.SetOverrideTag(LinkTag, preset != null ? Guid(preset) : string.Empty);
			if (preset != null) {
				Write(material, preset, cover);
			}
			EditorUtility.SetDirty(material);
			AssetDatabase.SaveAssetIfDirty(material);
		}

		/// <summary>
		/// Links the materials to the preset, or unlinks them for null. Linking
		/// takes the preset's values, so when <paramref name="mayCopy"/> and the
		/// materials differ from the preset, the user is asked whether to copy
		/// theirs into the preset first, which then passes them on to every
		/// material linked to it. Returns false when the user cancelled.
		/// </summary>
		/// <param name="materials">The body and cover materials to link, which must be writable</param>
		/// <param name="preset">The preset to link them to, or null to unlink them</param>
		/// <param name="mayCopy">Whether the materials are one insert's, or one material, whose values could go to the preset</param>
		/// <param name="whose">Whose colors they are, for the question: "This insert's"</param>
		public static bool LinkAsking(IReadOnlyList<(Material Material, bool Cover)> materials, InsertColorPreset preset, bool mayCopy, string whose)
		{
			if (preset != null && mayCopy && materials.Any(entry => Differs(entry.Material, preset, entry.Cover))) {
				var canCopy = !InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(preset));
				var message = $"{whose} colors differ from {preset.ColorName}'s. Linking gives {(materials.Count == 1 ? "it" : "them")} the preset's values";
				// 0: the preset's, 1: cancel, 2: copy them to the preset.
				var choice = canCopy
					? EditorUtility.DisplayDialogComplex("Link Insert Color Preset", message + ", unless they're copied to the preset first, which then passes them on to every material linked to it.",
						$"Use {preset.ColorName}'s", "Cancel", $"Copy to {preset.ColorName}")
					: EditorUtility.DisplayDialog("Link Insert Color Preset", message + ". The preset can't be written to, so its values can't be replaced.", $"Use {preset.ColorName}'s", "Cancel") ? 0 : 1;
				if (choice == 1) {
					return false;
				}
				if (choice == 2) {
					Capture(preset, materials.FirstOrDefault(entry => !entry.Cover).Material, materials.FirstOrDefault(entry => entry.Cover).Material);
				}
			}
			foreach (var (material, cover) in materials) {
				Link(material, preset, cover);
			}
			return true;
		}

		/// <summary>
		/// Gives the material its preset's values again, if it's linked: after
		/// something else, like a material preset, may have changed them.
		/// </summary>
		public static void Refresh(Material material)
		{
			var preset = LinkedPreset(material);
			if (preset != null && Write(material, preset, IsCover(material))) {
				EditorUtility.SetDirty(material);
			}
		}

		/// <summary>
		/// Every material linked to the preset. The link is a tag in the material
		/// file, so the files are searched for it before anything is loaded.
		/// </summary>
		public static List<Material> LinkedMaterials(InsertColorPreset preset)
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
		/// Writes the preset into the materials linked to it that can be written
		/// to, and returns how many changed. Materials in a read-only package are
		/// left as they are. An undoable change goes with the preset's own, to be
		/// saved with it; otherwise the materials are saved at once.
		/// </summary>
		public static int Sync(InsertColorPreset preset, IEnumerable<Material> linked, bool undoable)
		{
			var changed = 0;
			foreach (var material in linked) {
				if (material == null || InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(material))) {
					continue;
				}
				var cover = IsCover(material);
				if (!Differs(material, preset, cover)) {
					continue;
				}
				if (undoable) {
					Undo.RecordObject(material, "Change Insert Color Preset");
				}
				Write(material, preset, cover);
				EditorUtility.SetDirty(material);
				if (!undoable) {
					AssetDatabase.SaveAssetIfDirty(material);
				}
				changed++;
			}
			return changed;
		}

		/// <summary>
		/// Takes the colors of a body and a cover material into the preset,
		/// undoably, and passes them on to every material linked to it.
		/// </summary>
		public static void Capture(InsertColorPreset preset, Material body, Material cover)
		{
			Undo.RecordObject(preset, "Copy Insert Colors to Preset");
			if (body != null) {
				preset.Tint = GetColor(body, "_BaseColor", preset.Tint);
				preset.CavityColor = GetColor(body, "_InsertCavityColor", preset.CavityColor);
				preset.LampEmission = GetColor(body, "_EmissiveColor", preset.LampEmission);
				if (body.HasProperty("_InsertLuminosity")) {
					preset.MaxBrightness = body.GetFloat("_InsertLuminosity");
				}
			}
			if (cover != null) {
				preset.HazeColor = GetColor(cover, "_HazeColor", preset.HazeColor);
				preset.CoverTint = GetColor(cover, "_CoverTint", preset.CoverTint);
			}
			EditorUtility.SetDirty(preset);
			Sync(preset, LinkedMaterials(preset), true);
		}

		/// <summary>A cover has a haze and no lamp.</summary>
		private static bool IsCover(Material material) => !material.HasProperty("_InsertLuminosity") && material.HasProperty("_HazeColor");

		/// <summary>Writes the preset's values into the material and returns whether any changed.</summary>
		private static bool Write(Material material, InsertColorPreset preset, bool cover)
		{
			if (cover) {
				var hazed = SetColor(material, "_HazeColor", preset.HazeColor);
				return SetColor(material, "_CoverTint", preset.CoverTint) || hazed;
			}
			var changed = SetColor(material, "_BaseColor", preset.Tint);
			changed |= SetColor(material, "_InsertCavityColor", preset.CavityColor);
			changed |= SetColor(material, "_EmissiveColor", preset.LampEmission);
			changed |= SetFloat(material, "_InsertLuminosity", preset.MaxBrightness);
			return changed;
		}

		/// <summary>Whether the material has values other than the preset's.</summary>
		public static bool Differs(Material material, InsertColorPreset preset, bool cover)
		{
			if (cover) {
				return Differs(material, "_HazeColor", preset.HazeColor) || Differs(material, "_CoverTint", preset.CoverTint);
			}
			return Differs(material, "_BaseColor", preset.Tint)
				|| Differs(material, "_InsertCavityColor", preset.CavityColor)
				|| Differs(material, "_EmissiveColor", preset.LampEmission)
				|| material.HasProperty("_InsertLuminosity") && !Mathf.Approximately(material.GetFloat("_InsertLuminosity"), preset.MaxBrightness);
		}

		private static bool Differs(Material material, string property, Color color)
			=> material.HasProperty(property) && material.GetColor(property) != color;

		private static Color GetColor(Material material, string property, Color fallback)
			=> material.HasProperty(property) ? material.GetColor(property) : fallback;

		private static string Guid(Object asset)
			=> AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long _) ? guid : null;

		#endregion

		private static bool SetColor(Material material, string property, Color color)
		{
			if (!material.HasProperty(property) || material.GetColor(property) == color) {
				return false;
			}
			material.SetColor(property, color);
			return true;
		}

		private static bool SetFloat(Material material, string property, float value)
		{
			if (!material.HasProperty(property) || Mathf.Approximately(material.GetFloat(property), value)) {
				return false;
			}
			material.SetFloat(property, value);
			return true;
		}

		private static Material FindExisting(Material source, string sourcePath, string writableFolder, string wantedName, string colorName)
		{
			var sourceFolder = string.IsNullOrEmpty(sourcePath) ? null : Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
			var folders = new[] { writableFolder, sourceFolder }.Where(folder => !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)).Distinct();
			foreach (var folder in folders) {
				var matches = AssetDatabase.FindAssets("t:Material", new[] { folder })
					.Select(AssetDatabase.GUIDToAssetPath)
					.Where(path => Path.GetDirectoryName(path)?.Replace('\\', '/') == folder)
					.Select(AssetDatabase.LoadAssetAtPath<Material>)
					.Where(material => material != null && material.shader == source.shader && HasColor(material.name, colorName))
					.OrderBy(material => material.name)
					.ToArray();
				var exact = matches.FirstOrDefault(material => string.Equals(material.name, wantedName, StringComparison.OrdinalIgnoreCase));
				if (exact != null) {
					return exact;
				}
				var sourceStem = WithoutColor(source.name);
				var related = matches.FirstOrDefault(material => string.Equals(WithoutColor(material.name), sourceStem, StringComparison.OrdinalIgnoreCase));
				if (related != null) {
					return related;
				}
			}
			return null;
		}

		/// <summary>Replaces a color word in a material name, or appends one for a colorless source.</summary>
		public static string NameFor(string sourceName, string colorName)
		{
			var match = LastColor(sourceName);
			return match.Success ? sourceName.Substring(0, match.Index) + colorName + sourceName.Substring(match.Index + match.Length) : sourceName + ", " + colorName;
		}

		/// <summary>
		/// Maps a package material's folder to the same path under Assets. A
		/// folder that is part of a locked asset library maps to Assets/Inserts,
		/// since what's written there would be the library's again.
		/// </summary>
		public static string WritableFolder(string sourcePath)
		{
			var folder = MappedFolder(sourcePath);
			return InsertBaker.LockedLibrary(folder + "/") != null ? "Assets/Inserts" : folder;
		}

		private static string MappedFolder(string sourcePath)
		{
			var folder = string.IsNullOrEmpty(sourcePath) ? null : Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
			if (string.IsNullOrEmpty(folder)) {
				return "Assets/Inserts";
			}
			if (folder.StartsWith("Assets/", StringComparison.Ordinal) || folder == "Assets") {
				return folder;
			}
			if (folder.StartsWith("Packages/", StringComparison.Ordinal)) {
				var slash = folder.IndexOf('/', "Packages/".Length);
				var relative = slash < 0 ? string.Empty : folder.Substring(slash + 1);
				if (relative.StartsWith("Assets/", StringComparison.Ordinal)) {
					relative = relative.Substring("Assets/".Length);
				}
				return string.IsNullOrEmpty(relative) ? "Assets" : "Assets/" + relative;
			}
			return "Assets/Inserts";
		}

		/// <summary>Whether the asset is in a package, and that package can be written to.</summary>
		private static bool IsWritablePackage(string assetPath)
			=> !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Packages/", StringComparison.Ordinal) && !InsertBaker.IsReadOnly(assetPath);

		private static bool HasColor(string name, string colorName)
		{
			var match = LastColor(name);
			return match.Success && string.Equals(match.Value, colorName, StringComparison.OrdinalIgnoreCase);
		}

		private static bool HasKnownColor(string name) => LastColor(name).Success;

		private static string WithoutColor(string name)
		{
			var match = LastColor(name);
			return match.Success ? (name.Substring(0, match.Index) + name.Substring(match.Index + match.Length)).Trim(' ', ',') : name;
		}

		private static Match LastColor(string name)
		{
			var matches = ColorWord().Matches(name);
			return matches.Count == 0 ? Match.Empty : matches[matches.Count - 1];
		}

		private static Regex ColorWord()
		{
			var names = Find().Select(preset => preset.ColorName).Concat(new[] { "Clear", "White" }) // White, for names from before the white preset was called Clear
				.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderByDescending(name => name.Length).Select(Regex.Escape);
			return new Regex(@"(?<![A-Za-z0-9])(?:" + string.Join("|", names) + @")(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		}

		private static string FileName(string name)
		{
			foreach (var invalid in Path.GetInvalidFileNameChars()) {
				name = name.Replace(invalid, '_');
			}
			return name;
		}

		private static void EnsureFolder(string folder)
		{
			var parts = folder.Split('/');
			var path = parts[0];
			for (var i = 1; i < parts.Length; i++) {
				var child = path + "/" + parts[i];
				if (!AssetDatabase.IsValidFolder(child)) {
					AssetDatabase.CreateFolder(path, parts[i]);
				}
				path = child;
			}
		}
	}
}
