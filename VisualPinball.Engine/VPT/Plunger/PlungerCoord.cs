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

namespace VisualPinball.Engine.VPT.Plunger
{
	/// <summary>
	/// Plunger shape descriptor coordinate entry.
	///
	/// The plunger is essentially built on a virtual lathe:  it consists
	/// of a series of circles centered on the longitudinal axis. Each
	/// coordinate gives the position along the axis of the circle,
	/// expressed as the distance (in standard table units) from the tip,
	/// and the radius of the circle, expressed as a fraction of the
	/// nominal plunger width (_data.Width).  Each coordinate also
	/// specifies the normal for the vertices along that circle, and the
	/// vertical texture offset of the vertices.
	///
	/// The horizontal texture offset is inferred in the lathing process -
	/// the center of the texture is mapped to the top center of each
	/// circle, and the texture is wrapped around the sides of the circle.
	/// </summary>
	public struct PlungerCoord
	{
		/// <summary>
		/// Radius at this point, as a fraction of nominal plunger width
		/// </summary>
		public float r;

		/// <summary>
		/// Y position, in table distance units, with the tip at 0.0.
		/// </summary>
		public float y;

		/// <summary>
		/// Texture v coordinate of the vertices on this circle
		/// </summary>
		public float tv;

		/// <summary>
		/// normal of the top vertex along this circle
		/// </summary>
		public float nx;

		/// <summary>
		/// normal of the top vertex along this circle
		/// </summary>
		public float ny;

		public PlungerCoord(float r, float y, float tv, float nx, float ny)
		{
			this.r = r;
			this.y = y;
			this.tv = tv;
			this.nx = nx;
			this.ny = ny;
		}

		public void Set(float _r, float _y, float _tv, float _nx, float _ny)
		{
			r = _r;
			y = _y;
			tv = _tv;
			nx = _nx;
			ny = _ny;
		}

		public static readonly PlungerCoord[] modernCoords = new PlungerCoord[] {
			new PlungerCoord(0.20f, 0.0f, 0.00f, 1.0f, 0.0f), // tip
			new PlungerCoord(0.30f, 3.0f, 0.11f, 1.0f, 0.0f), // tip
			new PlungerCoord(0.35f, 5.0f, 0.14f, 1.0f, 0.0f), // tip
			new PlungerCoord(0.35f, 23.0f, 0.19f, 1.0f, 0.0f), // tip
			new PlungerCoord(0.45f, 23.0f, 0.21f, 0.8f, 0.0f), // ring
			new PlungerCoord(0.25f, 24.0f, 0.25f, 0.3f, 0.0f), // shaft
			new PlungerCoord(0.25f, 100.0f, 1.00f, 0.3f, 0.0f) // shaft
		};
	}
}
