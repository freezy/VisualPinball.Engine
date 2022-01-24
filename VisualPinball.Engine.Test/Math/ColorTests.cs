﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System.IO;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Test.Math
{
	public class ColorTests
	{
		[Test]
		public void ShouldCorrectlyParseRgbColor()
		{
			var color = new Color(0x123456, ColorFormat.Bgr);
			color.Red.Should().Be(0x56);
			color.Green.Should().Be(0x34);
			color.Blue.Should().Be(0x12);
		}

		[Test]
		public void ShouldCorrectlyParseArgbColor()
		{
			var color = new Color(0x12345678, ColorFormat.Argb);
			color.Red.Should().Be(0x34);
			color.Green.Should().Be(0x56);
			color.Blue.Should().Be(0x78);
			color.Alpha.Should().Be(0x12);
		}

		[Test]
		public void TestColor()
		{
			using (var memStream = new MemoryStream(new byte[] {0xff, 0xa0, 0xb4, 0x80 })) {
				using (var reader = new BinaryReader(memStream)) {
					var intCol = reader.ReadUInt32();
					var color = new Color(intCol, ColorFormat.Bgr);
					color.Red.Should().Be(0xff);
					color.Green.Should().Be(0xa0);
					color.Blue.Should().Be(0xb4);
					color.Alpha.Should().Be(0x80);
				}
			}
		}
	}
}
