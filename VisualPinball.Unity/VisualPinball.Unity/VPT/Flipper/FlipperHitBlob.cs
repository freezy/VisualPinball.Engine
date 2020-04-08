using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperHitBlob : IHitObject
	{
		public Rect3D HitBBox { get; }
		public void DoHitTest(Engine.VPT.Ball.Ball ball, CollisionEvent coll, PlayerPhysics physics)
		{

		}

		public static FlipperHitBlob Create(FlipperHit flipperHit)
		{
			return new FlipperHitBlob();
		}
	}

}
