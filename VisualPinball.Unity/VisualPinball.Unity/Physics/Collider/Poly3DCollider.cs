using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct Poly3DCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float3 _normal;
		//private NativeArray<float3> _rgv;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, Hit3DPoly src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<Poly3DCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src, builder);
		}

		private void Init(Hit3DPoly src, BlobBuilder builder)
		{
			_header.Type = ColliderType.Poly3D;
			_header.EntityIndex = src.ItemIndex;
			_header.HitBBox = src.HitBBox.ToAabb();

			_normal = src.Normal.ToUnityFloat3();
			// _rgv = new NativeArray<float3>(src.Rgv.Length, Allocator.Persistent);
			// for (var i = 0; i < src.Rgv.Length; i++) {
			// 	_rgv[i] = src.Rgv[i].ToUnityFloat3();
			// }
		}

		public float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			return -1;
		}
	}
}
