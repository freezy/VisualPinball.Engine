using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct FlipperCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		public ColliderType Type => _header.Type;

		public static void Create(FlipperHit src, ref BlobPtr<Collider> dest, BlobBuilder builder)
		{
			var collider = default(FlipperCollider);
			collider.Init(src);
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<FlipperCollider>>(ref dest);
			builder.Allocate(ref linePtr);
		}

		private void Init(FlipperHit src)
		{
			_header.Type = ColliderType.Flipper;
			_header.EntityIndex = src.ItemIndex;
			_header.HitBBox = src.HitBBox.ToAabb();
		}

		public float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			return -1;
		}
	}
}
