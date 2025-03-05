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

using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class DropTargetAnimation
	{
		internal static void Update(int itemId, ref DropTargetAnimationState animation, in DropTargetStaticState staticState, ref PhysicsState state)
		{
			var oldTimeMsec = animation.TimeMsec < state.Env.TimeMsec ? animation.TimeMsec : state.Env.TimeMsec;
			animation.TimeMsec = state.Env.TimeMsec;
			var diffTimeMsec = (float)(state.Env.TimeMsec - oldTimeMsec);

			if (animation.HitEvent) {
				if (!animation.IsDropped) {
					animation.MoveDown = true;
				}

				animation.MoveAnimation = true;
				animation.HitEvent = false;
			}

			if (animation.MoveAnimation) {
				var step = staticState.Speed;

				if (animation.MoveDown) {
					step = -step;

				} else if (animation.TimeMsec - animation.TimeStamp < (uint) staticState.RaiseDelay) {
					step = 0.0f;
				}

				animation.ZOffset += step * diffTimeMsec;
				if (animation.MoveDown) {
					if (animation.ZOffset <= -animation.DropDistance) {
						animation.ZOffset = -animation.DropDistance;
						animation.MoveDown = false;
						animation.IsDropped = true;
						animation.MoveAnimation = false;
						animation.TimeStamp = 0;
						if (staticState.UseHitEvent) {
							state.EventQueue.Enqueue(new EventData(EventId.TargetEventsDropped, itemId));
						}
						state.DisableColliders(itemId);
					}

				} else {
					if (animation.ZOffset >= 0.0f) {
						animation.ZOffset = 0.0f;
						animation.MoveAnimation = false;
						animation.IsDropped = false;
						if (staticState.UseHitEvent) {
							state.EventQueue.Enqueue(new EventData(EventId.TargetEventsRaised, itemId));
						}
						state.EnableColliders(itemId);
					}
				}
			}
		}
	}
}
