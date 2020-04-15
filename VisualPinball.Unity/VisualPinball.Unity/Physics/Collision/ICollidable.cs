using VisualPinball.Engine.Physics;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public interface ICollidable
	{
		float HitTest(BallData ball, float dTime, CollisionEvent coll);
	}
}
