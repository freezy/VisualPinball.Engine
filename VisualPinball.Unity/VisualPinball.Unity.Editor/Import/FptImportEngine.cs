// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.IO;

using NLog;
using UnityEditor;
using UnityEngine;

using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	public static class FptImportEngine
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static FptImportResult ImportIntoScene(
			string path,
			string tableName = null,
			GameObject parent = null,
			FptImportOptions options = null)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("FPT path is required.", nameof(path));
			if (!File.Exists(path)) throw new FileNotFoundException("Future Pinball table not found.", path);
			try {
				EditorUtility.DisplayProgressBar("Import Future Pinball Table", "Extracting source data and building static scene...", 0.35f);
				var converter = new FptSceneConverter(path, tableName, options ?? new FptImportOptions());
				var result = converter.Convert();
				if (parent != null) GameObjectUtility.SetParentAndAlign(result.Root, parent);
				Undo.RegisterCreatedObjectUndo(result.Root, "Import Future Pinball table");
				Selection.activeGameObject = result.Root;
				Logger.Info("Imported Future Pinball table {0}: {1} meshes, {2} colliders, {3} placeholders. Bundle: {4}",
					Path.GetFileName(path), result.Report.MeshAssets, result.Report.Colliders, result.Report.Placeholders, result.BundleAssetPath);
				return result;
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}
	}
}
