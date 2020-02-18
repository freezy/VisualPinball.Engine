using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Table;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.Physics
{
	public class PhysicsTests : BaseTests
	{
		public PhysicsTests(ITestOutputHelper output) : base(output) { }

		[Fact]
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
