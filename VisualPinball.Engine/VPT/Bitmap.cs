using System.Data.Common;
using System.IO;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// An bitmap blob, typically used by textures.
	///
	/// See "BaseTexture" class in VP.
	/// </summary>
	public class Bitmap
	{
		public const int RGBA = 0;
		public const int RGB_FP = 1;

		private int _width;
		private int _height;
		public int Format = RGBA;

		private int _compressedLen;
		private byte[] _data;

		public Bitmap(BinaryReader reader, int width, int height, int format = RGBA)
		{
			_width = width;
			_height = height;
			Format = format;

			var remainingLen = (int) (reader.BaseStream.Length - reader.BaseStream.Position);
			var compressed = reader.ReadBytes(remainingLen);
			var pitch = this.pitch();

			var lzw = new LzwReader(compressed, width * 4, height, pitch);
			lzw.decompress(out _data, out _compressedLen);

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

			_data = rgbToBgr(width, height);
		}

		private byte[] rgbToBgr(int width, int height) {
			var pitch = this.pitch();
			var from = _data;
			var to = new byte[pitch * height];
			for (var i = 0; i < height; i++) {
				for (var l = 0; l < width; l++) {
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

		private int pitch() {
			return (Format == RGBA ? 4 : 3 * 4) * _width;
		}
	}
}
