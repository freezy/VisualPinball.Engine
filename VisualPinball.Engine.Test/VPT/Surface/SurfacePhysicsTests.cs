using FluentAssertions;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfacePhysicsTests : BaseTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly Engine.VPT.Kicker.Kicker _kicker;

		public SurfacePhysicsTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			_kicker = _table.Kickers["BallRelease"];
		}

		// [Fact]
		// public void ShouldMakeTheBallBounceOffTheSides()
		// {
		// 	var player = new Player(_table).Init();
		//
		// 	// create ball
		// 	var ball = player.CreateBall(_kicker);
		// 	_kicker.Kick(90, 10);
		//
		// 	// let it roll right some
		// 	player.UpdatePhysics(0);
		// 	player.UpdatePhysics(170);
		//
		// 	// assert it's moving down right
		// 	ball.State.Pos.X.Should().BeGreaterThan(300);
		//
		// 	// let it hit and bounce back
		// 	player.UpdatePhysics(200);
		//
		// 	// assert it bounced back
		// 	ball.State.Pos.X.Should().BeLessThan(300);
		// }

	}
}
