// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public struct DmdSpriteImportOptions
	{
		public int CellWidth;
		public int CellHeight;
		public int DefaultFrameDuration;
		public byte AlphaThreshold;

		public static DmdSpriteImportOptions Default => new DmdSpriteImportOptions {
			DefaultFrameDuration = 1
		};
	}

	public sealed class DmdSpriteImportResult
	{
		public DmdSpriteAsset Sprite { get; internal set; }
		public List<string> Warnings { get; } = new List<string>();
		public int MaxDistinctIntensities { get; internal set; }
		public int[] ShadeHistogram { get; } = new int[256];
	}

	public static class DmdSpriteImporter
	{
		[MenuItem("Assets/Create/Pinball/DMD/Sprite from Image…", false, 314)]
		private static void ImportSelectedProject()
		{
			var project = Selection.activeObject as DmdProjectAsset;
			if (project == null) {
				EditorUtility.DisplayDialog("DMD Sprite Import",
					"Select a DmdProjectAsset before importing a sprite.", "OK");
				return;
			}
			var source = EditorUtility.OpenFilePanel("Import DMD Sprite", string.Empty, "png");
			if (string.IsNullOrEmpty(source)) {
				return;
			}
			var destination = DefaultAssetPath(project, Path.GetFileNameWithoutExtension(source));
			var result = Import(project, new[] { source }, destination, DmdSpriteImportOptions.Default);
			Selection.activeObject = result.Sprite;
			ReportWarnings(result.Warnings);
		}

		public static DmdSpriteImportResult Import(DmdProjectAsset project, IReadOnlyList<string> sourcePaths,
			string assetPath, DmdSpriteImportOptions options)
		{
			if (project == null) {
				throw new ArgumentNullException(nameof(project));
			}
			if (sourcePaths == null || sourcePaths.Count == 0) {
				throw new ArgumentException("At least one PNG is required.", nameof(sourcePaths));
			}
			ValidateAssetPath(assetPath);
			if (options.DefaultFrameDuration <= 0) {
				options.DefaultFrameDuration = 1;
			}

			var result = new DmdSpriteImportResult();
			var frames = new List<DmdBitmapData>();
			var convertedColor = false;
			for (var sourceIndex = 0; sourceIndex < sourcePaths.Count; sourceIndex++) {
				var decoded = DmdImageDecoder.DecodePng(sourcePaths[sourceIndex], project.ColorMode,
					options.CellWidth, options.CellHeight, options.AlphaThreshold);
				if (frames.Count + decoded.Count > DmdValidation.MaxSpriteFrames) {
					throw new ArgumentException($"A sprite cannot exceed {DmdValidation.MaxSpriteFrames} frames.");
				}
				foreach (var image in decoded) {
					frames.Add(image.Bitmap);
					convertedColor |= project.ColorMode != DmdColorMode.Rgb24 && image.ContainsColor;
					result.MaxDistinctIntensities = System.Math.Max(result.MaxDistinctIntensities,
						image.DistinctIntensities);
					for (var shade = 0; shade < result.ShadeHistogram.Length; shade++) {
						result.ShadeHistogram[shade] += image.IntensityHistogram[shade];
					}
					if (image.Bitmap.Width != project.Width || image.Bitmap.Height != project.Height) {
						AddWarning(result, $"Frame size {image.Bitmap.Width}x{image.Bitmap.Height} differs from the project canvas {project.Width}x{project.Height}.");
					}
				}
			}

			if (convertedColor) {
				AddWarning(result, "RGB source pixels were converted to Rec.601 luminance for this mono project.");
			}
			var shadeCount = project.ColorMode == DmdColorMode.Mono4 ? 4 : 16;
			if (project.ColorMode != DmdColorMode.Rgb24 && result.MaxDistinctIntensities > shadeCount) {
				AddWarning(result,
					$"Source contains {result.MaxDistinctIntensities} intensity levels; the project supports {shadeCount} shades.");
			}

			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			sprite.name = Path.GetFileNameWithoutExtension(assetPath);
			sprite.Frames.AddRange(frames);
			for (var frame = 0; frame < frames.Count; frame++) {
				sprite.FrameDurations.Add(options.DefaultFrameDuration);
			}
			var validation = sprite.Validate();
			if (!validation.IsValid) {
				UnityEngine.Object.DestroyImmediate(sprite);
				throw new DmdValidationException(validation.Diagnostics);
			}

			var createdPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
			try {
				AssetDatabase.CreateAsset(sprite, createdPath);
				Undo.RegisterCreatedObjectUndo(sprite, "Import DMD sprite");
				Undo.RecordObject(project, "Add DMD sprite");
				project.Sprites ??= new List<DmdSpriteAsset>();
				project.Sprites.Add(sprite);
				EditorUtility.SetDirty(project);
				AssetDatabase.SaveAssets();
				result.Sprite = sprite;
				return result;
			} catch {
				project.Sprites?.Remove(sprite);
				EditorUtility.SetDirty(project);
				if (!AssetDatabase.DeleteAsset(createdPath) && sprite != null) {
					UnityEngine.Object.DestroyImmediate(sprite);
				}
				throw;
			}
		}

		internal static string DefaultAssetPath(DmdProjectAsset project, string name)
		{
			var projectPath = AssetDatabase.GetAssetPath(project);
			var folder = string.IsNullOrEmpty(projectPath) ? "Assets" : Path.GetDirectoryName(projectPath);
			return $"{folder?.Replace('\\', '/')}/{name}.asset";
		}

		internal static void ReportWarnings(IReadOnlyList<string> warnings)
		{
			if (warnings == null || warnings.Count == 0) {
				return;
			}
			Debug.LogWarning("DMD import warnings:\n" + string.Join("\n", warnings));
		}

		private static void AddWarning(DmdSpriteImportResult result, string warning)
		{
			if (!result.Warnings.Contains(warning)) {
				result.Warnings.Add(warning);
			}
		}

		private static void ValidateAssetPath(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath) ||
			    !assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal)) {
				throw new ArgumentException("Imported assets must be saved below Assets/.", nameof(assetPath));
			}
		}
	}
}
