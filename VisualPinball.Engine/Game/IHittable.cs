using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Game
{
	public interface IHittable : IPlayable {

		int Index { get; set; }
		int Version { get; set; }
		bool IsCollidable { get; }
		HitObject[] GetHitShapes();
	}
}
