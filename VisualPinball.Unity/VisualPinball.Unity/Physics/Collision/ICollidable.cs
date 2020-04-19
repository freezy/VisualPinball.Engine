using VisualPinball.Engine.Physics;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public interface ICollidable
	{
		float HitTest(ref CollisionEventData coll, in BallData ball, float dTime);
	}
}
