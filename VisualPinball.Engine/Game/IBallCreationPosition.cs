using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public interface IBallCreationPosition {

		Vertex3D GetBallCreationPosition(Table table);

		Vertex3D GetBallCreationVelocity(Table table);

		void OnBallCreated(PlayerPhysics physics, Ball ball);
	}
}
