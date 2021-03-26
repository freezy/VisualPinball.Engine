// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct Aabb
	{
		public float Left;
		public float Top;
		public float Right;
		public float Bottom;
		public float ZLow;
		public float ZHigh;

		public float Width => math.abs(Left - Right);
		public float Height => math.abs(Top - Bottom);
		public float Depth => math.abs(ZLow - ZHigh);

		public Aabb2D Bounds2D => new Aabb2D(
			new float2(Left + Width / 2, Top + Height / 2),
			new float2(Width / 2, Height / 2)
		);

		public Aabb(float left, float right, float top, float bottom, float zLow, float zHigh)
		{
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

		// Checking Aabb 442.2034 → 509.7966 | 976.6798 ↘ 1044.273 | -8.79384 ↑ 58.79945 against Aabb 431 → 521 | 1036 ↘ 1126 | 90 ↑ 0 (2)

		public bool IntersectRect(Aabb rc)
		{
			return Right >= rc.Left  // 521 >= 442.2034
			       && Bottom >= rc.Top // 1126 >= 976.6798
			       && Left <= rc.Right // 431 <= 509.7966
			       && Top <= rc.Bottom // 1036 <= 1044.273
			       && ZLow <= rc.ZHigh // 0 <= -8.79384
			       && ZHigh >= rc.ZLow; // 90 >= 58.79945
		}

		public override string ToString()
		{
			return $"Aabb {Left} → {Right} | {Top} ↘ {Bottom} | {ZLow} ↑ {ZHigh}";
		}
	}
}
