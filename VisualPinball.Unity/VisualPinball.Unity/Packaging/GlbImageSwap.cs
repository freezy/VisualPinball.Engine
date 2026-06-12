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
using Newtonsoft.Json.Linq;

namespace VisualPinball.Unity
{
	// GLB post-processing for the lossless-image guarantee: glTFast re-encodes images on export
	// (JPEG for opaque base color), so the writer swaps the affected GLB images back to the
	// original asset bytes. This covers the materials that are NOT captured into the material
	// payload (unsupported shaders) — captured materials have their textures stripped from the
	// GLB entirely and ship in the source layer instead.
	public static class GlbImageSwap
	{
		public sealed class ImageReplacement
		{
			public string Name;
			public string MimeType;
			public byte[] Data;
		}

		public static byte[] ReplaceImages(
			byte[] glbData,
			IReadOnlyDictionary<string, ImageReplacement> replacementsByImageName,
			out int replacementCount,
			out long originalBytes,
			out long replacementBytes)
		{
			replacementCount = 0;
			originalBytes = 0L;
			replacementBytes = 0L;
			if (glbData == null || glbData.Length == 0 || replacementsByImageName == null || replacementsByImageName.Count == 0) {
				return glbData;
			}

			var chunks = GlbJsonUtil.ReadChunks(glbData);
			var jsonChunkIndex = chunks.FindIndex(chunk => chunk.Type == GlbJsonUtil.JsonChunkType);
			var binChunkIndex = chunks.FindIndex(chunk => chunk.Type == GlbJsonUtil.BinChunkType);
			if (jsonChunkIndex < 0 || binChunkIndex < 0) {
				return glbData;
			}

			var root = GlbJsonUtil.ParseJsonChunk(chunks[jsonChunkIndex].Data);
			if (root["images"] is not JArray images || root["bufferViews"] is not JArray bufferViews) {
				return glbData;
			}

			var oldBinData = chunks[binChunkIndex].Data;
			var originalBufferViewCount = bufferViews.Count;
			var newBinStream = new List<byte>(oldBinData.Length);
			var remappedBufferViews = new Dictionary<int, int>();

			for (var imageIndex = 0; imageIndex < images.Count; imageIndex++) {
				if (images[imageIndex] is not JObject image) {
					continue;
				}

				var imageName = image.Value<string>("name");
				if (string.IsNullOrWhiteSpace(imageName)
					|| !replacementsByImageName.TryGetValue(imageName, out var replacement)
					|| replacement?.Data == null
					|| replacement.Data.Length == 0) {
					continue;
				}

				var bufferViewIndex = image.Value<int?>("bufferView") ?? -1;
				if (bufferViewIndex < 0
					|| bufferViewIndex >= bufferViews.Count
					|| bufferViews[bufferViewIndex] is not JObject oldBufferView
					|| !TryReadBufferViewData(oldBufferView, oldBinData, out var oldData)) {
					continue;
				}

				var newBufferViewIndex = AppendReplacementBufferView(
					bufferViews,
					newBinStream,
					remappedBufferViews,
					bufferViewIndex,
					oldBufferView,
					replacement);
				image["bufferView"] = newBufferViewIndex;
				if (!string.IsNullOrWhiteSpace(replacement.Name)) {
					image["name"] = replacement.Name;
				}
				if (!string.IsNullOrWhiteSpace(replacement.MimeType)) {
					image["mimeType"] = replacement.MimeType;
				}

				replacementCount++;
				originalBytes += oldData.LongLength;
				replacementBytes += replacement.Data.LongLength;
			}

			if (replacementCount == 0) {
				return glbData;
			}

			AppendUnchangedBufferViews(bufferViews, originalBufferViewCount, oldBinData, newBinStream, remappedBufferViews);
			UpdateBufferViewReferences(root, remappedBufferViews);
			var newBinData = AlignBinaryChunk(newBinStream.ToArray());
			UpdateRootBufferLength(root, newBinData.Length);

			chunks[jsonChunkIndex] = new GlbJsonUtil.GlbChunk(GlbJsonUtil.JsonChunkType, GlbJsonUtil.SerializeJsonChunk(root));
			chunks[binChunkIndex] = new GlbJsonUtil.GlbChunk(GlbJsonUtil.BinChunkType, newBinData);
			return GlbJsonUtil.WriteChunks(chunks);
		}

