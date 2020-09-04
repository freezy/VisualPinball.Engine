// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// An bitmap blob, typically used by textures.
	///
	/// See "BaseTexture" class in VP.
	/// </summary>
	[Serializable]
	public class Bitmap : IImageData
	{
		public const int RGBA = 0;
		public const int RGB_FP = 1;

		public byte[] Bytes => Data;
		public byte[] FileContent => GetHeader().Concat(GetBody()).ToArray();

		public int Width;
		public int Height;
		public int Format;
		[NonSerialized]
		public byte[] Data;

		private int _compressedLen;

		public Bitmap(BinaryReader reader, int width, int height, int format = RGBA)
		{
			Width = width;
			Height = height;
			Format = format;

			var remainingLen = (int) (reader.BaseStream.Length - reader.BaseStream.Position);
			var compressed = reader.ReadBytes(remainingLen);
			var pitch = Pitch();

			var lzw = new LzwReader(compressed, width * 4, height, pitch);
			lzw.Decompress(out Data, out _compressedLen);

			var lenDiff = remainingLen - _compressedLen;
			reader.BaseStream.Seek(-lenDiff, SeekOrigin.Current);

			// Assume our 32 bit color structure
			// Find out if all alpha values are zero
			var pch = Data;
			var allAlphaZero = true;
			for (var i = 0; i < height && allAlphaZero; i++) {
				for (var l = 0; l < width; l++) {
					if (pch[i * pitch + 4 * l + 3] == 0) {
						continue;
					}
					allAlphaZero = false;
					break;
				}
			}

			// all alpha values are 0: set them all to 0xff
			if (allAlphaZero) {
				for (var i = 0; i < height; i++) {
					for (var l = 0; l < width; l++) {
						pch[i * pitch + 4 * l + 3] = 0xff;
					}
				}
			}

			Data = ToggleRgbBgr(Data);
		}

		public void WriteCompressed(BinaryWriter writer)
		{
			var lzwWriter = new LzwWriter(writer, ToggleRgbBgr(Data), Width * 4, Height, Pitch());
			lzwWriter.CompressBits(8 + 1);
		}

		private IEnumerable<byte> GetHeader()
		{
			const int headerSize = 54;
			var header = new byte[headerSize];
			var bmpLineSize = (Width * 4 + 3) & -4;    // line size ... 4 bytes per pixel + pad to 4 byte boundary

			using (var stream = new MemoryStream(header))
			using (var writer = new BinaryWriter(stream)) {

				// file header
				writer.Write((byte) 0x42);                                    // type
				writer.Write((byte) 0x4d);
				writer.Write((uint) (headerSize + Height * bmpLineSize));     // size
				writer.Write((short) 0);                                      // reserved 1
				writer.Write((short) 0);                                      // reserved 2
				writer.Write((uint) headerSize);                              // off bits

				// bitmap info header
				writer.Write((uint) 40);                         // size
				writer.Write(Width);                             // width
				writer.Write(Height);                            // height
				writer.Write((ushort) 1);                        // planes
				writer.Write((ushort) 32);                       // bit count
				writer.Write((uint) 0);                          // compression
				writer.Write((uint) (Height * bmpLineSize));     // size image
				writer.Write(0);                                 // x pels per meter
				writer.Write(0);                                 // y pels per meter
				writer.Write((uint) 0);                          // clr used
				writer.Write((uint) 0);                          // clr important
			}
			return header;
		}

		private IEnumerable<byte> GetBody()
		{
			var body = new byte[Data.Length];
			var lineSize = Data.Length / Height;
			for (var i = Height - 1; i >= 0; i--) {
				Array.Copy(Data, i * lineSize, body, (Height - i - 1) * lineSize, lineSize);
			}

			return ToggleRgbBgr(body);
		}

		private byte[] ToggleRgbBgr(IReadOnlyList<byte> from) {
			var pitch = Pitch();
			var to = new byte[pitch * Height];
			for (var i = 0; i < Height; i++) {
				for (var l = 0; l < Width; l++) {
					if (Format == RGBA) {
						to[i * pitch + 4 * l] = from[i * pitch + 4 * l + 2];     // r
						to[i * pitch + 4 * l + 1] = from[i * pitch + 4 * l + 1]; // g
						to[i * pitch + 4 * l + 2] = from[i * pitch + 4 * l];     // b
						to[i * pitch + 4 * l + 3] = from[i * pitch + 4 * l + 3]; // a

					} else {
						to[i * pitch + 4 * l] = from[i * pitch + 4 * l + 6];     // r
						to[i * pitch + 4 * l + 1] = from[i * pitch + 4 * l + 7];
						to[i * pitch + 4 * l + 2] = from[i * pitch + 4 * l + 8];

						to[i * pitch + 4 * l + 3] = from[i * pitch + 4 * l + 3]; // g
						to[i * pitch + 4 * l + 4] = from[i * pitch + 4 * l + 4];
						to[i * pitch + 4 * l + 5] = from[i * pitch + 4 * l + 5];

						to[i * pitch + 4 * l + 6] = from[i * pitch + 4 * l];     // b
						to[i * pitch + 4 * l + 7] = from[i * pitch + 4 * l + 1];
						to[i * pitch + 4 * l + 8] = from[i * pitch + 4 * l + 2];

						to[i * pitch + 4 * l + 9] = from[i * pitch + 4 * l + 9]; // a
					}
				}
			}
			return to;
		}

		public int Pitch() {
			return (Format == RGBA ? 4 : 3 * 4) * Width;
		}
	}
}
