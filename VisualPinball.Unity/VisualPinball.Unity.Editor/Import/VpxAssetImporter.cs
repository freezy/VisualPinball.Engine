// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

// ReSharper disable UnusedType.Global

using NLog;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	[ScriptedImporter(2, "vpx")]
	public class VpxAssetImporter : ScriptedImporter
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override void OnImportAsset(AssetImportContext ctx)
		{
			Logger.Info("Importing VPX table at {0}...", ctx.assetPath);

			// create root object
			var rootGameObj = new GameObject();
			// add lazy importer, will do a normal in memory import once the object ends up in a scene
			rootGameObj.AddComponent<VpxAssetLazyImporter>();

			ctx.AddObjectToAsset("main obj", rootGameObj);
			ctx.SetMainObject(rootGameObj);
		}
	}
}
