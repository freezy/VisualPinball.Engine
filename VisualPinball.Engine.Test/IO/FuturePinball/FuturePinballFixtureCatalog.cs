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
using System.Security.Cryptography;

using NUnit.Framework;

namespace VisualPinball.Engine.Test.IO.FuturePinball
{
	public sealed class FuturePinballFixtureExpectation
	{
		public string Name { get; }
		public string RelativePath { get; }
		public long SourceBytes { get; }
		public string SourceSha256 { get; }
		public int StreamCount { get; }
		public int TableDataBytes { get; }
		public int ElementCount { get; }
		public int ImageCount { get; }
		public int SoundCount { get; }
		public int MusicCount { get; }
		public int PinModelCount { get; }
		public int DmdFontCount { get; }
		public int ImageListCount { get; }
		public int LightListCount { get; }
		public string ElementTypeCounts { get; }
		public int ScriptRecordOffset { get; }
		public int ScriptRecordLength { get; }
		public int ScriptCompressedBytes { get; }
		public string ScriptCompressedSha256 { get; }
		public int ScriptDecodedBytes { get; }
		public string ScriptDecodedSha256 { get; }

		public int ScriptTagOffset => ScriptRecordOffset + sizeof(uint);

		public FuturePinballFixtureExpectation(
			string name,
			string relativePath,
			long sourceBytes,
			string sourceSha256,
			int streamCount,
			int tableDataBytes,
			int elementCount,
			int imageCount,
			int soundCount,
			int musicCount,
			int pinModelCount,
			int dmdFontCount,
			int imageListCount,
			int lightListCount,
			string elementTypeCounts,
			int scriptRecordOffset,
			int scriptRecordLength,
			int scriptCompressedBytes,
			string scriptCompressedSha256,
			int scriptDecodedBytes,
			string scriptDecodedSha256)
		{
			Name = name;
			RelativePath = relativePath;
			SourceBytes = sourceBytes;
			SourceSha256 = sourceSha256;
			StreamCount = streamCount;
			TableDataBytes = tableDataBytes;
			ElementCount = elementCount;
			ImageCount = imageCount;
			SoundCount = soundCount;
			MusicCount = musicCount;
			PinModelCount = pinModelCount;
			DmdFontCount = dmdFontCount;
			ImageListCount = imageListCount;
			LightListCount = lightListCount;
			ElementTypeCounts = elementTypeCounts;
			ScriptRecordOffset = scriptRecordOffset;
			ScriptRecordLength = scriptRecordLength;
			ScriptCompressedBytes = scriptCompressedBytes;
			ScriptCompressedSha256 = scriptCompressedSha256;
			ScriptDecodedBytes = scriptDecodedBytes;
			ScriptDecodedSha256 = scriptDecodedSha256;
		}

		public override string ToString()
		{
			return Name;
		}
	}

	public static class FuturePinballFixtureCatalog
	{
		public const string FixtureRootEnvironmentVariable = "VPE_FPT_FIXTURES";

		private const string OriginalElementTypes = "2:98,3:3,4:84,6:34,7:4,8:2,10:5,11:37,12:1,13:11,14:9,15:188,16:21,17:51,19:23,20:4,21:1,22:22,23:9,24:50,27:2,29:53,30:5,31:2,34:2,37:2,38:2,43:7,46:2,50:13,53:10,56:2,57:39,61:2";
		private const string UltraElementTypes = "2:100,3:3,4:84,6:34,7:4,8:2,10:5,11:37,12:1,13:11,14:9,15:168,16:21,17:51,19:23,20:4,21:1,22:38,23:9,24:50,27:2,29:55,30:5,31:2,34:2,37:2,38:2,43:7,46:2,50:13,53:10,56:2,57:39,61:2";

