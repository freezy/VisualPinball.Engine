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

namespace VisualPinball.Unity
{
	internal static class GateDisplacementPhysics
	{
		internal static void UpdateDisplacement(int itemId, ref GateMovementState movementState, in GateStaticState state,
			float dTime, ref NativeQueue<EventData>.ParallelWriter events)
		{
			if (state.TwoWay) {
				if (math.abs(movementState.Angle) > state.AngleMax) {
					if (movementState.Angle < 0.0) {
						movementState.Angle = -state.AngleMax;
					} else {
						movementState.Angle = state.AngleMax;
					}

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, movementState.AngleSpeed));

					if (!movementState.ForcedMove) {
						movementState.AngleSpeed = -movementState.AngleSpeed;
						movementState.AngleSpeed *= state.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementState.AngleSpeed > 0.0) {
						movementState.AngleSpeed = 0.0f;
					}
				}
				if (math.abs(movementState.Angle) < state.AngleMin) {
					if (movementState.Angle < 0.0) {
						movementState.Angle = -state.AngleMin;
					} else {
						movementState.Angle = state.AngleMin;
					}
					if (!movementState.ForcedMove) {
						movementState.AngleSpeed = -movementState.AngleSpeed;
						movementState.AngleSpeed *= state.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementState.AngleSpeed < 0.0) {
						movementState.AngleSpeed = 0.0f;
					}
				}
			} else {
				var direction = movementState.HitDirection ? -1f : 1f;
				if (direction * movementState.Angle > state.AngleMax) {
					movementState.Angle = direction * state.AngleMax;

					// send EOS event
					events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, movementState.AngleSpeed));

					if (!movementState.ForcedMove) {
						movementState.AngleSpeed = -movementState.AngleSpeed;
						movementState.AngleSpeed *= state.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementState.AngleSpeed > 0.0) {
						movementState.AngleSpeed = 0.0f;
					}
				}
				if (direction * movementState.Angle < state.AngleMin) {
					movementState.Angle = direction * state.AngleMin;

					// send Park event
					events.Enqueue(new EventData(EventId.LimitEventsBos, itemId, movementState.AngleSpeed));

					if (!movementState.ForcedMove) {
						movementState.AngleSpeed = -movementState.AngleSpeed;
						movementState.AngleSpeed *= state.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
					} else if (movementState.AngleSpeed < 0.0) {
						movementState.AngleSpeed = 0.0f;
					}
				}
			}

			movementState.Angle += movementState.AngleSpeed * dTime;

			if (movementState.IsLifting) {
				if (math.abs(movementState.Angle - movementState.LiftAngle) > 0.000001f) {
					var direction = movementState.Angle < movementState.LiftAngle ? 1f : -1f;
					movementState.Angle += direction * (movementState.LiftSpeed * dTime);

				} else {
					movementState.IsLifting = false;
				}
			}
		}
	}
}
