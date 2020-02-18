using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Flipper
{
	public class FlipperPhysicsTests
	{
		[Fact]
		public void ShouldMove()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			var player = new Player(table).Init();

			var flipper = table.Flippers["FlipperR"];
			var flipperState = flipper.State;
			var endAngleRad = MathF.DegToRad(flipper.Data.EndAngle);

			flipper.RotateToEnd();
			player.SimulateTime(100);

			Assert.Equal(endAngleRad, flipperState.Angle);
		}
	}
}
