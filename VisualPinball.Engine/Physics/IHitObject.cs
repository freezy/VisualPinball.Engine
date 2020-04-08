using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public interface IHitObject
	{
		Rect3D HitBBox { get; }
		void DoHitTest(Ball ball, CollisionEvent coll, PlayerPhysics physics);
	}
}
