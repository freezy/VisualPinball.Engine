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
using System.Text.RegularExpressions;

namespace VisualPinball.Unity
{
	/// <summary>Deterministically ingests a directory into table/content.</summary>
	public sealed class PackagedContentWriter
	{
		private readonly IPackageFolder _tableFolder;

		public PackagedContentWriter(IPackageFolder tableFolder)
		{
			_tableFolder = tableFolder ?? throw new ArgumentNullException(nameof(tableFolder));
		}

		public PackagedContentRef AddDirectory(string kind, string sourceRoot, ContentPackOptions options = null)
		{
			var prepared = PrepareDirectory(kind, sourceRoot, options);
			Write(prepared);
			return prepared.Reference;
		}

		public static PreparedContent PrepareDirectory(string kind, string sourceRoot, ContentPackOptions options = null)
		{
			if (string.IsNullOrWhiteSpace(kind)) {
				throw new ArgumentException("A content kind is required.", nameof(kind));
			}
			if (string.IsNullOrWhiteSpace(sourceRoot)) {
				throw new ArgumentException("A source directory is required.", nameof(sourceRoot));
			}

			options ??= new ContentPackOptions();
			var root = Path.GetFullPath(sourceRoot);
			if (!Directory.Exists(root)) {
				throw new DirectoryNotFoundException($"Content source directory does not exist: {root}");
			}

			var files = EnumerateFiles(root, options)
				.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
				.ToList();
			if (files.Count > options.MaxFileCount) {
				throw new InvalidDataException($"Content directory has {files.Count:N0} files; the configured limit is {options.MaxFileCount:N0}.");
			}

			long totalBytes = 0;
			var caseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			using var canonicalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			foreach (var file in files) {
				if (!caseNames.Add(file.RelativePath)) {
					throw new InvalidDataException($"Content contains duplicate paths that differ only by case: '{file.RelativePath}'. Rename one file for cross-platform packages.");
				}

				// Re-check for a reparse point swapped in after enumeration. Enumeration skips links,
				// but a source tree mutated mid-export could redirect this entry outside sourceRoot
				// before it is measured and hashed, which would otherwise package external bytes. The
				// hash captured here also guards the later write: bytes that change afterwards fail the
				// verification in Write().
				if (IsLink(file.FullPath)) {
					throw new InvalidDataException($"Content file '{file.RelativePath}' became a symbolic link after enumeration; aborting to avoid packaging content from outside the source directory.");
				}

				file.Size = new FileInfo(file.FullPath).Length;
				checked { totalBytes += file.Size; }
				if (totalBytes > options.MaxTotalBytes) {
					throw new InvalidDataException($"Content directory is {totalBytes:N0} bytes; the configured limit is {options.MaxTotalBytes:N0} bytes.");
				}
				file.Hash = ComputeFileHash(file.FullPath);
				var pathBytes = Encoding.UTF8.GetBytes(file.RelativePath);
				canonicalHash.AppendData(pathBytes);
				canonicalHash.AppendData(new byte[] { 0 });
				canonicalHash.AppendData(file.Hash);
				canonicalHash.AppendData(new byte[] { (byte)'\n' });
			}

			var contentHash = ToHex(canonicalHash.GetHashAndReset());
			var entryPoint = string.IsNullOrWhiteSpace(options.EntryPoint)
				? null
				: PackagedContentPath.ValidateRelative(options.EntryPoint, "content entry point");
			if (entryPoint != null && files.All(file => file.RelativePath != entryPoint)) {
				throw new InvalidDataException($"Content entry point '{entryPoint}' is not an included file.");
			}

			var id = contentHash.Substring(0, 16);
			var contentRef = new PackagedContentRef(id, kind, entryPoint, contentHash);
#if UNITY_EDITOR
			contentRef.SourceDirectory = root;
			contentRef.FileCount = files.Count;
			contentRef.TotalBytes = totalBytes;
			contentRef.ValidationStatus = "Valid";
#endif
			var manifest = new PackagedContentManifest {
				Format = "vpe-content",
				Version = 1,
				Kind = kind,
				EntryPoint = entryPoint,
				FileCount = files.Count,
				TotalBytes = totalBytes,
				ContentHash = contentHash,
				Files = files.Select(file => new PackagedContentFile {
					Path = file.RelativePath,
					Size = file.Size,
					Sha256 = ToHex(file.Hash),
				}).ToList(),
			};
			return new PreparedContent(root, files, manifest, contentRef);
		}

