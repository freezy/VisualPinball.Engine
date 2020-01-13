using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Bumper
{
	public class BumperDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Bumper);
			var data = table.Bumpers["Bumper1"].Data;

			Assert.Equal("Material2", data.BaseMaterial);
			Assert.Equal("Material1", data.CapMaterial);
			Assert.Equal(500f, data.Center.X);
			Assert.Equal(1250f, data.Center.Y);
			Assert.Equal(12.2234f, data.Force);
			Assert.Equal(80.654f, data.HeightScale);
			Assert.Equal(true, data.HitEvent);
			Assert.Equal(true, data.IsBaseVisible);
			Assert.Equal(false, data.IsCapVisible);
			Assert.Equal(true, data.IsCollidable);
			Assert.Equal(false, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsRingVisible);
			Assert.Equal(true, data.IsSocketVisible);
			Assert.Equal(9.17826f, data.Orientation);
			Assert.Equal(30.38182f, data.Radius);
			Assert.Equal(0.005561f, data.RingDropOffset);
			Assert.Equal("Material4", data.RingMaterial);
			Assert.Equal(0.52098f, data.RingSpeed);
			Assert.Equal(0.0068f, data.Scatter);
			Assert.Equal("Material3", data.SocketMaterial);
			Assert.Equal("", data.Surface);
			Assert.Equal(1.00658f, data.Threshold);
		}
	}
}
