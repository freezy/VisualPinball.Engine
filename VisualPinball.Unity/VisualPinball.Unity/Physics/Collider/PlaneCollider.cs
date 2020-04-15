using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct PlaneCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float3 _normal;
		private float _d;

		public ColliderType Type => _header.Type;

		public static void Create(HitPlane src, ref BlobPtr<Collider> dest, BlobBuilder builder)
		{
			var collider = default(PlaneCollider);
			collider.Init(src);
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<PlaneCollider>>(ref dest);
			builder.Allocate(ref ptr);
		}

		private void Init(HitPlane src)
		{
			_header.Type = ColliderType.Plane;
			_header.EntityIndex = src.ItemIndex;
			_header.HitBBox = src.HitBBox.ToAabb();

			_normal = src.Normal.ToUnityFloat3();
			_d = src.D;
		}

		public float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			return -1;
		}
	}
}
