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
using NLog;
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

		public static long ComputeHash()
		{
			return FormatVersion * 397L + ResolutionDivisor;
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
		private const int HeaderVersion = 1;
		private const string CacheFolderName = "TextureCache";
		private const string CacheExtension = ".vptc";

		[Serializable]
		public class Manifest
		{
			// Cooked texture entries; same schema as the package payload, with PixelFormat,
			// MipCount and byte ranges pointing into the cooked payload.
			public VpeTextureAssetV1[] Textures = Array.Empty<VpeTextureAssetV1>();
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

				var payloadLength = reader.ReadInt64();
				if (payloadLength < 0 || payloadLength > int.MaxValue
					|| stream.Length - stream.Position != payloadLength) {
					return false;
				}

				var payload = new byte[payloadLength];
				var read = 0;
				while (read < payload.Length) {
					var n = stream.Read(payload, read, payload.Length - read);
					if (n <= 0) {
						return false;
					}
					read += n;
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
					writer.Write((long)cookedData.Length);
					writer.Write(cookedData);
				}

				// Atomic-ish swap so a crashed write never leaves a torn cache behind.
				if (File.Exists(cachePath)) {
					File.Delete(cachePath);
				}
				File.Move(tmpPath, cachePath);
				Logger.Info($"VpeTextureCache: wrote {cookedData.Length / 1024f / 1024f:F1} MB cache for '{Path.GetFileName(vpePath)}'.");

			} catch (Exception ex) {
				Logger.Warn(ex, $"VpeTextureCache: failed writing cache for '{vpePath}'.");
			}
		}
	}
}
