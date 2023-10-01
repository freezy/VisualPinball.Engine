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
	internal struct FlipperState
	{
		internal int ItemId;
		internal FlipperStaticData Static;
		internal FlipperMovementData Movement;
		internal FlipperVelocityData Velocity;
		internal FlipperHitData Hit;
		internal FlipperTricksData Tricks;
		internal SolenoidStateData Solenoid;

		public FlipperState(int itemId, FlipperStaticData @static, FlipperMovementData movement,
			FlipperVelocityData velocity, FlipperHitData hit, FlipperTricksData tricks,
			SolenoidStateData solenoid)
		{
			ItemId = itemId;
			Static = @static;
			Movement = movement;
			Velocity = velocity;
			Hit = hit;
			Tricks = tricks;
			Solenoid = solenoid;
		}
	}
}
