// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System.Diagnostics;
using System.IO;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	public static class VpxImportEngine
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static GameObject ImportIntoScene(string path, GameObject parent = null, bool applyPatch = true, string tableName = null, ConvertOptions options = null)
		{
			var sw = Stopwatch.StartNew();
			return ImportIntoScene(TableLoader.LoadTable(path), Path.GetFileName(path), parent, applyPatch, tableName, sw, options);
		}

		public static GameObject ImportIntoScene(FileTableContainer tableContainer, string filename = "", GameObject parent = null, bool applyPatch = true, string tableName = null, Stopwatch sw = null, ConvertOptions options = null)
		{
			sw ??= Stopwatch.StartNew();
			if (tableName == null && !string.IsNullOrEmpty(filename)) {
				tableName = Path.GetFileNameWithoutExtension(filename);
			}

			// load table
			var loadedIn = sw.ElapsedMilliseconds;
			var converter = new VpxSceneConverter(tableContainer, filename, options);
			var tableGameObject = converter.Convert(applyPatch, tableName);
			var convertedIn = sw.ElapsedMilliseconds;

			// if an object was selected in the editor, make it its parent
			if (parent != null) {
				GameObjectUtility.SetParentAndAlign(tableGameObject, parent);
			}

			// register undo system
			Undo.RegisterCreatedObjectUndo(tableGameObject, "Import VPX table file");

			// select imported object
			Selection.activeObject = tableGameObject;

			Logger.Info($"Imported {filename} in {convertedIn}ms (loaded after {loadedIn}ms).");

			return tableGameObject;
		}
	}
}