		private static int AppendReplacementBufferView(
			JArray bufferViews,
			List<byte> newBinStream,
			Dictionary<int, int> remappedBufferViews,
			int oldBufferViewIndex,
			JObject oldBufferView,
			ImageReplacement replacement)
		{
			var byteOffset = AppendAligned(newBinStream, replacement.Data);
			var newBufferView = new JObject {
				["buffer"] = 0,
				["byteOffset"] = byteOffset,
				["byteLength"] = replacement.Data.Length
			};
			if (oldBufferView.TryGetValue("name", out var name)) {
				newBufferView["name"] = name.DeepClone();
			}
			bufferViews[oldBufferViewIndex] = newBufferView;
			remappedBufferViews[oldBufferViewIndex] = oldBufferViewIndex;
			return oldBufferViewIndex;
		}

		private static void AppendUnchangedBufferViews(
			JArray bufferViews,
			int originalBufferViewCount,
			byte[] oldBinData,
			List<byte> newBinStream,
			Dictionary<int, int> remappedBufferViews)
		{
			for (var i = 0; i < originalBufferViewCount; i++) {
				if (remappedBufferViews.ContainsKey(i) || bufferViews[i] is not JObject oldBufferView) {
					continue;
				}
				if (!TryReadBufferViewData(oldBufferView, oldBinData, out var data)) {
					continue;
				}

				var byteOffset = AppendAligned(newBinStream, data);
				oldBufferView["buffer"] = 0;
				oldBufferView["byteOffset"] = byteOffset;
				oldBufferView["byteLength"] = data.Length;
				remappedBufferViews[i] = i;
			}
		}

		private static int AppendAligned(List<byte> stream, byte[] data)
		{
			var byteOffset = GlbJsonUtil.AlignTo4(stream.Count);
			while (stream.Count < byteOffset) {
				stream.Add(0);
			}
			stream.AddRange(data);
			return byteOffset;
		}

		private static void UpdateBufferViewReferences(JObject root, IReadOnlyDictionary<int, int> remappedBufferViews)
		{
			foreach (var token in root.DescendantsAndSelf()) {
				if (token is not JProperty property
					|| !string.Equals(property.Name, "bufferView", StringComparison.Ordinal)
					|| property.Value.Type != JTokenType.Integer) {
					continue;
				}
				var oldIndex = property.Value.Value<int>();
				if (remappedBufferViews.TryGetValue(oldIndex, out var newIndex)) {
					property.Value = newIndex;
				}
			}
		}

		private static byte[] AlignBinaryChunk(byte[] binData)
		{
			if (binData == null || binData.Length == 0) {
				return Array.Empty<byte>();
			}

			var paddedLength = GlbJsonUtil.AlignTo4(binData.Length);
			if (paddedLength == binData.Length) {
				return binData;
			}

			var padded = new byte[paddedLength];
			Buffer.BlockCopy(binData, 0, padded, 0, binData.Length);
			return padded;
		}

		private static void UpdateRootBufferLength(JObject root, int byteLength)
		{
			if (root["buffers"] is not JArray buffers || buffers.Count == 0) {
				root["buffers"] = new JArray { new JObject { ["byteLength"] = byteLength } };
				return;
			}
			if (buffers[0] is not JObject buffer) {
				buffer = new JObject();
				buffers[0] = buffer;
			}
			buffer["byteLength"] = byteLength;
		}

		private static bool TryReadBufferViewData(JObject bufferView, byte[] binData, out byte[] data)
		{
			data = null;
			var bufferIndex = bufferView.Value<int?>("buffer") ?? 0;
			if (bufferIndex != 0) {
				return false;
			}

			var byteOffset = bufferView.Value<int?>("byteOffset") ?? 0;
			var byteLength = bufferView.Value<int?>("byteLength") ?? 0;
			if (byteOffset < 0 || byteLength <= 0 || byteOffset + byteLength > binData.Length) {
				return false;
			}

			data = new byte[byteLength];
			Buffer.BlockCopy(binData, byteOffset, data, 0, byteLength);
			return true;
		}
	}
}
