using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Gate
{
	public class GateDataTests
	{
		[Fact]
		public void ShouldReadGateData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Gate);
			ValidateGateData(table.Gates["Data"].Data);
		}

		private static void ValidateGateData(GateData data) {
			Assert.Equal(90f, MathF.RadToDeg(data.AngleMax));
			Assert.Equal(0f, MathF.RadToDeg(data.AngleMin));
			Assert.Equal(769f, data.Center.X);
			Assert.Equal(1019f, data.Center.Y);
			Assert.Equal(0.92958f, data.Damping);
			Assert.Equal(0.1348f, data.Elasticity);
			Assert.Equal(0.1983f, data.Friction);
			Assert.Equal(GateType.GatePlate, data.GateType);
			Assert.Equal(0.2198f, data.GravityFactor);
			Assert.Equal(123.42f, data.Height);
			Assert.Equal(true, data.IsCollidable);
			Assert.Equal(true, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal(40.12f, data.Length);
			Assert.Equal("Red", data.Material);
			Assert.Equal(-72.212f, data.Rotation);
			Assert.Equal(true, data.ShowBracket);
			Assert.Equal("", data.Surface);
			Assert.Equal(true, data.TwoWay);
		}
	}
}
