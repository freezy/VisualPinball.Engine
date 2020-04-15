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

		public static void Create(HitPoint src, ref BlobPtr<Collider> dest, BlobBuilder builder)
		{
			var collider = default(PointCollider);
			collider.Init(src);
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<PointCollider>>(ref dest);
			builder.Allocate(ref linePtr);
		}

		private void Init(HitPoint src)
		{
			_header.Type = ColliderType.Point;
			_header.EntityIndex = src.ItemIndex;
			_header.HitBBox = src.HitBBox.ToAabb();

			_p = src.P.ToUnityFloat3();
		}

		public float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			return -1;
		}
	}
}
