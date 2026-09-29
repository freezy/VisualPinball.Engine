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

		/// <summary>Assigns existing color materials, or creates new ones beside their source materials.</summary>
		public static bool Apply(InsertColorPreset preset, InsertComponent insert)
		{
			if (preset == null || !CanApply(insert)) {
				return false;
			}
			var renderer = insert.GetComponent<MeshRenderer>();
			var sourceBody = renderer.sharedMaterial;
			var sourceCover = insert.CoverMaterial;
			var created = new List<Material>();
			try {
				var body = Resolve(sourceBody, preset, false, created);
				var cover = sourceCover != null ? Resolve(sourceCover, preset, true, created) : null;
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

		private static Material Resolve(Material source, InsertColorPreset preset, bool cover, List<Material> created)
		{
			if (HasColor(source.name, preset.ColorName)) {
				return source;
			}
			var sourcePath = AssetDatabase.GetAssetPath(source);
			var folder = WritableFolder(sourcePath);
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
			if (cover) {
				SetColor(material, "_HazeColor", preset.CoverColor);
			} else {
				SetColor(material, "_BaseColor", preset.BodyColor);
				SetColor(material, "_InsertCavityColor", preset.CavityColor);
				SetColor(material, "_EmissiveColor", preset.LampEmission);
			}
			var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + FileName(name) + ".mat");
			AssetDatabase.CreateAsset(material, path);
			created.Add(material);
			return material;
		}

		private static void SetColor(Material material, string property, Color color)
		{
			if (material.HasProperty(property)) {
				material.SetColor(property, color);
			}
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

		/// <summary>Maps a package material's folder to the same path under Assets.</summary>
		public static string WritableFolder(string sourcePath)
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
			var names = Find().Select(preset => preset.ColorName).Concat(new[] { "Clear" })
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
