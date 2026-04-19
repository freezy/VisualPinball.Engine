// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Lightweight metadata extracted from a packaged .vpe table.
	/// </summary>
	public sealed class VpeTableMetadataSummary
	{
		public string Title { get; set; }
		public string Manufacturer { get; set; }
		public int? OriginalReleaseYear { get; set; }
		public IReadOnlyList<string> PrimaryAuthors { get; set; } = Array.Empty<string>();
	}

	/// <summary>
	/// Reads table metadata from <c>table/table.json</c> inside a .vpe package.
	/// </summary>
	public static class VpeTableMetadataReader
	{
		private const string TableMetadataEntryPath = "table/table.json";
		private const int DefaultMaxDegreeOfParallelism = 6;
		private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

		private sealed class CacheEntry
		{
			public long FileSizeBytes { get; set; }
			public long LastWriteTimeUtcTicks { get; set; }
			public bool HasMetadata { get; set; }
			public VpeTableMetadataSummary Metadata { get; set; }
		}

		public static bool TryRead(string vpePath, out VpeTableMetadataSummary metadata)
		{
			metadata = null;

			if (string.IsNullOrWhiteSpace(vpePath)) {
				return false;
			}

			var fileInfo = new FileInfo(vpePath);
			if (!fileInfo.Exists) {
				return false;
			}

			if (TryReadFromCache(fileInfo, out metadata, out var hasMetadata)) {
				return hasMetadata;
			}

			try {
				using var zip = new ZipFile(File.OpenRead(fileInfo.FullName));
				var entry = FindMetadataEntry(zip);
				if (entry == null) {
					UpdateCache(fileInfo, false, null);
					return false;
				}

				using var stream = zip.GetInputStream(entry);
				using var reader = new StreamReader(stream);
				using var jsonReader = new JsonTextReader(reader);
				var tableMetadata = JsonSerializer.CreateDefault().Deserialize<TableMetadata>(jsonReader);
				if (tableMetadata == null) {
					return false;
				}

				var primaryAuthors = tableMetadata.PrimaryAuthors?
					.Select(author => author?.Name ?? author?.VpuHandle)
					.Where(name => !string.IsNullOrWhiteSpace(name))
					.ToArray() ?? Array.Empty<string>();

				metadata = new VpeTableMetadataSummary {
					Title = tableMetadata.TableName?.Trim(),
					Manufacturer = tableMetadata.Manufacturer?.Trim(),
					OriginalReleaseYear = tableMetadata.OriginalReleaseYear > 0 ? tableMetadata.OriginalReleaseYear : null,
					PrimaryAuthors = primaryAuthors
				};
				UpdateCache(fileInfo, true, metadata);

				return true;

			} catch {
				UpdateCache(fileInfo, false, null);
				return false;
			}
		}

		public static IReadOnlyDictionary<string, VpeTableMetadataSummary> ReadMany(IEnumerable<string> vpePaths, int maxDegreeOfParallelism = 0)
		{
			if (vpePaths == null) {
				return new Dictionary<string, VpeTableMetadataSummary>(StringComparer.OrdinalIgnoreCase);
			}

			var files = vpePaths
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Select(path => new FileInfo(path))
				.Where(file => file.Exists)
				.ToArray();

			return ReadMany(files, maxDegreeOfParallelism);
		}

		public static IReadOnlyDictionary<string, VpeTableMetadataSummary> ReadMany(IEnumerable<FileInfo> vpeFiles, int maxDegreeOfParallelism = 0)
		{
			if (vpeFiles == null) {
				return new Dictionary<string, VpeTableMetadataSummary>(StringComparer.OrdinalIgnoreCase);
			}

			var result = new ConcurrentDictionary<string, VpeTableMetadataSummary>(StringComparer.OrdinalIgnoreCase);
			var options = new ParallelOptions {
				MaxDegreeOfParallelism = maxDegreeOfParallelism > 0
					? maxDegreeOfParallelism
					: System.Math.Min(DefaultMaxDegreeOfParallelism, System.Math.Max(1, Environment.ProcessorCount - 1))
			};

			Parallel.ForEach(vpeFiles, options, fileInfo => {
				if (TryRead(fileInfo.FullName, out var metadata)) {
					result[fileInfo.FullName] = metadata;
				}
			});

			return result;
		}

		private static bool TryReadFromCache(FileInfo fileInfo, out VpeTableMetadataSummary metadata, out bool hasMetadata)
		{
			metadata = null;
			hasMetadata = false;

			if (!Cache.TryGetValue(fileInfo.FullName, out var cacheEntry)) {
				return false;
			}

			if (cacheEntry.FileSizeBytes != fileInfo.Length || cacheEntry.LastWriteTimeUtcTicks != fileInfo.LastWriteTimeUtc.Ticks) {
				return false;
			}

			hasMetadata = cacheEntry.HasMetadata;
			metadata = cacheEntry.Metadata;
			return true;
		}

		private static void UpdateCache(FileInfo fileInfo, bool hasMetadata, VpeTableMetadataSummary metadata)
		{
			Cache[fileInfo.FullName] = new CacheEntry {
				FileSizeBytes = fileInfo.Length,
				LastWriteTimeUtcTicks = fileInfo.LastWriteTimeUtc.Ticks,
				HasMetadata = hasMetadata,
				Metadata = metadata
			};
		}

		private static ZipEntry FindMetadataEntry(ZipFile zip)
		{
			var exact = zip.GetEntry(TableMetadataEntryPath);
			if (exact != null) {
				return exact;
			}

			foreach (ZipEntry entry in zip) {
				if (!entry.IsDirectory && string.Equals(entry.Name, TableMetadataEntryPath, StringComparison.OrdinalIgnoreCase)) {
					return entry;
				}
			}

			return null;
		}
	}
}
