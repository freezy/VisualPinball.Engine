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

namespace VisualPinball.Engine.IO.FuturePinball
{
	public enum FuturePinballFileKind
	{
		Table,
		Library,
		Model
	}

	public enum FuturePinballStreamKind
	{
		Unknown,
		FileVersion,
		TableData,
		TableMac,
		TableElement,
		Image,
		Sound,
		Music,
		PinModel,
		DmdFont,
		ImageList,
		LightList,
		ModelData
	}

	public enum FuturePinballResourceKind
	{
		Unknown = 0,
		Image = 1,
		Sound = 2,
		Music = 3,
		Model = 4,
		DmdFont = 5,
		Script = 6,
		ImageList = 7,
		LightList = 8
	}

	public enum FuturePinballValueKind
	{
		Opaque,
		Integer,
		Float,
		Color,
		Vector2,
		String,
		WideString,
		StringList,
		CollisionData,
		CompressedData,
		End
	}

	public enum FuturePinballElementType : uint
	{
		Surface = 2,
		RoundLight = 3,
		ShapeableLight = 4,
		Peg = 6,
		Flipper = 7,
		Bumper = 8,
		LeafTarget = 10,
		DropTarget = 11,
		Plunger = 12,
		RoundRubber = 13,
		ShapeableRubber = 14,
		Ornament = 15,
		GuideWall = 16,
		Timer = 17,
		Decal = 18,
		Kicker = 19,
		LaneGuide = 20,
		ModelRubber = 21,
		Trigger = 22,
		Flasher = 23,
		WireGuide = 24,
		DispReel = 25,
		HudReel = 26,
		Overlay = 27,
		Bulb = 29,
		Gate = 30,
		Spinner = 31,
		CustomToy = 33,
		LightSequencer = 34,
		Segment = 37,
		HudSegment = 38,
		Dmd = 39,
		HudDmd = 40,
		Diverter = 43,
		Sta = 44,
		AutoPlunger = 46,
		RotoTarget = 49,
		Popup = 50,
		RampModel = 51,
		WireRamp = 53,
		SwingTarget = 54,
		Ramp = 55,
		SpinningDisk = 56,
		LightImage = 57,
		EmKicker = 58,
		HudLightImage = 60,
		OptoTrigger = 61,
		VariTarget = 62,
		Hologram = 64
	}

	public sealed class FuturePinballReaderOptions
	{
		public const int DefaultMaximumStreamBytes = 512 * 1024 * 1024;
		public const int DefaultMaximumDecompressedBytes = 512 * 1024 * 1024;

		public int MaximumStreamBytes { get; set; } = DefaultMaximumStreamBytes;
		public int MaximumDecompressedBytes { get; set; } = DefaultMaximumDecompressedBytes;
		public int MaximumRecordCount { get; set; } = 1_000_000;
		public int MaximumStringBytes { get; set; } = 16 * 1024 * 1024;
		public bool DecodeCompressedData { get; set; } = true;
	}

	public sealed class FuturePinballFormatException : IOException
	{
		public string SourceName { get; }
		public long SourceOffset { get; }

		public FuturePinballFormatException(string message, string sourceName = null, long sourceOffset = -1, Exception innerException = null)
			: base(FormatMessage(message, sourceName, sourceOffset), innerException)
		{
			SourceName = sourceName;
			SourceOffset = sourceOffset;
		}

		private static string FormatMessage(string message, string sourceName, long sourceOffset)
		{
			var location = sourceName == null ? string.Empty : $" in {sourceName}";
			if (sourceOffset >= 0) {
				location += $" at 0x{sourceOffset:X}";
			}
			return message + location;
		}
	}

	public readonly struct FuturePinballVector2
	{
		public float X { get; }
		public float Y { get; }

		public FuturePinballVector2(float x, float y)
		{
			X = x;
			Y = y;
		}
	}

