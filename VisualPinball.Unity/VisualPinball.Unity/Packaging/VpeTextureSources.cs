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
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Loads the package's source texture layer into the runtime texture pipeline's internal
	/// shape: one packed byte blob plus per-texture payload descriptors carrying offsets into it.
	///
	/// The package stores textures as plain files under <c>table/textures/</c> (the zip central
	/// directory is the on-disk index); the blob is built transiently at load time so the
	/// cook/cache/upload pipeline downstream works off one contiguous buffer.
	/// </summary>
	public static class VpeTextureSources
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public sealed class Result
		{
			/// <summary>Internal texture descriptors, offsets pointing into <see cref="Blob"/>.</summary>
			public VpeTexturePayload[] Entries = Array.Empty<VpeTexturePayload>();
			/// <summary>Packed source bytes. Null when the package carries no textures.</summary>
			public byte[] Blob;
		}

		/// <summary>
		/// Converts a package texture into the internal payload descriptor the runtime texture
		/// pipeline (source blob, cook, cache, provider) operates on. Byte offsets are filled in
		/// when the source bytes are packed into the transient load-time blob.
		/// </summary>
		public static VpeTexturePayload ToPayload(VpeTexture texture)
		{
			return new VpeTexturePayload {
				Id = texture.Id,
				FileName = texture.FileName,
				ByteOffset = -1,
				ByteLength = 0,
				MimeType = texture.MimeType,
				PixelFormat = null,
				MipCount = 1,
				ColorSpace = texture.ColorSpace,
				WrapMode = (int)VpeMaterialEnums.ParseWrapMode(texture.WrapMode),
				FilterMode = (int)VpeMaterialEnums.ParseFilterMode(texture.FilterMode),
				AnisoLevel = texture.AnisoLevel,
				GenerateMipMaps = texture.GenerateMipMaps,
				RuntimeCompress = texture.RuntimeCompress,
				SourceName = texture.SourceName,
				Width = texture.Width,
				Height = texture.Height,
			};
		}

		public static VpeTexturePayload[] ToPayloads(IEnumerable<VpeTexture> textures)
		{
			return textures == null
				? Array.Empty<VpeTexturePayload>()
				: textures.Where(t => t != null).Select(ToPayload).ToArray();
		}

		/// <summary>
		/// Reads the source layer: per-file entries under table/textures/, packed into one
		/// transient blob in payload order.
		/// </summary>
		public static Result Load(IPackageFolder tableFolder, VpeMaterialsPayload payload)
		{
			var entries = ToPayloads(payload?.Textures);
			var result = new Result { Entries = entries };
			if (entries.Length == 0 || tableFolder == null
				|| !tableFolder.TryGetFolder(PackageApi.TexturesFolder, out var texturesFolder)) {
				return result;
			}

			using var stream = new MemoryStream();
			foreach (var entry in entries) {
				if (string.IsNullOrWhiteSpace(entry.FileName)) {
					continue;
				}
				if (!texturesFolder.TryGetFile(entry.FileName, out var file)) {
					Logger.Warn($"Source texture '{entry.FileName}' (id '{entry.Id}') is missing from {PackageApi.TableFolder}/{PackageApi.TexturesFolder}/.");
					continue;
				}
				var data = file.GetData();
				if (data == null || data.Length == 0) {
					continue;
				}
				if (stream.Length + data.LongLength > int.MaxValue) {
					// The transient blob uses int offsets; this is a reader limitation, not a
					// format one (the package itself has no such limit).
					throw new InvalidOperationException(
						"The package's source textures exceed 2 GB; this reader cannot pack them into one load-time blob.");
				}
				entry.ByteOffset = (int)stream.Position;
				entry.ByteLength = data.Length;
				stream.Write(data, 0, data.Length);
			}

			result.Blob = stream.Length > 0 ? stream.ToArray() : null;
			return result;
		}
	}
}
