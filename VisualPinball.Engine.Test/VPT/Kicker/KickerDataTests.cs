using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Engine.Test.VPT.Kicker
{
	public class KickerDataTests
	{
		[Test]
		public void ShouldReadKickerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Kicker);
			ValidateKickerData(table.Kicker("Data").Data);
		}

		[Test]
		public void ShouldWriteKickerData()
		{
			const string tmpFileName = "ShouldWriteKickerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Kicker);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateKickerData(writtenTable.Kicker("Data").Data);
		}

		private static void ValidateKickerData(KickerData data)
		{
			data.Center.X.Should().Be(781.6662f);
			data.Center.Y.Should().Be(1585f);
			data.FallThrough.Should().Be(true);
			data.HitAccuracy.Should().Be(0.6428f);
			data.HitHeight.Should().Be(36.684f);
			data.IsEnabled.Should().Be(false);
			data.KickerType.Should().Be(KickerType.KickerHoleSimple);
			data.LegacyMode.Should().Be(true);
			data.Material.Should().Be("Red");
			data.Orientation.Should().Be(65.988f);
			data.Radius.Should().Be(25.98f);
			data.Scatter.Should().Be(4.98f);
			data.Surface.Should().Be("");
		}
	}
}
