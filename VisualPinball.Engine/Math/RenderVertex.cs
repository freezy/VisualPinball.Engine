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

namespace VisualPinball.Engine.Math
{
	public class RenderVertex2D : IRenderVertex
	{
		public float X;
		public float Y;

		public float GetX() => X;
		public float GetY() => Y;

		public bool Smooth { get; set; }
		public bool IsSlingshot { get; set; }
		public bool IsControlPoint { get; set; }

		public RenderVertex2D() { }

		public RenderVertex2D(float x, float y)
		{
			X = x;
			Y = y;
		}

		public static Vertex2D operator +(RenderVertex2D a, Vertex2D b) => new Vertex2D(a.X + b.X, a.Y + b.Y);
		public static Vertex2D operator +(Vertex2D a, RenderVertex2D b) => b + a;
		public static implicit operator Vertex2D(RenderVertex2D v) => new Vertex2D(v.X, v.Y);


		public void Set(Vertex3D v)
		{
			X = v.X;
			Y = v.Y;
		}
	}

	public class RenderVertex3D : IRenderVertex
	{
		public float X;
		public float Y;
		public float Z;

		public float GetX() => X;
		public float GetY() => Y;

		public bool Smooth { get; set; }
		public bool IsSlingshot { get; set; }
		public bool IsControlPoint { get; set; }

		public static Vertex3D operator -(RenderVertex3D a, RenderVertex3D b) => new Vertex3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

		public RenderVertex3D() { }

		public RenderVertex3D(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public void Set(Vertex3D v)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
		}

		public void Set(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}
	}

	public interface IRenderVertex
	{
		void Set(Vertex3D v);

		float GetX();
		float GetY();

		bool Smooth { get; set; }
		bool IsSlingshot { get; set; }
		bool IsControlPoint { get; set; }
	}
}
