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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ZstdSharp;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Cook settings that shape the local texture cache. Folded into the cache key, so changing
	/// any of them invalidates existing caches and triggers a re-cook on next load.
	/// </summary>
	public static class VpeTextureCookSettings
	{
		// Bump when the cook output format or pipeline changes incompatibly.
		public const int FormatVersion = 1;

		/// <summary>
		/// Divides texture resolution while cooking (1 = authored size, 2 = half, 4 = quarter).
		/// Lets low-spec machines trade texture detail for VRAM and bandwidth.
		/// </summary>
		public static int ResolutionDivisor = 1;

		/// <summary>
		/// Whether cooked textures are GPU block-compressed (BC7). Compressed textures use a
		/// quarter of the video memory and are visually near-identical; turning this off stores
		/// raw RGBA32 — maximum fidelity, but four times the memory and a much slower first load.
		/// </summary>
		public static bool CompressTextures = true;

		public static long ComputeHash()
		{
			return (FormatVersion * 397L + ResolutionDivisor) * 397L + (CompressTextures ? 1 : 0);
		}
	}

	/// <summary>
	/// Per-table cache of GPU-cooked textures (see VpeTextureCook). One file per .vpe package,
	/// validated against the package's size and write time plus the cook settings. The payload is
	/// the exact blob the material reader uploads from, so a cache hit costs one sequential read.
	/// </summary>
	public static class VpeTextureCache
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private const uint Magic = 0x43545056; // "VPTC"
		private const int HeaderVersion = 3; // v2 added the compression byte; v3 made zstd payloads chunked
		private const string CacheFolderName = "TextureCache";
		private const string CacheExtension = ".vptc";

		private const byte PayloadRaw = 0;
		private const byte PayloadZstd = 1;
		// Background-thread, write-once-read-many: a higher level buys disk space at one-time cost;
		// decompress speed is ~level-independent. 9 is a good ratio/compress-time balance.
		private const int ZstdLevel = 9;
		// The payload is compressed as independent ~16 MB frames so both compress (write) and
		// decompress (load) parallelize across cores — managed zstd is single-threaded per frame and
		// far slower than native, so one big frame would add seconds to every load. Chunking costs a
		// negligible amount of ratio. Keep at/above a few MB so the per-frame overhead stays small.
		private const int CompressChunkBytes = 16 * 1024 * 1024;

		/// <summary>
		/// When true, <see cref="Write"/> stores the cooked payload zstd-compressed (~60% smaller on
		/// disk, at the cost of a decompress pass on each load). Storage-only — it does not change the
		/// cooked content or the cache key, and <see cref="TryLoad"/> reads either form via the header
		/// flag, so toggling it never forces a re-cook (existing caches just keep their stored form).
		/// </summary>
		public static bool CompressPayloadOnWrite;

		[Serializable]
		public class Manifest
		{
			// Cooked texture entries; same schema as the package payload, with PixelFormat,
			// MipCount and byte ranges pointing into the cooked payload.
			public VpeTexturePayload[] Textures = Array.Empty<VpeTexturePayload>();
			// Ids of normal maps whose payload is AG-packed for HDRP; the loader flips the
			// corresponding refs to dxt5nm packing so the resolver skips the runtime repack.
			public string[] AgPackedNormalIds = Array.Empty<string>();
		}

		public sealed class CacheData
		{
			public Manifest Manifest;
			public byte[] CookedData;
		}

		public static string GetCachePath(string cacheRoot, string vpePath)
		{
			using var sha1 = SHA1.Create();
			var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(vpePath).ToLowerInvariant()));
			var name = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
			return Path.Combine(cacheRoot, CacheFolderName, name + CacheExtension);
		}

		public static bool TryLoad(string cacheRoot, string vpePath, long settingsHash, out CacheData data)
		{
			data = null;
			try {
				var cachePath = GetCachePath(cacheRoot, vpePath);
				if (!File.Exists(cachePath)) {
					return false;
				}

				var vpeInfo = new FileInfo(vpePath);
				using var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
				using var reader = new BinaryReader(stream);

				if (reader.ReadUInt32() != Magic || reader.ReadInt32() != HeaderVersion) {
					return false;
				}
				if (reader.ReadInt64() != settingsHash) {
					return false;
				}
				if (reader.ReadInt64() != vpeInfo.Length || reader.ReadInt64() != vpeInfo.LastWriteTimeUtc.Ticks) {
					return false;
				}

				var manifestLength = reader.ReadInt32();
				if (manifestLength <= 0 || manifestLength > 256 * 1024 * 1024) {
					return false;
				}
				var manifestBytes = reader.ReadBytes(manifestLength);
				var manifest = PackageApi.Packer.Unpack<Manifest>(manifestBytes);
				if (manifest?.Textures == null) {
					return false;
				}

				var compression = reader.ReadByte();
				var uncompressedLength = reader.ReadInt64();
				if (uncompressedLength < 0 || uncompressedLength > int.MaxValue) {
					return false;
				}

				var payload = new byte[uncompressedLength];
				if (compression == PayloadZstd) {
					// Independent ~16 MB frames: read the chunk table, slurp all frames, then
					// decompress them in parallel straight into their slots in the payload.
					var chunkCount = reader.ReadInt32();
					if (chunkCount <= 0 || chunkCount > 1 << 20) {
						return false;
					}
					var uncompLens = new int[chunkCount];
					var compLens = new int[chunkCount];
					var uncompOffsets = new long[chunkCount];
					var compOffsets = new long[chunkCount];
					long uncompTotal = 0, compTotal = 0;
					for (var ci = 0; ci < chunkCount; ci++) {
						uncompLens[ci] = reader.ReadInt32();
						compLens[ci] = reader.ReadInt32();
						if (uncompLens[ci] < 0 || compLens[ci] < 0) {
							return false;
						}
						uncompOffsets[ci] = uncompTotal;
						compOffsets[ci] = compTotal;
						uncompTotal += uncompLens[ci];
						compTotal += compLens[ci];
					}
					if (uncompTotal != uncompressedLength || stream.Length - stream.Position != compTotal) {
						return false;
					}

					var comp = new byte[compTotal];
					var read = 0;
					while (read < comp.Length) {
						var n = stream.Read(comp, read, comp.Length - read);
						if (n <= 0) {
							return false;
						}
						read += n;
					}

					Parallel.For(0, chunkCount, ci => {
						using var decompressor = new Decompressor();
						decompressor.Unwrap(
							new ReadOnlySpan<byte>(comp, (int)compOffsets[ci], compLens[ci]),
							new Span<byte>(payload, (int)uncompOffsets[ci], uncompLens[ci]));
					});
				} else {
					if (stream.Length - stream.Position != uncompressedLength) {
						return false;
					}
					var read = 0;
					while (read < payload.Length) {
						var n = stream.Read(payload, read, payload.Length - read);
						if (n <= 0) {
							return false;
						}
						read += n;
					}
				}

				data = new CacheData { Manifest = manifest, CookedData = payload };
				return true;

			} catch (Exception ex) {
				Logger.Warn(ex, $"VpeTextureCache: failed reading cache for '{vpePath}'; will re-cook.");
				data = null;
				return false;
			}
		}

		public static void Write(string cacheRoot, string vpePath, long settingsHash, Manifest manifest, byte[] cookedData)
		{
			try {
				var cachePath = GetCachePath(cacheRoot, vpePath);
				Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

				var vpeInfo = new FileInfo(vpePath);
				var manifestBytes = PackageApi.Packer.Pack(manifest);

				var compress = CompressPayloadOnWrite;

				// Compress (if enabled) into independent frames in parallel before opening the file, so
				// the background write is fast and the on-disk layout matches the parallel load path.
				byte[][] compressedChunks = null;
				if (compress) {
					var chunkCount = System.Math.Max(1, (cookedData.Length + CompressChunkBytes - 1) / CompressChunkBytes);
					compressedChunks = new byte[chunkCount][];
					Parallel.For(0, chunkCount, ci => {
						var off = (long)ci * CompressChunkBytes;
						var len = (int)System.Math.Min(CompressChunkBytes, cookedData.Length - off);
						using var compressor = new Compressor(ZstdLevel);
						compressedChunks[ci] = compressor.Wrap(new ReadOnlySpan<byte>(cookedData, (int)off, len)).ToArray();
					});
				}

				var tmpPath = cachePath + ".tmp";
				using (var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
				using (var writer = new BinaryWriter(stream)) {
					writer.Write(Magic);
					writer.Write(HeaderVersion);
					writer.Write(settingsHash);
					writer.Write(vpeInfo.Length);
					writer.Write(vpeInfo.LastWriteTimeUtc.Ticks);
					writer.Write(manifestBytes.Length);
					writer.Write(manifestBytes);
					writer.Write(compress ? PayloadZstd : PayloadRaw);
					writer.Write((long)cookedData.Length); // uncompressed length

					if (compress) {
						writer.Write(compressedChunks.Length);
						for (var ci = 0; ci < compressedChunks.Length; ci++) {
							var off = (long)ci * CompressChunkBytes;
							writer.Write((int)System.Math.Min(CompressChunkBytes, cookedData.Length - off)); // uncompressed chunk length
							writer.Write(compressedChunks[ci].Length);                                 // compressed chunk length
						}
						foreach (var chunk in compressedChunks) {
							writer.Write(chunk);
						}
					} else {
						writer.Write(cookedData);
					}
				}

				var storedLength = new FileInfo(tmpPath).Length;

				// Atomic-ish swap so a crashed write never leaves a torn cache behind.
				if (File.Exists(cachePath)) {
					File.Delete(cachePath);
				}
				File.Move(tmpPath, cachePath);
				if (compress) {
					Logger.Info($"VpeTextureCache: wrote {storedLength / 1024f / 1024f:F1} MB cache (zstd, " +
						$"{cookedData.Length / 1024f / 1024f:F1} MB uncompressed) for '{Path.GetFileName(vpePath)}'.");
				} else {
					Logger.Info($"VpeTextureCache: wrote {cookedData.Length / 1024f / 1024f:F1} MB cache for '{Path.GetFileName(vpePath)}'.");
				}

			} catch (Exception ex) {
				Logger.Warn(ex, $"VpeTextureCache: failed writing cache for '{vpePath}'.");
			}
		}
	}
}
