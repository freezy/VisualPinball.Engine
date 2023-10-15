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

// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class SpinnerDisplacementPhysics
	{
		internal static void UpdateDisplacement(int itemId, ref SpinnerMovementState movement, in SpinnerStaticState state,
			float dTime, ref NativeQueue<EventData>.ParallelWriter events)
		{

			// those are already converted to radian during authoring.
			var angleMin = state.AngleMin;
			var angleMax = state.AngleMax;

			// blocked spinner, limited motion spinner
			if (state.AngleMin != state.AngleMax) {

				movement.Angle += movement.AngleSpeed * dTime;

				if (movement.Angle > angleMax) {
					movement.Angle = angleMax;

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, math.abs(math.degrees(movement.AngleSpeed))));

					if (movement.AngleSpeed > 0.0f) {
						movement.AngleSpeed *= -0.005f - state.Elasticity;
					}
				}

				if (movement.Angle < angleMin) {
					movement.Angle = angleMin;

					// send Park event
					events.Enqueue(new EventData(EventId.LimitEventsBos, itemId, math.abs(math.degrees(movement.AngleSpeed))));

					if (movement.AngleSpeed < 0.0f) {
						movement.AngleSpeed *= -0.005f - state.Elasticity;
					}
				}

			} else {

				var target = movement.AngleSpeed > 0.0f
					? movement.Angle < math.PI ? math.PI : 3.0f * math.PI
					: movement.Angle < math.PI ? -math.PI : math.PI;

				movement.Angle += movement.AngleSpeed * dTime;

				if (movement.AngleSpeed > 0.0f) {

					if (movement.Angle > target) {
						events.Enqueue(new EventData(EventId.SpinnerEventsSpin, itemId, true));
					}

				} else {
					if (movement.Angle < target) {
						events.Enqueue(new EventData(EventId.SpinnerEventsSpin, itemId, true));
					}
				}

				while (movement.Angle > 2.0f * math.PI) {
					movement.Angle -= 2.0f * math.PI;
				}

				while (movement.Angle < 0.0f) {
					movement.Angle += 2.0f * math.PI;
				}
			}
		}
	}
}
