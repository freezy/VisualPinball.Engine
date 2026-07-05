// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Selects where a table's tilt-bob switch signal comes from.
	/// </summary>
	public enum TiltBobMode
	{
		/// <summary>
		/// Use the physics engine's simulated plumb-bob edges generated from
		/// cabinet acceleration.
		/// </summary>
		Simulated = 0,

		/// <summary>
		/// Use the player's mapped cabinet tilt input, typically a real plumb bob
		/// wired into the cabinet controller.
		/// </summary>
		Mapped = 1
	}
}
