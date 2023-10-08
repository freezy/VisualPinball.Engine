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
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	internal static class SpinnerDisplacementPhysics
	{
		internal static void UpdateDisplacement(int itemId, ref SpinnerMovementData movementData, in SpinnerStaticData data,
			float dTime, ref NativeQueue<EventData>.ParallelWriter events)
		{

			// those are already converted to radian during authoring.
			var angleMin = data.AngleMin;
			var angleMax = data.AngleMax;

			// blocked spinner, limited motion spinner
			if (data.AngleMin != data.AngleMax) {

				movementData.Angle += movementData.AngleSpeed * dTime;

				if (movementData.Angle > angleMax) {
					movementData.Angle = angleMax;

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, math.abs(math.degrees(movementData.AngleSpeed))));

					if (movementData.AngleSpeed > 0.0f) {
						movementData.AngleSpeed *= -0.005f - data.Elasticity;
					}
				}

				if (movementData.Angle < angleMin) {
					movementData.Angle = angleMin;

					// send Park event
					events.Enqueue(new EventData(EventId.LimitEventsBos, itemId, math.abs(math.degrees(movementData.AngleSpeed))));

					if (movementData.AngleSpeed < 0.0f) {
						movementData.AngleSpeed *= -0.005f - data.Elasticity;
					}
				}

			} else {

				var target = movementData.AngleSpeed > 0.0f
					? movementData.Angle < math.PI ? math.PI : 3.0f * math.PI
					: movementData.Angle < math.PI ? -math.PI : math.PI;

				movementData.Angle += movementData.AngleSpeed * dTime;

				if (movementData.AngleSpeed > 0.0f) {

					if (movementData.Angle > target) {
						events.Enqueue(new EventData(EventId.SpinnerEventsSpin, itemId, true));
					}

				} else {
					if (movementData.Angle < target) {
						events.Enqueue(new EventData(EventId.SpinnerEventsSpin, itemId, true));
					}
				}

				while (movementData.Angle > 2.0f * math.PI) {
					movementData.Angle -= 2.0f * math.PI;
				}

				while (movementData.Angle < 0.0f) {
					movementData.Angle += 2.0f * math.PI;
				}
			}
		}
	}
}
