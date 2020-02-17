using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.Physics
{
	public interface IMoverObject
	{
		void UpdateDisplacements(float dTime);

		void UpdateVelocities(PlayerPhysics physics);
	}
}
