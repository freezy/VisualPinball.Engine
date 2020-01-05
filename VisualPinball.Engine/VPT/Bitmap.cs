using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenMcdf;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// An bitmap blob, typically used by textures.
	///
	/// See "BaseTexture" class in VP.
	/// </summary>
	public class Bitmap : IBinaryData
	{
		public const int RGBA = 0;
		public const int RGB_FP = 1;

		public byte[] Bytes => _data;
		public byte[] FileContent => GetHeader().Concat(_data).ToArray();


		private readonly int _width;
		private readonly int _height;
		private readonly int _format;

		private int _compressedLen;
		private readonly byte[] _data;

		public Bitmap(BinaryReader reader, int width, int height, int format = RGBA)
		{
			_width = width;
			_height = height;
			_format = format;

			var remainingLen = (int) (reader.BaseStream.Length - reader.BaseStream.Position);
			var compressed = reader.ReadBytes(remainingLen);
			var pitch = Pitch();

			var lzw = new LzwReader(compressed, width * 4, height, pitch);
			lzw.Decompress(out _data, out _compressedLen);

			var lenDiff = remainingLen - _compressedLen;
			reader.BaseStream.Seek(-lenDiff, SeekOrigin.Current);

			// Assume our 32 bit color structure
			// Find out if all alpha values are zero
			var pch = _data;
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

			_data = RgbToBgr(width, height);
		}

		private byte[] RgbToBgr(int width, int height) {
			var pitch = Pitch();
			var from = _data;
			var to = new byte[pitch * height];
			for (var i = 0; i < height; i++) {
				for (var l = 0; l < width; l++) {
					if (_format == RGBA) {
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

		private IEnumerable<byte> GetHeader()
		{
			const int headerSize = 54;
			var header = new byte[headerSize];
			var stream = new BinaryWriter(new MemoryStream(header));

			var surfWidth = _width;                        // texture width
			var surfHeight = _height;                      // and height
			var bmpLineSize = (surfWidth * 4 + 3) & -4;    // line size ... 4 bytes per pixel + pad to 4 byte boundary

			// file header
			stream.Write((byte) 0x42);                                    // type
			stream.Write((byte) 0x4d);
			stream.Write((uint) (headerSize + surfHeight * bmpLineSize)); // size
			stream.Write((short) 0);                                      // reserved 1
			stream.Write((short) 0);                                      // reserved 2
			stream.Write((uint) headerSize);                              // off bits

			// bitmap info header
			stream.Write((uint) 40);                         // size
			stream.Write(surfWidth);                         // width
			stream.Write(surfHeight);                        // height
			stream.Write((ushort) 1);                        // planes
			stream.Write((ushort) 32);                       // bit count
			stream.Write((uint) 0);                          // compression
			stream.Write((uint) (surfHeight * bmpLineSize)); // size image
			stream.Write(0);                                 // x pels per meter
			stream.Write(0);                                 // y pels per meter
			stream.Write((uint) 0);                          // clr used
			stream.Write((uint) 0);                          // clr important

			return header;
		}

		private int Pitch() {
			return (_format == RGBA ? 4 : 3 * 4) * _width;
		}
	}
}
