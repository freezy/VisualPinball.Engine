using FluentAssertions;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Flipper
{
	public class FlipperPhysicsTests
	{
		private readonly Engine.VPT.Table.Table _table;

		public FlipperPhysicsTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
		}

		[Fact]
		public void ShouldMoveToTheEndWhenSolenoidIsTurnedOn()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flippers["FlipperR"];
			var startAngleRad = MathF.DegToRad(flipper.Data.StartAngle);
			var endAngleRad = MathF.DegToRad(flipper.Data.EndAngle);

			// assert it's down
			flipper.State.Angle.Should().Be(startAngleRad);

			flipper.RotateToEnd();
			player.SimulateTime(100);

			// assert it's up
			flipper.State.Angle.Should().Be(endAngleRad);
		}

		[Fact]
		public void ShouldMoveToStartWhenSolenoidIsTurnedOff()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flippers["FlipperR"];
			var startAngleRad = MathF.DegToRad(flipper.Data.StartAngle);

			// move up
			flipper.RotateToEnd();
			player.SimulateTime(52);

			// move down again
			flipper.RotateToStart();
			player.SimulateTime(300);

			flipper.State.Angle.Should().Be(startAngleRad);
		}

		[Fact]
		public void ShouldMoveBackToEndWhenPressedWhileMoving()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flippers["FlipperR"];
			var startAngleRad = MathF.DegToRad(flipper.Data.StartAngle);
			var endAngleRad = MathF.DegToRad(flipper.Data.EndAngle);

			// move up
			flipper.RotateToEnd();
			player.SimulateTime(52); // hit at 51ms
			flipper.State.Angle.Should().BeGreaterThan(startAngleRad);

			// move down
			flipper.RotateToStart();
			player.SimulateTime(200);

			// assert it's in the middle
			flipper.State.Angle.Should().BeGreaterThan(startAngleRad);
			flipper.State.Angle.Should().BeLessThan(endAngleRad);

			// move up again
			flipper.RotateToEnd();
			player.SimulateTime(500);

			// assert it's back up in less than half the time
			flipper.State.Angle.Should().Be(endAngleRad);
		}
	}
}
