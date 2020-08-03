using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Common;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.Physics.Event;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct Poly3DCollider : ICollider, ICollidable
	{

		private ColliderHeader _header;
		private float3 _normal;
		private BlobArray _rgvBlob; // read comment at Unity.Physics.BlobArray
		public BlobArray.Accessor<float3> _rgv => new BlobArray.Accessor<float3>(ref _rgvBlob);

		public ColliderType Type => _header.Type;
		public float3 Normal() => _normal;

		public static unsafe void Create(BlobBuilder builder, Hit3DPoly src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<Poly3DCollider>>(ref dest);
			var totalSize = sizeof(Poly3DCollider) + sizeof(float3) * src.Rgv.Length;
			totalSize = (totalSize + 15) & 0x7ffffff0;

			ref var collider = ref builder.Allocate(ref ptr);
			//ref var collider = ref builder.Allocate(ref ptr, totalSize, out var offsetPtr);
			//collider.Init(src, offsetPtr);
			collider.Init(src);
		}

		private unsafe void Init(Hit3DPoly src, int* offsetPtr)
		{
			_header.Init(ColliderType.Poly3D, src);
			_normal = src.Normal.ToUnityFloat3();

			var end = (byte*)offsetPtr + sizeof(Poly3DCollider);
			_rgvBlob.Offset = UnsafeEx.CalculateOffset(end, ref _rgvBlob);
			_rgvBlob.Length = src.Rgv.Length;
			for (var i = 0; i < src.Rgv.Length; i++) {
				_rgv[i] = src.Rgv[i].ToUnityFloat3();
			}
		}

		private void Init(Hit3DPoly src)
		{
			_header.Init(ColliderType.Poly3D, src);
			_normal = src.Normal.ToUnityFloat3();
		}

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			// todo
			return -1;
		}

		public void Collide(ref BallData ball,  ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref Random random)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in _normal, ref random);

			if (_header.FireEvents && dot >= _header.Threshold && _header.IsPrimitive) {
				// todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in _header);
			}
		}
	}
}
