using NUnit.Framework;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.Physics
{
	public class PhysicsTests : BaseTests
	{
		[Test]
		public void ShouldRunThePhysicsLoop()
		{
			var table = new TableBuilder()
				.AddFlipper("Flipper1")
				.Build();

			var player = new Player(table).Init();
			player.SimulateTime(1000);
		}
	}
}
