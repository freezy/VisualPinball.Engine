using System;
using Unity.Mathematics;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct Aabb : IEquatable<Aabb>
	{
		public int ColliderId;
		public float Left;
		public float Top;
		public float Right;
		public float Bottom;
		public float ZLow;
		public float ZHigh;

		public float Width => math.abs(Left - Right);
		public float Height => math.abs(Top - Bottom);
		public float Depth => math.abs(ZLow - ZHigh);

		public static Aabb Create(int colliderId)
		{
			return new Aabb(
				colliderId,
				float.MaxValue,
				-float.MaxValue,
				float.MaxValue,
				-float.MaxValue,
				float.MaxValue,
				-float.MaxValue
			);
		}

		public Aabb(int colliderId, float left, float right, float top, float bottom, float zLow, float zHigh)
		{
			ColliderId = colliderId;
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

		public bool Equals(Aabb other)
		{
			return ColliderId == other.ColliderId && Left.Equals(other.Left) && Top.Equals(other.Top) && Right.Equals(other.Right) && Bottom.Equals(other.Bottom) && ZLow.Equals(other.ZLow) && ZHigh.Equals(other.ZHigh);
		}

		public override bool Equals(object obj)
		{
			return obj is Aabb other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked {
				var hashCode = ColliderId;
				hashCode = (hashCode * 397) ^ Left.GetHashCode();
				hashCode = (hashCode * 397) ^ Top.GetHashCode();
				hashCode = (hashCode * 397) ^ Right.GetHashCode();
				hashCode = (hashCode * 397) ^ Bottom.GetHashCode();
				hashCode = (hashCode * 397) ^ ZLow.GetHashCode();
				hashCode = (hashCode * 397) ^ ZHigh.GetHashCode();
				return hashCode;
			}
		}
	}
}
