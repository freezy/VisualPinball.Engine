using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.Game
{
	public interface IMovable : IPlayable {

		MoverObject GetMover();
	}
}
