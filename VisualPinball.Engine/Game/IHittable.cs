using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.Game
{
	public interface IHittable : IPlayable {

		int Index { get; set; }
		bool IsCollidable { get; }
		EventProxy EventProxy { get; }
		HitObject[] GetHitShapes();
	}
}
