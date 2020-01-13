using System;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Light
{
	public class LightDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Light);
			var data = table.Lights["Light1"].Data;

			Assert.Equal(126, data.BlinkInterval);
			Assert.Equal("10011", data.BlinkPattern);
			Assert.Equal(28.298f, data.BulbHaloHeight);
			Assert.Equal(0.9723f, data.BulbModulateVsAdd);
			Assert.Equal(450.7777f, data.Center.X);
			Assert.Equal(109.552f, data.Center.Y);
			Assert.Equal(151, data.Color.Red);
			Assert.Equal(221, data.Color.Green);
			Assert.Equal(34, data.Color.Blue);
			Assert.Equal(235, data.Color2.Red);
			Assert.Equal(50, data.Color2.Green);
			Assert.Equal(193, data.Color2.Blue);
			Assert.Equal(0.0012f, data.DepthBias);
			Assert.Equal(8, data.DragPoints.Length);
			Assert.Equal(0.223f, data.FadeSpeedDown);
			Assert.Equal(0.265f, data.FadeSpeedUp);
			Assert.Equal(2.021f, data.FalloffPower);
			Assert.Equal(1.293f, data.Intensity);
			Assert.Equal(false, data.IsBackglass);
			Assert.Equal(true, data.IsBulbLight);
			Assert.Equal(false, data.IsImageMode);
			Assert.Equal(false, data.IsRoundLight);
			Assert.Equal(20.231f, data.MeshRadius);
			Assert.Equal("smiley", data.OffImage);
			Assert.Equal(false, data.ShowBulbMesh);
			Assert.Equal(true, data.ShowReflectionOnBall);
			Assert.Equal(LightStatus.LightStateOff, data.State);
			Assert.Equal("", data.Surface);
			Assert.Equal(0.5916f, data.TransmissionScale);
		}

		[Fact]
		public void ShouldLoadCorrectDragPointData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Light);
			var dragPoints = table.Lights["PlayfieldLight"].Data.DragPoints;

			Assert.Equal(false, dragPoints[0].IsSmooth);
			Assert.Equal(true, dragPoints[0].IsSlingshot);
			Assert.Equal(491.6666f, dragPoints[0].Vertex.X);
			Assert.Equal(376.882f, dragPoints[0].Vertex.Y);
			Assert.Equal(true, dragPoints[6].IsSmooth);
			Assert.Equal(false, dragPoints[7].IsSmooth);
		}
	}
}
