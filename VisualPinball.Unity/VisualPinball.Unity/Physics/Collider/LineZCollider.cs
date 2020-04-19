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
			_header.Id = src.Id;
			_header.EntityIndex = src.ItemIndex;

			_xy = src.Xy.ToUnityFloat2();
		}

		public float HitTest(ref CollisionEventData coll, in BallData ball, float dTime)
		{
			return -1;
		}
	}
}
