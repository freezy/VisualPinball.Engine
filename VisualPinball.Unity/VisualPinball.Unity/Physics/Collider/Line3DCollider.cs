using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct Line3DCollider
	{
		private ColliderHeader _header;

		private float2 _xy;
		private float _zLow;
		private float _zHigh;
		private float3x3 _matrix;

		public static void Create(BlobBuilder builder, HitLine3D src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<Line3DCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(HitLine3D src)
		{
			_header.Init(ColliderType.Line3D, src);

			_xy = src.Xy.ToUnityFloat2();
			_zLow = src.ZLow;
			_zHigh = src.ZHigh;
			_matrix = src.Matrix.ToUnityFloat3x3();
		}

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			return HitTest(ref collEvent, ref this, in ball, dTime);
		}

		public static float HitTest(ref CollisionEventData collEvent, ref Line3DCollider coll, in BallData ball, float dTime)
		{
			// todo
			// if (!IsEnabled) {
			// 	return -1.0f;
			// }

			var hitTestBall = ball;

			// transform ball to cylinder coordinate system
			hitTestBall.Position = math.mul(coll._matrix, ball.Position);
			hitTestBall.Velocity = math.mul(coll._matrix, ball.Velocity);

			ref var lineZColl = ref UnsafeUtilityEx.As<Line3DCollider, LineZCollider>(ref coll);
			var hitTime = LineZCollider.HitTest(ref collEvent, in lineZColl, in hitTestBall, dTime);

			// transform hit normal back to world coordinate system
			if (hitTime >= 0) {
				collEvent.HitNormal = math.mul(coll._matrix, collEvent.HitNormal);
			}

			return hitTime;
		}
	}
}
