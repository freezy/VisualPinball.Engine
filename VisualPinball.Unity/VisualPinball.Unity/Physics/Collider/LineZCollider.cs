using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct LineZCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float2 _xy;

		public ColliderType Type => _header.Type;

		public static void Create(HitLineZ src, ref BlobPtr<Collider> dest, BlobBuilder builder)
		{
			var collider = default(LineZCollider);
			collider.Init(src);
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineZCollider>>(ref dest);
			builder.Allocate(ref linePtr);
		}

		private void Init(HitLineZ src)
		{
			_header.Type = ColliderType.LineZ;
			_header.HitBBox = src.HitBBox.ToAabb();

			_xy = src.Xy.ToUnityFloat2();
		}

		private void CalcHitBBox(float zLow, float zHigh)
		{
			_header.HitBBox.Left = _xy.x;
			_header.HitBBox.Right = _xy.x;
			_header.HitBBox.Top = _xy.y;
			_header.HitBBox.Bottom = _xy.y;
			_header.HitBBox.ZLow = zLow;
			_header.HitBBox.ZHigh = zHigh;
		}

		public float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			return -1;
		}
	}
}
