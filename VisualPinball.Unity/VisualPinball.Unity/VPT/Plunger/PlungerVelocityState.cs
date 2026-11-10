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
	internal struct PlungerVelocityState
	{
		/// <summary>
		/// Auto Fire mode timer.  When we're acting as an Auto Plunger,
		/// we'll initiate a synthetic Fire event, which consists of a
		/// KeyDown(Return) message to the script, followed a short
		/// time later by a corresponding KeyUp(Return) message.  This
		/// lets the player use the natural pull-and-release gesture with
		/// a mechanical plunger to trigger the Launch Ball event on a
		/// table that has a pushbutton launcher instead of a regular
		/// plunger.  This timer handles the interval between the KeyDown
		/// and KeyUp events.
		/// </summary>
		public int AutoFireTimer;

		/// <summary>
		/// Incremented each time a pull-and-release of the analog plunger
		/// triggered a synthetic Launch Ball press on an auto plunger. Together
		/// with <see cref="AutoFireTimer"/> this lets the input side see a press
		/// that started and ended within one physics batch.
		/// </summary>
		public int AutoFireCount;

		/// <summary>
		/// Pull force.  This models the force being applied by the player
		/// when pulling back the plunger via the keyboard interface.  When
		/// this is non-zero, we ignore the mechanical plunger position and
		/// instead move under this force.
		/// </summary>
		public float PullForce;

		public bool AddRetractMotion;
		public int RetractWaitLoop;
		public float InitialSpeed;

		public float MechStrength;
	}
}
