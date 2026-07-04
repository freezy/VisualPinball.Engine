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
	/// Logical axis slot used when routing native analog samples into a configured
	/// nudge sensor.
	/// </summary>
	public enum NudgeSensorChannel
	{
		/// <summary>Gamepad/intent position X.</summary>
		X = 0,
		/// <summary>Gamepad/intent position Y.</summary>
		Y = 1,
		/// <summary>Cabinet velocity X.</summary>
		VelocityX = 2,
		/// <summary>Cabinet velocity Y.</summary>
		VelocityY = 3,
		/// <summary>Cabinet acceleration X.</summary>
		AccelerationX = 4,
		/// <summary>Cabinet acceleration Y.</summary>
		AccelerationY = 5
	}
}
