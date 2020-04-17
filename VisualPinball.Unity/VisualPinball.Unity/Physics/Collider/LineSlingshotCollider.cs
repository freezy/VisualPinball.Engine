using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct LineSlingshotCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float2 _v1;
		private float2 _v2;
		private float2 _normal;
		private float _length;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, LineSeg src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineSlingshotCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(LineSeg src)
		{
			_header.Type = ColliderType.Line;
			_header.EntityIndex = src.ItemIndex;
			_header.Aabb = src.HitBBox.ToAabb();

			_v1 = src.V1.ToUnityFloat2();
			_v2 = src.V2.ToUnityFloat2();

			CalcNormal();
		}

		private void CalcNormal()
		{
			var vT = new float2(_v1.x - _v2.x, _v1.y - _v2.y);

			// Set up line normal
			_length = math.length(vT);
			var invLength = 1.0f / _length;
			_normal.x = vT.y * invLength;
			_normal.y = -vT.x * invLength;
		}

		private void CalcHitBBox(float zLow, float zHigh)
		{
			// Allow roundoff
			_header.Aabb.Left = math.min(_v1.x, _v2.x);
			_header.Aabb.Right = math.max(_v1.x, _v2.x);
			_header.Aabb.Top = math.min(_v1.y, _v2.y);
			_header.Aabb.Bottom = math.max(_v1.y, _v2.y);
			_header.Aabb.ZLow = zLow;
			_header.Aabb.ZHigh = zHigh;
		}

		public float HitTest(in BallData ball, float dTime, CollisionEventData coll)
		{
			return -3;
		}
	}
}
