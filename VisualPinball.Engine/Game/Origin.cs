// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

namespace VisualPinball.Engine.Game
{
	public enum Origin
	{
		/// <summary>
		/// Keeps the origin the same as in Visual Pinball. <p/>
		///
		/// This means that the object must additional retrieve a
		/// transformation matrix.
		/// </summary>
		Original,

		/// <summary>
		/// Transforms all vertices so their origin is the global origin. <p/>
		///
		/// No additional transformation matrices must be applied if the object
		/// is static.
		/// </summary>
		Global
	}
}
