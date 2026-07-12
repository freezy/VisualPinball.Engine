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

using NUnit.Framework;
using OpenMcdf;

using VisualPinball.Engine.IO.FuturePinball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.IO.FuturePinball
{
	[TestFixture]
	public class FuturePinballReaderTests
	{
		private const uint ScriptTag = 0x4F5A4C7A;

		[TestCaseSource(typeof(FuturePinballFixtureCatalog), nameof(FuturePinballFixtureCatalog.All))]
		public void ParsesLockedTableWithoutLosingStreams(FuturePinballFixtureExpectation fixture)
		{
			var fixturePath = FixturePath(fixture);
			var table = FuturePinballTableReader.Load(fixturePath);

			Assert.That(table.CompoundEntryCount, Is.EqualTo(fixture.CompoundEntryCount));
			Assert.That(table.Streams.Count, Is.EqualTo(fixture.CompoundEntryCount - 1));
			Assert.That(table.TableData.RawData.Length, Is.EqualTo(fixture.TableDataBytes));
			Assert.That(table.Elements.Count, Is.EqualTo(fixture.ElementCount));
			Assert.That(table.Images.Count, Is.EqualTo(fixture.ImageCount));
			Assert.That(table.Sounds.Count, Is.EqualTo(fixture.SoundCount));
			Assert.That(table.Music.Count, Is.EqualTo(fixture.MusicCount));
			Assert.That(table.PinModels.Count, Is.EqualTo(fixture.PinModelCount));
			Assert.That(table.DmdFonts.Count, Is.EqualTo(fixture.DmdFontCount));
			Assert.That(table.ImageLists.Count, Is.EqualTo(fixture.ImageListCount));
			Assert.That(table.LightLists.Count, Is.EqualTo(fixture.LightListCount));
			Assert.That(ElementTypeCounts(table.Elements), Is.EqualTo(fixture.ElementTypeCounts));
			Assert.That(table.Issues, Is.Empty);
		}

		[TestCaseSource(typeof(FuturePinballFixtureCatalog), nameof(FuturePinballFixtureCatalog.All))]
		public void DecodesLockedTableScriptAtExactRecordBoundary(FuturePinballFixtureExpectation fixture)
		{
			var table = FuturePinballTableReader.Load(FixturePath(fixture));
			var scriptRecord = table.TableData.FirstRecord(ScriptTag);
			Assert.That(scriptRecord, Is.Not.Null);
			Assert.That(scriptRecord.Offset, Is.EqualTo(fixture.ScriptRecordOffset));
			Assert.That(scriptRecord.Offset + sizeof(uint), Is.EqualTo(fixture.ScriptTagOffset));
			Assert.That(scriptRecord.StoredLength, Is.EqualTo(fixture.ScriptRecordLength));
			Assert.That(scriptRecord.ConsumedLength, Is.EqualTo(fixture.ScriptRecordLength + sizeof(uint)));

			var script = (FuturePinballCompressedData)scriptRecord.Value;
			Assert.That(script.IsCompressed, Is.True);
			Assert.That(script.RawBytes.Length, Is.EqualTo(fixture.ScriptRecordLength));
			Assert.That(script.RawBytes.Span.Slice(0, 4).ToArray(), Is.EqualTo(new byte[] { 0x7a, 0x4c, 0x5a, 0x4f }));
			Assert.That(script.RawBytes.Length - 8, Is.EqualTo(fixture.ScriptCompressedBytes));
			Assert.That(Sha256(script.RawBytes.Slice(8).ToArray()), Is.EqualTo(fixture.ScriptCompressedSha256));
			Assert.That(script.DecodedBytes.Length, Is.EqualTo(fixture.ScriptDecodedBytes));
			Assert.That(Sha256(script.DecodedBytes), Is.EqualTo(fixture.ScriptDecodedSha256));

			var following = table.TableData.Records.SkipWhile(record => record != scriptRecord).Skip(1).ToArray();
			Assert.That(following.Length, Is.EqualTo(9));
			Assert.That(following.Last().Offset + following.Last().ConsumedLength, Is.EqualTo(table.TableData.RawData.Length));
		}

		[Test]
		public void RejectsLzoOutputBeyondConfiguredLimit()
		{
			var data = new byte[] { (byte)'z', (byte)'L', (byte)'Z', (byte)'O', 0x00, 0x10, 0x00, 0x00 };
			var action = new TestDelegate(() => FuturePinballCompression.Decode(data, 1024, "bomb"));
			Assert.That(action, Throws.TypeOf<FuturePinballFormatException>()
				.With.Message.Contains("exceeds the configured limit"));
		}

		[Test]
		public void RejectsTruncatedLzoHeader()
		{
			var data = new byte[] { (byte)'z', (byte)'L', (byte)'Z', (byte)'O', 0x01 };
			var action = new TestDelegate(() => FuturePinballCompression.Decode(data));
			Assert.That(action, Throws.TypeOf<FuturePinballFormatException>()
				.With.Message.Contains("Truncated zLZO header"));
		}

		[Test]
		public void ParsesNumericElementOrderBareTypesAndBrokenListLength()
		{
			var path = TemporaryPath("fpt");
			try {
				using (var root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.None)) {
					var storage = root.CreateStorage("Future Pinball");
					Write(storage.CreateStream("File Version"), UInt32(0x00020000));
					Write(storage.CreateStream("Table Data"), Join(
						IntegerRecord(0x95FDCDD2, 2),
						IntegerRecord(0xA2F4C9D2, 0),
						IntegerRecord(0xA5F3BFD2, 0),
						IntegerRecord(0x96ECC5D2, 0),
						IntegerRecord(0xA5F2C5D2, 0),
						IntegerRecord(0x95F5C9D2, 1),
						IntegerRecord(0x95F5C6D2, 0),
						IntegerRecord(0x9BFBCED2, 0),
						Record(0xA7FDC4E0, Array.Empty<byte>())
					));
					Write(storage.CreateStream("Table MAC"), new byte[16]);
					Write(storage.CreateStream("Table Element 10"), Join(
						UInt32((uint)FuturePinballElementType.Flipper),
						Record(0xDEADBEEF, new byte[] { 1, 2, 3 }),
						Record(0xA7FDC4E0, Array.Empty<byte>())
					));
					Write(storage.CreateStream("Table Element 2"), Join(
						UInt32((uint)FuturePinballElementType.Surface),
						Record(0xA7FDC4E0, Array.Empty<byte>())
					));
					var listPayload = Join(UInt32(2), AsciiString("first"), AsciiString("second"));
					Write(storage.CreateStream("ImageList 1"), Join(
						StringRecord(0xA4F4D1D7, "frames"),
						Record(0xA8EDD1E1, listPayload, sizeof(uint)),
						Record(0xA7FDC4E0, Array.Empty<byte>())
					));
					root.Flush(true);
				}

				var table = FuturePinballTableReader.Load(path);
				Assert.That(table.Elements.Select(element => element.SourceIndex), Is.EqualTo(new int?[] { 2, 10 }));
				Assert.That(table.Elements.Select(element => element.ElementType), Is.EqualTo(new[] {
					FuturePinballElementType.Surface,
					FuturePinballElementType.Flipper
				}));
				var unknown = table.Elements[1].Records.First();
				Assert.That(unknown.ValueKind, Is.EqualTo(FuturePinballValueKind.Opaque));
				Assert.That(unknown.Payload.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
				var items = (IReadOnlyList<string>)table.ImageLists.Single().Records[1].Value;
				Assert.That(items, Is.EqualTo(new[] { "first", "second" }));
				Assert.That(table.ImageLists.Single().Records[1].StoredLength, Is.EqualTo(sizeof(uint)));
				Assert.That(table.Issues, Has.Count.EqualTo(7));
				Assert.That(table.Issues, Has.All.Contains("TableElement stream index"));
			} finally {
				File.Delete(path);
			}
		}

		[Test]
		public void ReadsLibraryEntriesWithoutTrustingOriginalPath()
		{
			var path = TemporaryPath("fpl");
			try {
				using (var root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.None)) {
					var item = root.CreateStorage("Playfield");
					Write(item.CreateStream("FTYP"), UInt32((uint)FuturePinballResourceKind.Image));
					Write(item.CreateStream("FPAT"), System.Text.Encoding.ASCII.GetBytes("C:\\unsafe\\playfield.bmp\0"));
					Write(item.CreateStream("FLAD"), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
					Write(item.CreateStream("FDAT"), new byte[] { (byte)'B', (byte)'M', 1, 2, 3 });
					root.Flush(true);
				}

				var library = FuturePinballLibraryReader.Load(path);
				var entry = library.Entries.Single();
				Assert.That(entry.Kind, Is.EqualTo(FuturePinballResourceKind.Image));
				Assert.That(entry.OriginalPath, Is.EqualTo("C:\\unsafe\\playfield.bmp"));
				Assert.That(entry.Data.IsCompressed, Is.False);
				Assert.That(entry.Data.DecodedBytes, Is.EqualTo(new byte[] { (byte)'B', (byte)'M', 1, 2, 3 }));
				Assert.That(entry.Streams.Keys, Is.EquivalentTo(new[] { "FTYP", "FPAT", "FLAD", "FDAT" }));
			} finally {
				File.Delete(path);
			}
		}

		[Test]
		public void ReadsStandaloneModelRecords()
		{
			var path = TemporaryPath("fpm");
			try {
				using (var root = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.None)) {
					var modelStorage = root.CreateStorage("PinModel");
					Write(modelStorage.CreateStream("ModelData"), Join(
						StringRecord(0xA4F4D1D7, "Test Model"),
						IntegerRecord(0xA4F1B9D1, 8),
						Record(0xA7FDC4E0, Array.Empty<byte>())
					));
					root.Flush(true);
				}

				var model = FuturePinballModelReader.Load(path);
				Assert.That(model.ModelData.Text(0xA4F4D1D7), Is.EqualTo("Test Model"));
				Assert.That(model.ModelData.Integer(0xA4F1B9D1), Is.EqualTo(8));
			} finally {
				File.Delete(path);
			}
		}

		[Test]
		public void OpenMcdfUpgradeRoundTripsVpxTables()
		{
			var path = TemporaryPath("vpx");
			try {
				var source = new TableBuilder().AddBumper("Bumper1").Build();
				source.Table.Data.NumGameItems = source.ItemDatas.Count();
				source.Export(path);
				using (var root = RootStorage.OpenRead(path, StorageModeFlags.None)) {
					var storage = root.OpenStorage("GameStg");
					var item = storage.OpenStream("GameItem0");
					Assert.That(item.Length, Is.GreaterThan(4));
					var type = new byte[4];
					Assert.That(item.Read(type, 0, type.Length), Is.EqualTo(type.Length));
					Assert.That(BitConverter.ToInt32(type, 0), Is.EqualTo((int)VisualPinball.Engine.VPT.ItemType.Bumper));
				}
				var loaded = FileTableContainer.Load(path);
				Assert.That(loaded.FileVersion, Is.EqualTo(1060));
				Assert.That(loaded.NumGameItems, Is.EqualTo(source.NumGameItems));
				Assert.That(loaded.ItemDatas.Count(), Is.EqualTo(source.ItemDatas.Count()));
				Assert.That(loaded.Bumper("Bumper1"), Is.Not.Null);
				Assert.That(loaded.FileHash, Has.Length.EqualTo(16));
			} finally {
				File.Delete(path);
			}
		}

		private static string FixturePath(FuturePinballFixtureExpectation fixture)
		{
			var fixtureRoot = Environment.GetEnvironmentVariable(FuturePinballFixtureCatalog.FixtureRootEnvironmentVariable);
			if (string.IsNullOrWhiteSpace(fixtureRoot)) {
				Assert.Ignore($"Set {FuturePinballFixtureCatalog.FixtureRootEnvironmentVariable} to run Future Pinball corpus tests.");
			}
			return Path.GetFullPath(Path.Combine(fixtureRoot, fixture.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
		}

		private static string ElementTypeCounts(IEnumerable<FuturePinballSourceStream> elements)
		{
			return string.Join(",", elements
				.GroupBy(element => element.ElementTypeId.Value)
				.OrderBy(group => group.Key)
				.Select(group => $"{group.Key}:{group.Count()}"));
		}

		private static string Sha256(byte[] data)
		{
			using (var sha = SHA256.Create()) {
				return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
			}
		}

		private static string TemporaryPath(string extension)
		{
			return Path.Combine(Path.GetTempPath(), $"vpe-fp-{Guid.NewGuid():N}.{extension}");
		}

		private static void Write(CfbStream stream, byte[] data)
		{
			stream.SetLength(data.Length);
			stream.Position = 0;
			stream.Write(data, 0, data.Length);
			stream.Flush();
		}

		private static byte[] IntegerRecord(uint tag, int value)
		{
			return Record(tag, UInt32((uint)value));
		}

		private static byte[] StringRecord(uint tag, string value)
		{
			return Record(tag, AsciiString(value));
		}

		private static byte[] AsciiString(string value)
		{
			var data = System.Text.Encoding.ASCII.GetBytes(value);
			return Join(UInt32((uint)data.Length), data);
		}

		private static byte[] Record(uint tag, byte[] payload, int? storedLength = null)
		{
			return Join(UInt32((uint)(storedLength ?? payload.Length + sizeof(uint))), UInt32(tag), payload);
		}

		private static byte[] UInt32(uint value)
		{
			return new[] {
				(byte)value,
				(byte)(value >> 8),
				(byte)(value >> 16),
				(byte)(value >> 24)
			};
		}

		private static byte[] Join(params byte[][] parts)
		{
			var length = parts.Sum(part => part.Length);
			var result = new byte[length];
			var offset = 0;
			foreach (var part in parts) {
				Buffer.BlockCopy(part, 0, result, offset, part.Length);
				offset += part.Length;
			}
			return result;
		}
	}
}
