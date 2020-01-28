using VisualPinball.Engine.Common;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT
{
	public class TextureBitmapTests
	{
		private readonly Engine.VPT.Table.Table _table;

		public TextureBitmapTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Texture);
		}

		[Fact]
		public void ShouldAnalyzeAnOpaqueTexture()
		{
			var texture = _table.Textures["test_pattern_png"];
			var stats = texture.GetStats();

			Assert.Equal(1f, stats.Opaque);
			Assert.Equal(0f, stats.Translucent);
			Assert.Equal(0f, stats.Transparent);
		}

		[Fact]
		public void ShouldAnalyzeAnotherOpaqueTexture()
		{
			var texture = _table.Textures["test_pattern_argb"];
			var stats = texture.GetStats();

			Assert.Equal(1f, stats.Opaque);
			Assert.Equal(0f, stats.Translucent);
			Assert.Equal(0f, stats.Transparent);
		}

		[Fact]
		public void ShouldAnalyzeATransparentTexture()
		{
			var texture = _table.Textures["test_pattern_transparent"];
			texture.Analyze(true);
			var stats = texture.GetStats();

			Assert.Equal(0.657285035f, stats.Opaque);
			Assert.Equal(0.0102373762f, stats.Translucent);
			Assert.Equal(0.33247757f, stats.Transparent);
		}
	}
}
