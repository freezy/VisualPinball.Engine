using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Common;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
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

		public static unsafe void Create(BlobBuilder builder, Hit3DPoly src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<Poly3DCollider>>(ref dest);
			var totalSize = sizeof(Poly3DCollider) + sizeof(float3) * src.Rgv.Length;
			totalSize = (totalSize + 15) & 0x7ffffff0;

			ref var collider = ref builder.Allocate(ref ptr, totalSize, out var offsetPtr);
			collider.Init(src, offsetPtr);
		}


		public static unsafe BlobAssetReference<Poly3DCollider> Create(Hit3DPoly hitObject)
		{
			// Allocate
			int totalSize = sizeof(Poly3DCollider) + sizeof(float3) * hitObject.Rgv.Length;
			totalSize = (totalSize + 15) & 0x7ffffff0;
			Poly3DCollider* data = (Poly3DCollider*)UnsafeUtility.Malloc(totalSize, 16, Allocator.Temp);
			UnsafeUtility.MemClear(data, totalSize);

			// Initialize
			{
				byte* end = (byte*)data + sizeof(Poly3DCollider);
				data->_rgvBlob.Offset = UnsafeEx.CalculateOffset(end, ref data->_rgvBlob);
				data->_rgvBlob.Length = hitObject.Rgv.Length;

				for (var i = 0; i < hitObject.Rgv.Length; i++)
				{
					data->_rgv[i] = hitObject.Rgv[i].ToUnityFloat3();
				}
			}

			var collider = BlobAssetReference<Poly3DCollider>.Create(data, totalSize);
			UnsafeUtility.Free(data, Allocator.Temp);
			return collider;
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

		public float HitTest(ref CollisionEventData coll, in BallData ball, float dTime)
		{
			// todo
			return -1;
		}

		public override string ToString()
		{
			return $"Poly3DCollider, rgv[0] = {_rgv[0]}";
		}
	}
}
