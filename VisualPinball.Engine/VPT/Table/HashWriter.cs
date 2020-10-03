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
using System.Text;

namespace VisualPinball.Engine.VPT.Table
{
	public class HashWriter : IDisposable
	{
		private readonly MemoryStream _memoryStream;
		private readonly BinaryWriter _writer;

		public HashWriter()
		{
			_memoryStream = new MemoryStream();
			_writer = new BinaryWriter(_memoryStream);

			// header is always there.
			Write(Encoding.Default.GetBytes("Visual Pinball"));
		}

		public void Write(byte[] data)
		{
			_writer.Write(data);
		}

		public byte[] Hash()
		{
			return Md2.Hash(_memoryStream.ToArray());
		}

		public void Dispose()
		{
			_writer.Dispose();
			_memoryStream.Dispose();
		}
	}

	/// <summary>
	/// Visual Pinball uses MD2 to hash.
	///
	/// That's not something the stdlib supports, so let's implement it here!
	/// </summary>
	internal static class Md2
	{
		private const int BlockSize = 16;

		public static byte[] Hash(byte[] src)
		{
			src = PaddingData(src);
			src = AddCheckSum(src);
			return Round(src);
		}

		private static byte[] PaddingData(byte[] dz) {
			var n = dz.Length;
			byte[] resizedDz;
			var md = BlockSize - n % BlockSize;
			if (md != 0) {
				resizedDz = new byte[dz.Length + md];
				Array.Copy(dz, 0, resizedDz, 0, dz.Length);

			} else {
				resizedDz = dz;
			}
			for (var i = 0; i < md; i++, n++) {
				resizedDz[n] = (byte)md;
			}
			return resizedDz;
		}

		private static byte[] AddCheckSum(IReadOnlyList<byte> dz) {
			var cc = new byte[0x10];
			var ll = 0;
			var n = (double)dz.Count / BlockSize;

			for (var i = 0; i <= 0xf; i++) {
				cc[i] = 0;
			}
			for (var i = 0; i < n; i++) {
				for (var j = 0; j <= 0xf; j++) {
					int c = dz[i * 16 + j];
					cc[j] ^= S[c ^ ll];
					ll = cc[j];
				}
			}
			return dz.Concat(cc).ToArray();
		}

		private static byte[] Round(IReadOnlyList<byte> dz)
		{
			var xx = new byte[48];
			var n = (double)dz.Count / BlockSize;
			for (var i = 0; i < 48; i++) {
				xx[i] = 0;
			}
			for (var i = 0; i < n; i++) {
				int j;
				for (j = 0; j <= 0xf; j++) {
					xx[16 + j] = dz[i * 16 + j];
					xx[32 + j] = (byte)(xx[16 + j] ^ xx[j]);
				}
				for (var t = j = 0; j < 18; j++) {
					int k;
					for (k = 0; k < 48; k++) {
						t = xx[k] ^= S[t];
					}
					t = (t + j) % 256;
				}
			}
			return xx.Take(16).ToArray();
		}

		private static readonly byte[] S = {
			41, 46, 67, 201, 162, 216, 124, 1, 61, 54, 84, 161, 236, 240, 6, 19,
			98, 167, 5, 243, 192, 199, 115, 140, 152, 147, 43, 217, 188, 76, 130, 202,
			30, 155, 87, 60, 253, 212, 224, 22, 103, 66, 111, 24, 138, 23, 229, 18,
			190, 78, 196, 214, 218, 158, 222, 73, 160, 251, 245, 142, 187, 47, 238, 122,
			169, 104, 121, 145, 21, 178, 7, 63, 148, 194, 16, 137, 11, 34, 95, 33,
			128, 127, 93, 154, 90, 144, 50, 39, 53, 62, 204, 231, 191, 247, 151, 3,
			255, 25, 48, 179, 72, 165, 181, 209, 215, 94, 146, 42, 172, 86, 170, 198,
			79, 184, 56, 210, 150, 164, 125, 182, 118, 252, 107, 226, 156, 116, 4, 241,
			69, 157, 112, 89, 100, 113, 135, 32, 134, 91, 207, 101, 230, 45, 168, 2,
			27, 96, 37, 173, 174, 176, 185, 246, 28, 70, 97, 105, 52, 64, 126, 15,
			85, 71, 163, 35, 221, 81, 175, 58, 195, 92, 249, 206, 186, 197, 234, 38,
			44, 83, 13, 110, 133, 40, 132, 9, 211, 223, 205, 244, 65, 129, 77, 82,
			106, 220, 55, 200, 108, 193, 171, 250, 36, 225, 123, 8, 12, 189, 177, 74,
			120, 136, 149, 139, 227, 99, 232, 109, 233, 203, 213, 254, 59, 0, 29, 57,
			242, 239, 183, 14, 102, 88, 208, 228, 166, 119, 114, 248, 235, 117, 75, 10,
			49, 68, 80, 180, 143, 237, 31, 26, 219, 153, 141, 51, 159, 17, 131, 20
		};
	}
}
