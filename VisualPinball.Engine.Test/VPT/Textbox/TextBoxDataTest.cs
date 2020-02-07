using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Trigger;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.TextBox
{
	public class TextBoxDataTest : BaseTests
	{
		public TextBoxDataTest(ITestOutputHelper output) : base(output) { }

		[Fact]
		public void ShouldReadTextBoxData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.TextBox);
			ValidateTableData(table.TextBoxes["TextBox001"].Data);
		}

		[Fact]
		public void ShouldWriteTextBoxData()
		{
			const string tmpFileName = "ShouldWriteTextBoxData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.TextBox);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.TextBoxes["TextBox001"].Data);
		}

		private static void ValidateTableData(TextBoxData data)
		{
			Assert.Equal(TextAlignment.TextAlignCenter, data.Align);
			Assert.Equal(0, data.BackColor.Red);
			Assert.Equal(128, data.BackColor.Green);
			Assert.Equal(128, data.BackColor.Blue);
			Assert.Equal("BentonSans", data.Font.Name);
			Assert.Equal(true, data.Font.Italic);
			Assert.Equal(330000U, data.Font.Size);
			Assert.Equal(700U, data.Font.Weight);
			Assert.Equal(230, data.FontColor.Red);
			Assert.Equal(132, data.FontColor.Green);
			Assert.Equal(210, data.FontColor.Blue);
			Assert.Equal(0.98f, data.IntensityScale);
			Assert.Equal(false, data.IsDmd);
			Assert.Equal(false, data.IsTransparent);
			Assert.Equal("007", data.Text);
			Assert.Equal(285, data.V1.X);
			Assert.Equal(290, data.V1.Y);
			Assert.Equal(285 + 250, data.V2.X);
			Assert.Equal(290 + 70, data.V2.Y);
		}
	}
}
