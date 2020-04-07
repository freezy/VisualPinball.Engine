using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Physics
{
	public interface IHitObject
	{
		Rect3D HitBBox { get; }
	}
}
