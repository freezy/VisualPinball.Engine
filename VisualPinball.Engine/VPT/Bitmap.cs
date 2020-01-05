using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		public byte[] FileContent => GetHeader().Concat(GetBody()).ToArray();


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

			_data = ToggleRgbBgr(_data);
		}

		private IEnumerable<byte> GetHeader()
		{
			const int headerSize = 54;
			var header = new byte[headerSize];
			var stream = new BinaryWriter(new MemoryStream(header));
			var bmpLineSize = (_width * 4 + 3) & -4;    // line size ... 4 bytes per pixel + pad to 4 byte boundary

			// file header
			stream.Write((byte) 0x42);                                    // type
			stream.Write((byte) 0x4d);
			stream.Write((uint) (headerSize + _height * bmpLineSize)); // size
			stream.Write((short) 0);                                      // reserved 1
			stream.Write((short) 0);                                      // reserved 2
			stream.Write((uint) headerSize);                              // off bits

			// bitmap info header
			stream.Write((uint) 40);                         // size
			stream.Write(_width);                            // width
			stream.Write(_height);                           // height
			stream.Write((ushort) 1);                        // planes
			stream.Write((ushort) 32);                       // bit count
			stream.Write((uint) 0);                          // compression
			stream.Write((uint) (_height * bmpLineSize));    // size image
			stream.Write(0);                                 // x pels per meter
			stream.Write(0);                                 // y pels per meter
			stream.Write((uint) 0);                          // clr used
			stream.Write((uint) 0);                          // clr important

			return header;
		}

		private IEnumerable<byte> GetBody()
		{
			var timer = new Stopwatch();
			timer.Stop();
			var body = new byte[_data.Length];
			var lineSize = _data.Length / _height;
			for (var i = _height - 1; i >= 0; i--) {
				Array.Copy(_data, i * lineSize, body, (_height - i - 1) * lineSize, lineSize);
			}
			timer.Stop();
			Console.WriteLine("Re-ordered after {0}ms", timer.ElapsedMilliseconds);

			return ToggleRgbBgr(body);
		}

		private byte[] ToggleRgbBgr(IReadOnlyList<byte> from) {
			var pitch = Pitch();
			var to = new byte[pitch * _height];
			for (var i = 0; i < _height; i++) {
				for (var l = 0; l < _width; l++) {
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

		private int Pitch() {
			return (_format == RGBA ? 4 : 3 * 4) * _width;
		}
	}
}
