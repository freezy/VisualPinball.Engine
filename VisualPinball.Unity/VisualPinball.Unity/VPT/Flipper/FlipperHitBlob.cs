using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Physics;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperHitBlob : IHitObject
	{
		public Rect3D HitBBox { get; }

		public float HitTest(BallData ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			return -1;
		}


		public static FlipperHitBlob Create(FlipperHit flipperHit, uint id)
		{
			flipperHit.Id = id;
			return new FlipperHitBlob();
		}
	}

}
