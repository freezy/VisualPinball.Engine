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
using System.Linq;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	internal sealed class AssetBrowserPostprocessor : AssetPostprocessor
	{
		public static event Action<string[]> AssetFilesChanged;

		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			var changedPaths = importedAssets
				.Concat(deletedAssets)
				.Concat(movedAssets)
				.Concat(movedFromAssetPaths)
				.Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
				.Distinct(StringComparer.Ordinal)
				.ToArray();
			if (changedPaths.Length > 0) {
				AssetFilesChanged?.Invoke(changedPaths);
			}
		}
	}
}
