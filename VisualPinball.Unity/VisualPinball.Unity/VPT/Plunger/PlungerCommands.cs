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

namespace VisualPinball.Unity
{
	internal static class PlungerCommands
	{

		public static void PullBack(float speed, ref PlungerVelocityData velocityData, ref PlungerMovementData movementData)
		{
			movementData.Speed = 0.0f;
			velocityData.PullForce = speed;

			// deactivate the retract code
			velocityData.AddRetractMotion = false;
		}

		public static void PullBackAndRetract(float speedPull, ref PlungerVelocityData velocityData, ref PlungerMovementData movementData)
		{
			movementData.Speed = 0.0f;
			velocityData.PullForce = speedPull;

			// deactivate the retract code
			velocityData.AddRetractMotion = false;
			movementData.RetractMotion = false;
			velocityData.InitialSpeed = speedPull;
		}

		public static void Fire(float startPos, ref PlungerVelocityData velocityData, ref PlungerMovementData movementData, in PlungerStaticData staticData)
		{
			// cancel any pull force
			velocityData.PullForce = 0.0f;

			// make sure the starting point is behind the park position
			if (startPos < staticData.RestPosition) {
				startPos = staticData.RestPosition;
			}

			// move immediately to the starting position
			movementData.Position = staticData.FrameEnd + startPos * staticData.FrameLen;

			// Figure the release speed as a fraction of the
			// fire speed property, linearly proportional to the
			// starting distance.  Note that the release motion
			// is upwards, so the speed is negative.
			var dx = startPos - staticData.RestPosition;
			const float normalize = Engine.VPT.Plunger.Plunger.PlungerNormalize / 13.0f / 100.0f;
			movementData.FireSpeed = -staticData.SpeedFire
			              * dx * staticData.FrameLen / Engine.VPT.Plunger.Plunger.PlungerMass
			              * normalize;

			// Figure the target stopping position for the
			// bounce off of the barrel spring.  Treat this
			// as proportional to the pull distance, but max
			// out (i.e., go all the way to the forward travel
			// limit, position 0.0) if the pull position is
			// more than about halfway.
			const float maxPull = .5f;
			var bounceDist = dx < maxPull ? dx / maxPull : 1.0f;

			// the initial bounce will be negative, since we're moving upwards,
			// and we calculated it as a fraction of the forward travel distance
			// (which is the part between 0 and the rest position)
			movementData.FireBounce = -bounceDist * staticData.RestPosition;

			// enter Fire mode for long enough for the process to complete
			movementData.FireTimer = 200;

			movementData.RetractMotion = false;
		}
	}
}
