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

namespace VisualPinball.Unity
{
	// Raw GPU pixel formats for cooked texture payloads in the local texture cache. When
	// VpeTexturePayload.PixelFormat carries one of these, the payload is the exact byte layout of
	// Texture2D.GetRawTextureData() for that format (all mip levels concatenated), and the
	// reader uploads it via LoadRawTextureData without any decode step.
	public static class VpePixelFormats
	{
		// BC7 RGBA, used for sRGB color textures and linear mask/thickness data.
		public const string Bc7 = "bc7";
		// DXT5/BC3. Used for normal maps in DXT5nm-style AG packing (X in alpha, Y in green).
		public const string Dxt5 = "dxt5";
		// Uncompressed RGBA32 fallback for textures whose dimensions block compression can't handle.
		public const string Rgba32 = "rgba32";
	}

	/// <summary>
	/// Runtime-internal texture descriptor used by the load pipeline: the package's source
	/// textures normalized into one transient blob (see <see cref="VpeTextureSources"/>), the
	/// cooked GPU payloads of the local texture cache (see <see cref="VpeTextureCache"/>), and
	/// the upload path of the material reader. This is NOT part of the package format — the
	/// on-disk schema is <see cref="VpeTexture"/>.
	/// </summary>
	[Serializable]
	public class VpeTexturePayload
	{
		// Stable id referenced by VpeTextureRef.TextureId.
		public string Id;
		// Source file name (entry under table/textures/).
		public string FileName;
		// Byte range inside the packed blob (transient source blob or cooked cache blob).
		public int ByteOffset = -1;
		public int ByteLength;
		// MIME type of the encoded source bytes.
		public string MimeType = "image/png";
		// When set (see VpePixelFormats), the payload is raw GPU-ready pixel data instead of an
		// encoded image; MimeType, GenerateMipMaps and RuntimeCompress are ignored on that path.
		public string PixelFormat;
		// Number of mip levels contained in the raw payload. Only used when PixelFormat is set.
		public int MipCount = 1;
		// "sRGB" or "Linear". See VpeColorSpaces.
		public string ColorSpace = VpeColorSpaces.SRgb;
		public int WrapMode;       // UnityEngine.TextureWrapMode
		public int FilterMode = 2; // Trilinear
		public int AnisoLevel = 1;
		public bool GenerateMipMaps = true;
		// Whether a reader without a cook path should GPU-compress after decoding.
		public bool RuntimeCompress = true;
		// Optional source hint for debugging.
		public string SourceName;
		public int Width;
		public int Height;
	}
}
