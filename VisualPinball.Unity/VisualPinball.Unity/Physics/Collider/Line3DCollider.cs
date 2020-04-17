using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct Line3DCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float2 _xy;
		private float3x3 _matrix;
		private float _zLow;
		private float _zHigh;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, HitLine3D src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<Line3DCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(HitLine3D src)
		{
			_header.Type = ColliderType.Line3D;
			_header.EntityIndex = src.ItemIndex;
			_header.Aabb = src.HitBBox.ToAabb();

			_xy = src.Xy.ToUnityFloat2();
			_matrix = src.Matrix.ToUnityFloat3x3();
			_zLow = src.ZLow;
			_zHigh = src.ZHigh;
		}

		public float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			return -1;
		}
	}
}