		public void Write(PreparedContent prepared)
		{
			var contentFolder = GetOrAddFolder(_tableFolder, PackageApi.ContentFolder);
			if (contentFolder.TryGetFolder(prepared.Reference.Id, out var existing)) {
				if (!existing.TryGetFile("manifest", out var manifestFile, PackageApi.Packer.FileExtension)) {
					throw new InvalidDataException($"Content id collision at '{prepared.Reference.Id}': existing bundle has no manifest.");
				}
				var existingManifest = PackageApi.Packer.Unpack<PackagedContentManifest>(manifestFile.GetData());
				if (existingManifest.ContentHash != prepared.Reference.ContentHash) {
					throw new InvalidDataException($"Content id collision at '{prepared.Reference.Id}'.");
				}
				return;
			}

			var bundleFolder = contentFolder.AddFolder(prepared.Reference.Id);
			bundleFolder.AddFile("manifest", PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(prepared.Manifest));
			var filesFolder = bundleFolder.AddFolder("files");
			foreach (var file in prepared.Files) {
				var targetFolder = filesFolder;
				var parts = file.RelativePath.Split('/');
				for (var i = 0; i < parts.Length - 1; i++) {
					targetFolder = GetOrAddFolder(targetFolder, parts[i]);
				}

				using var source = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
				using var target = targetFolder.AddFile(parts[^1]).AsStream();
				using var sha = SHA256.Create();
				var buffer = new byte[128 * 1024];
				int read;
				while ((read = source.Read(buffer, 0, buffer.Length)) > 0) {
					target.Write(buffer, 0, read);
					sha.TransformBlock(buffer, 0, read, null, 0);
				}
				sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
				if (!sha.Hash.SequenceEqual(file.Hash)) {
					throw new IOException($"Content file changed while the package was being written: {file.RelativePath}");
				}
			}
		}

		private static IEnumerable<PreparedFile> EnumerateFiles(string root, ContentPackOptions options)
		{
			var pending = new Stack<string>();
			pending.Push(root);
			while (pending.Count > 0) {
				var directory = pending.Pop();
				foreach (var childDirectory in Directory.EnumerateDirectories(directory).OrderBy(path => path, StringComparer.Ordinal)) {
					if (IsLink(childDirectory)) {
						options.Warning?.Invoke($"Skipping symbolic link or junction '{childDirectory}'.");
						continue;
					}
					pending.Push(childDirectory);
				}
				foreach (var fullPath in Directory.EnumerateFiles(directory).OrderBy(path => path, StringComparer.Ordinal)) {
					if (IsLink(fullPath)) {
						options.Warning?.Invoke($"Skipping symbolic link '{fullPath}'.");
						continue;
					}
					var relative = Path.GetRelativePath(root, fullPath);
					if (Path.DirectorySeparatorChar == '\\') {
						relative = relative.Replace('\\', '/');
					}
					relative = PackagedContentPath.ValidateRelative(relative, "source-relative content path");
					if (MatchesOptions(relative, options)) {
						yield return new PreparedFile(fullPath, relative);
					}
				}
			}
		}

		private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

		private static bool MatchesOptions(string path, ContentPackOptions options)
		{
			var included = options.IncludeGlobs == null || options.IncludeGlobs.Length == 0 ||
				options.IncludeGlobs.Any(glob => GlobMatches(path, glob));
			return included && (options.ExcludeGlobs == null || !options.ExcludeGlobs.Any(glob => GlobMatches(path, glob)));
		}

		internal static bool GlobMatches(string path, string glob)
		{
			if (string.IsNullOrWhiteSpace(glob)) {
				return false;
			}
			glob = glob.Replace('\\', '/');
			var pattern = "^" + Regex.Escape(glob)
				.Replace(@"\*\*/", "(?:.*/)?")
				.Replace(@"\*\*", ".*")
				.Replace(@"\*", "[^/]*")
				.Replace(@"\?", "[^/]") + "$";
			return Regex.IsMatch(path, pattern, RegexOptions.CultureInvariant);
		}

		private static byte[] ComputeFileHash(string path)
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var sha = SHA256.Create();
			return sha.ComputeHash(stream);
		}

		internal static string ToHex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

		private static IPackageFolder GetOrAddFolder(IPackageFolder parent, string name) =>
			parent.TryGetFolder(name, out var folder) ? folder : parent.AddFolder(name);

		public sealed class PreparedContent
		{
			public readonly string Root;
			public readonly List<PreparedFile> Files;
			public readonly PackagedContentManifest Manifest;
			public readonly PackagedContentRef Reference;

			public PreparedContent(string root, List<PreparedFile> files, PackagedContentManifest manifest, PackagedContentRef reference)
			{
				Root = root;
				Files = files;
				Manifest = manifest;
				Reference = reference;
			}
		}

		public sealed class PreparedFile
		{
			public readonly string FullPath;
			public readonly string RelativePath;
			public long Size;
			public byte[] Hash;

			public PreparedFile(string fullPath, string relativePath)
			{
				FullPath = fullPath;
				RelativePath = relativePath;
			}
		}
	}
}
