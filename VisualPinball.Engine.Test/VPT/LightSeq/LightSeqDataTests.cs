using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.LightSeq;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.LightSeq
{
	public class LightSeqDataTests : BaseTests
	{
		public LightSeqDataTests(ITestOutputHelper output) : base(output) { }

		[Fact]
		public void ShouldReadLightSeqData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.LightSeq);
			ValidateLightSeqData(table.LightSeqs["LightSeq001"].Data);
		}

		[Fact]
		public void ShouldWriteLightSeqData()
		{
			const string tmpFileName = "ShouldWriteLightSeqData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.LightSeq);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateLightSeqData(writtenTable.LightSeqs["LightSeq001"].Data);
		}

		private static void ValidateLightSeqData(LightSeqData data)
		{
			Assert.Equal(false, data.Backglass);
			Assert.Equal(21.23f, data.Center.X);
			Assert.Equal(503.68f, data.Center.Y);
			Assert.Equal("Collection001", data.Collection);
			Assert.Equal(0, data.EditorLayer);
			Assert.Equal(false, data.IsLocked);
			Assert.Equal(true, data.IsTimerEnabled);
			Assert.Equal(112, data.TimerInterval);
			Assert.Equal(256, data.UpdateInterval);
		}
	}
}
