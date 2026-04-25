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
	// Minimal GLB post-process helper for carrying VPE material metadata inside the exported
	// table.glb. Keeps the existing payload shape intact so runtime can migrate without a second
	// schema translation step.
	public static class VpeMaterialsGltfExtension
	{
		public const string ExtensionName = "VPE_materials";

		private const uint GlbMagic = 0x46546C67;
		private const uint GlbVersion = 2;
		private const uint JsonChunkType = 0x4E4F534A;

		public static bool TryReadPayload(byte[] glbData, out VpeMaterialsPayloadV1 payload)
		{
			payload = null;
			if (!TryReadRoot(glbData, out var root)) {
				return false;
			}

			var payloadToken = root["extensions"]?[ExtensionName];
			if (payloadToken == null) {
				return false;
			}

			try {
				var payloadJson = payloadToken.ToString();
				payload = PackageApi.Packer.Unpack<VpeMaterialsPayloadV1>(Encoding.UTF8.GetBytes(payloadJson));
				return payload != null;
			} catch (Exception) {
				return false;
			}
		}

		public static byte[] WritePayload(byte[] glbData, VpeMaterialsPayloadV1 payload)
		{
			if (glbData == null || glbData.Length == 0) {
				throw new ArgumentException("GLB data is missing.", nameof(glbData));
			}
			if (payload == null) {
				return glbData;
			}

			var chunks = ReadChunks(glbData);
			var jsonChunkIndex = chunks.FindIndex(chunk => chunk.Type == JsonChunkType);
			if (jsonChunkIndex < 0) {
				throw new InvalidOperationException("GLB does not contain a JSON chunk.");
			}

			var root = ParseRoot(chunks[jsonChunkIndex].Data);
			var extensions = root["extensions"] as JObject ?? new JObject();
			root["extensions"] = extensions;
			var payloadJson = Encoding.UTF8.GetString(PackageApi.Packer.Pack(payload));
			extensions[ExtensionName] = JToken.Parse(payloadJson);
			EnsureStringArrayContains(root, "extensionsUsed", ExtensionName);

			chunks[jsonChunkIndex] = new GlbChunk(JsonChunkType, SerializeJsonChunk(root));
			return WriteChunks(chunks);
		}

		private static bool TryReadRoot(byte[] glbData, out JObject root)
		{
			root = null;
			try {
				var chunks = ReadChunks(glbData);
				var jsonChunk = chunks.Find(chunk => chunk.Type == JsonChunkType);
				if (jsonChunk.Data == null) {
					return false;
				}

				root = ParseRoot(jsonChunk.Data);
				return true;
			} catch (Exception) {
				return false;
			}
		}

		private static List<GlbChunk> ReadChunks(byte[] glbData)
		{
			if (glbData.Length < 12) {
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

		private static byte[] WriteChunks(IReadOnlyList<GlbChunk> chunks)
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

		private static JObject ParseRoot(byte[] jsonChunk)
		{
			var json = Encoding.UTF8.GetString(jsonChunk).TrimEnd('\0', ' ', '\t', '\r', '\n');
			return JObject.Parse(json);
		}

		private static byte[] SerializeJsonChunk(JObject root)
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

		private static void EnsureStringArrayContains(JObject root, string propertyName, string value)
		{
			var array = root[propertyName] as JArray ?? new JArray();
			root[propertyName] = array;
			foreach (var token in array) {
				if (string.Equals(token.Value<string>(), value, StringComparison.Ordinal)) {
					return;
				}
			}
			array.Add(value);
		}

		private static int AlignTo4(int value)
		{
			return (value + 3) & ~3;
		}

		private readonly struct GlbChunk
		{
			public readonly uint Type;
			public readonly byte[] Data;

			public GlbChunk(uint type, byte[] data)
			{
				Type = type;
				Data = data ?? Array.Empty<byte>();
			}
		}
	}
}
