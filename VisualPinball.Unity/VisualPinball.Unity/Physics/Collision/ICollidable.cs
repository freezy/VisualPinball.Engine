using VisualPinball.Engine.Physics;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public interface ICollidable
	{
		float HitTest(in BallData ball, float dTime, CollisionEventData coll);
	}
}