		public static readonly IReadOnlyList<FuturePinballFixtureExpectation> All = new[] {
			Original(
				"Three Angels 2008 v1.666",
				"fp-2008-1.666/3ANGELS.fpt",
				1669120,
				"dd2b5701dcfebe78bca9e717ba6c17fd7e79143275d359e46c7be1168f03f72f"
			),
			Original(
				"Three Angels 2012 installed",
				"fp-2012-installed/3 Angels.fpt",
				1676800,
				"e5be5c004d1ab2caf43d46c3416759082a2c6403b3e30e86ff7142048f32dcdb"
			),
			Ultra(
				"Three Angels 2012 Ultra release",
				"fp-2012-ultra-release/Three Angels_ehanc_Slam.fpt",
				"c62e9cfe1936722b721a1ac797c3956ee0007ad5764eb126b40c3d6898202052",
				559084,
				557539,
				557531,
				"0d6cb0a69079c3fca656e124210fbaff25679e13a2b3a5be362115af58747994",
				2552611,
				"d3b7da77414f9d351cca3d4dd811f7d54b9f949afc4fc00604bfe51a2479a1bb"
			),
			Ultra(
				"Three Angels 2012 Ultra source",
				"fp-2012-ultra-source/Three Angels.fpt",
				"c62e9cfe1936722b721a1ac797c3956ee0007ad5764eb126b40c3d6898202052",
				559084,
				557539,
				557531,
				"0d6cb0a69079c3fca656e124210fbaff25679e13a2b3a5be362115af58747994",
				2552611,
				"d3b7da77414f9d351cca3d4dd811f7d54b9f949afc4fc00604bfe51a2479a1bb"
			),
			Ultra(
				"Three Angels 2013 enhanced",
				"fp-2013-enhanced/Three Angels_ehanc_Slam.fpt",
				"d5a8280c466ef1d3bd23a8b2080c06a289f601254b05fecc494c624a1b7b16fc",
				559088,
				557543,
				557535,
				"723ee7235b291423603df98a6d9a598637d4167297dd0f9110040107d7fe96e3",
				2552609,
				"a494cba86affeaf8551c99575366b213cbfc376fd394d110ea41b2bb9d78fa44"
			)
		};

		private static FuturePinballFixtureExpectation Original(string name, string relativePath, long bytes, string hash)
		{
			return new FuturePinballFixtureExpectation(
				name, relativePath, bytes, hash, 2246, 559078, 800, 263, 840, 48, 259, 0, 30, 2,
				OriginalElementTypes, 1594, 557376, 557368,
				"3a9379c2ea632b75a09d6c1fc2f92d6017e2bf2222342ce182d46688a4d1bceb",
				2550062, "b8141312683b075b4bccc59cb9b098207cda622207f2c9a8f20805c3d8a6ea4f"
			);
		}

		private static FuturePinballFixtureExpectation Ultra(
			string name,
			string relativePath,
			string hash,
			int tableDataBytes,
			int scriptRecordLength,
			int scriptCompressedBytes,
			string scriptCompressedHash,
			int scriptDecodedBytes,
			string scriptDecodedHash)
		{
			return new FuturePinballFixtureExpectation(
				name, relativePath, 8980992, hash, 2059, tableDataBytes, 800, 255, 869, 48, 51, 0, 30, 2,
				UltraElementTypes, 1437, scriptRecordLength, scriptCompressedBytes,
				scriptCompressedHash, scriptDecodedBytes, scriptDecodedHash
			);
		}
	}

	[TestFixture]
	public class FuturePinballFixtureIntegrityTests
	{
		[TestCaseSource(typeof(FuturePinballFixtureCatalog), nameof(FuturePinballFixtureCatalog.All))]
		public void FixtureMatchesLockedSource(FuturePinballFixtureExpectation fixture)
		{
			var fixtureRoot = Environment.GetEnvironmentVariable(FuturePinballFixtureCatalog.FixtureRootEnvironmentVariable);
			if (string.IsNullOrWhiteSpace(fixtureRoot)) {
				Assert.Ignore($"Set {FuturePinballFixtureCatalog.FixtureRootEnvironmentVariable} to run Future Pinball corpus tests.");
			}

			var relativePath = fixture.RelativePath.Replace('/', Path.DirectorySeparatorChar);
			var fixturePath = Path.GetFullPath(Path.Combine(fixtureRoot, relativePath));
			Assert.That(File.Exists(fixturePath), Is.True, $"Missing locked fixture {fixture.RelativePath}");
			Assert.That(new FileInfo(fixturePath).Length, Is.EqualTo(fixture.SourceBytes));
			Assert.That(Sha256(fixturePath), Is.EqualTo(fixture.SourceSha256));
		}

		private static string Sha256(string path)
		{
			using (var input = File.OpenRead(path))
			using (var sha = SHA256.Create()) {
				return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", string.Empty).ToLowerInvariant();
			}
		}
	}
}
