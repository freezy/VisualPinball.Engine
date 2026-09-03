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
using System.Text;

using OpenMcdf;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public static class FuturePinballLibraryReader
	{
		public static FuturePinballLibrary Load(string fileName, FuturePinballReaderOptions options = null)
		{
			if (fileName == null) {
				throw new ArgumentNullException(nameof(fileName));
			}
			options ??= new FuturePinballReaderOptions();
			using (var root = RootStorage.OpenRead(fileName, StorageModeFlags.None)) {
				var entries = new List<FuturePinballLibraryEntry>();
				foreach (var item in root.EnumerateEntries()
					.Where(item => item.Type == EntryType.Storage)
					.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)) {
					var storage = root.OpenStorage(item.Name);
					var streams = ReadStreams(storage, options, item.Name);
					streams.TryGetValue("FTYP", out var typeBytes);
					streams.TryGetValue("FPAT", out var pathBytes);
					streams.TryGetValue("FLAD", out var flad);
					streams.TryGetValue("FDAT", out var dataBytes);
					var typeId = typeBytes != null && typeBytes.Length >= 4 ? ReadUInt32(typeBytes, 0) : 0;
					entries.Add(new FuturePinballLibraryEntry {
						Name = item.Name,
						TypeId = typeId,
						Kind = Enum.IsDefined(typeof(FuturePinballResourceKind), (int)typeId)
							? (FuturePinballResourceKind)typeId
							: FuturePinballResourceKind.Unknown,
						OriginalPath = pathBytes == null ? null : Encoding.ASCII.GetString(pathBytes).TrimEnd('\0'),
						Flad = flad,
						Data = dataBytes == null
							? null
							: FuturePinballCompression.Decode(dataBytes, options.MaximumDecompressedBytes, item.Name),
						Streams = streams
					});
				}
				return new FuturePinballLibrary {
					SourcePath = Path.GetFullPath(fileName),
					Entries = entries
				};
			}
		}

		private static Dictionary<string, byte[]> ReadStreams(
			Storage storage,
			FuturePinballReaderOptions options,
			string sourceName)
		{
			var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in storage.EnumerateEntries().Where(item => item.Type == EntryType.Stream)) {
				if (item.Length > options.MaximumStreamBytes) {
					throw new FuturePinballFormatException(
						$"Stream length {item.Length} exceeds the configured limit {options.MaximumStreamBytes}",
						$"{sourceName}/{item.Name}"
					);
				}
				result[item.Name] = storage.OpenStream(item.Name).ReadAll();
			}
			return result;
		}

		private static uint ReadUInt32(byte[] data, int offset)
		{
			return (uint)(data[offset]
				| data[offset + 1] << 8
				| data[offset + 2] << 16
				| data[offset + 3] << 24);
		}
	}

	public static class FuturePinballModelReader
	{
		public static FuturePinballModel Load(string fileName, FuturePinballReaderOptions options = null)
		{
			if (fileName == null) {
				throw new ArgumentNullException(nameof(fileName));
			}
			options ??= new FuturePinballReaderOptions();
			using (var root = RootStorage.OpenRead(fileName, StorageModeFlags.None)) {
				var pinModelEntry = root.EnumerateEntries().FirstOrDefault(item =>
					item.Type == EntryType.Storage && item.Name.Equals("PinModel", StringComparison.OrdinalIgnoreCase));
				if (pinModelEntry == null) {
					throw new FuturePinballFormatException("Required PinModel storage is missing", fileName);
				}
				var pinModel = root.OpenStorage(pinModelEntry.Name);
				var modelEntry = pinModel.EnumerateEntries().FirstOrDefault(item =>
					item.Type == EntryType.Stream && item.Name.Equals("ModelData", StringComparison.OrdinalIgnoreCase));
				if (modelEntry == null) {
					throw new FuturePinballFormatException("Required ModelData stream is missing", fileName);
				}
				if (modelEntry.Length > options.MaximumStreamBytes) {
					throw new FuturePinballFormatException(
						$"ModelData length {modelEntry.Length} exceeds the configured limit {options.MaximumStreamBytes}", fileName
					);
				}
				var data = pinModel.OpenStream(modelEntry.Name).ReadAll();
				return new FuturePinballModel {
					SourcePath = Path.GetFullPath(fileName),
					ModelData = new FuturePinballSourceStream {
						Name = modelEntry.Name,
						Kind = FuturePinballStreamKind.ModelData,
						RawData = data,
						Records = FuturePinballRecordReader.Read(
							data, 0, FuturePinballRecordContext.PinModel, options, modelEntry.Name
						)
					}
				};
			}
		}
	}
}
