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
using System.Text;

namespace VisualPinball.Engine.IO.FuturePinball
{
	internal enum FuturePinballRecordContext
	{
		TableData,
		TableElement,
		Resource,
		List,
		PinModel
	}

	internal sealed class FuturePinballChunkDescriptor
	{
		public string Name { get; }
		public FuturePinballValueKind Kind { get; }

		public FuturePinballChunkDescriptor(string name, FuturePinballValueKind kind)
		{
			Name = name;
			Kind = kind;
		}
	}

	internal static class FuturePinballRecordReader
	{
		internal const uint LegacyTagCorrection = 0x15BDECDB;
		internal const uint EndTag = 0xA7FDC4E0;
		internal const uint ScriptTag = 0x4F5A4C7A;

		private static readonly IReadOnlyDictionary<uint, FuturePinballChunkDescriptor> TableDataDescriptors = CreateTableDataDescriptors();
		private static readonly IReadOnlyDictionary<uint, FuturePinballChunkDescriptor> ResourceDescriptors = CreateResourceDescriptors();
		private static readonly IReadOnlyDictionary<uint, FuturePinballChunkDescriptor> ListDescriptors = CreateListDescriptors();
		private static readonly IReadOnlyDictionary<uint, FuturePinballChunkDescriptor> PinModelDescriptors = CreatePinModelDescriptors();
		private static readonly IReadOnlyDictionary<uint, FuturePinballChunkDescriptor> ElementDescriptors = CreateElementDescriptors();

		public static IReadOnlyList<FuturePinballRecord> Read(
			byte[] data,
			int startOffset,
			FuturePinballRecordContext context,
			FuturePinballReaderOptions options,
			string sourceName)
		{
			var records = new List<FuturePinballRecord>();
			var offset = startOffset;
			while (offset < data.Length) {
				if (records.Count >= options.MaximumRecordCount) {
					throw new FuturePinballFormatException(
						$"Record count exceeds the configured limit {options.MaximumRecordCount}", sourceName, offset
					);
				}
				if (data.Length - offset < 8) {
					throw new FuturePinballFormatException("Truncated record header", sourceName, offset);
				}

				var storedLength = ReadUInt32(data, offset);
				if (storedLength < sizeof(uint)) {
					throw new FuturePinballFormatException($"Invalid record length {storedLength}", sourceName, offset);
				}
				if (storedLength > int.MaxValue) {
					throw new FuturePinballFormatException($"Record length {storedLength} is too large", sourceName, offset);
				}

				var originalTag = ReadUInt32(data, offset + 4);
				var descriptor = FindDescriptor(context, originalTag, out var canonicalTag);
				var payloadOffset = offset + 8;
				var storedPayloadLength = (int)storedLength - sizeof(uint);
				var payloadLength = descriptor?.Kind == FuturePinballValueKind.StringList
					? MeasureStringList(data, payloadOffset, options, sourceName)
					: storedPayloadLength;

				if (payloadLength < 0 || payloadOffset > data.Length - payloadLength) {
					throw new FuturePinballFormatException(
						$"Record payload length {payloadLength} crosses the stream boundary", sourceName, offset
					);
				}

				var consumedLength = 8 + payloadLength;
				var record = new FuturePinballRecord {
					Offset = offset,
					StoredLength = storedLength,
					OriginalTag = originalTag,
					CanonicalTag = canonicalTag,
					ConsumedLength = consumedLength,
					Name = descriptor?.Name,
					ValueKind = descriptor?.Kind ?? FuturePinballValueKind.Opaque,
					RawRecord = new ReadOnlyMemory<byte>(data, offset, consumedLength),
					Payload = new ReadOnlyMemory<byte>(data, payloadOffset, payloadLength)
				};
				record.Value = DecodeValue(record, options, sourceName);
				records.Add(record);
				offset += consumedLength;
			}

			if (offset != data.Length) {
				throw new FuturePinballFormatException("Record parsing did not align to the stream boundary", sourceName, offset);
			}
			return records;
		}

