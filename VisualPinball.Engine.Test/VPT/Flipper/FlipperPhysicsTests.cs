using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Flipper
{
	public class FlipperPhysicsTests : BaseTests
	{
		private readonly Engine.VPT.Table.Table _table;

		public FlipperPhysicsTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);

		}

		[Test]
		public void ShouldMoveToTheEndWhenSolenoidIsTurnedOn()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flipper("FlipperR");
			var startAngleRad = MathF.DegToRad(flipper.Data.StartAngle);
			var endAngleRad = MathF.DegToRad(flipper.Data.EndAngle);

			// assert it's down
			flipper.State.Angle.Should().Be(startAngleRad);

			flipper.RotateToEnd();
			player.SimulateTime(100);

			// assert it's up
			flipper.State.Angle.Should().Be(endAngleRad);
		}

		[Test]
		public void ShouldMoveToStartWhenSolenoidIsTurnedOff()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flipper("FlipperR");
			var startAngleRad = MathF.DegToRad(flipper.Data.StartAngle);

			// move up
			flipper.RotateToEnd();
			player.SimulateTime(52);

			// move down again
			flipper.RotateToStart();
			player.SimulateTime(300);

			flipper.State.Angle.Should().Be(startAngleRad);
		}

		[Test]
		public void ShouldMoveBackToEndWhenPressedWhileMoving()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flipper("FlipperR");
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

		[Test]
		public void ShouldCollideWithTheBallWhenHittingOnTheFace()
		{
			var player = new Player(_table).Init();

			// put ball on top of flipper face
			var ball = CreateBall(player, 350f, 1600f, 0f);

			player.SimulateTime(2000);

			ball.State.Pos.X.Should().BeGreaterThan(420f);  // diverted to the right
			ball.State.Pos.Y.Should().BeGreaterThan(1650f);  // but still below
		}

		[Test]
		public void ShouldCollideWithTheBallWhenHittingOnTheEnd()
		{
			var player = new Player(_table).Init();

			// put ball on top of flipper end
			var ball = CreateBall(player, 420f, 1645f, 0);

			player.SimulateTime(2000);

			ball.State.Pos.X.Should().BeGreaterThan(460f);  // diverted to the right
			ball.State.Pos.Y.Should().BeGreaterThan(1670f);  // but still below
		}

		[Test]
		public void ShouldRollOnTheFlipper()
		{
			var player = new Player(_table).Init();

			// put ball on top of flipper
			var ball = CreateBall(player, 310, 1590, 0);

			player.SimulateTime(2000);

			// assert it's on flipper's bottom
			ball.State.Pos.X.Should().BeApproximately(395f, 10f);
			ball.State.Pos.Y.Should().BeApproximately(1649f, 10f);
		}

		[Test]
		public void ShouldMoveTheBallUp()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flipper("DefaultFlipper");

			// put ball on top of flipper
			var ball = CreateBall(player, 310, 1590, 0);

			// let it roll a bit
			player.SimulateTime(1500);

			// now, flip
			flipper.RotateToEnd();
			player.SimulateTime(1550);

			// should be moving top right
			ball.State.Pos.X.Should().BeGreaterThan(380);
			ball.State.Pos.Y.Should().BeLessThan(1550f);
		}

		[Test]
		public void ShouldPushTheCoilDownWhenHitWithHighSpeed()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flipper("DefaultFlipper");
			CreateBall(player, 395, 1547, 0, 0, 20);

			// assert initial flipper position
			MathF.RadToDeg(flipper.State.Angle).Should().BeApproximately(121, 0.00001f);

			// let it collide
			player.SimulateTime(100);

			MathF.RadToDeg(flipper.State.Angle).Should().BeLessThan(115);
		}

		[Test]
		public void ShouldMoveWhenHitAtTheSameTime()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flipper("DefaultFlipper");

			// shoot ball onto flipper and flip at the same time
			var ball = CreateBall(player, 420, 1550, 0, 0, 5);
			flipper.RotateToEnd();

			player.UpdatePhysics(280);

			// should be moving up
			ball.State.Pos.Y.Should().BeLessThan(830);

			// now, flip
			flipper.RotateToEnd();
			player.SimulateTime(1550);
		}

		[Test]
		public void ShouldSlideOnTheFlipper()
		{
			var player = new Player(_table).Init();

			// shoot ball parallel onto flipper
			var ball = CreateBall(player, 214, 1520, 0, 10, 7.1f);

			player.UpdatePhysics(0);
			ball.State.Pos.X.Should().Be(214);
			ball.State.Pos.Y.Should().Be(1520);

			player.UpdatePhysics(50);
			ball.State.Pos.X.Should().BeInRange(259, 263);
			ball.State.Pos.Y.Should().BeInRange(1552, 1556);

			player.UpdatePhysics(100);
			ball.State.Pos.X.Should().BeInRange(306, 310);
			ball.State.Pos.Y.Should().BeInRange(1586, 1590);

			player.UpdatePhysics(150);
			ball.State.Pos.X.Should().BeInRange(350, 354);
			ball.State.Pos.Y.Should().BeInRange(1617, 1621);
		}

		[Test]
		public void ShouldMoveTheFlipperUpWhenHitFromBelow()
		{
			var player = new Player(_table).Init();
			var flipper = _table.Flipper("DefaultFlipper");

			// shoot ball from below onto flipper
			CreateBall(player, 374, 1766, 0, 0, -10);

			player.UpdatePhysics(0);
			MathF.RadToDeg(flipper.State.Angle).Should().BeApproximately(121, 0.00001f);

			player.UpdatePhysics(50);
			MathF.RadToDeg(flipper.State.Angle).Should().BeLessThan(121);

			player.UpdatePhysics(100);
			MathF.RadToDeg(flipper.State.Angle).Should().BeLessThan(110);

			player.UpdatePhysics(150);
			MathF.RadToDeg(flipper.State.Angle).Should().BeGreaterThan(110);

			player.UpdatePhysics(200);
			MathF.RadToDeg(flipper.State.Angle).Should().BeApproximately(121, 0.00001f);
		}
	}
}
