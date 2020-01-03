using System;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT
{
	public class TextureDataTests
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public TextureDataTests(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\VPX\TableData.vpx");
			_testOutputHelper.WriteLine(table.Textures.ToString());
		}
	}
}
