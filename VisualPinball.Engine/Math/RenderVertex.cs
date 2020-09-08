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

namespace VisualPinball.Engine.Math
{
	public class RenderVertex2D : Vertex2D, IRenderVertex
	{
		public void Set(Vertex3D v)
		{
			base.Set(v.X, v.Y);
		}

		public bool Smooth { get; set; }
		public bool IsSlingshot { get; set; }
		public bool IsControlPoint { get; set; }

		public RenderVertex2D() { }
		public RenderVertex2D(float x, float y) : base(x, y) { }
	}

	public class RenderVertex3D : Vertex3D, IRenderVertex
	{
		public new void Set(Vertex3D v)
		{
			base.Set(v);
		}

		public bool Smooth { get; set; }
		public bool IsSlingshot { get; set; }
		public bool IsControlPoint { get; set; }

		public RenderVertex3D() { }
		public RenderVertex3D(float x, float y, float z) : base(x, y, z) { }
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
