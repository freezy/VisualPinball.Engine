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
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	public static class VpxImportEngine
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void ImportIntoScene(string path, GameObject parent = null, bool applyPatch = true, string tableName = null)
		{
			var sw = Stopwatch.StartNew();

			// load table
			var th = TableLoader.LoadTable(path);
			var loadedIn = sw.ElapsedMilliseconds;

			var converter = new VpxSceneConverter(th, Path.GetFileName(path));

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

			Logger.Info($"Imported {path} in {convertedIn}ms (loaded after {loadedIn}ms).");
		}
	}
}
