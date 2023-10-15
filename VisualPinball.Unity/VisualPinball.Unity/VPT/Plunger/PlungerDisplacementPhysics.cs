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
	internal static class PlungerDisplacementPhysics
	{
		internal static void UpdateDisplacement(int itemId, ref PlungerMovementState movement,
			ref PlungerColliderState colliderState, in PlungerStaticState staticState, float dTime,
			ref NativeQueue<EventData>.ParallelWriter events)
		{
			// figure the travel distance
			var dx = dTime * movement.Speed;

			// figure the position change
			movement.Position += dx;

			// apply the travel limit
			if (movement.Position < movement.TravelLimit) {
				movement.Position = movement.TravelLimit;
			}

			// if we're in firing mode and we've crossed the bounce position, reverse course
			var relPos = (movement.Position - staticState.FrameEnd) / staticState.FrameLen;
			var bouncePos = staticState.RestPosition + movement.FireBounce;
			if (movement.FireTimer != 0 && dTime != 0.0f &&
			    (movement.FireSpeed < 0.0f ? relPos <= bouncePos : relPos >= bouncePos))
			{
				// stop at the bounce position
				movement.Position = staticState.FrameEnd + bouncePos * staticState.FrameLen;

				// reverse course at reduced speed
				movement.FireSpeed = -movement.FireSpeed * 0.4f;

				// figure the new bounce as a fraction of the previous bounce
				movement.FireBounce *= -0.4f;
			}

			// apply the travel limit (again)
			if (movement.Position < movement.TravelLimit) {
				movement.Position = movement.TravelLimit;
			}

			// limit motion to the valid range
			if (dTime != 0.0f) {

				if (movement.Position < staticState.FrameEnd) {
					movement.Speed = 0.0f;
					movement.Position = staticState.FrameEnd;

				} else if (movement.Position > staticState.FrameStart) {
					movement.Speed = 0.0f;
					movement.Position = staticState.FrameStart;
				}

				// apply the travel limit (yet again)
				if (movement.Position < movement.TravelLimit) {
					movement.Position = movement.TravelLimit;
				}
			}

			// the travel limit applies to one displacement update only - reset it
			movement.TravelLimit = staticState.FrameEnd;

			// fire an Start/End of Stroke events, as appropriate
			var strokeEventLimit = staticState.FrameLen / 50.0f;
			var strokeEventHysteresis = strokeEventLimit * 2.0f;
			if (movement.StrokeEventsArmed && movement.Position + dx > staticState.FrameStart - strokeEventLimit) {
				events.Enqueue(new EventData(EventId.LimitEventsBos, itemId, math.abs(movement.Speed)));
				movement.StrokeEventsArmed = false;

			} else if (movement.StrokeEventsArmed && movement.Position + dx < staticState.FrameEnd + strokeEventLimit) {
				events.Enqueue(new EventData(EventId.LimitEventsEos, itemId, math.abs(movement.Speed)));
				movement.StrokeEventsArmed = false;

			} else if (movement.Position > staticState.FrameEnd + strokeEventHysteresis && movement.Position < staticState.FrameStart - strokeEventHysteresis) {
				// away from the limits - arm the stroke events
				movement.StrokeEventsArmed = true;
			}

			// update the display
			UpdateCollider(movement.Position, ref colliderState);
		}

		private static void UpdateCollider(float len, ref PlungerColliderState colliderState)
		{
			colliderState.LineSegSide0.V1y = len;
			colliderState.LineSegSide1.V2y = len;

			colliderState.LineSegEnd.V2y = len;
			colliderState.LineSegEnd.V1y = len; // + 0.0001f;

			colliderState.JointEnd0.XyY = len;
			colliderState.JointEnd1.XyY = len; // + 0.0001f;

			colliderState.LineSegSide0.CalcNormal();
			colliderState.LineSegSide1.CalcNormal();
			colliderState.LineSegEnd.CalcNormal();
		}
	}
}
