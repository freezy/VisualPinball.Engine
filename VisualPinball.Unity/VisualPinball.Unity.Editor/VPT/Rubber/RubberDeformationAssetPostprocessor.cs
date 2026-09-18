// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Repaints inspectors when a material a deforming rubber depends on is
	/// reimported, so the bake freshness badge, which is derived from the hash,
	/// updates without a selection change.
	/// </summary>
	internal sealed class RubberDeformationAssetPostprocessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(string[] importedAssets,
			string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			if (!TouchesDeformationMaterial(importedAssets)
				&& !TouchesDeformationMaterial(movedAssets)
				&& !TouchesDeformationMaterial(deletedAssets)) {
				return;
			}
			// The hash comparison at read time is the source of truth for staleness, so
			// nothing has to be rewritten here. Dependent components in open scenes only
			// need their inspectors and gizmos to redraw.
			var hasDependent = UnityEngine.Object.FindObjectsByType<RubberColliderComponent>(
					FindObjectsInactive.Include, FindObjectsSortMode.None)
				.Any(collider => collider && collider.DeformationEnabled);
			if (!hasDependent) {
				return;
			}
			SceneView.RepaintAll();
			foreach (var editor in Resources.FindObjectsOfTypeAll<RubberColliderInspector>()) {
				editor.Repaint();
			}
		}

		private static bool TouchesDeformationMaterial(IEnumerable<string> paths)
		{
			foreach (var path in paths) {
				if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset",
					StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				var type = AssetDatabase.GetMainAssetTypeAtPath(path);
				if (type == typeof(RubberPhysicsMaterialAsset) || type == typeof(PhysicsMaterialAsset)) {
					return true;
				}
			}
			return false;
		}
	}
}
