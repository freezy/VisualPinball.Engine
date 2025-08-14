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

namespace VisualPinball.Unity
{
	internal struct GateMovementState
	{

		/// <summary>
		/// Current angle of the gate bracket, in radians.
		/// </summary>
		public float Angle;
		public float AngleSpeed;

		/// <summary>
		/// This is from VPX, there it's set when a gate is moved by script and not by the physics engine.
		/// </summary>
		public bool ForcedMove;

		/// <summary>
		/// If open, the gate is letting through the in both directions.
		/// </summary>
		public bool IsOpen;

		/// <summary>
		/// From which side the ball is hitting the gate.
		/// </summary>
		public bool HitDirection;

		public bool IsLifting;
		public float LiftAngle;
		public float LiftSpeed;
	}
}
