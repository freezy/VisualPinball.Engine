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
		internal static void UpdateDisplacement(int itemId, ref FlipperMovementData state, ref FlipperTricksData tricks, in FlipperStaticData data,
			float dTime, ref NativeQueue<EventData>.ParallelWriter events)
		{
			//var dTime = _simulateCycleSystemGroup.HitTime;
			var currentTime = 0;// todo SystemAPI.Time.ElapsedTime;

			state.Angle += state.AngleSpeed * dTime; // move flipper angle

			var angleMin = math.min(data.AngleStart, tricks.AngleEnd);
			var angleMax = math.max(data.AngleStart, tricks.AngleEnd);

			if (state.Angle > angleMax) {
				state.Angle = angleMax;
			}

			if (state.Angle < angleMin) {
				state.Angle = angleMin;
			}

			if (math.abs(state.AngleSpeed) < 0.0005f) {
				// avoids "jumping balls" when two or more balls held on flipper (and more other balls are in play) //!! make dependent on physics update rate
				return;
			}

			var handleEvent = false;

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (state.Angle == tricks.AngleEnd) {
				tricks.FlipperAngleEndTime = currentTime;
			}

			if (state.Angle >= angleMax) {
				// hit stop?
				if (state.AngleSpeed > 0) {
					handleEvent = true;
				}

			} else if (state.Angle <= angleMin) {
				if (state.AngleSpeed < 0) {
					handleEvent = true;
				}
			}

			if (handleEvent) {
				var angleSpeed = math.abs(math.degrees(state.AngleSpeed));
				state.AngularMomentum *= -0.3f; // make configurable?
				state.AngleSpeed = state.AngularMomentum / data.Inertia;

				if (state.EnableRotateEvent > 0) {

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, angleSpeed));

				} else if (state.EnableRotateEvent < 0) {

					// send BOS event
					events.Enqueue(new EventData(EventId.LimitEventsBos, itemId, angleSpeed));
				}

				state.EnableRotateEvent = 0;
			}
		}
	}
}
