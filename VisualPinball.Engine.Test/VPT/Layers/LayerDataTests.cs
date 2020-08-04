using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Engine.Test.VPT.Layers
{
	public class LayersDataTests
	{
		[Test]
		public void ShouldReadLayerDataVPX1060()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Bumper);
			var data = table.Bumper("Bumper1").Data;
			ValidateTableDataVPX1060(data);
		}

		[Test]
		public void ShouldReadLayerDataVPX1070()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.BumperVPX1070);
			var data = table.Bumper("Bumper1").Data;
			ValidateTableDataVPX1070(data);
		}

		[Test]
		public void ShouldWriteLayerData()
		{
			const string tmpFileName = "ShouldWriteBumperData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Bumper);
			var data = table.Bumper("Bumper1").Data;
			data.EditorLayerName = "Layer_1";
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableDataVPX1070(writtenTable.Bumper("Bumper1").Data);
		}

		private static void ValidateTableDataVPX1060(BumperData data)
		{
			data.EditorLayer.Should().Be(0);
			data.EditorLayerName.Should().Be(string.Empty);
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(false);
		}
		private static void ValidateTableDataVPX1070(BumperData data)
		{
			data.EditorLayer.Should().Be(0);
			data.EditorLayerName.Should().Be("Layer_1");
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(false);
		}
	}
}
