using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity.Physics
{
	public interface IHitObject
	{
		Rect3D HitBBox { get; }

		float HitTest(VPT.Ball.BallData ball, float dTime, CollisionEvent coll, PlayerPhysics physics);
	}
}
