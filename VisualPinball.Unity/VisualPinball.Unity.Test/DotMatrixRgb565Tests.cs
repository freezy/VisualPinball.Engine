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

using NUnit.Framework;
using Unity.Collections;

namespace VisualPinball.Unity.Test
{
	public class DotMatrixRgb565Tests
	{
		[Test]
		public void Rgb565FramesExpandToRgb24BottomRowFirst()
		{
			// top row red, green; bottom row blue, white; little-endian 5-6-5 with red in the top bits
			var frame = new byte[] {
				0x00, 0xf8, 0xe0, 0x07,
				0x1f, 0x00, 0xff, 0xff,
			};
			using var texture = new NativeArray<byte>(2 * 2 * 3, Allocator.Temp);

			DotMatrixDisplayComponent.CopyRgb565Frame(frame, 2, 2, texture);

			Assert.That(texture.ToArray(), Is.EqualTo(new byte[] {
				0, 0, 255, 255, 255, 255, // the texture starts with the bottom row
				255, 0, 0, 0, 255, 0,
			}));
		}

		[Test]
		public void Rgb565ChannelsRepeatTheirTopBits()
		{
			// r = 0b10000, g = 0b100000, b = 0b00001
			var value = 0b10000_100000_00001;
			var frame = new[] { (byte)value, (byte)(value >> 8) };
			using var texture = new NativeArray<byte>(3, Allocator.Temp);

			DotMatrixDisplayComponent.CopyRgb565Frame(frame, 1, 1, texture);

			Assert.That(texture.ToArray(), Is.EqualTo(new byte[] { 0b10000_100, 0b100000_10, 0b00001_000 }));
		}
	}
}
