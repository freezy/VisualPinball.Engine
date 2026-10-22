// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VisualPinball.Unity
{
	/// <summary>Portable entry identities and persisted local verification. Receipts are change detectors, not signatures.</summary>
	public static class VpeIntegrity
	{
		public const string ManifestName = "integrity.json";
		public const int MaxManifestBytes = 16 * 1024 * 1024;
		public const int MaxEntries = 65536;
		public const long MaxEntryBytes = 16L * 1024 * 1024 * 1024;
		public const long MaxPackageBytes = 64L * 1024 * 1024 * 1024;
		public static string CacheRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisualPinball", "VPE");
		public static Action<long> BytesHashed;
		private static readonly UTF8Encoding Utf8 = new(false, true);
		private static readonly JsonSerializerSettings Settings = new() { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None };

		[Serializable]
		public sealed class Entry
		{
			[JsonProperty(Required = Required.Always)] public string Path;
			[JsonProperty(Required = Required.Always)] public long Length;
			[JsonProperty(Required = Required.Always)] public string Sha256;
		}

		[Serializable]
		public sealed class Manifest
		{
			[JsonProperty(Required = Required.Always)] public int Version = 1;
			[JsonProperty(Required = Required.Always)] public List<Entry> Entries = new();
		}

		private sealed class Receipt
		{
			[JsonProperty(Required = Required.Always)] public int Version = 1;
			[JsonProperty(Required = Required.Always)] public long Length;
			public long Modified;
			public long Created;
			public string Identity;
		}

		public static string Hash(byte[] bytes)
		{
			using var stream = new MemoryStream(bytes, false);
			return Hash(stream, bytes.LongLength);
		}

		public static string Hash(Stream stream, long expectedLength, CancellationToken ct = default, Action<int> readProgress = null)
		{
			using var hash = SHA256.Create();
			var buffer = new byte[(int)System.Math.Max(1, System.Math.Min(128 * 1024, expectedLength))];
			long count = 0;
			int read;
			while ((read = stream.Read(buffer, 0, buffer.Length)) != 0) {
				ct.ThrowIfCancellationRequested();
				count += read;
				if (count > expectedLength) throw new InvalidDataException("Entry exceeds its declared length.");
				hash.TransformBlock(buffer, 0, read, buffer, 0);
				readProgress?.Invoke(read);
			}
			if (count != expectedLength) throw new InvalidDataException("Truncated entry.");
			hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			BytesHashed?.Invoke(count);
			return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
		}

		public static bool IsHash(string hash) => hash != null && hash.Length == 64 && hash.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
		public static byte[] Json(object value) => Utf8.GetBytes(JsonConvert.SerializeObject(value, Formatting.None, Settings));
		public static T Parse<T>(byte[] bytes, int limit = MaxManifestBytes)
		{
			if (bytes.Length > limit) throw new InvalidDataException("JSON exceeds its size limit.");
			using var reader = new JsonTextReader(new StringReader(Utf8.GetString(bytes))) { MaxDepth = 64, DateParseHandling = DateParseHandling.None };
			var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
			if (reader.Read()) throw new InvalidDataException("Trailing JSON content.");
			return token.ToObject<T>(JsonSerializer.Create(Settings)) ?? throw new InvalidDataException("Missing JSON value.");
		}

		public static byte[] ReadBounded(Stream stream, int max)
		{
			using var output = new MemoryStream();
			var buffer = new byte[32768];
			int read;
			while ((read = stream.Read(buffer, 0, buffer.Length)) != 0) {
				if (output.Length + read > max) throw new InvalidDataException("Entry exceeds its size limit.");
				output.Write(buffer, 0, read);
			}
			return output.ToArray();
		}

		public static void ValidatePath(string path)
		{
			if (string.IsNullOrEmpty(path) || path.Length > 4096 || path.Split('/').Any(p => p.Length == 0 || p == "." || p == ".." || p.EndsWith(".") || p.EndsWith(" ") || p.Any(c => c < 32 || "\\:*?\"<>|".Contains(c))))
				throw new InvalidDataException("Invalid package entry path.");
		}

		public static Manifest Verify(ZipArchive zip, string path, bool force = false, CancellationToken ct = default, Action<long, long> progress = null)
		{
			try { return VerifyCore(zip, path, force, ct, progress); }
			catch (Exception e) when (!(e is OperationCanceledException)) { InvalidateReceipt(path, "packages"); throw; }
		}

		private static Manifest VerifyCore(ZipArchive zip, string path, bool force, CancellationToken ct, Action<long, long> progress)
		{
			var initial = new FileInfo(path);
			initial.Refresh();
			var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
			var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var nodes = new Dictionary<string, (string Path, bool Directory)>(StringComparer.OrdinalIgnoreCase);
			if (zip.Entries.Count > MaxEntries) throw new InvalidDataException("Too many package entries.");
			void Register(string name, bool directory) {
				if (nodes.TryGetValue(name, out var previous)) {
					if (previous.Path != name || previous.Directory != directory) throw new InvalidDataException("Conflicting package paths.");
				} else nodes.Add(name, (name, directory));
			}
			long total = 0;
			foreach (var entry in zip.Entries) {
				ct.ThrowIfCancellationRequested();
				var directoryEntry = entry.FullName.EndsWith("/", StringComparison.Ordinal);
				var name = directoryEntry ? entry.FullName.Substring(0, entry.FullName.Length - 1) : entry.FullName;
				ValidatePath(name);
				var parts = name.Split('/');
				for (var i = 1; i < parts.Length; i++) Register(string.Join("/", parts.Take(i)), true);
				Register(name, directoryEntry);
				if (directoryEntry) {
					if (entry.Length != 0 || !names.Add(entry.FullName)) throw new InvalidDataException("Invalid directory entry.");
					continue;
				}
				if (!names.Add(entry.FullName) || files.Count >= MaxEntries || entry.Length < 0 || entry.Length > MaxEntryBytes)
					throw new InvalidDataException("Duplicate or oversized package entry.");
				total = checked(total + entry.Length);
				if (total > MaxPackageBytes) throw new InvalidDataException("Package exceeds its expanded size limit.");
				files.Add(entry.FullName, entry);
			}
			if (!files.TryGetValue(ManifestName, out var manifestEntry)) throw new InvalidDataException("VPE integrity manifest is required. Re-export this development package.");
			byte[] bytes;
			using (var stream = manifestEntry.Open()) bytes = ReadBounded(stream, MaxManifestBytes);
			var manifest = Parse<Manifest>(bytes);
			if (manifest.Version != 1 || manifest.Entries == null || manifest.Entries.Count != files.Count - 1) throw new InvalidDataException("Invalid integrity manifest.");
			var listed = new HashSet<string>(StringComparer.Ordinal);
			foreach (var entry in manifest.Entries) {
				if (entry == null || entry.Path == ManifestName || !listed.Add(entry.Path) || !IsHash(entry.Sha256) || !files.TryGetValue(entry.Path, out var file) || entry.Length != file.Length)
					throw new InvalidDataException("Integrity manifest does not match the package directory.");
			}
			var directory = string.Join("\n", files.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "\0" + p.Value.Length + "\0" + p.Value.CompressedLength + "\0" + p.Value.Crc32));
			var identity = Hash(bytes) + Hash(Utf8.GetBytes(directory));
			if (!force && ReceiptMatches(path, initial, identity, "packages")) return manifest;
			long verified = 0;
			var payloadBytes = total - manifestEntry.Length;
			var progressTimer = System.Diagnostics.Stopwatch.StartNew();
			foreach (var entry in manifest.Entries) {
				using var stream = files[entry.Path].Open();
				if (Hash(stream, entry.Length, ct, count => { verified += count; if (progressTimer.ElapsedMilliseconds >= 100 || verified == payloadBytes) { progress?.Invoke(verified, payloadBytes); progressTimer.Restart(); } }) != entry.Sha256) throw new InvalidDataException($"Package checksum mismatch: {entry.Path}");
			}
			ct.ThrowIfCancellationRequested();
			EnsureUnchanged(path, initial);
			SaveReceipt(path, initial, identity, "packages");
			return manifest;
		}

		public static void VerifyFile(string path)
		{
			using var file = File.OpenRead(path);
			using var zip = new ZipArchive(file, ZipArchiveMode.Read);
			Verify(zip, path, true);
		}

		public static Manifest ReadManifest(IPackageStorage storage)
		{
			if (!storage.TryGetFile(ManifestName, out var file)) throw new InvalidDataException("Missing integrity manifest.");
			using var stream = file.AsStream();
			return Parse<Manifest>(ReadBounded(stream, MaxManifestBytes));
		}

		public static void EnsureUnchanged(string path, FileInfo before)
		{
			var after = new FileInfo(path);
			if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc || before.CreationTimeUtc != after.CreationTimeUtc)
				throw new IOException("File changed during verification.");
		}

		private static string ReceiptPath(string path, string kind) => Path.Combine(CacheRoot, "verification", kind, Hash(Utf8.GetBytes(Path.GetFullPath(path))) + ".json");
		public static bool ReceiptMatches(string path, FileInfo info, string identity, string kind)
		{
			try {
				using var file = File.OpenRead(ReceiptPath(path, kind));
				var envelope = Parse<string[]>(ReadBounded(file, 16384), 16384);
				if (envelope.Length != 2 || envelope[0] == null || Hash(Utf8.GetBytes(envelope[0])) != envelope[1]) return false;
				var receipt = Parse<Receipt>(Utf8.GetBytes(envelope[0]));
				return receipt.Version == 1 && receipt.Length == info.Length && receipt.Modified == info.LastWriteTimeUtc.Ticks && receipt.Created == info.CreationTimeUtc.Ticks && receipt.Identity == identity;
			} catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is JsonException || e is ArgumentException) { return false; }
		}

		public static void SaveReceipt(string path, FileInfo info, string identity, string kind)
		{
			try {
				var bytes = Json(new Receipt { Length = info.Length, Modified = info.LastWriteTimeUtc.Ticks, Created = info.CreationTimeUtc.Ticks, Identity = identity });
				AtomicWrite(ReceiptPath(path, kind), Json(new[] { Utf8.GetString(bytes), Hash(bytes) }));
			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { /* Read-only cache: verification still succeeded. */ }
		}

		public static void InvalidateReceipt(string path, string kind)
		{
			try { File.Delete(ReceiptPath(path, kind)); }
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
		}

		public static void AtomicWrite(string path, byte[] data)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
			var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try {
				using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(data, 0, data.Length); file.Flush(true); }
				Publish(temp, path);
			} finally { if (File.Exists(temp)) File.Delete(temp); }
		}

		public static void Publish(string temp, string path)
		{
			if (File.Exists(path)) File.Replace(temp, path, null);
			else File.Move(temp, path);
		}
	}
}
