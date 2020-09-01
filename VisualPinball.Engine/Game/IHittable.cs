using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.Game
{
	public interface IHittable : IPlayable {

		int Index { get; }
		int Version { get; }
		HitObject[] GetHitShapes();
	}
}
