// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using ZstdSharp;

namespace VisualPinball.Unity
{
	/// <summary>One immutable cooked object per source/recipe, shared across packages. Warm reads never hash payloads.</summary>
	public static class VpeSharedTextureCache
	{
		public const int Version = 1;
		public const int MaxObjectBytes = 512 * 1024 * 1024;
		public static long QuotaBytes = 10L * 1024 * 1024 * 1024;
		private static readonly object Gate = new();
		private static readonly Dictionary<string, DateTime> Recency = new(StringComparer.Ordinal);
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
		public static string Root => Path.Combine(VpeIntegrity.CacheRoot, "TextureCache", "v2", "objects");

		public sealed class Header
		{
			public int Version = VpeSharedTextureCache.Version;
			public string Key;
			public int Width, Height, Mips, Length;
			public string Format, Mime, Digest;
			public bool NormalPacked, Compressed;
		}
		public sealed class ObjectData { public Header Header; public byte[] Bytes; }

		public static string Key(VpeTexturePayload source, string hash, bool normal, int divisor, bool compress, string backend, bool cookSupported = true)
		{
			if (!VpeIntegrity.IsHash(hash)) throw new InvalidDataException("Missing source SHA-256.");
			return VpeIntegrity.Hash(VpeIntegrity.Json(new { Domain = "VPE-TEXTURE-RECIPE", Version, Cook = VpeTextureCookSettings.FormatVersion, Source = hash, source.MimeType, source.PixelFormat, source.Width, source.Height, source.GenerateMipMaps, source.ForceUncompressed, Normal = normal, Divisor = source.ForceUncompressed ? 1 : divisor, Compress = !source.ForceUncompressed && compress, Backend = backend, Compute = cookSupported }));
		}
		public static string ObjectPath(string key)
		{
			if (!VpeIntegrity.IsHash(key)) throw new InvalidDataException("Invalid texture object key.");
			return Path.Combine(Root, key.Substring(0, 2), key + ".vpto");
		}