		private static object DecodeValue(FuturePinballRecord record, FuturePinballReaderOptions options, string sourceName)
		{
			var payload = record.Payload.Span;
			switch (record.ValueKind) {
				case FuturePinballValueKind.Integer:
					Require(payload, 4, record, sourceName);
					return ReadInt32(payload, 0);
				case FuturePinballValueKind.Float:
					Require(payload, 4, record, sourceName);
					return BitConverter.Int32BitsToSingle(ReadInt32(payload, 0));
				case FuturePinballValueKind.Color:
					Require(payload, 4, record, sourceName);
					return ReadUInt32(payload, 0);
				case FuturePinballValueKind.Vector2:
					Require(payload, 8, record, sourceName);
					return new FuturePinballVector2(
						BitConverter.Int32BitsToSingle(ReadInt32(payload, 0)),
						BitConverter.Int32BitsToSingle(ReadInt32(payload, 4))
					);
				case FuturePinballValueKind.String:
					return ReadString(payload, Encoding.ASCII, options, record, sourceName);
				case FuturePinballValueKind.WideString:
					return ReadString(payload, Encoding.Unicode, options, record, sourceName);
				case FuturePinballValueKind.StringList:
					return ReadStringList(payload, options, record, sourceName);
				case FuturePinballValueKind.CollisionData:
					return ReadCollisionData(payload, record, sourceName);
				case FuturePinballValueKind.CompressedData:
					if (!options.DecodeCompressedData) {
						return new FuturePinballCompressedData { RawBytes = record.Payload };
					}
					var compressedBytes = record.CanonicalTag == ScriptTag
						? record.RawRecord.Slice(sizeof(uint), (int)record.StoredLength)
						: record.Payload;
					return FuturePinballCompression.Decode(
						compressedBytes, options.MaximumDecompressedBytes, sourceName, record.Offset + 4
					);
				case FuturePinballValueKind.End:
					return null;
				default:
					return record.Payload;
			}
		}

		private static void Require(ReadOnlySpan<byte> payload, int length, FuturePinballRecord record, string sourceName)
		{
			if (payload.Length < length) {
				throw new FuturePinballFormatException(
					$"{record.Name ?? "Known"} record needs {length} payload bytes but has {payload.Length}",
					sourceName,
					record.Offset
				);
			}
		}

		private static string ReadString(
			ReadOnlySpan<byte> payload,
			Encoding encoding,
			FuturePinballReaderOptions options,
			FuturePinballRecord record,
			string sourceName)
		{
			Require(payload, 4, record, sourceName);
			var byteLength = ReadInt32(payload, 0);
			if (byteLength < 0 || byteLength > options.MaximumStringBytes || byteLength > payload.Length - 4) {
				throw new FuturePinballFormatException($"Invalid string length {byteLength}", sourceName, record.Offset + 8);
			}
			return encoding.GetString(payload.Slice(4, byteLength).ToArray()).TrimEnd('\0');
		}

		private static IReadOnlyList<string> ReadStringList(
			ReadOnlySpan<byte> payload,
			FuturePinballReaderOptions options,
			FuturePinballRecord record,
			string sourceName)
		{
			Require(payload, 4, record, sourceName);
			var count = ReadInt32(payload, 0);
			if (count < 0 || count > options.MaximumRecordCount) {
				throw new FuturePinballFormatException($"Invalid string-list count {count}", sourceName, record.Offset + 8);
			}
			var result = new List<string>(count);
			var offset = 4;
			for (var i = 0; i < count; i++) {
				if (offset > payload.Length - 4) {
					throw new FuturePinballFormatException("Truncated string-list length", sourceName, record.Offset + 8 + offset);
				}
				var length = ReadInt32(payload, offset);
				offset += 4;
				if (length < 0 || length > options.MaximumStringBytes || offset > payload.Length - length) {
					throw new FuturePinballFormatException($"Invalid string-list item length {length}", sourceName, record.Offset + 8 + offset);
				}
				result.Add(Encoding.ASCII.GetString(payload.Slice(offset, length).ToArray()).TrimEnd('\0'));
				offset += length;
			}
			return result;
		}

		private static int MeasureStringList(byte[] data, int payloadOffset, FuturePinballReaderOptions options, string sourceName)
		{
			if (payloadOffset > data.Length - 4) {
				throw new FuturePinballFormatException("Truncated string-list count", sourceName, payloadOffset);
			}
			var count = ReadInt32(data, payloadOffset);
			if (count < 0 || count > options.MaximumRecordCount) {
				throw new FuturePinballFormatException($"Invalid string-list count {count}", sourceName, payloadOffset);
			}
			var offset = payloadOffset + 4;
			for (var i = 0; i < count; i++) {
				if (offset > data.Length - 4) {
					throw new FuturePinballFormatException("Truncated string-list length", sourceName, offset);
				}
				var length = ReadInt32(data, offset);
				offset += 4;
				if (length < 0 || length > options.MaximumStringBytes || offset > data.Length - length) {
					throw new FuturePinballFormatException($"Invalid string-list item length {length}", sourceName, offset);
				}
				offset += length;
			}
			return offset - payloadOffset;
		}

