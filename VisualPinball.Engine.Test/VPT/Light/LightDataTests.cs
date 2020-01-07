using System;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Light
{
	public class LightDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\LightData.vpx");
			Console.WriteLine(table);
		}
	}
}
