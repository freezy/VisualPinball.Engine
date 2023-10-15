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

using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	internal static class FlipperDisplacementPhysics
	{
		internal static void UpdateDisplacement(int itemId, ref FlipperMovementState movementState, ref FlipperTricksData tricks, in FlipperStaticData staticState,
			float dTime, ref NativeQueue<EventData>.ParallelWriter events)
		{
			//var dTime = _simulateCycleSystemGroup.HitTime;
			var currentTime = 0;// todo SystemAPI.Time.ElapsedTime;

			movementState.Angle += movementState.AngleSpeed * dTime; // move flipper angle

			var angleMin = math.min(staticState.AngleStart, tricks.AngleEnd);
			var angleMax = math.max(staticState.AngleStart, tricks.AngleEnd);

			if (movementState.Angle > angleMax) {
				movementState.Angle = angleMax;
			}

			if (movementState.Angle < angleMin) {
				movementState.Angle = angleMin;
			}

			if (math.abs(movementState.AngleSpeed) < 0.0005f) {
				// avoids "jumping balls" when two or more balls held on flipper (and more other balls are in play) //!! make dependent on physics update rate
				return;
			}

			var handleEvent = false;

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (movementState.Angle == tricks.AngleEnd) {
				tricks.FlipperAngleEndTime = currentTime;
			}

			if (movementState.Angle >= angleMax) {
				// hit stop?
				if (movementState.AngleSpeed > 0) {
					handleEvent = true;
				}

			} else if (movementState.Angle <= angleMin) {
				if (movementState.AngleSpeed < 0) {
					handleEvent = true;
				}
			}

			if (handleEvent) {
				var angleSpeed = math.abs(math.degrees(movementState.AngleSpeed));
				movementState.AngularMomentum *= -0.3f; // make configurable?
				movementState.AngleSpeed = movementState.AngularMomentum / staticState.Inertia;

				if (movementState.EnableRotateEvent > 0) {

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, angleSpeed));

				} else if (movementState.EnableRotateEvent < 0) {

					// send BOS event
					events.Enqueue(new EventData(EventId.LimitEventsBos, itemId, angleSpeed));
				}

				movementState.EnableRotateEvent = 0;
			}
		}
	}
}
