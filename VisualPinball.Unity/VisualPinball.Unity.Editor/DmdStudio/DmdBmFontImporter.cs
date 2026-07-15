// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public static class DmdBmFontImporter
	{
		private static readonly Regex AttributePattern = new Regex(
			@"(?<name>[A-Za-z][A-Za-z0-9]*)=(?<value>""[^""]*""|[^\s]+)",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public static DmdFontAsset Import(DmdProjectAsset project, string descriptorPath, string assetPath)
		{
			if (project == null) {
				throw new ArgumentNullException(nameof(project));
			}
			if (string.IsNullOrWhiteSpace(descriptorPath) || !File.Exists(descriptorPath)) {
				throw new FileNotFoundException("BMFont text descriptor not found.", descriptorPath);
			}
			if (string.IsNullOrWhiteSpace(assetPath) ||
			    !assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal)) {
				throw new ArgumentException("Imported assets must be saved below Assets/.", nameof(assetPath));
			}

			var font = ScriptableObject.CreateInstance<DmdFontAsset>();
			font.name = Path.GetFileNameWithoutExtension(assetPath);
			font.Notes = $"Imported from BMFont descriptor {Path.GetFileName(descriptorPath)}.";
			string pageFile = null;
			var declaredWidth = 0;
			var declaredHeight = 0;
			var declaredPages = 0;
			try {
				foreach (var rawLine in File.ReadLines(descriptorPath)) {
					var line = rawLine.Trim();
					if (line.Length == 0) {
						continue;
					}
					var space = line.IndexOf(' ');
					var kind = space < 0 ? line : line.Substring(0, space);
					var values = ParseAttributes(line);
					switch (kind) {
						case "common":
							font.LineHeight = RequiredInt(values, "lineHeight", kind);
							font.Baseline = RequiredInt(values, "base", kind);
							declaredWidth = RequiredInt(values, "scaleW", kind);
							declaredHeight = RequiredInt(values, "scaleH", kind);
							declaredPages = RequiredInt(values, "pages", kind);
							break;
						case "page":
							if (RequiredInt(values, "id", kind) == 0) {
								pageFile = Required(values, "file", kind);
							}
							break;
						case "char":
							font.Glyphs.Add(new DmdGlyph {
								Codepoint = RequiredInt(values, "id", kind),
								X = RequiredInt(values, "x", kind),
								Y = RequiredInt(values, "y", kind),
								W = RequiredInt(values, "width", kind),
								H = RequiredInt(values, "height", kind),
								OffsetX = RequiredInt(values, "xoffset", kind),
								OffsetY = RequiredInt(values, "yoffset", kind),
								Advance = RequiredInt(values, "xadvance", kind)
							});
							break;
						case "kerning":
							font.Kerning.Add(new DmdKerningPair {
								LeftCodepoint = RequiredInt(values, "first", kind),
								RightCodepoint = RequiredInt(values, "second", kind),
								Adjustment = RequiredInt(values, "amount", kind)
							});
							break;
					}
				}

				if (declaredPages != 1) {
					throw new NotSupportedException("DMD Studio v1 supports single-page BMFont text descriptors only.");
				}
				if (string.IsNullOrWhiteSpace(pageFile)) {
					throw new InvalidDataException("BMFont descriptor does not declare page id 0.");
				}
				var pagePath = Path.Combine(Path.GetDirectoryName(descriptorPath) ?? string.Empty, pageFile);
				var decoded = DmdImageDecoder.DecodePng(pagePath, DmdColorMode.Mono16);
				if (decoded.Count != 1) {
					throw new InvalidDataException("BMFont atlas must decode to one image.");
				}
				font.Atlas = decoded[0].Bitmap;
				if (font.Atlas.Width != declaredWidth || font.Atlas.Height != declaredHeight) {
					throw new InvalidDataException(
						$"BMFont atlas is {font.Atlas.Width}x{font.Atlas.Height}, descriptor declares {declaredWidth}x{declaredHeight}.");
				}

				var validation = font.Validate();
				if (!validation.IsValid) {
					throw new DmdValidationException(validation.Diagnostics);
				}

				var createdPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
				AssetDatabase.CreateAsset(font, createdPath);
				Undo.RegisterCreatedObjectUndo(font, "Import DMD font");
				Undo.RecordObject(project, "Add DMD font");
				project.Fonts ??= new List<DmdFontAsset>();
				project.Fonts.Add(font);
				EditorUtility.SetDirty(project);
				AssetDatabase.SaveAssets();
				return font;
			} catch {
				project.Fonts?.Remove(font);
				EditorUtility.SetDirty(project);
				if (AssetDatabase.Contains(font)) {
					AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(font));
				} else {
					UnityEngine.Object.DestroyImmediate(font);
				}
				throw;
			}
		}

		private static Dictionary<string, string> ParseAttributes(string line)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (Match match in AttributePattern.Matches(line)) {
				var value = match.Groups["value"].Value;
				if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"') {
					value = value.Substring(1, value.Length - 2);
				}
				values[match.Groups["name"].Value] = value;
			}
			return values;
		}

		private static int RequiredInt(IReadOnlyDictionary<string, string> values, string name, string line)
		{
			var value = Required(values, name, line);
			if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) {
				throw new InvalidDataException($"BMFont {line} has invalid integer {name}=\"{value}\".");
			}
			return parsed;
		}

		private static string Required(IReadOnlyDictionary<string, string> values, string name, string line)
		{
			if (!values.TryGetValue(name, out var value)) {
				throw new InvalidDataException($"BMFont {line} is missing {name}.");
			}
			return value;
		}
	}
}
