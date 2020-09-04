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

using System.IO;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT
{
	public class TextureDataTests
	{
		private readonly Engine.VPT.Table.Table _table;

		public TextureDataTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Texture);
		}

		[Test]
		public void ShouldLoadCorrectArgb()
		{
			var texture = _table.Textures["test_pattern_argb"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.BmpArgb);
			texture.Data.Width.Should().Be(1024);
			texture.Data.Height.Should().Be(768);
			texture.Data.InternalName.Should().Be("test_pattern_argb");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldLoadCorrectBmp()
		{
			var texture = _table.Textures["test_pattern_bmp"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.Bmp);
			texture.Data.Width.Should().Be(1024);
			texture.Data.Height.Should().Be(768);
			texture.Data.InternalName.Should().Be("test_pattern_bmp");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldLoadCorrectExr()
		{
			var texture = _table.Textures["test_pattern_exr"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.Exr);
			texture.Data.Width.Should().Be(587);
			texture.Data.Height.Should().Be(675);
			texture.Data.InternalName.Should().Be("test_pattern_exr");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldLoadCorrectHdr()
		{
			var texture = _table.Textures["test_pattern_hdr"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.Hdr);
			texture.Data.Width.Should().Be(1024);
			texture.Data.Height.Should().Be(512);
			texture.Data.InternalName.Should().Be("test_pattern_hdr");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldLoadCorrectJpg()
		{
			var texture = _table.Textures["test_pattern_jpg"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.Jpg);
			texture.Data.Width.Should().Be(1024);
			texture.Data.Height.Should().Be(768);
			texture.Data.InternalName.Should().Be("test_pattern_jpg");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldLoadCorrectPng()
		{
			var texture = _table.Textures["test_pattern_png"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.Png);
			texture.Data.Width.Should().Be(1024);
			texture.Data.Height.Should().Be(768);
			texture.Data.InternalName.Should().Be("test_pattern_png");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldLoadCorrectTransparentPng()
		{
			var texture = _table.Textures["test_pattern_transparent"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.PngTransparent);
			//File.WriteAllBytes(@"..\..\Fixtures\debug.bmp", textureData);
			texture.Data.Width.Should().Be(1024);
			texture.Data.Height.Should().Be(768);
			texture.Data.InternalName.Should().Be("test_pattern_transparent");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldLoadCorrectTransparentXrgb()
		{
			var texture = _table.Textures["test_pattern_xrgb"];
			var blob = texture.FileContent;
			var image = File.ReadAllBytes(TexturePath.BmpXrgb);
			//File.WriteAllBytes(@"..\..\Fixtures\debug.bmp", textureData);
			texture.Data.Width.Should().Be(1024);
			texture.Data.Height.Should().Be(768);
			texture.Data.InternalName.Should().Be("test_pattern_xrgb");
			texture.Data.AlphaTestValue.Should().Be(1.0f);
			texture.Data.Path.Should().StartWith(@"C:\");
			blob.Should().Equal(image);
		}

		[Test]
		public void ShouldWriteCorrectBinary()
		{
			const string tmpFileName = "ShouldWriteCorrectBinary.vpx";
			new TableWriter(_table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			writtenTable.Textures["test_pattern_jpg"].Data.Binary.Data.Should().Equal(_table.Textures["test_pattern_jpg"].Data.Binary.Data);
		}

		[Test]
		public void ShouldWriteCorrectBitmap()
		{
			const string tmpFileName = "ShouldWriteCorrectBitmap.vpx";
			new TableWriter(_table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			writtenTable.Textures["test_pattern_bmp"].Data.Bitmap.Bytes.Should().Equal(_table.Textures["test_pattern_bmp"].Data.Bitmap.Bytes);
		}
	}
}
