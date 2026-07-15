// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	public static class DmdStarterFontLibrary
	{
		private const string Root =
			"Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity/DmdStudio/StarterFonts/";

		private static readonly string[] AssetNames = {
			"VpeMicro5", "VpeMicro7", "VpeArcade9", "VpeArcade15"
		};

		public static IReadOnlyList<DmdFontAsset> LoadAll()
		{
			var fonts = new List<DmdFontAsset>(AssetNames.Length);
			foreach (var assetName in AssetNames) {
				var font = AssetDatabase.LoadAssetAtPath<DmdFontAsset>($"{Root}{assetName}.asset");
				if (font != null) fonts.Add(font);
			}
			return fonts;
		}

		public static int AddToProject(DmdProjectAsset project)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			var fonts = LoadAll();
			if (fonts.Count != AssetNames.Length) {
				throw new InvalidOperationException("The DMD starter font package is incomplete.");
			}
			project.Fonts ??= new List<DmdFontAsset>();
			var added = 0;
			foreach (var font in fonts) {
				if (project.Fonts.Contains(font)) continue;
				if (added == 0) Undo.RecordObject(project, "Add DMD starter fonts");
				project.Fonts.Add(font);
				added++;
			}
			if (added > 0) EditorUtility.SetDirty(project);
			return added;
		}
	}
}