		public static ObjectData TryRead(string key, bool force = false)
		{
			var path = ObjectPath(key);
			try {
				using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
				var info = new FileInfo(path);
				info.Refresh();
				using var reader = new BinaryReader(stream);
				var length = reader.ReadInt32();
				if (length < 1 || length > 16384) throw new InvalidDataException("Invalid texture header length.");
				var bytes = reader.ReadBytes(length);
				if (bytes.Length != length) throw new EndOfStreamException();
				var header = VpeIntegrity.Parse<Header>(bytes, 16384);
				Validate(header, key);
				var identity = VpeIntegrity.Hash(bytes);
				if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(64)) != identity) throw new InvalidDataException("Texture header checksum mismatch.");
				var storedLength = reader.ReadInt32();
				if (storedLength <= 0 || storedLength > MaxObjectBytes || stream.Length - stream.Position != storedLength) throw new InvalidDataException("Truncated texture object.");
				var payload = reader.ReadBytes(storedLength);
				if (payload.Length != storedLength) throw new EndOfStreamException();
				if (header.Compressed) {
					var expanded = new byte[header.Length];
					using var decompressor = new Decompressor();
					var written = decompressor.Unwrap(payload, expanded);
					if (written != expanded.Length) throw new InvalidDataException("Invalid decompressed texture length.");
					payload = expanded;
				}
				if (payload.Length != header.Length) throw new InvalidDataException("Texture payload length mismatch.");
				if (force || !VpeIntegrity.ReceiptMatches(path, info, identity, "textures")) {
					if (VpeIntegrity.Hash(payload) != header.Digest) throw new InvalidDataException("Texture checksum mismatch.");
					VpeIntegrity.EnsureUnchanged(path, info);
					VpeIntegrity.SaveReceipt(path, info, identity, "textures");
				}
				lock (Gate) Recency[path] = DateTime.UtcNow;
				return new ObjectData { Header = header, Bytes = payload };
			} catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException || e is ArgumentException || e is ZstdException) {
				VpeIntegrity.InvalidateReceipt(path, "textures");
				Logger.Debug(e, "Texture object unavailable: {Key}", key);
				return null;
			}
		}

		private static void Validate(Header header, string key)
		{
			if (header.Version != Version || header.Key != key || !VpeIntegrity.IsHash(header.Digest) || header.Length <= 0 || header.Length > MaxObjectBytes) throw new InvalidDataException("Invalid texture object header.");
			if (string.IsNullOrEmpty(header.Format)) return; // Encoded passthrough is intentionally distinct from raw GPU output.
			if (header.Width < 1 || header.Height < 1 || header.Width > 32768 || header.Height > 32768 || header.Mips < 1 || header.Mips > 16) throw new InvalidDataException("Invalid GPU texture dimensions.");
			if (header.Format != VpePixelFormats.Bc7 && header.Format != VpePixelFormats.Rgba32) throw new InvalidDataException("Unsupported cached pixel format.");
			long expected = 0; var w = header.Width; var h = header.Height;
			for (var i = 0; i < header.Mips; i++) { expected += header.Format == VpePixelFormats.Bc7 ? (long)((w + 3) / 4) * ((h + 3) / 4) * 16 : (long)w * h * 4; w = System.Math.Max(1, w / 2); h = System.Math.Max(1, h / 2); }
			if (expected != header.Length) throw new InvalidDataException("GPU texture layout mismatch.");
		}

		public static void Write(string key, VpeTexturePayload output, byte[] data, bool normalPacked)
		{
			if (data.Length > MaxObjectBytes) return;
			var header = new Header { Key = key, Width = output.Width, Height = output.Height, Mips = output.MipCount, Length = data.Length, Format = output.PixelFormat, Mime = output.MimeType, NormalPacked = normalPacked, Digest = VpeIntegrity.Hash(data), Compressed = VpeTextureCache.CompressPayloadOnWrite };
			Validate(header, key);
			var payload = data;
			if (header.Compressed) { using var compressor = new Compressor(3); payload = compressor.Wrap(data).ToArray(); }
			var path = ObjectPath(key);
			lock (Gate) {
				if (TryRead(key) != null) return;
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
				try {
					using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
						using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
						var bytes = VpeIntegrity.Json(header);
						writer.Write(bytes.Length); writer.Write(bytes); writer.Write(System.Text.Encoding.ASCII.GetBytes(VpeIntegrity.Hash(bytes))); writer.Write(payload.Length); writer.Write(payload); writer.Flush(); stream.Flush(true);
					}
					VpeIntegrity.Publish(temp, path);
					if (TryRead(key, true) == null) throw new IOException("Cannot verify the written texture object.");
				} finally { if (File.Exists(temp)) File.Delete(temp); }
			}
		}

		/// <summary>Runs only as explicit maintenance or after cache creation, never a warm-load prerequisite.</summary>
		public static void Trim() => TrimTo(QuotaBytes);
		public static void Clear() => TrimTo(0);

		public static int VerifyAll(CancellationToken ct = default)
		{
			if (!Directory.Exists(Root)) return 0;
			var failures = 0;
			foreach (var path in Directory.EnumerateFiles(Root, "*.vpto", SearchOption.AllDirectories)) {
				ct.ThrowIfCancellationRequested();
				var key = Path.GetFileNameWithoutExtension(path);
				if (VpeIntegrity.IsHash(key) && TryRead(key, true) == null) failures++;
			}
			return failures;
		}

		private static void TrimTo(long quota)
		{
			lock (Gate) {
				if (!Directory.Exists(Root)) return;
				foreach (var temp in new DirectoryInfo(Root).EnumerateFiles("*.tmp", SearchOption.AllDirectories).Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1))) {
					try { temp.Delete(); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
				}
				var files = new DirectoryInfo(Root).EnumerateFiles("*.vpto", SearchOption.AllDirectories).ToArray();
				var total = files.Sum(f => f.Length);
				foreach (var file in files.OrderBy(f => Recency.TryGetValue(f.FullName, out var used) ? used : f.LastWriteTimeUtc)) {
					if (total <= quota) break;
					try { var length = file.Length; file.Delete(); total -= length; Recency.Remove(file.FullName); VpeIntegrity.InvalidateReceipt(file.FullName, "textures"); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
				}
			}
		}

		public static async Task<Dictionary<string, Texture2D>> LoadAsync(IPackageFolder table, VpeMaterialsPayload payload, VpeIntegrity.Manifest integrity, int divisor, bool compress, CancellationToken ct, Action<int, int> progress = null)
		{
			SplitNormalUses(payload);
			var normals = VpeTextureCook.CollectNormalTextureIds(payload);
			var digests = integrity.Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
			var backend = SystemInfo.graphicsDeviceType + ":" + SystemInfo.graphicsDeviceVendorID;
			compress &= SystemInfo.SupportsTextureFormat(TextureFormat.BC7);
			var cookSupported = VpeTextureCook.IsSupported;
			var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
			var normalPacked = new List<string>();
			var wrote = false;
			var writtenKeys = new HashSet<string>(StringComparer.Ordinal);
			var requests = (payload.Textures ?? Array.Empty<VpeTexture>()).Where(t => t != null && !t.EditorOnly).Select(source => {
				if (!digests.TryGetValue(PackageApi.TableFolder + "/" + PackageApi.TexturesFolder + "/" + source.FileName, out var digest)) throw new InvalidDataException("Texture missing from integrity manifest.");
				var descriptor = VpeTextureSources.ToPayload(source);
				return (Source: source, Descriptor: descriptor, Digest: digest, Key: Key(descriptor, digest.Sha256, normals.Contains(source.Id), divisor, compress, backend, cookSupported));
			}).ToArray();
			// A batch amortizes Unity's main-thread continuation over several cache reads.
			// Only the current batch and one worker batch are retained, never the whole table.
			var keys = requests.Select(request => request.Key).ToArray();
			Task<List<ObjectData>> next = keys.Length == 0 ? null : Task.Run(() => ReadBatch(keys, 0, ct), ct);
			List<ObjectData> batch = null;
			var batchStart = 0;
			var uploadSlice = System.Diagnostics.Stopwatch.StartNew();
			try {
				for (var i = 0; i < requests.Length; i++) {
					ct.ThrowIfCancellationRequested();
					var (source, descriptor, digest, key) = requests[i];
					if (batch == null || i == batchStart + batch.Count) {
						// Waiting for I/O already gives the UI a frame; it must not consume the upload budget too.
						uploadSlice.Stop();
						batch = await next;
						uploadSlice.Start();
						batchStart = i;
						var following = i + batch.Count;
						next = following < requests.Length ? Task.Run(() => ReadBatch(keys, following, ct), ct) : null;
					}
					var cached = batch[i - batchStart];
					batch[i - batchStart] = null; // Release consumed CPU payloads while the next batch reads.
					// Read-ahead can see a duplicate before its first occurrence has populated the cache.
					if (cached == null && writtenKeys.Contains(key)) {
						uploadSlice.Stop();
						cached = await Task.Run(() => TryRead(key), ct);
						uploadSlice.Start();
					}
					byte[] data;
					if (cached != null) {
						data = cached.Bytes;
						descriptor.Width = cached.Header.Width; descriptor.Height = cached.Header.Height; descriptor.MipCount = cached.Header.Mips;
						descriptor.PixelFormat = cached.Header.Format; descriptor.MimeType = cached.Header.Mime;
						if (cached.Header.NormalPacked) normalPacked.Add(source.Id);
					} else {
						data = await Task.Run(() => {
							using var stream = table.GetFolder(PackageApi.TexturesFolder).GetFile(source.FileName).AsStream();
							var bytes = VpeIntegrity.ReadBounded(stream, MaxObjectBytes);
							if (bytes.LongLength != digest.Length || VpeIntegrity.Hash(bytes) != digest.Sha256) throw new InvalidDataException("Texture source checksum mismatch: " + source.FileName);
							return bytes;
						}, ct);
						descriptor.ByteOffset = 0; descriptor.ByteLength = data.Length;
						VpeTextureCook.Result cooked = null;
						try { if (cookSupported) cooked = await VpeTextureCook.CookAsync(new[] { descriptor }, normals, data, null, ct, divisor, compress); }
						catch (Exception e) when (!(e is OperationCanceledException) && !(e is OutOfMemoryException)) { Logger.Warn(e, "Texture cook failed; using encoded source {Texture}", source.FileName); }
						var packed = cooked?.Manifest.AgPackedNormalIds.Contains(source.Id) == true;
						if (cooked != null) { descriptor = cooked.Manifest.Textures.Single(); data = cooked.CookedData; }
						if (packed) normalPacked.Add(source.Id);
						// Encoded fallbacks are cached too. Compute availability is part of the recipe,
						// so a later supported backend does not inherit an unsupported backend's fallback.
						{
							try { await Task.Run(() => Write(key, descriptor, data, packed), ct); writtenKeys.Add(key); wrote = true; }
							catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException) { Logger.Warn(e, "Cannot persist shared texture cache."); }
						}
					}
					descriptor.ByteOffset = 0; descriptor.ByteLength = data.Length;
					using var provider = new VpeMaterialReader.TextureProvider(new[] { descriptor }, data);
					Texture2D texture = null;
					try { texture = provider.Get(source.Id); }
					catch (Exception e) when (!(e is OperationCanceledException) && !(e is OutOfMemoryException)) { Logger.Warn(e, "Cannot upload texture {Texture}; continuing without it.", source.FileName); }
					textures.Add(source.Id, texture);
					progress?.Invoke(i + 1, requests.Length);
					if (uploadSlice.ElapsedMilliseconds >= 8) { await Task.Yield(); uploadSlice.Restart(); }
				}
				VpeTextureCook.RewriteNormalRefs(payload, new VpeTextureCache.Manifest { AgPackedNormalIds = normalPacked.ToArray() });
				if (wrote) {
					try { await Task.Run(Trim, ct); }
					catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Logger.Warn(e, "Cannot trim shared texture cache."); }
				}
				return textures;
			} catch {
				// Observe the bounded read-ahead task before releasing this load attempt.
				if (next != null) { try { await next; } catch { /* Preserve the original failure. */ } }
				foreach (var texture in textures.Values) if (texture) { if (Application.isPlaying) UnityEngine.Object.Destroy(texture); else UnityEngine.Object.DestroyImmediate(texture); }
				throw;
			}
		}

		// Stop after 16 objects or 32 MiB. A single object may cross the byte budget, but
		// TryRead caps it at MaxObjectBytes. Even oversized inputs retain at most two bounded batches.
		internal static List<ObjectData> ReadBatch(IReadOnlyList<string> keys, int start, CancellationToken ct)
		{
			var batch = new List<ObjectData>(16);
			long bytes = 0;
			while (start + batch.Count < keys.Count && batch.Count < 16 && bytes < 32L * 1024 * 1024) {
				ct.ThrowIfCancellationRequested();
				var data = TryRead(keys[start + batch.Count]);
				batch.Add(data); // Keep misses in position so the main thread can cook the right source.
				bytes += data?.Bytes.LongLength ?? 0;
			}
			return batch;
		}

		private static void SplitNormalUses(VpeMaterialsPayload payload)
		{
			var textures = (payload.Textures ?? Array.Empty<VpeTexture>()).ToList();
			var copies = new Dictionary<string, string>(StringComparer.Ordinal);
			string NormalId(string id) {
				if (string.IsNullOrEmpty(id)) return id;
				if (copies.TryGetValue(id, out var copyId)) return copyId;
				var source = textures.SingleOrDefault(t => t.Id == id) ?? throw new InvalidDataException("Unknown normal texture ID.");
				var copy = PackageApi.Packer.Unpack<VpeTexture>(PackageApi.Packer.Pack(source));
				copy.Id = id + ":normal";
				while (textures.Any(t => t.Id == copy.Id)) copy.Id += ":normal";
				textures.Add(copy); copies.Add(id, copy.Id); return copy.Id;
			}
			foreach (var profile in payload.Profiles ?? Array.Empty<VpeMaterialProfile>()) {
				if (profile?.Lit?.NormalMap != null) profile.Lit.NormalMap.TextureId = NormalId(profile.Lit.NormalMap.TextureId);
				if (profile?.Decal?.NormalMap != null) profile.Decal.NormalMap.TextureId = NormalId(profile.Decal.NormalMap.TextureId);
				foreach (var insert in new[] { profile?.InsertHybrid, profile?.InsertCover })
					foreach (var property in insert?.Properties ?? Array.Empty<VpeInsertShaderProperty>())
						if (property?.Texture != null && property.Name is "_NormalMap" or "_CoverNormalMap") property.Texture.TextureId = NormalId(property.Texture.TextureId);
			}
			var used = new HashSet<string>(((JContainer)JToken.Parse(System.Text.Encoding.UTF8.GetString(PackageApi.Packer.Pack(payload.Profiles ?? Array.Empty<VpeMaterialProfile>())))).DescendantsAndSelf().OfType<JProperty>().Where(p => p.Name == "TextureId").Select(p => (string)p.Value), StringComparer.Ordinal);
			payload.Textures = textures.Where(t => used.Contains(t.Id)).ToArray();
		}
	}
}
