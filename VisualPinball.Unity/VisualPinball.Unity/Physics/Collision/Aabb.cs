using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct Aabb
	{
		public int ColliderId;
		public Entity ColliderEntity;
		public float Left;
		public float Top;
		public float Right;
		public float Bottom;
		public float ZLow;
		public float ZHigh;

		public float Width => math.abs(Left - Right);
		public float Height => math.abs(Top - Bottom);
		public float Depth => math.abs(ZLow - ZHigh);

		public Aabb(int colliderId, float left, float right, float top, float bottom, float zLow, float zHigh)
		{
			ColliderId = colliderId;
			ColliderEntity = Entity.Null;
			Left = left;
			Right = right;
			Top = top;
			Bottom = bottom;
			ZLow = 0;
			ZLow = zLow;
			ZHigh = zHigh;
		}

		public Aabb(Entity entity, float left, float right, float top, float bottom, float zLow, float zHigh)
		{
			ColliderId = -1;
			ColliderEntity = entity;
			Left = left;
			Right = right;
			Top = top;
			Bottom = bottom;
			ZLow = 0;
			ZLow = zLow;
			ZHigh = zHigh;
		}

		public void Clear()
		{
			Left = float.MaxValue;
			Right = -float.MaxValue;
			Top = float.MaxValue;
			Bottom = -float.MaxValue;
			ZLow = float.MaxValue;
			ZHigh = -float.MaxValue;
		}

		public void Extend(Aabb other)
		{
			Left = math.min(Left, other.Left);
			Right = math.max(Right, other.Right);
			Top = math.min(Top, other.Top);
			Bottom = math.max(Bottom, other.Bottom);
			ZLow = math.min(ZLow, other.ZLow);
			ZHigh = math.max(ZHigh, other.ZHigh);
		}

		public bool IntersectSphere(float3 sphereP, float sphereRsqr)
		{
			var ex = math.max(Left - sphereP.x, 0) + math.max(sphereP.x - Right, 0);
			var ey = math.max(Top - sphereP.y, 0) + math.max(sphereP.y - Bottom, 0);
			var ez = math.max(ZLow - sphereP.z, 0) + math.max(sphereP.z - ZHigh, 0);
			ex *= ex;
			ey *= ey;
			ez *= ez;
			return ex + ey + ez <= sphereRsqr;
		}

		public bool IntersectRect(Aabb rc)
		{
			return Right >= rc.Left
			       && Bottom >= rc.Top
			       && Left <= rc.Right
			       && Top <= rc.Bottom
			       && ZLow <= rc.ZHigh
			       && ZHigh >= rc.ZLow;
		}

		public override string ToString()
		{
			return $"Aabb {Left} → {Right} | {Top} ↘ {Bottom} | {ZLow} ↑ {ZHigh}";
		}
	}
}
