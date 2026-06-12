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

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Applies the packaged audio import intent (mono, load type, compression) during the initial
	/// asset import of unpacked sound files, mirroring what VpeTextureImportPreprocessor does for
	/// textures. PackagedFiles registers the pending settings before triggering the import and
	/// clears them afterwards.
	/// </summary>
	public class VpeSoundImportPreprocessor : AssetPostprocessor
	{
		private static readonly Dictionary<string, SoundMetaPackable> PendingByAssetPath = new(StringComparer.OrdinalIgnoreCase);

		public static void Register(string assetPath, SoundMetaPackable meta)
		{
			PendingByAssetPath[assetPath] = meta;
		}

		public static void Clear()
		{
			PendingByAssetPath.Clear();
		}

		private void OnPreprocessAudio()
		{
			if (!PendingByAssetPath.TryGetValue(assetPath, out var meta)) {
				return;
			}

			var audioImporter = (AudioImporter)assetImporter;
			audioImporter.forceToMono = meta.ForceToMono;
			audioImporter.ambisonic = meta.Ambisonic;
			audioImporter.loadInBackground = meta.LoadInBackground;

			var sampleSettings = audioImporter.defaultSampleSettings;
			if (Enum.TryParse<AudioClipLoadType>(meta.LoadType, out var loadType)) {
				sampleSettings.loadType = loadType;
			}
			if (Enum.TryParse<AudioCompressionFormat>(meta.CompressionFormat, out var compressionFormat)) {
				sampleSettings.compressionFormat = compressionFormat;
			}
			if (meta.Quality > 0f) {
				sampleSettings.quality = meta.Quality;
			}
			audioImporter.defaultSampleSettings = sampleSettings;
		}
	}
}

#endif
