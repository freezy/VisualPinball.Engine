using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public class Player
	{
		private readonly Table _table;
		private readonly PlayerPhysics _physics;

		public Player(Table table)
		{
			_table = table;
			_physics = new PlayerPhysics(table);
		}
	}
}
