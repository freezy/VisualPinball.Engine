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

using NLog;
using NLog.Targets;
using NUnit.Framework;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.Test
{
	public abstract class BaseTests
	{
		protected readonly Logger Logger;

		protected BaseTests()
		{
			var config = new NLog.Config.LoggingConfiguration();
			var logConsole = new TestTarget();
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
			LogManager.Configuration = config;
			Logger = LogManager.GetCurrentClassLogger();
		}
	}

	[Target("Test")]
	public class TestTarget : TargetWithLayout
	{
		protected override void Write(LogEventInfo logEvent)
		{
			var msg = Layout.Render(logEvent);
			TestContext.Out.WriteLine(msg);
		}
	}


	public class TestBallCreator : IBallCreationPosition
	{
		private readonly Vertex3D _pos;
		private readonly Vertex3D _vel;

		public TestBallCreator(float x, float y, float z, float vx, float vy, float vz)
		{
			_pos = new Vertex3D(x, y, z);
			_vel = new Vertex3D(vx, vy, vz);
		}

		public Vertex3D GetBallCreationPosition(Table table) => _pos;

		public Vertex3D GetBallCreationVelocity(Table table) => _vel;

		public void OnBallCreated(Ball ball)
		{
			// do nothing
		}
	}
}
