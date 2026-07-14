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

using OpenMcdf;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public static class FuturePinballTableReader
	{
		private const uint ElementCountTag = 0x95FDCDD2;
		private const uint ImageCountTag = 0xA2F4C9D2;
		private const uint SoundCountTag = 0xA5F3BFD2;
		private const uint MusicCountTag = 0x96ECC5D2;
		private const uint PinModelCountTag = 0xA5F2C5D2;
		private const uint ImageListCountTag = 0x95F5C9D2;
		private const uint LightListCountTag = 0x95F5C6D2;
		private const uint DmdFontCountTag = 0x9BFBCED2;

		public static FuturePinballTable Load(string fileName, FuturePinballReaderOptions options = null)
		{
			if (fileName == null) {
				throw new ArgumentNullException(nameof(fileName));
			}
			options ??= new FuturePinballReaderOptions();
			using (var root = RootStorage.OpenRead(fileName, StorageModeFlags.None)) {
				var storage = OpenStorage(root, "Future Pinball", fileName);
				var entries = storage.EnumerateEntries().ToArray();
				var streams = new List<FuturePinballSourceStream>();
				foreach (var entry in entries.Where(entry => entry.Type == EntryType.Stream)) {
					if (entry.Length > options.MaximumStreamBytes) {
						throw new FuturePinballFormatException(
							$"Stream length {entry.Length} exceeds the configured limit {options.MaximumStreamBytes}",
							entry.Name
						);
					}
					var data = storage.OpenStream(entry.Name).ReadAll();
					streams.Add(ParseStream(entry.Name, data, options));
				}

				streams.Sort(CompareStreams);
				var issues = new List<string>();
				ReportIndexProblems(streams, issues);
				var table = BuildTable(fileName, entries.Length + 1, streams, issues);
				ValidateDeclaredCounts(table, issues);
				table.Issues = issues;
				return table;
			}
		}

		private static FuturePinballSourceStream ParseStream(
			string name,
			byte[] data,
			FuturePinballReaderOptions options)
		{
			var kind = Classify(name, out var sourceIndex);
			var stream = new FuturePinballSourceStream {
				Name = name,
				SourceIndex = sourceIndex,
				Kind = kind,
				RawData = data
			};

			switch (kind) {
				case FuturePinballStreamKind.FileVersion:
				case FuturePinballStreamKind.TableMac:
				case FuturePinballStreamKind.DmdFont:
				case FuturePinballStreamKind.Unknown:
					return stream;
				case FuturePinballStreamKind.TableElement:
					if (data.Length < sizeof(uint)) {
						throw new FuturePinballFormatException("Table element has no type prefix", name, 0);
					}
					stream.ElementTypeId = ReadUInt32(data, 0);
					if (Enum.IsDefined(typeof(FuturePinballElementType), stream.ElementTypeId.Value)) {
						stream.ElementType = (FuturePinballElementType)stream.ElementTypeId.Value;
					}
					stream.Records = FuturePinballRecordReader.Read(
						data, sizeof(uint), FuturePinballRecordContext.TableElement, options, name
					);
					return stream;
				case FuturePinballStreamKind.TableData:
					stream.Records = FuturePinballRecordReader.Read(
						data, 0, FuturePinballRecordContext.TableData, options, name
					);
					return stream;
				case FuturePinballStreamKind.ImageList:
				case FuturePinballStreamKind.LightList:
					stream.Records = FuturePinballRecordReader.Read(data, 0, FuturePinballRecordContext.List, options, name);
					return stream;
				case FuturePinballStreamKind.PinModel:
					stream.Records = FuturePinballRecordReader.Read(data, 0, FuturePinballRecordContext.PinModel, options, name);
					return stream;
				default:
					stream.Records = FuturePinballRecordReader.Read(data, 0, FuturePinballRecordContext.Resource, options, name);
					return stream;
			}
		}

		private static FuturePinballTable BuildTable(
			string fileName,
			int compoundEntryCount,
			IReadOnlyList<FuturePinballSourceStream> streams,
			ICollection<string> issues)
		{
			var fileVersion = streams.FirstOrDefault(stream => stream.Kind == FuturePinballStreamKind.FileVersion);
			uint? version = null;
			if (fileVersion != null) {
				if (fileVersion.RawData.Length >= sizeof(uint)) {
					version = ReadUInt32(fileVersion.RawData, 0);
				} else {
					issues.Add("File Version stream is shorter than four bytes.");
				}
			}

			return new FuturePinballTable {
				SourcePath = Path.GetFullPath(fileName),
				FileVersion = version,
				CompoundEntryCount = compoundEntryCount,
				Streams = streams,
				TableData = streams.FirstOrDefault(stream => stream.Kind == FuturePinballStreamKind.TableData),
				TableMac = streams.FirstOrDefault(stream => stream.Kind == FuturePinballStreamKind.TableMac),
				Elements = OfKind(streams, FuturePinballStreamKind.TableElement),
				Images = OfKind(streams, FuturePinballStreamKind.Image),
				Sounds = OfKind(streams, FuturePinballStreamKind.Sound),
				Music = OfKind(streams, FuturePinballStreamKind.Music),
				PinModels = OfKind(streams, FuturePinballStreamKind.PinModel),
				DmdFonts = OfKind(streams, FuturePinballStreamKind.DmdFont),
				ImageLists = OfKind(streams, FuturePinballStreamKind.ImageList),
				LightLists = OfKind(streams, FuturePinballStreamKind.LightList)
			};
		}

		private static FuturePinballSourceStream[] OfKind(
			IEnumerable<FuturePinballSourceStream> streams,
			FuturePinballStreamKind kind)
		{
			return streams.Where(stream => stream.Kind == kind).ToArray();
		}

		private static void ValidateDeclaredCounts(FuturePinballTable table, ICollection<string> issues)
		{
			if (table.TableData == null) {
				issues.Add("Table Data stream is missing.");
				return;
			}

			CompareCount(table.TableData, ElementCountTag, table.Elements.Count, "table elements", issues);
			CompareCount(table.TableData, ImageCountTag, table.Images.Count, "images", issues);
			CompareCount(table.TableData, SoundCountTag, table.Sounds.Count, "sounds", issues);
			CompareCount(table.TableData, MusicCountTag, table.Music.Count, "music", issues);
			CompareCount(table.TableData, PinModelCountTag, table.PinModels.Count, "pin models", issues);
			CompareCount(table.TableData, ImageListCountTag, table.ImageLists.Count, "image lists", issues);
			CompareCount(table.TableData, LightListCountTag, table.LightLists.Count, "light lists", issues);
			CompareCount(table.TableData, DmdFontCountTag, table.DmdFonts.Count, "DMD fonts", issues);
		}

		private static void CompareCount(
			FuturePinballSourceStream tableData,
			uint tag,
			int actual,
			string category,
			ICollection<string> issues)
		{
			var declared = tableData.Integer(tag);
			if (declared.HasValue && declared.Value != actual) {
				issues.Add($"Table Data declares {declared.Value} {category}, but the compound file contains {actual}.");
			}
		}

		private static void ReportIndexProblems(
			IEnumerable<FuturePinballSourceStream> streams,
			ICollection<string> issues)
		{
			foreach (var group in streams.Where(stream => stream.SourceIndex.HasValue).GroupBy(stream => stream.Kind)) {
				var indices = group.Select(stream => stream.SourceIndex.Value).OrderBy(index => index).ToArray();
				var duplicates = indices.GroupBy(index => index).Where(item => item.Count() > 1).Select(item => item.Key).ToArray();
				if (duplicates.Length > 0) {
					issues.Add($"{group.Key} streams contain duplicate source indices: {string.Join(", ", duplicates)}.");
				}
				if (indices.Length > 0) {
					var expected = indices[0];
					foreach (var index in indices.Distinct()) {
						while (expected < index) {
							issues.Add($"{group.Key} stream index {expected} is missing.");
							expected++;
						}
						expected = index + 1;
					}
				}
			}
		}

		private static int CompareStreams(FuturePinballSourceStream left, FuturePinballSourceStream right)
		{
			var kind = left.Kind.CompareTo(right.Kind);
			if (kind != 0) {
				return kind;
			}
			if (left.SourceIndex.HasValue && right.SourceIndex.HasValue) {
				return left.SourceIndex.Value.CompareTo(right.SourceIndex.Value);
			}
			return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
		}

		private static FuturePinballStreamKind Classify(string name, out int? sourceIndex)
		{
			sourceIndex = null;
			if (name.Equals("File Version", StringComparison.OrdinalIgnoreCase)) return FuturePinballStreamKind.FileVersion;
			if (name.Equals("Table Data", StringComparison.OrdinalIgnoreCase)) return FuturePinballStreamKind.TableData;
			if (name.Equals("Table MAC", StringComparison.OrdinalIgnoreCase)) return FuturePinballStreamKind.TableMac;
			if (TryIndex(name, "Table Element ", out sourceIndex)) return FuturePinballStreamKind.TableElement;
			if (TryIndex(name, "Image ", out sourceIndex)) return FuturePinballStreamKind.Image;
			if (TryIndex(name, "Sound ", out sourceIndex)) return FuturePinballStreamKind.Sound;
			if (TryIndex(name, "Music ", out sourceIndex)) return FuturePinballStreamKind.Music;
			if (TryIndex(name, "PinModel ", out sourceIndex)) return FuturePinballStreamKind.PinModel;
			if (TryIndex(name, "DmdFont ", out sourceIndex)) return FuturePinballStreamKind.DmdFont;
			if (TryIndex(name, "ImageList ", out sourceIndex)) return FuturePinballStreamKind.ImageList;
			if (TryIndex(name, "LightList ", out sourceIndex)) return FuturePinballStreamKind.LightList;
			return FuturePinballStreamKind.Unknown;
		}

		private static bool TryIndex(string name, string prefix, out int? index)
		{
			index = null;
			if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			if (int.TryParse(name.Substring(prefix.Length), out var value) && value >= 0) {
				index = value;
			}
			return true;
		}

		private static Storage OpenStorage(Storage parent, string name, string sourceName)
		{
			var entry = parent.EnumerateEntries().FirstOrDefault(item =>
				item.Type == EntryType.Storage && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (entry == null) {
				throw new FuturePinballFormatException($"Required storage '{name}' is missing", sourceName);
			}
			return parent.OpenStorage(entry.Name);
		}

		private static uint ReadUInt32(byte[] data, int offset)
		{
			return (uint)(data[offset]
				| data[offset + 1] << 8
				| data[offset + 2] << 16
				| data[offset + 3] << 24);
		}
	}
}
