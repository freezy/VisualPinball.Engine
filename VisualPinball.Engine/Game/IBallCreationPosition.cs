using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	/// <summary>
	/// Game items like kicker or plunger implement this, so the game knows
	/// how to position and accelerate the ball.
	/// </summary>
	public interface IBallCreationPosition {

		Vertex3D GetBallCreationPosition(Table table);

		Vertex3D GetBallCreationVelocity(Table table);

		void OnBallCreated(Ball ball);
	}
}
