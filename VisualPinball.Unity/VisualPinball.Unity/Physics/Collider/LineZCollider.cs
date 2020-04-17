using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct LineZCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float2 _xy;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, HitLineZ src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineZCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(HitLineZ src)
		{
			_header.Type = ColliderType.LineZ;
			_header.EntityIndex = src.ItemIndex;
			_header.Aabb = src.HitBBox.ToAabb();

			_xy = src.Xy.ToUnityFloat2();
		}

		private void CalcHitBBox(float zLow, float zHigh)
		{
			_header.Aabb.Left = _xy.x;
			_header.Aabb.Right = _xy.x;
			_header.Aabb.Top = _xy.y;
			_header.Aabb.Bottom = _xy.y;
			_header.Aabb.ZLow = zLow;
			_header.Aabb.ZHigh = zHigh;
		}

		public float HitTest(BallData ball, float dTime, CollisionEventData coll)
		{
			return -1;
		}
	}
}