		private static IReadOnlyList<FuturePinballCollisionShape> ReadCollisionData(
			ReadOnlySpan<byte> payload,
			FuturePinballRecord record,
			string sourceName)
		{
			const int shapeBytes = 44;
			if (payload.Length % shapeBytes != 0) {
				throw new FuturePinballFormatException(
					$"Collision payload length {payload.Length} is not a multiple of {shapeBytes}", sourceName, record.Offset
				);
			}
			var shapes = new List<FuturePinballCollisionShape>(payload.Length / shapeBytes);
			for (var offset = 0; offset < payload.Length; offset += shapeBytes) {
				shapes.Add(new FuturePinballCollisionShape {
					Type = ReadUInt32(payload, offset),
					GenerateHitEvent = ReadUInt32(payload, offset + 4) != 0,
					AffectsBall = ReadUInt32(payload, offset + 8) != 0,
					EventId = ReadUInt32(payload, offset + 12),
					X = ReadSingle(payload, offset + 16),
					Y = ReadSingle(payload, offset + 20),
					Z = ReadSingle(payload, offset + 24),
					Value1 = ReadSingle(payload, offset + 28),
					Value2 = ReadSingle(payload, offset + 32),
					Value3 = ReadSingle(payload, offset + 36),
					Value4 = ReadSingle(payload, offset + 40)
				});
			}
			return shapes;
		}

		private static FuturePinballChunkDescriptor FindDescriptor(
			FuturePinballRecordContext context,
			uint originalTag,
			out uint canonicalTag)
		{
			var descriptors = Descriptors(context);
			if (descriptors.TryGetValue(originalTag, out var descriptor)) {
				canonicalTag = originalTag;
				return descriptor;
			}
			if (originalTag >= LegacyTagCorrection) {
				var legacyTag = originalTag - LegacyTagCorrection;
				if (descriptors.TryGetValue(legacyTag, out descriptor)) {
					canonicalTag = legacyTag;
					return descriptor;
				}
			}
			canonicalTag = originalTag;
			return null;
		}

		private static IReadOnlyDictionary<uint, FuturePinballChunkDescriptor> Descriptors(FuturePinballRecordContext context)
		{
			switch (context) {
				case FuturePinballRecordContext.TableData: return TableDataDescriptors;
				case FuturePinballRecordContext.TableElement: return ElementDescriptors;
				case FuturePinballRecordContext.Resource: return ResourceDescriptors;
				case FuturePinballRecordContext.List: return ListDescriptors;
				case FuturePinballRecordContext.PinModel: return PinModelDescriptors;
				default: throw new ArgumentOutOfRangeException(nameof(context), context, null);
			}
		}

