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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A deferred source for one packaged texture's bytes. The material capture (on the main thread)
	/// records these — file path or already-encoded inline bytes — instead of reading/encoding the
	/// bytes inline, so the heavy disk + PNG16→8 work can run on worker threads later
	/// (see <see cref="VpeTextureBlobLoader"/>). Nothing here touches Unity APIs, so it is thread-safe.
	/// </summary>
	public readonly struct VpeTextureBlobSource
	{
		// Package file name (matches VpeTexture.FileName).
		public string FileName { get; }
		// Source asset path on disk (null when InlineBytes is set).
		public string AssetPath { get; }
		// Bytes captured on the main thread for textures with no decodable source file (rare).
		public byte[] InlineBytes { get; }
		// Whether AssetPath is a PNG (only PNGs are candidates for the 16→8 downconvert).
		public bool IsPng { get; }

		public VpeTextureBlobSource(string fileName, string assetPath, byte[] inlineBytes, bool isPng)
		{
			FileName = fileName;
			AssetPath = assetPath;
			InlineBytes = inlineBytes;
			IsPng = isPng;
		}

		public static VpeTextureBlobSource FromFile(string fileName, string assetPath, bool isPng)
			=> new(fileName, assetPath, null, isPng);

		public static VpeTextureBlobSource FromBytes(string fileName, byte[] bytes)
			=> new(fileName, null, bytes, false);

		// Loads (and, for 16-bit PNGs, downconverts to 8-bit) the bytes. Safe to call concurrently —
		// but ONLY because libvips is fully initialized once on the main thread first (see
		// VpeTextureBlobLoader.EnsureVipsInitialized). libvips' operations are thread-safe after init;
		// it's the lazy cold init that isn't, and racing it across workers hard-crashes Unity
		// ("invalid class cast from (NULL) pointer to 'VipsObject'").
		public byte[] Load()
		{
			if (InlineBytes != null) {
				return InlineBytes;
			}
			if (string.IsNullOrEmpty(AssetPath)) {
				return null;
			}

			var bytes = File.ReadAllBytes(AssetPath);
			if (!IsPng || !IsPng16Bit(bytes)) {
				return bytes;
			}

			try {
				// 16-bit PNG → 8-bit: load, shift the 16-bit channels down to their high byte (preserving
				// channel count, so alpha is kept), re-save as 8-bit PNG. libvips is native (libpng/zlib),
				// far faster than Unity's LoadImage+EncodeToPNG or any managed encoder.
				using var image = NetVips.Image.NewFromBuffer(bytes);
				using var reduced = image.Cast(NetVips.Enums.BandFormat.Uchar, shift: true);
				var png = reduced.PngsaveBuffer();
				if (png != null && png.Length > 0) {
					return png;
				}
			} catch (Exception ex) {
				VpeTextureBlobLoader.Log.Warn(ex, $"VpeTextureBlobSource: failed downconverting 16-bit PNG '{AssetPath}' via libvips; packing original.");
			}
			return bytes;
		}

		// PNG layout: 8-byte signature, then IHDR (4 length + 4 "IHDR" + 4 width + 4 height + 1 bit
		// depth) — bit depth sits at byte 24.
		internal static bool IsPng16Bit(byte[] png)
		{
			return png != null && png.Length > 25
				&& png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47
				&& png[24] == 16;
		}
	}

	/// <summary>
	/// Loads a set of <see cref="VpeTextureBlobSource"/>s into their final packaged bytes, in
	/// parallel on worker threads (the heaviest part of a .vpe export: reading hundreds of MB of
	/// source PNGs and downconverting the 16-bit ones).
	/// </summary>
	public static class VpeTextureBlobLoader
	{
		internal static readonly Logger Log = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Loads all sources in parallel. <paramref name="onItemDone"/> is invoked (from worker
		/// threads) with the running completed count after each blob, for progress reporting.
		/// </summary>
		public static Dictionary<string, byte[]> LoadAll(
			IReadOnlyList<VpeTextureBlobSource> sources,
			Action<int> onItemDone,
			CancellationToken cancellationToken)
		{
			var result = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
			if (sources == null || sources.Count == 0) {
				return new Dictionary<string, byte[]>(StringComparer.Ordinal);
			}

			// Precondition: EnsureVipsInitialized() must already have run on the main thread (the caller
			// does this before fanning out), so the parallel libvips calls below don't race its cold init.
			var done = 0;
			var options = new ParallelOptions {
				CancellationToken = cancellationToken,
				MaxDegreeOfParallelism = System.Math.Max(2, Environment.ProcessorCount - 1),
			};
			Parallel.ForEach(sources, options, source => {
				var bytes = source.Load();
				if (bytes != null && bytes.Length > 0 && !string.IsNullOrEmpty(source.FileName)) {
					result[source.FileName] = bytes;
				}
				onItemDone?.Invoke(Interlocked.Increment(ref done));
			});

			return new Dictionary<string, byte[]>(result, StringComparer.Ordinal);
		}

		/// <summary>
		/// Forces libvips' native + GObject initialization (vips_init, which registers all foreign
		/// load/save types) on the calling thread, via a tiny 1×1 PNG round-trip. MUST run once on the
		/// main thread before <see cref="LoadAll"/> fans out: libvips operations are thread-safe only
		/// after init, and racing that lazy cold init across worker threads hard-crashes Unity
		/// ("invalid class cast from (NULL) pointer to 'VipsObject'"). Cheap; safe to call repeatedly.
		/// </summary>
		public static void EnsureVipsInitialized()
		{
			try {
				using var probe = NetVips.Image.Black(1, 1);
				var png = probe.PngsaveBuffer();
				using var reloaded = NetVips.Image.NewFromBuffer(png);
				_ = reloaded.Width;
			} catch (Exception ex) {
				Log.Warn(ex, "VpeTextureBlobLoader: libvips pre-warm failed; 16-bit PNG downconvert may fall back to packing originals.");
			}
		}
	}
}
