using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
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

		public static void Create(BlobBuilder builder, FlipperHit src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<FlipperCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(FlipperHit src)
		{
			_header.Type = ColliderType.Flipper;
			_header.EntityIndex = src.ItemIndex;
			_header.Id = src.Id;
		}

		public float HitTest(ref CollisionEventData coll, in BallData ball, float dTime)
		{
			return -1;
		}
	}
}