		private static Dictionary<uint, FuturePinballChunkDescriptor> CreateTableDataDescriptors()
		{
			var result = CommonEnd();
			Add(result, 0xA4F4D1D7, "name", FuturePinballValueKind.WideString);
			Add(result, 0xA5F8BBD1, "width", FuturePinballValueKind.Integer);
			Add(result, 0x9BFCC6D1, "length", FuturePinballValueKind.Integer);
			Add(result, 0xA1FACCD1, "front_glass_height", FuturePinballValueKind.Integer);
			Add(result, 0xA1FAC0D1, "rear_glass_height", FuturePinballValueKind.Integer);
			Add(result, 0x9AF5BFD1, "slope", FuturePinballValueKind.Float);
			Add(result, 0x9DF2CFD5, "playfield_color", FuturePinballValueKind.Color);
			Add(result, 0xA2F4C9D5, "playfield_texture", FuturePinballValueKind.String);
			Add(result, 0x9AFECBE3, "translite_color", FuturePinballValueKind.Color);
			Add(result, 0xA2F4C9E3, "translite_image", FuturePinballValueKind.String);
			Add(result, 0xA0EACBE3, "translite_width", FuturePinballValueKind.Integer);
			Add(result, 0xA4F9CBE3, "translite_height", FuturePinballValueKind.Integer);
			Add(result, 0x99E8BED8, "machine_type", FuturePinballValueKind.Integer);
			Add(result, 0xA2F4C9E2, "cabinet_texture", FuturePinballValueKind.String);
			Add(result, 0x9BEDC9D1, "table_name", FuturePinballValueKind.String);
			Add(result, 0xA4EBC9D1, "version", FuturePinballValueKind.String);
			Add(result, 0x9500C9D1, "authors", FuturePinballValueKind.String);
			Add(result, 0xA5EFC9D1, "release_date", FuturePinballValueKind.String);
			Add(result, 0x9CFCC9D1, "email", FuturePinballValueKind.String);
			Add(result, 0x96EAC9D1, "web_page", FuturePinballValueKind.String);
			Add(result, 0xA4FDC9D1, "description", FuturePinballValueKind.String);
			Add(result, 0x96EFC9D1, "rules_length", FuturePinballValueKind.Integer);
			Add(result, 0x99F5C9D1, "loading_picture", FuturePinballValueKind.String);
			Add(result, 0x95FDCDD2, "element_count", FuturePinballValueKind.Integer);
			Add(result, 0xA2F4C9D2, "image_count", FuturePinballValueKind.Integer);
			Add(result, 0xA5F3BFD2, "sound_count", FuturePinballValueKind.Integer);
			Add(result, 0x96ECC5D2, "music_count", FuturePinballValueKind.Integer);
			Add(result, 0xA5F2C5D2, "pin_model_count", FuturePinballValueKind.Integer);
			Add(result, 0x95F5C9D2, "image_list_count", FuturePinballValueKind.Integer);
			Add(result, 0x95F5C6D2, "light_list_count", FuturePinballValueKind.Integer);
			Add(result, 0x9BFBCED2, "dmd_font_count", FuturePinballValueKind.Integer);
			Add(result, ScriptTag, "script", FuturePinballValueKind.CompressedData);
			return result;
		}

		private static Dictionary<uint, FuturePinballChunkDescriptor> CreateResourceDescriptors()
		{
			var result = CommonEnd();
			Add(result, 0xA4F1B9D1, "type", FuturePinballValueKind.Integer);
			Add(result, 0xA4F4D1D7, "name", FuturePinballValueKind.String);
			Add(result, 0xA4F4C4DC, "id", FuturePinballValueKind.String);
			Add(result, 0xA1EDD1D5, "linked_path", FuturePinballValueKind.String);
			Add(result, 0x9EF3C6D9, "linked", FuturePinballValueKind.Integer);
			Add(result, 0xA6E9BEE4, "compression", FuturePinballValueKind.Integer);
			Add(result, 0x95F5CCE1, "disable_filtering", FuturePinballValueKind.Integer);
			Add(result, 0x96F3C0D1, "transparent_color", FuturePinballValueKind.Color);
			Add(result, 0xA4E7C9D2, "data_length", FuturePinballValueKind.Integer);
			Add(result, 0xA8EDD1E1, "data", FuturePinballValueKind.CompressedData);
			return result;
		}

		private static Dictionary<uint, FuturePinballChunkDescriptor> CreateListDescriptors()
		{
			var result = CommonEnd();
			Add(result, 0xA4F4D1D7, "name", FuturePinballValueKind.String);
			Add(result, 0xA8EDD1E1, "items", FuturePinballValueKind.StringList);
			return result;
		}

