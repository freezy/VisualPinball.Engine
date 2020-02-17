using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public interface IPlayable
	{
		void SetupPlayer(Player player, Table table);
	}
}
