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
using System.Text.RegularExpressions;

namespace VisualPinball.Unity
{
	public enum PackagedContentLintSeverity
	{
		Warning,
		Error,
	}

	public sealed class PackagedContentLintIssue
	{
		public string Code { get; }
		public PackagedContentLintSeverity Severity { get; }
		public string Message { get; }
		public string Path { get; }

		public PackagedContentLintIssue(string code, PackagedContentLintSeverity severity, string message, string path = null)
		{
			Code = code;
			Severity = severity;
			Message = message;
			Path = path;
		}

		public override string ToString() => string.IsNullOrEmpty(Path)
			? $"{Severity} {Code}: {Message}"
			: $"{Severity} {Code} ({Path}): {Message}";
	}

	public static class PackagedContentValidator
	{
		private static readonly Regex Sha256 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
		private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase) {
			".app", ".bat", ".cmd", ".com", ".dll", ".dylib", ".exe", ".msi", ".ps1", ".scr", ".so",
		};

		/// <summary>
		/// Returns actionable authoring diagnostics in addition to the normative manifest validation.
		/// Consumers choose whether their bundle kind requires an entry point or forbids executable payloads.
		/// </summary>
		public static IReadOnlyList<PackagedContentLintIssue> LintManifest(PackagedContentManifest manifest,
			PackagedContentRef contentRef, bool requireEntryPoint = false, bool forbidExecutables = false,
			long maxFileBytes = PackagedContentLimits.DefaultMaxBundleBytes,
			int maxFileCount = PackagedContentLimits.DefaultMaxFileCount,
			long maxBundleBytes = PackagedContentLimits.DefaultMaxBundleBytes)
		{
			var issues = new List<PackagedContentLintIssue>();
			try {
				ValidateReference(contentRef);
				ValidateManifest(manifest, contentRef, maxFileCount, maxBundleBytes);
			} catch (Exception ex) when (ex is InvalidDataException or OverflowException) {
				issues.Add(new PackagedContentLintIssue("CONTENT_MANIFEST_INVALID", PackagedContentLintSeverity.Error, ex.Message));
			}

			if (manifest == null) {
				return issues;
			}
			if (requireEntryPoint && string.IsNullOrWhiteSpace(manifest.EntryPoint)) {
				issues.Add(new PackagedContentLintIssue("CONTENT_ENTRY_POINT_REQUIRED", PackagedContentLintSeverity.Error,
					$"Content kind '{manifest.Kind ?? "<null>"}' requires an entryPoint."));
			}

			var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var file in manifest.Files ?? new List<PackagedContentFile>()) {
				if (file == null) {
					issues.Add(new PackagedContentLintIssue("CONTENT_FILE_NULL", PackagedContentLintSeverity.Error,
						"The content manifest contains a null file entry."));
					continue;
				}
				if (file.Size > maxFileBytes) {
					issues.Add(new PackagedContentLintIssue("CONTENT_FILE_OVERSIZED", PackagedContentLintSeverity.Error,
						$"File is {file.Size:N0} bytes; the configured per-file limit is {maxFileBytes:N0} bytes.", file.Path));
				}
				if (forbidExecutables && ExecutableExtensions.Contains(System.IO.Path.GetExtension(file.Path ?? string.Empty))) {
					issues.Add(new PackagedContentLintIssue("CONTENT_EXECUTABLE_FORBIDDEN", PackagedContentLintSeverity.Error,
						"Executable payloads are forbidden for this content kind. Use player-owned runtimes and dependencies.", file.Path));
				}
				if (!string.IsNullOrEmpty(file.Sha256) && hashes.TryGetValue(file.Sha256, out var firstPath)) {
					issues.Add(new PackagedContentLintIssue("CONTENT_DUPLICATE_PAYLOAD", PackagedContentLintSeverity.Warning,
						$"File duplicates '{firstPath}' byte-for-byte; remove one copy or reference the shared asset.", file.Path));
				} else if (!string.IsNullOrEmpty(file.Sha256)) {
					hashes[file.Sha256] = file.Path;
				}
			}
			return issues;
		}

		/// <summary>Validates every content manifest and payload in a package without extracting it.</summary>
		public static IReadOnlyList<string> ValidatePackage(string packagePath,
			int maxFileCount = PackagedContentLimits.DefaultMaxFileCount,
			long maxBundleBytes = PackagedContentLimits.DefaultMaxBundleBytes)
		{
			var errors = new List<string>();
			try {
				using var storage = PackageApi.StorageManager.OpenStorage(packagePath);
				var table = storage.GetFolder(PackageApi.TableFolder);
				if (!table.TryGetFolder(PackageApi.ContentFolder, out var content)) {
					return errors;
				}
				content.VisitFolders(bundle => {
					try {
						if (!Regex.IsMatch(bundle.Name, "^[0-9a-f]{16}$")) {
							throw new InvalidDataException($"Content folder id '{bundle.Name}' is not 16 lowercase hexadecimal characters.");
						}
						if (!bundle.TryGetFile("manifest", out var manifestFile, PackageApi.Packer.FileExtension)) {
							throw new InvalidDataException($"Content bundle '{bundle.Name}' is missing manifest.json.");
						}
						PackagedContentManifest manifest;
						try {
							manifest = PackageApi.Packer.Unpack<PackagedContentManifest>(manifestFile.GetData());
						} catch (Exception ex) {
							throw new InvalidDataException($"Content bundle '{bundle.Name}' has a corrupt manifest.json.", ex);
						}
						var contentRef = new PackagedContentRef(bundle.Name, manifest.Kind, manifest.EntryPoint, manifest.ContentHash);
						ValidateReference(contentRef);
						ValidateManifest(manifest, contentRef, maxFileCount, maxBundleBytes);
						if (!bundle.TryGetFolder("files", out var filesFolder)) {
							throw new InvalidDataException($"Content bundle '{bundle.Name}' is missing its files directory.");
						}
						foreach (var entry in manifest.Files) {
							var file = FindFile(filesFolder, entry.Path);
							using var stream = file.AsStream();
							if (stream.CanSeek && stream.Length != entry.Size) {
								throw new InvalidDataException($"Content file '{entry.Path}' in '{bundle.Name}' has {stream.Length:N0} bytes; manifest declares {entry.Size:N0}.");
							}
							using var sha = System.Security.Cryptography.SHA256.Create();
							long actualBytes = 0;
							var buffer = new byte[128 * 1024];
							int read;
							while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) {
								actualBytes += read;
								sha.TransformBlock(buffer, 0, read, null, 0);
							}
							sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
							if (actualBytes != entry.Size) {
								throw new InvalidDataException($"Content file '{entry.Path}' in '{bundle.Name}' has {actualBytes:N0} bytes; manifest declares {entry.Size:N0}.");
							}
							var hash = sha.Hash;
							if (!string.Equals(PackagedContentWriter.ToHex(hash), entry.Sha256, StringComparison.Ordinal)) {
								throw new InvalidDataException($"Content file '{entry.Path}' in '{bundle.Name}' failed SHA-256 verification.");
							}
						}
					} catch (Exception ex) {
						var detail = ex.InnerException == null ? ex.Message : $"{ex.Message} {ex.InnerException.Message}";
						errors.Add(detail);
					}
				});
			} catch (Exception ex) {
				errors.Add($"Cannot validate package '{packagePath}': {ex.Message}");
			}
			return errors;
		}

		public static void ValidateReference(PackagedContentRef contentRef)
		{
			if (string.IsNullOrWhiteSpace(contentRef.Id) || !Regex.IsMatch(contentRef.Id, "^[0-9a-f]{16}$")) {
				throw new InvalidDataException($"Content reference id '{contentRef.Id}' must be 16 lowercase hexadecimal characters.");
			}
			if (string.IsNullOrWhiteSpace(contentRef.Kind)) {
				throw new InvalidDataException("Content reference has no kind.");
			}
			if (string.IsNullOrWhiteSpace(contentRef.ContentHash) || !Sha256.IsMatch(contentRef.ContentHash)) {
				throw new InvalidDataException("Content reference hash must be a 64-character lowercase SHA-256 value.");
			}
			if (!contentRef.ContentHash.StartsWith(contentRef.Id, StringComparison.Ordinal)) {
				throw new InvalidDataException("Content reference id does not match its content hash.");
			}
			if (!string.IsNullOrEmpty(contentRef.EntryPoint)) {
				PackagedContentPath.ValidateRelative(contentRef.EntryPoint, "content entry point");
			}
		}

		public static void ValidateManifest(PackagedContentManifest manifest, PackagedContentRef contentRef,
			int maxFileCount = PackagedContentLimits.DefaultMaxFileCount,
			long maxBundleBytes = PackagedContentLimits.DefaultMaxBundleBytes)
		{
			if (manifest == null) {
				throw new InvalidDataException("Content manifest is empty.");
			}
			if (manifest.Format != "vpe-content" || manifest.Version != 1) {
				throw new InvalidDataException($"Unsupported content manifest '{manifest.Format}' version {manifest.Version}; expected vpe-content version 1.");
			}
			if (manifest.ContentHash != contentRef.ContentHash || manifest.Kind != contentRef.Kind || manifest.EntryPoint != contentRef.EntryPoint) {
				throw new InvalidDataException($"Content manifest for '{contentRef.Id}' does not match its PackagedContentRef.");
			}
			if (!Sha256.IsMatch(manifest.ContentHash ?? string.Empty)) {
				throw new InvalidDataException("Content manifest has an invalid contentHash.");
			}
			if (manifest.FileCount < 0 || manifest.FileCount > maxFileCount) {
				throw new InvalidDataException($"Content manifest declares {manifest.FileCount:N0} files; limit is {maxFileCount:N0}.");
			}
			if (manifest.TotalBytes < 0 || manifest.TotalBytes > maxBundleBytes) {
				throw new InvalidDataException($"Content manifest declares {manifest.TotalBytes:N0} bytes; limit is {maxBundleBytes:N0} bytes.");
			}
			if (manifest.Files == null || manifest.Files.Count != manifest.FileCount) {
				throw new InvalidDataException("Content manifest fileCount does not match its files list.");
			}

			long totalBytes = 0;
			var exactPaths = new HashSet<string>(StringComparer.Ordinal);
			var casePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var file in manifest.Files) {
				PackagedContentPath.ValidateRelative(file.Path);
				if (!exactPaths.Add(file.Path)) {
					throw new InvalidDataException($"Content manifest contains duplicate path '{file.Path}'.");
				}
				if (!casePaths.Add(file.Path)) {
					throw new InvalidDataException($"Content manifest contains paths that differ only by case at '{file.Path}'.");
				}
				if (file.Size < 0 || !Sha256.IsMatch(file.Sha256 ?? string.Empty)) {
					throw new InvalidDataException($"Content manifest entry '{file.Path}' has an invalid size or SHA-256.");
				}
				checked { totalBytes += file.Size; }
			}
			if (totalBytes != manifest.TotalBytes) {
				throw new InvalidDataException($"Content manifest totalBytes is {manifest.TotalBytes}; file list totals {totalBytes}.");
			}
			if (!string.IsNullOrEmpty(manifest.EntryPoint) && !exactPaths.Contains(manifest.EntryPoint)) {
				throw new InvalidDataException($"Content entry point '{manifest.EntryPoint}' is not in the files list.");
			}
			if (!IsCanonicalOrder(manifest.Files)) {
				throw new InvalidDataException("Content manifest files are not in deterministic ordinal path order.");
			}
			var canonicalHash = ComputeCanonicalHash(manifest.Files);
			if (canonicalHash != manifest.ContentHash) {
				throw new InvalidDataException("Content manifest contentHash does not match its canonical file list.");
			}
		}

		private static bool IsCanonicalOrder(IReadOnlyList<PackagedContentFile> files)
		{
			for (var i = 1; i < files.Count; i++) {
				if (string.CompareOrdinal(files[i - 1].Path, files[i].Path) >= 0) {
					return false;
				}
			}
			return true;
		}

		private static string ComputeCanonicalHash(IEnumerable<PackagedContentFile> files)
		{
			using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
			foreach (var file in files) {
				hash.AppendData(System.Text.Encoding.UTF8.GetBytes(file.Path));
				hash.AppendData(new byte[] { 0 });
				hash.AppendData(HexToBytes(file.Sha256));
				hash.AppendData(new byte[] { (byte)'\n' });
			}
			return PackagedContentWriter.ToHex(hash.GetHashAndReset());
		}

		private static byte[] HexToBytes(string value)
		{
			var bytes = new byte[value.Length / 2];
			for (var i = 0; i < bytes.Length; i++) {
				bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
			}
			return bytes;
		}

		private static IPackageFile FindFile(IPackageFolder root, string path)
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
	}
}
