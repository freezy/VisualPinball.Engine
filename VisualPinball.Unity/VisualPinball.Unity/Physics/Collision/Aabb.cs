// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct Aabb : IEquatable<Aabb>
	{
		public float Left;
		public float Top;
		public float Right;
		public float Bottom;
		public float ZLow;
		public float ZHigh;

		public readonly float Width => math.abs(Left - Right);
		public readonly float Height => math.abs(Top - Bottom);
		public readonly float Depth => math.abs(ZLow - ZHigh);

		public readonly Vector3 Min => new Vector3(Left, Top, ZLow);
		public readonly Vector3 Max => new Vector3(Right, Bottom, ZHigh);

		public readonly Vector3 Center => new Vector3(
			(Right + Left) / 2f,
			(Bottom + Top) / 2f,
			(ZHigh + ZLow) / 2f
		);

		public readonly Vector3 Size => new Vector3(Width, Height, Depth);

		public Aabb(float left, float right, float top, float bottom, float zLow, float zHigh)
		{
			Left = left;
			Right = right;
			Top = top;
			Bottom = bottom;
			ZLow = zLow;
			ZHigh = zHigh;
		}

		public Aabb(float3 min, float3 max)
		{
			Left = min.x;
			Right = max.x;
			Top = min.y;
			Bottom = max.y;
			ZLow = min.z;
			ZHigh = max.z;
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

		public readonly bool IntersectSphere(float3 sphereP, float sphereRsqr)
		{
			var ex = math.max(Left - sphereP.x, 0) + math.max(sphereP.x - Right, 0);
			var ey = math.max(Top - sphereP.y, 0) + math.max(sphereP.y - Bottom, 0);
			var ez = math.max(ZLow - sphereP.z, 0) + math.max(sphereP.z - ZHigh, 0);
			ex *= ex;
			ey *= ey;
			ez *= ez;
			return ex + ey + ez <= sphereRsqr;
		}

		// Checking Aabb 442.2034 → 509.7966 | 976.6798 ↘ 1044.273 | -8.79384 ↑ 58.79945 against Aabb 431 → 521 | 1036 ↘ 1126 | 90 ↑ 0 (2)

		public readonly bool IntersectRect(Aabb rc)
		{
			return Right >= rc.Left  // 521 >= 442.2034
				   && Bottom >= rc.Top // 1126 >= 976.6798
				   && Left <= rc.Right // 431 <= 509.7966
				   && Top <= rc.Bottom // 1036 <= 1044.273
				   && ZLow <= rc.ZHigh // 0 <= -8.79384
				   && ZHigh >= rc.ZLow; // 90 >= 58.79945
		}

		public static implicit operator NativeTrees.AABB(Aabb aabb)
		{
			return new NativeTrees.AABB(aabb.Min, aabb.Max);
		}

		public static implicit operator NativeTrees.AABB2D(Aabb aabb)
		{
			return new NativeTrees.AABB2D(new float2(aabb.Min.x, aabb.Min.y), new float2(aabb.Min.x, aabb.Max.y));
		}

		public static bool operator ==(Aabb a, Aabb b) => a.Equals(b);

		public static bool operator !=(Aabb a, Aabb b) => !a.Equals(b);

		public readonly bool Equals(Aabb a)
		{
			return
				a.Right == Left &&
				a.Left == Left &&
				a.Bottom == Bottom &&
				a.Top == Top &&
				a.ZLow == ZLow &&
				a.ZHigh == ZHigh;
		}

		public Aabb Transform(float4x4 m)
		{
			var p1 = m.MultiplyPoint(new float3(Left, Top, ZHigh));
			var p2 = m.MultiplyPoint(new float3(Right, Top, ZHigh));
			var p3 = m.MultiplyPoint(new float3(Left, Bottom, ZHigh));
			var p4 = m.MultiplyPoint(new float3(Right, Bottom, ZHigh));
			var p5 = m.MultiplyPoint(new float3(Left, Top, ZLow));
			var p6 = m.MultiplyPoint(new float3(Right, Top, ZLow));
			var p7 = m.MultiplyPoint(new float3(Left, Bottom, ZLow));
			var p8 = m.MultiplyPoint(new float3(Right, Bottom, ZLow));

			var min = math.min(p1, math.min(p2, math.min(p3, math.min(p4, math.min(p5, math.min(p6, math.min(p7, p8)))))));
			var max = math.max(p1, math.max(p2, math.max(p3, math.max(p4, math.max(p5, math.max(p6, math.max(p7, p8)))))));

			return new Aabb(min, max);
		}

		private static float min(params float[] values)
		{
			var min = float.MaxValue;
			foreach (var value in values) {
				min = math.min(min, value);
			}
			return min;
		}

		private static float max(params float[] values)
		{
			var max = float.MinValue;
			foreach (var value in values) {
				max = math.max(max, value);
			}
			return max;
		}

		public readonly override bool Equals(object obj)
		{
			if (obj is Aabb)
				return Equals(obj);
			return false;
		}

		public readonly override string ToString()
		{
			return $"Aabb {Left} → {Right} | {Top} ↘ {Bottom} | {ZLow} ↑ {ZHigh}";
		}

		public readonly override int GetHashCode()
		{
			return HashCode.Combine(Right, Left, Bottom, Top, ZLow, ZHigh);
		}
	}
}
