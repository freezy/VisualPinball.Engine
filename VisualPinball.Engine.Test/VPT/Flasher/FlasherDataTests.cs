using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flasher;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Flasher
{
	public class FlasherDataTests
	{
		[Fact]
		public void ShouldReadFlasherData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flasher);
			ValidateFlasher1(table.Flashers["Data"].Data);
		}

		[Fact]
		public void ShouldWriteFlasherData()
		{
			const string tmpFileName = "ShouldWriteFlasherData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flasher);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateFlasher1(writtenTable.Flashers["Data"].Data);
		}

		private static void ValidateFlasher1(FlasherData data)
		{
			Assert.Equal(false, data.AddBlend);
			Assert.Equal(69, data.Alpha);
			Assert.Equal(383f, data.Center.X);
			Assert.Equal(785f, data.Center.Y);
			Assert.Equal(64, data.Color.Red);
			Assert.Equal(153, data.Color.Green);
			Assert.Equal(225, data.Color.Blue);
			Assert.Equal(0.282f, data.DepthBias);
			Assert.Equal(false, data.DisplayTexture);
			Assert.Equal(4, data.DragPoints.Length);
			Assert.Equal(333f, data.DragPoints[0].Vertex.X);
			Assert.Equal(735f, data.DragPoints[0].Vertex.Y);
			Assert.Equal(0f, data.DragPoints[0].Vertex.Z);
			Assert.Equal(Filters.Filter_Overlay, data.Filter);
			Assert.Equal(100, data.FilterAmount);
			Assert.Equal(50.22f, data.Height);
			Assert.Equal("", data.ImageA);
			Assert.Equal(ImageAlignment.ImageAlignTopLeft, data.ImageAlignment);
			Assert.Equal("", data.ImageB);
			Assert.Equal(false, data.IsDmd);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal(0.921f, data.ModulateVsAdd);
			Assert.Equal(15.651f, data.RotX);
			Assert.Equal(32.918f, data.RotY);
			Assert.Equal(14.32f, data.RotZ);

			Assert.Equal(123f, data.TimerInterval);
			Assert.Equal(false, data.IsTimerEnabled);

			Assert.Equal(10, data.EditorLayer);
			Assert.Equal(true, data.IsLocked);
		}
	}
}
