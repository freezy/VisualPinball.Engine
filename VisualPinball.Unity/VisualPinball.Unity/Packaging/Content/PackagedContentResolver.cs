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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Editor- and Player-safe resolver for content stored in a .vpe package.</summary>
	public sealed class PackagedContentResolver : IPackagedContentResolver
	{
		private const string CompleteMarker = ".complete";

		// Temp directories younger than this are assumed to belong to a concurrent extraction and are
		// left alone; older ones are orphans from a crashed run and are safe to reap on startup.
		private static readonly TimeSpan TemporaryDirectoryStaleAfter = TimeSpan.FromHours(1);

		private readonly string _packagePath;
		private readonly PackagedContentCacheOptions _options;
		private readonly string _cacheRoot;

		public PackagedContentResolver(string packagePath, PackagedContentCacheOptions options = null)
		{
			_packagePath = Path.GetFullPath(packagePath ?? throw new ArgumentNullException(nameof(packagePath)));
			_options = options ?? new PackagedContentCacheOptions();
			_cacheRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(_options.CacheRoot)
				? Path.Combine(Application.persistentDataPath, "ContentCache")
				: _options.CacheRoot);
			Directory.CreateDirectory(_cacheRoot);
			CleanupTemporaryDirectories();
		}

		public PackagedContentRef AddDirectory(string kind, string sourceRoot, ContentPackOptions options)
		{
			throw new NotSupportedException("This resolver is read-only. Use PackagedContentWriter while creating a package.");
		}

		public async Task<string> ResolveAsync(PackagedContentRef contentRef, IProgress<float> progress, CancellationToken ct)
		{
			PackagedContentValidator.ValidateReference(contentRef);
			if (!File.Exists(_packagePath)) {
				throw new FileNotFoundException("The .vpe package containing this content does not exist.", _packagePath);
			}

			var destination = Path.Combine(_cacheRoot, contentRef.ContentHash);
			if (IsComplete(destination, contentRef.ContentHash)) {
				Directory.SetLastWriteTimeUtc(destination, DateTime.UtcNow);
				progress?.Report(1f);
				return destination;
			}

			var temporary = Path.Combine(_cacheRoot, contentRef.ContentHash + ".tmp-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(temporary);
			try {
				using var storage = PackageApi.StorageManager.OpenStorage(_packagePath);
				var bundleFolder = GetBundleFolder(storage, contentRef.Id);
				var manifest = ReadManifest(bundleFolder, contentRef.Id);
				PackagedContentValidator.ValidateManifest(manifest, contentRef, _options.MaxFileCount, _options.MaxBundleBytes);
				var filesFolder = bundleFolder.GetFolder("files");
				long writtenBytes = 0;
				foreach (var entry in manifest.Files.OrderBy(file => file.Path, StringComparer.Ordinal)) {
					ct.ThrowIfCancellationRequested();
					var targetPath = PackagedContentPath.GetContainedPath(temporary, entry.Path);
					Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
					var packageFile = GetFile(filesFolder, entry.Path);
					using var source = packageFile.AsStream();
					using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true);
					using var sha = SHA256.Create();
					var buffer = new byte[128 * 1024];
					long fileBytes = 0;
					while (true) {
						var read = await source.ReadAsync(buffer, 0, buffer.Length, ct);
						if (read == 0) {
							break;
						}
						await target.WriteAsync(buffer, 0, read, ct);
						sha.TransformBlock(buffer, 0, read, null, 0);
						fileBytes += read;
						writtenBytes += read;
						if (fileBytes > entry.Size || writtenBytes > manifest.TotalBytes) {
							throw new InvalidDataException($"Content file '{entry.Path}' is larger than declared in its manifest.");
						}
						progress?.Report(manifest.TotalBytes == 0 ? 1f : System.Math.Min(1f, writtenBytes / (float)manifest.TotalBytes));
					}
					sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
					if (fileBytes != entry.Size) {
						throw new InvalidDataException($"Content file '{entry.Path}' has {fileBytes} bytes; manifest declares {entry.Size}.");
					}
					if (!string.Equals(PackagedContentWriter.ToHex(sha.Hash), entry.Sha256, StringComparison.Ordinal)) {
						throw new InvalidDataException($"Content file '{entry.Path}' failed SHA-256 verification.");
					}
				}

				ct.ThrowIfCancellationRequested();
				File.WriteAllText(Path.Combine(temporary, CompleteMarker), contentRef.ContentHash, new UTF8Encoding(false));
				Publish(temporary, destination, contentRef.ContentHash);
				progress?.Report(1f);
				TrimCache(contentRef.ContentHash);
				return destination;
			} catch {
				TryDeleteDirectory(temporary);
				throw;
			}
		}

		private static IPackageFolder GetBundleFolder(IPackageStorage storage, string id)
		{
			var table = storage.GetFolder(PackageApi.TableFolder);
			if (!table.TryGetFolder(PackageApi.ContentFolder, out var content) || !content.TryGetFolder(id, out var bundle)) {
				throw new InvalidDataException($"Package does not contain referenced content bundle '{id}'.");
			}
			return bundle;
		}

		private static PackagedContentManifest ReadManifest(IPackageFolder bundle, string id)
		{
			if (!bundle.TryGetFile("manifest", out var file, PackageApi.Packer.FileExtension)) {
				throw new InvalidDataException($"Content bundle '{id}' is missing manifest.json.");
			}
			try {
				return PackageApi.Packer.Unpack<PackagedContentManifest>(file.GetData());
			} catch (Exception ex) {
				throw new InvalidDataException($"Content bundle '{id}' has a corrupt manifest.json.", ex);
			}
		}

		private static IPackageFile GetFile(IPackageFolder root, string path)
		{
			var parts = PackagedContentPath.ValidateRelative(path).Split('/');
			var folder = root;
			for (var i = 0; i < parts.Length - 1; i++) {
				if (!folder.TryGetFolder(parts[i], out folder)) {
					throw new InvalidDataException($"Content file '{path}' is missing from the package.");
				}
			}
			if (!folder.TryGetFile(parts[^1], out var file)) {
				throw new InvalidDataException($"Content file '{path}' is missing from the package.");
			}
			return file;
		}

		private static void Publish(string temporary, string destination, string hash)
		{
			if (Directory.Exists(destination)) {
				if (IsComplete(destination, hash)) {
					TryDeleteDirectory(temporary);
					return;
				}
				TryDeleteDirectory(destination);
			}
			try {
				Directory.Move(temporary, destination);
			} catch (IOException) when (IsComplete(destination, hash)) {
				TryDeleteDirectory(temporary);
			}
		}

		private static bool IsComplete(string directory, string hash)
		{
			try {
				var marker = Path.Combine(directory, CompleteMarker);
				return Directory.Exists(directory) && File.Exists(marker) && File.ReadAllText(marker).Trim() == hash;
			} catch {
				return false;
			}
		}

		private void CleanupTemporaryDirectories()
		{
			// Only reap orphans left by a previous run. Resolvers share the default cache root, so a
			// second resolver may be constructed while another is still extracting into its own
			// GUID-suffixed temp directory. An in-progress extraction keeps its directory young
			// (creating entries touches the directory's write time), so an age guard avoids deleting
			// a tree another resolver is still publishing, on file systems where an open handle does
			// not block the delete.
			var now = DateTime.UtcNow;
			foreach (var directory in Directory.EnumerateDirectories(_cacheRoot, "*.tmp-*", SearchOption.TopDirectoryOnly)) {
				DateTime lastWriteUtc;
				try {
					lastWriteUtc = Directory.GetLastWriteTimeUtc(directory);
				} catch {
					continue;
				}
				if (now - lastWriteUtc < TemporaryDirectoryStaleAfter) {
					continue;
				}
				TryDeleteDirectory(directory);
			}
		}

		private void TrimCache(string protectedHash)
		{
			if (_options.CapacityBytes < 0) {
				return;
			}
			var entries = Directory.EnumerateDirectories(_cacheRoot)
				.Where(path => Path.GetFileName(path).IndexOf(".tmp-", StringComparison.Ordinal) < 0)
				.Select(path => new CacheEntry(path, GetDirectorySize(path), Directory.GetLastWriteTimeUtc(path)))
				.OrderBy(entry => entry.LastAccessUtc)
				.ToList();
			var total = entries.Sum(entry => entry.Size);
			foreach (var entry in entries) {
				if (total <= _options.CapacityBytes) {
					break;
				}
				if (Path.GetFileName(entry.Path) == protectedHash) {
					continue;
				}
				TryDeleteDirectory(entry.Path);
				total -= entry.Size;
			}
		}

		private static long GetDirectorySize(string directory)
		{
			try {
				return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
			} catch {
				return 0;
			}
		}

		private static void TryDeleteDirectory(string directory)
		{
			try {
				if (Directory.Exists(directory)) {
					Directory.Delete(directory, true);
				}
			} catch (IOException) {
				// A second resolver/process may still be publishing or reading this directory.
			} catch (UnauthorizedAccessException) {
				// Best-effort cache maintenance must not prevent content from loading.
			}
		}

		private readonly struct CacheEntry
		{
			public readonly string Path;
			public readonly long Size;
			public readonly DateTime LastAccessUtc;

			public CacheEntry(string path, long size, DateTime lastAccessUtc)
			{
				Path = path;
				Size = size;
				LastAccessUtc = lastAccessUtc;
			}
		}
	}
}
