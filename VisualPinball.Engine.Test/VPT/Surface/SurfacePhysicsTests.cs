// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfacePhysicsTests : BaseTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly Engine.VPT.Kicker.Kicker _kicker;

		public SurfacePhysicsTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			_kicker = _table.Kicker("BallRelease");
		}

		// [Test]
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
