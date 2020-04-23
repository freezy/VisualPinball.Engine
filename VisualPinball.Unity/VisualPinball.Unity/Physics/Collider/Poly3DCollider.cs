using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
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

		public static void Create(BlobBuilder builder, Hit3DPoly src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<Poly3DCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		public static unsafe Poly3DCollider Create(Hit3DPoly hitObject)
		{
			// Allocate
			int totalSize = sizeof(Poly3DCollider) + sizeof(float3) * hitObject.Rgv.Length;
			Poly3DCollider* data = (Poly3DCollider*)UnsafeUtility.Malloc(totalSize, 16, Allocator.Persistent);
			UnsafeUtility.MemClear(data, totalSize);

			// Initialize
			{
				byte* end = (byte*) data + sizeof(Poly3DCollider);
				data->_rgvBlob.Offset = UnsafeEx.CalculateOffset(end, ref data->_rgvBlob);
				data->_rgvBlob.Length = hitObject.Rgv.Length;

				for (var i = 0; i < hitObject.Rgv.Length; i++) {
					data->_rgv[i] = hitObject.Rgv[i].ToUnityFloat3();
				}
			}

			UnsafeUtility.CopyPtrToStructure(data, out Poly3DCollider collider);
			return collider;
		}

		private void Init(Hit3DPoly src)
		{
			_header.Init(ColliderType.Poly3D, src);
			_normal = src.Normal.ToUnityFloat3();
		}

		public float HitTest(ref CollisionEventData coll, in BallData ball, float dTime)
		{
			// todo
			return -1;
		}
	}
}
