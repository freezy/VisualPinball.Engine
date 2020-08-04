using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Engine.Test.VPT.Spinner
{
	public class SpinnerDataTests
	{
		[Test]
		public void ShouldReadSpinnerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Spinner);
			ValidateSpinnerData(table.Spinner("Data").Data);
		}

		[Test]
		public void ShouldWriteSpinnerData()
		{
			const string tmpFileName = "ShouldWriteSpinnerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Spinner);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateSpinnerData(writtenTable.Spinner("Data").Data);
		}

		private static void ValidateSpinnerData(SpinnerData data)
		{
			data.AngleMax.Should().Be(50.698f);
			data.AngleMin.Should().Be(-12.87f);
			data.Center.X.Should().Be(494f);
			data.Center.Y.Should().Be(1401.62f);
			data.Elasticity.Should().Be(0.6824f);
			data.Height.Should().Be(13.532f);
			data.Image.Should().Be("");
			data.IsVisible.Should().Be(true);
			data.Length.Should().Be(124.31f);
			data.Material.Should().Be("Red");
			data.Rotation.Should().Be(47.98f);
			data.ShowBracket.Should().Be(true);
			data.Surface.Should().Be("");
		}
	}
}
