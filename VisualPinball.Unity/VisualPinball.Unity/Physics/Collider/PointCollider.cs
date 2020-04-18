using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct PointCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float3 _p;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, HitPoint src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<PointCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(HitPoint src)
		{
			_header.Type = ColliderType.Point;
			_header.EntityIndex = src.ItemIndex;
			_header.Aabb = src.HitBBox.ToAabb();

			_p = src.P.ToUnityFloat3();
		}

		public float HitTest(BallData ball, float dTime, CollisionEventData coll)
		{
			return -1;
		}
	}
}
