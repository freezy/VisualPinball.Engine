using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.Physics
{
	public interface MoverObject
	{
		void UpdateDisplacements(float dTime);

		void UpdateVelocities(PlayerPhysics physics);
	}
}
