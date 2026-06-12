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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Minimal GLB (binary glTF 2.0) chunk reader/writer shared by the package post-processors
	/// (node ids, image replacement). Operates on raw bytes; no glTFast involved.
	/// </summary>
	public static class GlbJsonUtil
	{
		public const uint GlbMagic = 0x46546C67;
		public const uint GlbVersion = 2;
		public const uint JsonChunkType = 0x4E4F534A;
		public const uint BinChunkType = 0x004E4942;

		public readonly struct GlbChunk
		{
			public readonly uint Type;
			public readonly byte[] Data;

			public GlbChunk(uint type, byte[] data)
			{
				Type = type;
				Data = data ?? Array.Empty<byte>();
			}
		}

		public static List<GlbChunk> ReadChunks(byte[] glbData)
		{
			if (glbData == null || glbData.Length < 12) {
				throw new InvalidOperationException("GLB is shorter than the 12-byte header.");
			}

			var magic = BinaryPrimitives.ReadUInt32LittleEndian(glbData.AsSpan(0, 4));
			var version = BinaryPrimitives.ReadUInt32LittleEndian(glbData.AsSpan(4, 4));
			var declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(glbData.AsSpan(8, 4));
			if (magic != GlbMagic) {
				throw new InvalidOperationException("Data is not a GLB file.");
			}
			if (version != GlbVersion) {
				throw new InvalidOperationException($"Unsupported GLB version {version}.");
			}
			if (declaredLength != glbData.Length) {
				throw new InvalidOperationException($"GLB header length {declaredLength} does not match data length {glbData.Length}.");
			}

			var chunks = new List<GlbChunk>();
			var offset = 12;
			while (offset < glbData.Length) {
				if (offset + 8 > glbData.Length) {
					throw new InvalidOperationException("GLB chunk header is truncated.");
				}

				var length = BinaryPrimitives.ReadInt32LittleEndian(glbData.AsSpan(offset, 4));
				var type = BinaryPrimitives.ReadUInt32LittleEndian(glbData.AsSpan(offset + 4, 4));
				offset += 8;

				if (length < 0 || offset + length > glbData.Length) {
					throw new InvalidOperationException("GLB chunk length is invalid.");
				}

				var data = new byte[length];
				Buffer.BlockCopy(glbData, offset, data, 0, length);
				chunks.Add(new GlbChunk(type, data));
				offset += length;
			}

			return chunks;
		}

		public static byte[] WriteChunks(IReadOnlyList<GlbChunk> chunks)
		{
			var totalLength = 12;
			foreach (var chunk in chunks) {
				totalLength += 8 + chunk.Data.Length;
			}

			var glbData = new byte[totalLength];
			BinaryPrimitives.WriteUInt32LittleEndian(glbData.AsSpan(0, 4), GlbMagic);
			BinaryPrimitives.WriteUInt32LittleEndian(glbData.AsSpan(4, 4), GlbVersion);
			BinaryPrimitives.WriteUInt32LittleEndian(glbData.AsSpan(8, 4), (uint)totalLength);

			var offset = 12;
			foreach (var chunk in chunks) {
				BinaryPrimitives.WriteInt32LittleEndian(glbData.AsSpan(offset, 4), chunk.Data.Length);
				BinaryPrimitives.WriteUInt32LittleEndian(glbData.AsSpan(offset + 4, 4), chunk.Type);
				offset += 8;
				Buffer.BlockCopy(chunk.Data, 0, glbData, offset, chunk.Data.Length);
				offset += chunk.Data.Length;
			}

			return glbData;
		}

		public static JObject ParseJsonChunk(byte[] jsonChunk)
		{
			var json = Encoding.UTF8.GetString(jsonChunk).TrimEnd('\0', ' ', '\t', '\r', '\n');
			return JObject.Parse(json);
		}

		/// <summary>
		/// Parses the JSON chunk of a GLB without copying the binary chunk. Returns null when the
		/// data is not a well-formed GLB.
		/// </summary>
		public static JObject TryParseRoot(byte[] glbData)
		{
			try {
				var chunks = ReadChunks(glbData);
				var jsonChunk = chunks.Find(chunk => chunk.Type == JsonChunkType);
				return jsonChunk.Data == null || jsonChunk.Data.Length == 0 ? null : ParseJsonChunk(jsonChunk.Data);
			} catch (Exception) {
				return null;
			}
		}

		/// <summary>
		/// Re-serializes the JSON chunk (4-byte aligned, space-padded per spec) and returns the
		/// rewritten GLB, keeping all other chunks as they were.
		/// </summary>
		public static byte[] ReplaceJsonChunk(byte[] glbData, JObject root)
		{
			var chunks = ReadChunks(glbData);
			var jsonChunkIndex = chunks.FindIndex(chunk => chunk.Type == JsonChunkType);
			if (jsonChunkIndex < 0) {
				throw new InvalidOperationException("GLB does not contain a JSON chunk.");
			}
			chunks[jsonChunkIndex] = new GlbChunk(JsonChunkType, SerializeJsonChunk(root));
			return WriteChunks(chunks);
		}

		public static byte[] SerializeJsonChunk(JObject root)
		{
			var json = root.ToString(Formatting.None);
			var bytes = Encoding.UTF8.GetBytes(json);
			var paddedLength = AlignTo4(bytes.Length);
			if (paddedLength == bytes.Length) {
				return bytes;
			}

			var padded = new byte[paddedLength];
			Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
			for (var i = bytes.Length; i < padded.Length; i++) {
				padded[i] = 0x20;
			}
			return padded;
		}

		public static int AlignTo4(int value)
		{
			return (value + 3) & ~3;
		}
	}
}
