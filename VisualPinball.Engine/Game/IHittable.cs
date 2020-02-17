using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.Game
{
	public interface IHittable : IPlayable {

		bool IsCollidable { get; }
		EventProxy EventProxy { get; }
		HitObject[] GetHitShapes();
	}
}
