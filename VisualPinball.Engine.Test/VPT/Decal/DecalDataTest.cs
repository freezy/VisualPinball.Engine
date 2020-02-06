using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Decal;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Decal
{
	public class DecalDataTest : BaseTests
	{
		public DecalDataTest(ITestOutputHelper output) : base(output) { }

		[Fact]
		public void ShouldReadDecalData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Decal);
			ValidateDecal0(table.Decals[0].Data);
			ValidateDecal1(table.Decals[1].Data);
		}

		[Fact]
		public void ShouldWriteDecalData()
		{
			const string tmpFileName = "ShouldWriteDecalData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Decal);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateDecal0(writtenTable.Decals[0].Data);
			ValidateDecal1(writtenTable.Decals[1].Data);
		}

		private static void ValidateDecal0(DecalData data)
		{
			Assert.Equal(false, data.Backglass);
			Assert.Equal(205.4f, data.Center.X);
			Assert.Equal(540.68f, data.Center.Y);
			Assert.Equal(60, data.Color.Red);
			Assert.Equal(217, data.Color.Green);
			Assert.Equal(142, data.Color.Blue);
			Assert.Equal(DecalType.DecalImage, data.DecalType);
			Assert.Equal("Arial Black", data.Font.Name);
			Assert.Equal(32.5f, data.Height);
			Assert.Equal("tex_transparent", data.Image);
			Assert.Equal("DecalMat", data.Material);
			Assert.Equal(45.98f, data.Rotation);
			Assert.Equal(SizingType.AutoWidth, data.SizingType);
			Assert.Equal("", data.Surface);
			Assert.Equal("", data.Text);
			Assert.Equal(false, data.VerticalText);
			Assert.Equal(66.1165f, data.Width);
			Assert.Equal(2, data.EditorLayer);
			Assert.Equal(false, data.IsLocked);
		}

		private static void ValidateDecal1(DecalData data)
		{
			Assert.Equal(true, data.Backglass);
			Assert.Equal(509f, data.Center.X);
			Assert.Equal(354f, data.Center.Y);
			Assert.Equal(216, data.Color.Red);
			Assert.Equal(63, data.Color.Green);
			Assert.Equal(204, data.Color.Blue);
			Assert.Equal(DecalType.DecalText, data.DecalType);
			Assert.Equal("Fixedsys", data.Font.Name);
			Assert.Equal(100f, data.Height);
			Assert.Equal("", data.Image);
			Assert.Equal("", data.Material);
			Assert.Equal(0f, data.Rotation);
			Assert.Equal(SizingType.ManualSize, data.SizingType);
			Assert.Equal("", data.Surface);
			Assert.Equal("My Decal Text", data.Text);
			Assert.Equal(true, data.VerticalText);
			Assert.Equal(100f, data.Width);
			Assert.Equal(0, data.EditorLayer);
			Assert.Equal(true, data.IsLocked);
		}
	}
}
