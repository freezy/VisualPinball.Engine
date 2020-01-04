using System;
using System.IO;
using System.Net;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT
{
	public class TextureDataTests
	{
		private readonly Engine.VPT.Table.Table _table;

		public TextureDataTests()
		{
			_table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\TextureData.vpx");
		}

		[Fact]
		public void ShouldLoadCorrectArgb()
		{
			var texture = _table.Textures["test_pattern_argb"];
			var image = File.ReadAllBytes(@"..\..\Fixtures\test_pattern_argb.bmp");
			File.WriteAllBytes(@"..\..\Fixtures\debug.bmp", texture.Content);
			Assert.Equal(1024, texture.Data.Width);
			Assert.Equal(768, texture.Data.Height);
			Assert.Equal(image, texture.Content);
		}
	}
}
