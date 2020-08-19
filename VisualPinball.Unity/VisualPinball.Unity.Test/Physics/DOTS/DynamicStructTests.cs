using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	[TestFixture]
	public class DynamicStructTests
	{
		[Test]
		public unsafe void ShouldSerializeBlobAssetReference()
		{
			var quadTreeBlobAssetRef = QuadTree.CreateBlobAssetReference();

			ref var collider1 = ref quadTreeBlobAssetRef.Value.Colliders[0].Value;
			ref var collider2 = ref quadTreeBlobAssetRef.Value.Colliders[1].Value;

			Assert.AreEqual(ColliderType.Line, collider1.Type);
			//Assert.AreEqual(new Aabb(1f, 3f, 20f, 4f, 5f, 6f), collider1.Aabb);
			fixed (Collider* collider = &collider1) {
				Assert.AreEqual(new float2(1f, 20f), ((LineCollider*)collider)->V1);
				Assert.AreEqual(new float2(1f, 20f), ((LineCollider*)collider)->GetV1());
			}
			Assert.AreEqual(ColliderType.Point, collider2.Type);
		}

		[Test]
		public unsafe void ShouldSerializeStruc()
		{
			var coll = LineCollider.Create(new float2(1f, 2f), new float2(3f, 4f), 5f, 6f);
			var collider = &coll;
			Assert.AreEqual(new float2(1f, 2f), ((LineCollider*)collider)->V1);
		}

		[Test]
		public void ShouldSerializeBlobArray()
		{
			// var hit = new Hit3DPoly(new[] { new Vertex3D(1, 2, 3), new Vertex3D(4, 5, 6) });
			// var coll = Poly3DCollider.Create(hit);
			//
			// Assert.AreEqual(hit.Rgv[0].ToUnityFloat3(), coll.Value._rgv[0]);
			// Assert.AreEqual(hit.Rgv[1].ToUnityFloat3(), coll.Value._rgv[1]);
		}

		[Test]
		public void ShouldSerializePoly3DCollider()
		{
			var colliderBlob = ColliderBlob.CreateBlobAssetReference( new List<HitObject> {
				new Hit3DPoly(new[] { new Vertex3D(1, 2, 3), new Vertex3D(4, 5, 6) }, ItemType.Table)
			}, 0, 0);

			ref var poly3DCollider = ref colliderBlob.Value.Colliders[0].Value;

			Assert.AreEqual("Poly3DCollider, rgv[0] = float3(1f, 2f, 3f)", Unity.Collider.ToString(ref poly3DCollider));
		}
	}

	public struct QuadTree
	{
		public BlobArray<BlobPtr<Collider>> Colliders;

		public static BlobAssetReference<QuadTree> CreateBlobAssetReference()
		{
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var rootQuadTree = ref builder.ConstructRoot<QuadTree>();

				var colliders = builder.Allocate(ref rootQuadTree.Colliders, 2);
				LineCollider.Create(builder, ref colliders[0], new float2(1f, 20f), new float2(3f, 4f), 5f, 6f);
				PointCollider.Create(builder, ref colliders[1], new float3(7f, 8f, 9f));

				return builder.CreateBlobAssetReference<QuadTree>(Allocator.Persistent);
			}
		}
	}

	public struct Collider : ICollider
	{
		public ColliderHeader Header;

		public ColliderType Type => Header.Type;
		public Aabb Aabb => Header.Aabb;
	}

	public struct LineCollider : ICollider
	{
		public ColliderHeader Header;
		public float2 V1;
		public float2 V2;

		public float2 GetV1() => V1;

		public static void Create(BlobBuilder builder, ref BlobPtr<Collider> dest, float2 v1, float2 v2, float zLow, float zHigh)
		{
			ref var linePtr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<LineCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(v1, v2, zLow, zHigh);
		}

		public static Collider Create(float2 v1, float2 v2, float zLow, float zHigh)
		{
			var dest = default(LineCollider);
			dest.Init(v1, v2, zLow, zHigh);
			return UnsafeUtility.As<LineCollider, Collider>(ref dest);
		}

		private void Init(float2 v1, float2 v2, float zLow, float zHigh)
		{
			Header.Type = ColliderType.Line;
			Header.Aabb = GetAabb(v1, v2, zLow, zHigh);

			V1 = v1;
			V2 = v2;
		}

		private static Aabb GetAabb(float2 v1, float2 v2, float zLow, float zHigh) => new Aabb(
			math.min(v1.x, v2.x),
			math.max(v1.x, v2.x),
			math.min(v1.y, v2.y),
			math.max(v1.y, v2.y),
			zLow,
			zHigh
		);
	}

	public struct PointCollider : ICollider
	{
		public ColliderHeader Header;
		public float3 Pos;

		public static void Create(BlobBuilder builder, ref BlobPtr<Collider> dest, float3 pos)
		{
			ref var linePtr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<PointCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(pos);
		}

		private void Init(float3 pos)
		{
			Header.Type = ColliderType.Point;
			Header.Aabb = GetAabb(pos);

			Pos = pos;
		}

		private static Aabb GetAabb(float3 pos) => new Aabb(pos.x, pos.x, pos.y, pos.y, pos.z, pos.z);
	}

	public struct ColliderHeader
	{
		public ColliderType Type;
		public Aabb Aabb;
	}

	public enum ColliderType
	{
		Line,
		Point,
	}

	public interface ICollider
	{

	}


	public struct Aabb
	{
		public float Left;
		public float Top;
		public float Right;
		public float Bottom;
		public float ZLow;
		public float ZHigh;

		public Aabb(float left, float right, float top, float bottom, float zLow, float zHigh)
		{
			Left = left;
			Right = right;
			Top = top;
			Bottom = bottom;
			ZLow = zLow;
			ZHigh = zHigh;
		}
	}
}
