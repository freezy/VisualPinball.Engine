using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct LineCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float2 _v1;
		private float2 _v2;
		private float2 _normal;
		private float _length;

		public ColliderType Type => _header.Type;
		public int MemorySize  => UnsafeUtility.SizeOf<LineCollider>();

		public static unsafe BlobAssetReference<Collider> CreateBlob(float2 p1, float2 p2, float zLow, float zHigh)
		{
			var collider = default(LineCollider);
			collider.Init(p1, p2, zLow, zHigh);
			return BlobAssetReference<Collider>.Create(&collider, sizeof(LineCollider));
		}

		public static void CreatePtr(LineSeg lineSeg, ref BlobPtr<Collider> ptr)
		{
			var collider = default(LineCollider);
			collider.Init(lineSeg.V1.ToUnityFloat2(), lineSeg.V2.ToUnityFloat2(), lineSeg.HitBBox.ZLow, lineSeg.HitBBox.ZHigh);

			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineCollider>>(ref ptr);
			linePtr.Value = collider;
		}

		private void Init(float2 p1, float2 p2, float zLow, float zHigh)
		{
			_header.Type = ColliderType.Line;
			_v1 = p1;
			_v2 = p2;

			CalcNormal();
			CalcHitBBox(zLow, zHigh);
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
			_header.HitBBox.Left = math.min(_v1.x, _v2.x);
			_header.HitBBox.Right = math.max(_v1.x, _v2.x);
			_header.HitBBox.Top = math.min(_v1.y, _v2.y);
			_header.HitBBox.Bottom = math.max(_v1.y, _v2.y);
			_header.HitBBox.ZLow = zLow;
			_header.HitBBox.ZHigh = zHigh;
		}

		public float HitTest(float dTime)
		{
			return -1;
		}

	}
}