		private static Dictionary<uint, FuturePinballChunkDescriptor> CreatePinModelDescriptors()
		{
			var result = CommonEnd();
			Add(result, 0xA4F4D1D7, "name", FuturePinballValueKind.String);
			Add(result, 0xA4F4C4DC, "id", FuturePinballValueKind.String);
			Add(result, 0x9EF3C6D9, "linked", FuturePinballValueKind.Integer);
			Add(result, 0xA4F1B9D1, "type", FuturePinballValueKind.Integer);
			Add(result, 0x99E8BED8, "material_type", FuturePinballValueKind.Integer);
			Add(result, 0x9D00C4DC, "preview_path", FuturePinballValueKind.String);
			Add(result, 0x8FF8BFDC, "preview_data_length", FuturePinballValueKind.Integer);
			Add(result, 0x9600CEDC, "preview_data", FuturePinballValueKind.CompressedData);
			Add(result, 0x9AFEC2D5, "per_polygon_collision", FuturePinballValueKind.Integer);
			Add(result, 0xA8EFCBD3, "special_value", FuturePinballValueKind.Float);
			Add(result, 0x9D00C4D5, "primary_model_path", FuturePinballValueKind.String);
			Add(result, 0x8FF8BFD5, "primary_model_data_length", FuturePinballValueKind.Integer);
			Add(result, 0x9600CED5, "primary_model_data", FuturePinballValueKind.CompressedData);
			Add(result, 0x9D00C4D2, "secondary_model_path", FuturePinballValueKind.String);
			Add(result, 0x8FF8BFD2, "secondary_model_data_length", FuturePinballValueKind.Integer);
			Add(result, 0x9600CED2, "secondary_model_data", FuturePinballValueKind.CompressedData);
			Add(result, 0x9D00C4D1, "mask_model_path", FuturePinballValueKind.String);
			Add(result, 0x8FF8BFD1, "mask_model_data_length", FuturePinballValueKind.Integer);
			Add(result, 0x9600CED1, "mask_model_data", FuturePinballValueKind.CompressedData);
			Add(result, 0x9D00C4D3, "reflection_model_path", FuturePinballValueKind.String);
			Add(result, 0x8FF8BFD3, "reflection_model_data_length", FuturePinballValueKind.Integer);
			Add(result, 0x9600CED3, "reflection_model_data", FuturePinballValueKind.CompressedData);
			Add(result, 0x8FEEC3E2, "collision_shape_count", FuturePinballValueKind.Integer);
			Add(result, 0x93FBC3E2, "collision_shapes_enabled", FuturePinballValueKind.Integer);
			Add(result, 0x9DFCC3E2, "collision_shapes", FuturePinballValueKind.CollisionData);
			Add(result, 0xA1EDD1D5, "linked_path", FuturePinballValueKind.String);
			return result;
		}

