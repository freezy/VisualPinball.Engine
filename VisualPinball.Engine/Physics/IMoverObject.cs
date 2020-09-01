using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.Physics
{
	public interface IMoverObject
	{
		void UpdateDisplacements();

		void UpdateVelocities();
	}
}
