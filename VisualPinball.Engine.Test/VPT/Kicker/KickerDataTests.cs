using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Kicker
{
	public class KickerDataTests
	{
		[Fact]
		public void ShouldReadKickerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Kicker);
			ValidateKickerData(table.Kickers["Data"].Data);
		}

		[Fact]
		public void ShouldWriteKickerData()
		{
			const string tmpFileName = "ShouldWriteKickerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Kicker);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateKickerData(writtenTable.Kickers["Data"].Data);
		}

		private static void ValidateKickerData(KickerData data)
		{
			Assert.Equal(781.6662f, data.Center.X);
			Assert.Equal(1585f, data.Center.Y);
			Assert.Equal(true, data.FallThrough);
			Assert.Equal(0.6428f, data.HitAccuracy);
			Assert.Equal(36.684f, data.HitHeight);
			Assert.Equal(false, data.IsEnabled);
			Assert.Equal(KickerType.KickerHoleSimple, data.KickerType);
			Assert.Equal(true, data.LegacyMode);
			Assert.Equal("Red", data.Material);
			Assert.Equal(65.988f, data.Orientation);
			Assert.Equal(25.98f, data.Radius);
			Assert.Equal(4.98f, data.Scatter);
			Assert.Equal("", data.Surface);
		}
	}
}
