using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;

namespace VisualPinball.Engine.Test.VPT.TextBox
{
	public class TextBoxDataTest : BaseTests
	{
		[Test]
		public void ShouldReadTextBoxData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.TextBox);
			ValidateTableData(table.TextBox("TextBox001").Data);
		}

		[Test]
		public void ShouldWriteTextBoxData()
		{
			const string tmpFileName = "ShouldWriteTextBoxData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.TextBox);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.TextBox("TextBox001").Data);
		}

		private static void ValidateTableData(TextBoxData data)
		{
			data.Align.Should().Be(TextAlignment.TextAlignCenter);
			data.BackColor.Red.Should().Be(0);
			data.BackColor.Green.Should().Be(128);
			data.BackColor.Blue.Should().Be(128);
			data.Font.Name.Should().Be("BentonSans");
			data.Font.Italic.Should().Be(true);
			data.Font.Size.Should().Be(330000U);
			data.Font.Weight.Should().Be(700);
			data.FontColor.Red.Should().Be(230);
			data.FontColor.Green.Should().Be(132);
			data.FontColor.Blue.Should().Be(210);
			data.IntensityScale.Should().Be(0.98f);
			data.IsDmd.Should().Be(false);
			data.IsTransparent.Should().Be(false);
			data.Text.Should().Be("007");
			data.V1.X.Should().Be(285);
			data.V1.Y.Should().Be(290);
			data.V2.X.Should().Be(285 + 250);
			data.V2.Y.Should().Be(290 + 70);
		}
	}
}
