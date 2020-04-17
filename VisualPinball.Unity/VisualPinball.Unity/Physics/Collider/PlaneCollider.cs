using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
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

		public static void Create(BlobBuilder builder, HitPlane src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<PlaneCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		public static Collider Create(HitPlane src)
		{
			var collider = default(PlaneCollider);
			collider.Init(src);
			ref var ptr = ref UnsafeUtilityEx.As<PlaneCollider, Collider>(ref collider);
			return ptr;
		}

		private void Init(HitPlane src)
		{
			_header.Type = ColliderType.Plane;
			_header.EntityIndex = src.ItemIndex;
			_header.Aabb = src.HitBBox.ToAabb();

			_normal = src.Normal.ToUnityFloat3();
			_d = src.D;
		}

		public float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			return -1;
		}
	}
}
