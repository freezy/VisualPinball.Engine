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
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Applies the .vpe payload's texture import settings (normal map type, colorspace, sampling,
	/// authored max size) during the initial asset import, so package import doesn't have to run
	/// every texture through the asset pipeline twice. PackageReader registers the pending
	/// settings before triggering the import and clears them afterwards.
	/// </summary>
	public class VpeTextureImportPreprocessor : AssetPostprocessor
	{
		private sealed class PendingSettings
		{
			public VpeTextureAssetV1 Asset;
			public bool IsNormalMap;
		}

		private static readonly Dictionary<string, PendingSettings> PendingByAssetPath = new(StringComparer.OrdinalIgnoreCase);

		public static void Register(string assetPath, VpeTextureAssetV1 asset, bool isNormalMap)
		{
			PendingByAssetPath[assetPath] = new PendingSettings { Asset = asset, IsNormalMap = isNormalMap };
		}

		public static void Clear()
		{
			PendingByAssetPath.Clear();
		}

		private void OnPreprocessTexture()
		{
			if (!PendingByAssetPath.TryGetValue(assetPath, out var pending)) {
				return;
			}

			var asset = pending.Asset;
			var textureImporter = (TextureImporter)assetImporter;
			textureImporter.textureType = pending.IsNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
			textureImporter.sRGBTexture = !string.Equals(asset.ColorSpace, VpeColorSpaces.Linear, StringComparison.OrdinalIgnoreCase);
			textureImporter.mipmapEnabled = asset.GenerateMipMaps;
			textureImporter.wrapMode = (TextureWrapMode)asset.WrapMode;
			textureImporter.filterMode = (FilterMode)asset.FilterMode;
			textureImporter.anisoLevel = Mathf.Max(1, asset.AnisoLevel);
			if (asset.Width > 0 && asset.Height > 0) {
				// The payload dimensions carry the authored import clamp.
				textureImporter.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(asset.Width, asset.Height)), 32, 16384);
			}
		}
	}
}