	public sealed class FuturePinballCollisionShape
	{
		public uint Type { get; internal set; }
		public bool GenerateHitEvent { get; internal set; }
		public bool AffectsBall { get; internal set; }
		public uint EventId { get; internal set; }
		public float X { get; internal set; }
		public float Y { get; internal set; }
		public float Z { get; internal set; }
		public float Value1 { get; internal set; }
		public float Value2 { get; internal set; }
		public float Value3 { get; internal set; }
		public float Value4 { get; internal set; }
	}

	public sealed class FuturePinballCompressedData
	{
		public ReadOnlyMemory<byte> RawBytes { get; internal set; }
		public bool IsCompressed { get; internal set; }
		public int DeclaredUncompressedLength { get; internal set; }
		public int CompressedBytesConsumed { get; internal set; }
		public byte[] DecodedBytes { get; internal set; }
	}

	public sealed class FuturePinballRecord
	{
		public int Offset { get; internal set; }
		public uint StoredLength { get; internal set; }
		public uint OriginalTag { get; internal set; }
		public uint CanonicalTag { get; internal set; }
		public int ConsumedLength { get; internal set; }
		public string Name { get; internal set; }
		public FuturePinballValueKind ValueKind { get; internal set; }
		public object Value { get; internal set; }
		public ReadOnlyMemory<byte> RawRecord { get; internal set; }
		public ReadOnlyMemory<byte> Payload { get; internal set; }
		public bool UsesLegacyTag => OriginalTag != CanonicalTag;
	}

	public sealed class FuturePinballSourceStream
	{
		public string Name { get; internal set; }
		public int? SourceIndex { get; internal set; }
		public FuturePinballStreamKind Kind { get; internal set; }
		public byte[] RawData { get; internal set; }
		public uint? ElementTypeId { get; internal set; }
		public FuturePinballElementType? ElementType { get; internal set; }
		public IReadOnlyList<FuturePinballRecord> Records { get; internal set; } = Array.Empty<FuturePinballRecord>();

		public FuturePinballRecord FirstRecord(uint canonicalTag)
		{
			return Records.FirstOrDefault(record => record.CanonicalTag == canonicalTag);
		}

		public int? Integer(uint canonicalTag)
		{
			return FirstRecord(canonicalTag)?.Value as int?;
		}

		public string Text(uint canonicalTag)
		{
			return FirstRecord(canonicalTag)?.Value as string;
		}
	}

	public sealed class FuturePinballTable
	{
		public string SourcePath { get; internal set; }
		public uint? FileVersion { get; internal set; }
		public int CompoundEntryCount { get; internal set; }
		public IReadOnlyList<FuturePinballSourceStream> Streams { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public FuturePinballSourceStream TableData { get; internal set; }
		public FuturePinballSourceStream TableMac { get; internal set; }
		public IReadOnlyList<FuturePinballSourceStream> Elements { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<FuturePinballSourceStream> Images { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<FuturePinballSourceStream> Sounds { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<FuturePinballSourceStream> Music { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<FuturePinballSourceStream> PinModels { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<FuturePinballSourceStream> DmdFonts { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<FuturePinballSourceStream> ImageLists { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<FuturePinballSourceStream> LightLists { get; internal set; } = Array.Empty<FuturePinballSourceStream>();
		public IReadOnlyList<string> Issues { get; internal set; } = Array.Empty<string>();
	}

	public sealed class FuturePinballLibraryEntry
	{
		public string Name { get; internal set; }
		public FuturePinballResourceKind Kind { get; internal set; }
		public uint TypeId { get; internal set; }
		public string OriginalPath { get; internal set; }
		public byte[] Flad { get; internal set; }
		public FuturePinballCompressedData Data { get; internal set; }
		public IReadOnlyDictionary<string, byte[]> Streams { get; internal set; }
	}

	public sealed class FuturePinballLibrary
	{
		public string SourcePath { get; internal set; }
		public IReadOnlyList<FuturePinballLibraryEntry> Entries { get; internal set; } = Array.Empty<FuturePinballLibraryEntry>();
	}

	public sealed class FuturePinballModel
	{
		public string SourcePath { get; internal set; }
		public FuturePinballSourceStream ModelData { get; internal set; }
	}
}
