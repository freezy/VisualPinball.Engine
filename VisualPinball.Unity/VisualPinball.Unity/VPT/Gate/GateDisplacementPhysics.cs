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
	internal static class GateDisplacementPhysics
	{
		internal static void UpdateDisplacement(int itemId, ref GateMovementData movementData, in GateStaticData data,
			float dTime, ref NativeQueue<EventData>.ParallelWriter events)
		{
			if (data.TwoWay) {
				if (math.abs(movementData.Angle) > data.AngleMax) {
					if (movementData.Angle < 0.0) {
						movementData.Angle = -data.AngleMax;
					} else {
						movementData.Angle = data.AngleMax;
					}

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, movementData.AngleSpeed));

					if (!movementData.ForcedMove) {
						movementData.AngleSpeed = -movementData.AngleSpeed;
						movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementData.AngleSpeed > 0.0) {
						movementData.AngleSpeed = 0.0f;
					}
				}
				if (math.abs(movementData.Angle) < data.AngleMin) {
					if (movementData.Angle < 0.0) {
						movementData.Angle = -data.AngleMin;
					} else {
						movementData.Angle = data.AngleMin;
					}
					if (!movementData.ForcedMove) {
						movementData.AngleSpeed = -movementData.AngleSpeed;
						movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementData.AngleSpeed < 0.0) {
						movementData.AngleSpeed = 0.0f;
					}
				}
			} else {
				var direction = movementData.HitDirection ? -1f : 1f;
				if (direction * movementData.Angle > data.AngleMax) {
					movementData.Angle = direction * data.AngleMax;

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, movementData.AngleSpeed));

					if (!movementData.ForcedMove) {
						movementData.AngleSpeed = -movementData.AngleSpeed;
						movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementData.AngleSpeed > 0.0) {
						movementData.AngleSpeed = 0.0f;
					}
				}
				if (direction * movementData.Angle < data.AngleMin) {
					movementData.Angle = direction * data.AngleMin;

					// send Park event
					events.Enqueue(new EventData(EventId.LimitEventsBos, itemId, movementData.AngleSpeed));

					if (!movementData.ForcedMove) {
						movementData.AngleSpeed = -movementData.AngleSpeed;
						movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementData.AngleSpeed < 0.0) {
						movementData.AngleSpeed = 0.0f;
					}
				}
			}

			movementData.Angle += movementData.AngleSpeed * dTime;

			if (movementData.IsLifting) {
				if (math.abs(movementData.Angle - movementData.LiftAngle) > 0.000001f) {
					var direction = movementData.Angle < movementData.LiftAngle ? 1f : -1f;
					movementData.Angle += direction * (movementData.LiftSpeed * dTime);

				} else {
					movementData.IsLifting = false;
				}
			}
		}
	}
}
