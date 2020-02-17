using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class CollisionEvent
	{
		/// <summary>
		/// The ball that collided with something
		/// </summary>
		public Ball Ball;

		/// <summary>
		/// What the ball collided with
		/// </summary>
		public HitObject Obj;

		/// <summary>
		/// Set to true if impact velocity is ~0
		/// </summary>
		public bool IsContact = false;

		/// <summary>
		/// When the collision happens (relative to current physics state)
		/// </summary>
		public float HitTime = 0;

		/// <summary>
		/// Hit distance
		/// </summary>
		public float HitDistance = 0;

		/// <summary>
		/// Additional collision information
		/// </summary>
		public readonly Vertex3D HitNormal = new Vertex3D();

		/// <summary>
		/// Only "correctly" used by plunger and flipper
		/// </summary>
		public Vertex2D HitVel = new Vertex2D();

		/// <summary>
		/// Only set if isContact is true
		/// </summary>
		public float HitOrgNormalVelocity = 0;

		/// <summary>
		/// Currently only one bit is used (hitmoment == 0 or not)
		/// </summary>
		public bool HitMomentBit = true;

		/// <summary>
		/// UnHit signal/direction of hit/side of hit (spinner/gate)
		/// </summary>
		public bool HitFlag = false;

		public CollisionEvent(Ball ball)
		{
			Ball = ball;
		}

		public static void Reset(CollisionEvent evt)
		{
			evt.Ball = null;
			evt.Obj = null;
			evt.IsContact = false;
			evt.HitTime = 0;
			evt.HitDistance = 0;
			evt.HitNormal.SetZero();
			evt.HitVel.SetZero();
			evt.HitOrgNormalVelocity = 0;
			evt.HitMomentBit = true;
			evt.HitFlag = false;
		}

		public void Clear()
		{
			Obj = null;
		}

		public CollisionEvent Set(CollisionEvent coll)
		{
			Ball = coll.Ball;
			Obj = coll.Obj;
			IsContact = coll.IsContact;
			HitTime = coll.HitTime;
			HitDistance = coll.HitDistance;
			HitNormal.Set(coll.HitNormal);
			HitVel.Set(coll.HitVel.X, coll.HitVel.Y);
			HitOrgNormalVelocity = coll.HitOrgNormalVelocity;
			HitMomentBit = coll.HitMomentBit;
			HitFlag = coll.HitFlag;
			return this;
		}
	}
}