		private static Dictionary<uint, FuturePinballChunkDescriptor> CreateElementDescriptors()
		{
			var result = CommonEnd();
			Add(result, 0xA4F4D1D7, "name", FuturePinballValueKind.WideString);
			Add(result, 0x9BFCCFCF, "position", FuturePinballValueKind.Vector2);
			Add(result, 0x9BFCCFDD, "glow_center", FuturePinballValueKind.Vector2);
			Add(result, 0x9DFDC3D8, "model", FuturePinballValueKind.String);
			Add(result, 0xA3EFBDD2, "surface", FuturePinballValueKind.String);
			Add(result, 0xA300C5DC, "texture", FuturePinballValueKind.String);
			Add(result, 0x97F5C3E2, "color", FuturePinballValueKind.Color);
			Add(result, 0xA8EDC3D3, "rotation", FuturePinballValueKind.Integer);
			Add(result, 0xA900BED2, "start_angle", FuturePinballValueKind.Integer);
			Add(result, 0xA2EABFE4, "swing", FuturePinballValueKind.Integer);
			Add(result, 0xA1FABED2, "strength", FuturePinballValueKind.Integer);
			Add(result, 0x9700C6E0, "elasticity", FuturePinballValueKind.Integer);
			Add(result, 0x9DFBCDD3, "reflects_off_playfield", FuturePinballValueKind.Integer);
			Add(result, 0xA0EED1D5, "passive", FuturePinballValueKind.Integer);
			Add(result, 0x9EEED1DD, "trigger_skirt", FuturePinballValueKind.Integer);
			Add(result, 0x9100BBD6, "one_way", FuturePinballValueKind.Integer);
			Add(result, 0x99F4D1E1, "damping", FuturePinballValueKind.Integer);
			Add(result, 0x96FBCCD6, "offset", FuturePinballValueKind.Integer);
			Add(result, 0xA5F2C5D3, "render_model", FuturePinballValueKind.Integer);
			Add(result, 0x99E8BEDA, "kicker_type", FuturePinballValueKind.Integer);
			Add(result, 0x9600BED2, "state", FuturePinballValueKind.Integer);
			Add(result, 0x95F3C9E3, "blink_interval", FuturePinballValueKind.Integer);
			Add(result, 0x9600C2E3, "blink_pattern", FuturePinballValueKind.String);
			Add(result, 0x9DF2CFD9, "lit_color", FuturePinballValueKind.Color);
			Add(result, 0x9DF2CFD0, "unlit_color", FuturePinballValueKind.Color);
			Add(result, 0x9D00C9E1, "diameter", FuturePinballValueKind.Integer);
			Add(result, 0x96FDD1D3, "glow_radius", FuturePinballValueKind.Integer);
			Add(result, 0x95EBCDDD, "generate_hit_event", FuturePinballValueKind.Integer);
			// Element-dependent: GuideWall stores a float while several display and guide types store an integer.
			Add(result, 0xA2F8CDDD, "height", FuturePinballValueKind.Opaque);
			Add(result, 0x95FDC9CE, "width", FuturePinballValueKind.Integer);
			Add(result, 0x95F3C2D2, "point", FuturePinballValueKind.Opaque);
			Add(result, 0xA1EDC5D2, "smooth", FuturePinballValueKind.Integer);
			Add(result, 0x99F2BEDD, "top_height", FuturePinballValueKind.Float);
			Add(result, 0x95F2D0DD, "bottom_height", FuturePinballValueKind.Float);
			Add(result, 0x9DF5C3E2, "collidable", FuturePinballValueKind.Integer);
			Add(result, 0x97FDC4D3, "render_object", FuturePinballValueKind.Integer);
			Add(result, 0x9DF2CFD1, "top_color", FuturePinballValueKind.Color);
			Add(result, 0xA2F4C9D1, "top_texture", FuturePinballValueKind.String);
			Add(result, 0x9DF2CFD2, "side_color", FuturePinballValueKind.Color);
			Add(result, 0xA2F4C9D2, "side_texture", FuturePinballValueKind.String);
			Add(result, 0x9C00C0D1, "transparency", FuturePinballValueKind.Integer);
			Add(result, 0x99E8BED8, "material_type", FuturePinballValueKind.Integer);
			Add(result, 0xA2F8CAD2, "start_height", FuturePinballValueKind.Integer);
			Add(result, 0xA2F8CAE0, "end_height", FuturePinballValueKind.Integer);
			Add(result, 0xA5F8BBD2, "start_width", FuturePinballValueKind.Integer);
			Add(result, 0xA5F8BBE0, "end_width", FuturePinballValueKind.Integer);
			Add(result, 0xA2F8CAD9, "left_side_height", FuturePinballValueKind.Integer);
			Add(result, 0xA2F8CAD3, "right_side_height", FuturePinballValueKind.Integer);
			Add(result, 0xA3F2C0D5, "ramp_profile", FuturePinballValueKind.Integer);
			Add(result, 0x9AF1CAE0, "ramp_end_point", FuturePinballValueKind.Integer);
			Add(result, 0xA4F5C9D8, "left_guide", FuturePinballValueKind.Integer);
			Add(result, 0xA4F5C2D0, "left_upper_guide", FuturePinballValueKind.Integer);
			Add(result, 0xA0EFC9D8, "right_guide", FuturePinballValueKind.Integer);
			Add(result, 0xA0EFC2D0, "right_upper_guide", FuturePinballValueKind.Integer);
			Add(result, 0x95ECC3D1, "top_wire", FuturePinballValueKind.Integer);
			Add(result, 0xA2F3C9D3, "ring_type", FuturePinballValueKind.Integer);
			Add(result, 0x9EFEC3D9, "locked", FuturePinballValueKind.Integer);
			Add(result, 0x9100C6E4, "layer", FuturePinballValueKind.Integer);
			return result;
		}

		private static Dictionary<uint, FuturePinballChunkDescriptor> CommonEnd()
		{
			return new Dictionary<uint, FuturePinballChunkDescriptor> {
				{ EndTag, new FuturePinballChunkDescriptor("end", FuturePinballValueKind.End) }
			};
		}

		private static void Add(
			IDictionary<uint, FuturePinballChunkDescriptor> descriptors,
			uint tag,
			string name,
			FuturePinballValueKind kind)
		{
			descriptors[tag] = new FuturePinballChunkDescriptor(name, kind);
		}

		private static uint ReadUInt32(byte[] data, int offset)
		{
			return ReadUInt32(new ReadOnlySpan<byte>(data), offset);
		}

		private static int ReadInt32(byte[] data, int offset)
		{
			return ReadInt32(new ReadOnlySpan<byte>(data), offset);
		}

		private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
		{
			return (uint)(data[offset]
				| data[offset + 1] << 8
				| data[offset + 2] << 16
				| data[offset + 3] << 24);
		}

		private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
		{
			return data[offset]
				| data[offset + 1] << 8
				| data[offset + 2] << 16
				| data[offset + 3] << 24;
		}

		private static float ReadSingle(ReadOnlySpan<byte> data, int offset)
		{
			return BitConverter.Int32BitsToSingle(ReadInt32(data, offset));
		}
	}
}
