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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public sealed class FuturePinballExtractionOptions
	{
		public bool CopyOriginalTable { get; set; } = true;
		public bool OverwriteChangedFiles { get; set; }
		public bool SearchAdjacentLibraries { get; set; } = true;
		public bool ExtractOpaqueRecords { get; set; } = true;
		public IReadOnlyList<string> LibrarySearchRoots { get; set; } = Array.Empty<string>();
		public FuturePinballReaderOptions ReaderOptions { get; set; } = new FuturePinballReaderOptions();
	}

	public static class FuturePinballExtractor
	{
		private const uint NameTag = 0xA4F4D1D7;
		private const uint TypeTag = 0xA4F1B9D1;
		private const uint LinkedTag = 0x9EF3C6D9;
		private const uint LinkedPathTag = 0xA1EDD1D5;
		private const uint DataTag = 0xA8EDD1E1;
		private const uint ScriptTag = 0x4F5A4C7A;
		private const uint ListItemsTag = 0xA8EDD1E1;

		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
		private static readonly HashSet<char> InvalidFileNameCharacters = new HashSet<char>("<>:\"/\\|?*");

		public static FuturePinballExtractionManifest Extract(
			string tablePath,
			string outputDirectory,
			FuturePinballExtractionOptions options = null)
		{
			if (tablePath == null) throw new ArgumentNullException(nameof(tablePath));
			if (outputDirectory == null) throw new ArgumentNullException(nameof(outputDirectory));
			options ??= new FuturePinballExtractionOptions();
			var sourcePath = Path.GetFullPath(tablePath);
			var outputRoot = Path.GetFullPath(outputDirectory);
			Directory.CreateDirectory(outputRoot);

			var table = FuturePinballTableReader.Load(sourcePath, options.ReaderOptions);
			var issues = new List<string>(table.Issues);
			var manifestStreams = ExtractStreams(outputRoot, table.Streams, options);
			var libraries = LoadLibraries(sourcePath, options, issues);
			var resources = new List<FuturePinballManifestResource>();
			resources.AddRange(ExtractResources(outputRoot, table.Images, "images", libraries, options, issues));
			resources.AddRange(ExtractResources(outputRoot, table.Sounds, "sounds", libraries, options, issues));
			resources.AddRange(ExtractResources(outputRoot, table.Music, "music", libraries, options, issues));
			resources.AddRange(ExtractPinModels(outputRoot, table.PinModels, libraries, options, issues));
			resources.AddRange(ExtractDmdFonts(outputRoot, table.DmdFonts, options));
			resources.AddRange(ExtractLists(table.ImageLists, "image-lists"));
			resources.AddRange(ExtractLists(table.LightLists, "light-lists"));
			ExtractScript(outputRoot, table, resources, options);
			AddReferrers(table.Streams, resources);

			var originalFile = options.CopyOriginalTable
				? $"original/{SafeName(Path.GetFileName(sourcePath), "table.fpt")}" : null;
			if (originalFile != null) {
				WriteFile(outputRoot, originalFile, File.ReadAllBytes(sourcePath), options);
			}

			var manifest = new FuturePinballExtractionManifest {
				SourceFileName = Path.GetFileName(sourcePath),
				SourceBytes = new FileInfo(sourcePath).Length,
				SourceSha256 = Sha256(sourcePath),
				OriginalFile = originalFile,
				FileVersion = table.FileVersion,
				CompoundEntryCount = table.CompoundEntryCount,
				Counts = new FuturePinballManifestCounts {
					Elements = table.Elements.Count,
					Images = table.Images.Count,
					Sounds = table.Sounds.Count,
					Music = table.Music.Count,
					PinModels = table.PinModels.Count,
					DmdFonts = table.DmdFonts.Count,
					ImageLists = table.ImageLists.Count,
					LightLists = table.LightLists.Count,
					OpaqueRecords = manifestStreams.Sum(stream => stream.Records.Count(record => record.OpaqueFile != null)),
					UnresolvedLinkedResources = resources.Count(resource => resource.Linked && resource.ResolutionStatus == "unresolved")
				},
				Streams = manifestStreams,
				Resources = resources,
				Issues = issues
			};

			var tableData = manifestStreams.FirstOrDefault(stream => stream.Kind == FuturePinballStreamKind.TableData.ToString());
			if (tableData != null) {
				WriteText(outputRoot, "table/table-data.json", FuturePinballManifestJson.SerializeTableData(tableData), options);
			}
			var elements = manifestStreams.Where(stream => stream.Kind == FuturePinballStreamKind.TableElement.ToString()).ToArray();
			WriteText(outputRoot, "table/elements.json", FuturePinballManifestJson.SerializeElements(elements), options);
			WriteText(outputRoot, "manifest.json", FuturePinballManifestJson.Serialize(manifest), options);
			return manifest;
		}

		private static List<FuturePinballManifestStream> ExtractStreams(
			string outputRoot,
			IReadOnlyList<FuturePinballSourceStream> streams,
			FuturePinballExtractionOptions options)
		{
			var result = new List<FuturePinballManifestStream>(streams.Count);
			foreach (var stream in streams) {
				var index = stream.SourceIndex.HasValue ? stream.SourceIndex.Value.ToString("D5") + "-" : string.Empty;
				var category = stream.Kind.ToString().ToLowerInvariant();
				var rawPath = $"streams/{category}/{index}{SafeName(stream.Name, "stream")}.bin";
				WriteFile(outputRoot, rawPath, stream.RawData, options);

				var records = new List<FuturePinballManifestRecord>();
				for (var i = 0; i < stream.Records.Count; i++) {
					var record = stream.Records[i];
					string opaquePath = null;
					if (options.ExtractOpaqueRecords && record.ValueKind == FuturePinballValueKind.Opaque) {
						opaquePath = $"opaque/{category}/{index}{SafeName(stream.Name, "stream")}-{i:D4}-{record.OriginalTag:X8}.bin";
						WriteFile(outputRoot, opaquePath, record.Payload.ToArray(), options);
					}
					records.Add(new FuturePinballManifestRecord {
						Offset = record.Offset,
						StoredLength = record.StoredLength,
						ConsumedLength = record.ConsumedLength,
						OriginalTag = $"0x{record.OriginalTag:X8}",
						CanonicalTag = $"0x{record.CanonicalTag:X8}",
						Name = record.Name,
						ValueKind = record.ValueKind.ToString(),
						Value = DisplayValue(record.Value),
						PayloadBytes = record.Payload.Length,
						PayloadSha256 = Sha256(record.Payload.Span),
						OpaqueFile = opaquePath
					});
				}
				result.Add(new FuturePinballManifestStream {
					Name = stream.Name,
					Kind = stream.Kind.ToString(),
					SourceIndex = stream.SourceIndex,
					ElementTypeId = stream.ElementTypeId,
					ElementType = stream.ElementType?.ToString(),
					Bytes = stream.RawData.Length,
					Sha256 = Sha256(stream.RawData),
					RawFile = rawPath,
					Records = records
				});
			}
			return result;
		}

		private static IEnumerable<FuturePinballManifestResource> ExtractResources(
			string outputRoot,
			IEnumerable<FuturePinballSourceStream> streams,
			string category,
			IReadOnlyList<FuturePinballLibrary> libraries,
			FuturePinballExtractionOptions options,
			ICollection<string> issues)
		{
			foreach (var stream in streams) {
				var logicalName = stream.Text(NameTag) ?? $"{category}-{stream.SourceIndex}";
				var linked = (stream.Integer(LinkedTag) ?? 0) != 0;
				var linkedPath = stream.Text(LinkedPathTag);
				var data = (stream.FirstRecord(DataTag)?.Value as FuturePinballCompressedData)?.DecodedBytes;
				FuturePinballLibrary library = null;
				FuturePinballLibraryEntry entry = null;
				if (data == null && linked) {
					FindLibraryResource(libraries, logicalName, linkedPath, out library, out entry);
					data = entry?.Data?.DecodedBytes;
				}

				var files = new List<FuturePinballManifestFile>();
				var format = data == null ? null : DetectFormat(data, stream.Integer(TypeTag), linkedPath);
				if (data != null) {
					var path = $"media/{category}/{stream.SourceIndex:D5}-{SafeName(logicalName, category)}.{Extension(format)}";
					WriteFile(outputRoot, path, data, options);
					files.Add(ManifestFile("original", path, data));
				}
				if (entry != null) ExtractLibraryStreams(outputRoot, library, entry, files, options);
				var status = data != null ? (entry == null ? "embedded" : "resolved") : (linked ? "unresolved" : "missing-data");
				if (status == "unresolved") {
					issues.Add($"Could not resolve linked {category} resource '{logicalName}' ({linkedPath ?? "no linked path"}).");
				}
				yield return new FuturePinballManifestResource {
					Category = category,
					SourceIndex = stream.SourceIndex,
					SourceStream = stream.Name,
					LogicalName = logicalName,
					DeclaredType = stream.Integer(TypeTag),
					Linked = linked,
					LinkedPath = linkedPath,
					ResolutionStatus = status,
					ResolvedLibrary = library == null ? null : Path.GetFileName(library.SourcePath),
					ResolvedLibraryEntry = entry?.Name,
					DetectedFormat = format,
					Files = files
				};
			}
		}

		private static IEnumerable<FuturePinballManifestResource> ExtractPinModels(
			string outputRoot,
			IEnumerable<FuturePinballSourceStream> streams,
			IReadOnlyList<FuturePinballLibrary> libraries,
			FuturePinballExtractionOptions options,
			ICollection<string> issues)
		{
			foreach (var stream in streams) {
				var logicalName = stream.Text(NameTag) ?? $"model-{stream.SourceIndex}";
				var linked = (stream.Integer(LinkedTag) ?? 0) != 0;
				var linkedPath = stream.Text(LinkedPathTag);
				var files = new List<FuturePinballManifestFile>();
				foreach (var record in stream.Records.Where(record => record.Value is FuturePinballCompressedData)) {
					var data = ((FuturePinballCompressedData)record.Value).DecodedBytes;
					if (data == null || data.Length == 0) continue;
					var format = DetectFormat(data, null, null);
					var path = $"models/{stream.SourceIndex:D5}-{SafeName(logicalName, "model")}/{SafeName(record.Name, "data")}.{Extension(format)}";
					WriteFile(outputRoot, path, data, options);
					files.Add(ManifestFile(record.Name ?? "data", path, data));
				}

				FuturePinballLibrary library = null;
				FuturePinballLibraryEntry entry = null;
				string detectedFormat = files.Count == 0 ? null : Path.GetExtension(files[0].Path).TrimStart('.');
				if (files.Count == 0 && linked) {
					FindLibraryResource(libraries, logicalName, linkedPath, out library, out entry);
					var data = entry?.Data?.DecodedBytes;
					if (data != null) {
						var format = DetectFormat(data, null, linkedPath);
						detectedFormat = format;
						var path = $"models/{stream.SourceIndex:D5}-{SafeName(logicalName, "model")}/linked.{Extension(format)}";
						WriteFile(outputRoot, path, data, options);
						files.Add(ManifestFile("linked-model", path, data));
					}
				}
				if (entry != null) ExtractLibraryStreams(outputRoot, library, entry, files, options);

				var status = files.Count > 0 ? (entry == null ? "embedded" : "resolved") : (linked ? "unresolved" : "missing-data");
				if (status == "unresolved") {
					issues.Add($"Could not resolve linked model resource '{logicalName}' ({linkedPath ?? "no linked path"}).");
				}
				yield return new FuturePinballManifestResource {
					Category = "models",
					SourceIndex = stream.SourceIndex,
					SourceStream = stream.Name,
					LogicalName = logicalName,
					DeclaredType = stream.Integer(TypeTag),
					Linked = linked,
					LinkedPath = linkedPath,
					ResolutionStatus = status,
					ResolvedLibrary = library == null ? null : Path.GetFileName(library.SourcePath),
					ResolvedLibraryEntry = entry?.Name,
					DetectedFormat = detectedFormat,
					Files = files
				};
			}
		}

		private static IEnumerable<FuturePinballManifestResource> ExtractDmdFonts(
			string outputRoot,
			IEnumerable<FuturePinballSourceStream> streams,
			FuturePinballExtractionOptions options)
		{
			foreach (var stream in streams) {
				var logicalName = $"dmd-font-{stream.SourceIndex}";
				var path = $"media/dmd-fonts/{stream.SourceIndex:D5}-{logicalName}.dmdf";
				WriteFile(outputRoot, path, stream.RawData, options);
				yield return new FuturePinballManifestResource {
					Category = "dmd-fonts",
					SourceIndex = stream.SourceIndex,
					SourceStream = stream.Name,
					LogicalName = logicalName,
					ResolutionStatus = "embedded",
					DetectedFormat = "dmdf",
					Files = new[] { ManifestFile("original", path, stream.RawData) }
				};
			}
		}

		private static IEnumerable<FuturePinballManifestResource> ExtractLists(
			IEnumerable<FuturePinballSourceStream> streams,
			string category)
		{
			foreach (var stream in streams) {
				var logicalName = stream.Text(NameTag) ?? $"{category}-{stream.SourceIndex}";
				var items = stream.FirstRecord(ListItemsTag)?.Value as IReadOnlyList<string> ?? Array.Empty<string>();
				yield return new FuturePinballManifestResource {
					Category = category,
					SourceIndex = stream.SourceIndex,
					SourceStream = stream.Name,
					LogicalName = logicalName,
					ResolutionStatus = "embedded",
					Items = items
				};
			}
		}

		private static void ExtractScript(
			string outputRoot,
			FuturePinballTable table,
			ICollection<FuturePinballManifestResource> resources,
			FuturePinballExtractionOptions options)
		{
			var record = table.TableData?.FirstRecord(ScriptTag);
			var script = record?.Value as FuturePinballCompressedData;
			if (script == null) return;
			const string rawPath = "scripts/table.zlzo";
			const string decodedPath = "scripts/table.vbs";
			WriteFile(outputRoot, rawPath, script.RawBytes.ToArray(), options);
			WriteFile(outputRoot, decodedPath, script.DecodedBytes, options);
			resources.Add(new FuturePinballManifestResource {
				Category = "scripts",
				SourceStream = table.TableData.Name,
				LogicalName = "table",
				ResolutionStatus = "embedded",
				DetectedFormat = "vbs",
				Files = new[] {
					ManifestFile("compressed", rawPath, script.RawBytes.ToArray()),
					ManifestFile("decoded", decodedPath, script.DecodedBytes)
				}
			});
		}

		private static void AddReferrers(
			IReadOnlyList<FuturePinballSourceStream> streams,
			IEnumerable<FuturePinballManifestResource> resources)
		{
			foreach (var resource in resources) {
				if (string.IsNullOrEmpty(resource.LogicalName)) continue;
				resource.Referrers = streams.Where(stream => stream.Records.Any(record => References(record.Value, resource.LogicalName)))
					.Select(stream => stream.Name)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
					.ToArray();
			}
		}

		private static bool References(object value, string logicalName)
		{
			if (value is string text) return text.Equals(logicalName, StringComparison.OrdinalIgnoreCase);
			if (value is IReadOnlyList<string> list) return list.Any(item => item.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
			return false;
		}

		private static IReadOnlyList<FuturePinballLibrary> LoadLibraries(
			string tablePath,
			FuturePinballExtractionOptions options,
			ICollection<string> issues)
		{
			var roots = new List<string>();
			if (options.SearchAdjacentLibraries) roots.Add(Path.GetDirectoryName(tablePath));
			if (options.LibrarySearchRoots != null) roots.AddRange(options.LibrarySearchRoots);
			var files = new List<string>();
			foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase)) {
				try {
					if (Directory.Exists(root)) files.AddRange(Directory.EnumerateFiles(root, "*.fpl", SearchOption.TopDirectoryOnly));
				} catch (IOException exception) {
					issues.Add($"Could not enumerate library root '{root}': {exception.Message}");
				} catch (UnauthorizedAccessException exception) {
					issues.Add($"Could not enumerate library root '{root}': {exception.Message}");
				}
			}
			var libraries = new List<FuturePinballLibrary>();
			foreach (var file in files.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) {
				try {
					libraries.Add(FuturePinballLibraryReader.Load(file, options.ReaderOptions));
				} catch (IOException exception) {
					issues.Add($"Could not read library '{Path.GetFileName(file)}': {exception.Message}");
				} catch (UnauthorizedAccessException exception) {
					issues.Add($"Could not read library '{Path.GetFileName(file)}': {exception.Message}");
				} catch (OpenMcdf.FileFormatException exception) {
					issues.Add($"Could not read library '{Path.GetFileName(file)}': {exception.Message}");
				}
			}
			return libraries;
		}

		private static void FindLibraryResource(
			IReadOnlyList<FuturePinballLibrary> libraries,
			string logicalName,
			string linkedPath,
			out FuturePinballLibrary library,
			out FuturePinballLibraryEntry entry)
		{
			foreach (var candidate in libraries) {
				var match = candidate.Entries.FirstOrDefault(item => item.Name.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
				if (match == null && linkedPath != null) {
					var linkedFileName = FileNameOnly(linkedPath);
					match = candidate.Entries.FirstOrDefault(item =>
						FileNameOnly(item.OriginalPath).Equals(linkedFileName, StringComparison.OrdinalIgnoreCase));
				}
				if (match != null) {
					library = candidate;
					entry = match;
					return;
				}
			}
			library = null;
			entry = null;
		}

		private static string FileNameOnly(string sourcePath)
		{
			if (sourcePath == null) return string.Empty;
			var normalized = sourcePath.Replace('\\', '/');
			return normalized.Substring(normalized.LastIndexOf('/') + 1);
		}

		private static FuturePinballManifestFile ManifestFile(string role, string path, byte[] data)
		{
			return new FuturePinballManifestFile { Role = role, Path = path, Bytes = data.Length, Sha256 = Sha256(data) };
		}

		private static void ExtractLibraryStreams(
			string outputRoot,
			FuturePinballLibrary library,
			FuturePinballLibraryEntry entry,
			ICollection<FuturePinballManifestFile> files,
			FuturePinballExtractionOptions options)
		{
			var libraryName = SafeName(Path.GetFileNameWithoutExtension(library.SourcePath), "library");
			var entryName = SafeName(entry.Name, "entry");
			foreach (var stream in entry.Streams.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
				var path = $"libraries/{libraryName}/{entryName}/{SafeName(stream.Key, "stream")}.bin";
				WriteFile(outputRoot, path, stream.Value, options);
				files.Add(ManifestFile($"library-{stream.Key}", path, stream.Value));
			}
		}

		private static string DisplayValue(object value)
		{
			switch (value) {
				case null: return null;
				case string text: return text;
				case int integer: return integer.ToString(CultureInfo.InvariantCulture);
				case uint color: return $"0x{color:X8}";
				case float number: return number.ToString("R", CultureInfo.InvariantCulture);
				case FuturePinballVector2 vector: return $"{vector.X.ToString("R", CultureInfo.InvariantCulture)},{vector.Y.ToString("R", CultureInfo.InvariantCulture)}";
				case IReadOnlyList<string> strings: return string.Join("|", strings);
				case IReadOnlyList<FuturePinballCollisionShape> shapes: return $"{shapes.Count} collision shapes";
				case FuturePinballCompressedData compressed: return $"{compressed.DecodedBytes?.Length ?? 0} decoded bytes";
				default: return null;
			}
		}

		private static string DetectFormat(byte[] data, int? declaredType, string originalPath)
		{
			if (data.Length >= 2 && data[0] == 'B' && data[1] == 'M') return "bmp";
			if (data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff) return "jpg";
			if (StartsWith(data, "OggS")) return "ogg";
			if (data.Length >= 12 && StartsWith(data, "RIFF") && Encoding.ASCII.GetString(data, 8, 4) == "WAVE") return "wav";
			if (StartsWith(data, "ID3") || data.Length >= 2 && data[0] == 0xff && (data[1] & 0xe0) == 0xe0) return "mp3";
			if (StartsWith(data, "MS3D000000")) return "ms3d";
			if (data.Length >= 8 && data[0] == 0xd0 && data[1] == 0xcf && data[2] == 0x11 && data[3] == 0xe0
				&& data[4] == 0xa1 && data[5] == 0xb1 && data[6] == 0x1a && data[7] == 0xe1) return "fpm";
			if (data.Length >= 18 && Encoding.ASCII.GetString(data, data.Length - 18, 18) == "TRUEVISION-XFILE.\0") return "tga";
			if (declaredType == 15) return "dmdf";
			if (declaredType == 4 && LooksLikeTga(data)) return "tga";
			return "unknown";
		}

		private static bool LooksLikeTga(byte[] data)
		{
			return data.Length >= 18 && (data[2] == 1 || data[2] == 2 || data[2] == 3 || data[2] == 9 || data[2] == 10 || data[2] == 11);
		}

		private static bool StartsWith(byte[] data, string value)
		{
			if (data.Length < value.Length) return false;
			for (var i = 0; i < value.Length; i++) if (data[i] != value[i]) return false;
			return true;
		}

		private static string Extension(string format)
		{
			return string.IsNullOrEmpty(format) || format == "unknown" ? "bin" : format;
		}

		private static string SafeName(string value, string fallback)
		{
			if (string.IsNullOrWhiteSpace(value)) value = fallback;
			var result = new StringBuilder(value.Length);
			foreach (var character in value) {
				result.Append(character < 0x20 || InvalidFileNameCharacters.Contains(character) ? '_' : character);
			}
			var safe = result.ToString().Trim().TrimEnd('.', ' ');
			if (safe.Length == 0) safe = fallback;
			if (safe.Length > 100) safe = safe.Substring(0, 100);
			var stem = Path.GetFileNameWithoutExtension(safe);
			if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
				.Contains(stem, StringComparer.OrdinalIgnoreCase)) safe = "_" + safe;
			return safe;
		}

		private static void WriteText(string outputRoot, string relativePath, string content, FuturePinballExtractionOptions options)
		{
			WriteFile(outputRoot, relativePath, Utf8.GetBytes(content), options);
		}

		private static void WriteFile(string outputRoot, string relativePath, byte[] data, FuturePinballExtractionOptions options)
		{
			var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
			if (Path.IsPathRooted(normalized)) throw new InvalidOperationException($"Extraction path must be relative: {relativePath}");
			var path = Path.GetFullPath(Path.Combine(outputRoot, normalized));
			var rootPrefix = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			if (!path.StartsWith(rootPrefix, comparison)) throw new InvalidOperationException($"Extraction path escapes the output root: {relativePath}");
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			if (File.Exists(path)) {
				var existing = File.ReadAllBytes(path);
				if (existing.SequenceEqual(data)) return;
				if (!options.OverwriteChangedFiles) throw new IOException($"Extraction target already exists with different content: {path}");
			}
			File.WriteAllBytes(path, data);
		}

		private static string Sha256(string path)
		{
			using (var stream = File.OpenRead(path))
			using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(stream));
		}

		private static string Sha256(byte[] data)
		{
			using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(data));
		}

		private static string Sha256(ReadOnlySpan<byte> data)
		{
			return Sha256(data.ToArray());
		}

		private static string Hex(byte[] data)
		{
			return BitConverter.ToString(data).Replace("-", string.Empty).ToLowerInvariant();
		}
	}
}
