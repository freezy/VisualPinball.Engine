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
	internal static class PlungerCommands
	{

		public static void PullBack(float speed, ref PlungerVelocityState velocity, ref PlungerMovementState movement)
		{
			movement.Speed = 0.0f;
			velocity.PullForce = speed;

			// deactivate the retract code
			velocity.AddRetractMotion = false;
		}

		public static void PullBackAndRetract(float speedPull, ref PlungerVelocityState velocity, ref PlungerMovementState movement)
		{
			movement.Speed = 0.0f;
			velocity.PullForce = speedPull;

			// deactivate the retract code
			velocity.AddRetractMotion = false;
			movement.RetractMotion = false;
			velocity.InitialSpeed = speedPull;
		}

		public static void Fire(float startPos, ref PlungerVelocityState velocity, ref PlungerMovementState movement, in PlungerStaticState staticState)
		{
			// cancel any pull force
			velocity.PullForce = 0.0f;

			// make sure the starting point is behind the park position
			if (startPos < staticState.RestPosition) {
				startPos = staticState.RestPosition;
			}

			// move immediately to the starting position
			movement.Position = staticState.FrameEnd + startPos * staticState.FrameLen;

			// Figure the release speed as a fraction of the
			// fire speed property, linearly proportional to the
			// starting distance.  Note that the release motion
			// is upwards, so the speed is negative.
			var dx = startPos - staticState.RestPosition;
			const float normalize = Engine.VPT.Plunger.Plunger.PlungerNormalize / 13.0f / 100.0f;
			movement.FireSpeed = -staticState.SpeedFire
			              * dx * staticState.FrameLen / Engine.VPT.Plunger.Plunger.PlungerMass
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
			movement.FireBounce = -bounceDist * staticState.RestPosition;

			// enter Fire mode for long enough for the process to complete
			movement.FireTimer = 200;

			movement.RetractMotion = false;
		}
	}
}
