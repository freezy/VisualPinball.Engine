// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using VisualPinball.Engine.Common;

namespace VisualPinball.Engine.Math
{
	public struct Rect3D
	{
		public float Left;
		public float Top;
		public float Right;
		public float Bottom;
		public float ZLow;
		public float ZHigh;

		public float Width => MathF.Abs(Left - Right);
		public float Height => MathF.Abs(Top - Bottom);
		public float Depth => MathF.Abs(ZLow - ZHigh);

		public Rect3D(bool init)
		{
			Left = Constants.FloatMax;
			Right = -Constants.FloatMax;
			Top = Constants.FloatMax;
			Bottom = -Constants.FloatMax;
			ZLow = Constants.FloatMax;
			ZHigh = -Constants.FloatMax;
		}

		public Rect3D(float left, float right, float top, float bottom, float zLow, float zHigh)
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
			Left = Constants.FloatMax;
			Right = -Constants.FloatMax;
			Top = Constants.FloatMax;
			Bottom = -Constants.FloatMax;
			ZLow = Constants.FloatMax;
			ZHigh = -Constants.FloatMax;
		}

		public void Extend(Rect3D other)
		{
			Left = MathF.Min(Left, other.Left);
			Right = MathF.Max(Right, other.Right);
			Top = MathF.Min(Top, other.Top);
			Bottom = MathF.Max(Bottom, other.Bottom);
			ZLow = MathF.Min(ZLow, other.ZLow);
			ZHigh = MathF.Max(ZHigh, other.ZHigh);
		}

		public bool IntersectSphere(Vertex3D sphereP, float sphereRsqr)
		{
			var ex = MathF.Max(Left - sphereP.X, 0) + MathF.Max(sphereP.X - Right, 0);
			var ey = MathF.Max(Top - sphereP.Y, 0) + MathF.Max(sphereP.Y - Bottom, 0);
			var ez = MathF.Max(ZLow - sphereP.Z, 0) + MathF.Max(sphereP.Z - ZHigh, 0);
			ex *= ex;
			ey *= ey;
			ez *= ez;
			return ex + ey + ez <= sphereRsqr;
		}

		public bool IntersectRect(Rect3D rc)
		{
			return Right >= rc.Left
			       && Bottom >= rc.Top
			       && Left <= rc.Right
			       && Top <= rc.Bottom
			       && ZLow <= rc.ZHigh
			       && ZHigh >= rc.ZLow;
		}
	}
}
