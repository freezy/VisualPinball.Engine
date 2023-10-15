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
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	internal static class DropTargetAnimation
	{
		internal static void Update(int itemId, ref DropTargetAnimationData data, in DropTargetStaticData staticData,
			ref PhysicsState state)
		{
			var oldTimeMsec = data.TimeMsec < state.Env.TimeMsec ? data.TimeMsec : state.Env.TimeMsec;
			data.TimeMsec = state.Env.TimeMsec;
			var diffTimeMsec = (float)(state.Env.TimeMsec - oldTimeMsec);

			if (data.HitEvent) {
				if (!data.IsDropped) {
					data.MoveDown = true;
				}

				data.MoveAnimation = true;
				data.HitEvent = false;
			}

			if (data.MoveAnimation) {
				var step = staticData.Speed;

				if (data.MoveDown) {
					step = -step;

				} else if (data.TimeMsec - data.TimeStamp < (uint) staticData.RaiseDelay) {
					step = 0.0f;
				}

				data.ZOffset += step * diffTimeMsec;
				if (data.MoveDown) {
					if (data.ZOffset <= -DropTargetAnimationData.DropTargetLimit) {
						data.ZOffset = -DropTargetAnimationData.DropTargetLimit;
						data.MoveDown = false;
						data.IsDropped = true;
						data.MoveAnimation = false;
						data.TimeStamp = 0;
						if (staticData.UseHitEvent) {
							state.EventQueue.Enqueue(new EventData(EventId.TargetEventsDropped, itemId));
						}
					}

				} else {
					if (data.ZOffset >= 0.0f) {
						data.ZOffset = 0.0f;
						data.MoveAnimation = false;
						data.IsDropped = false;
						if (staticData.UseHitEvent) {
							state.EventQueue.Enqueue(new EventData(EventId.TargetEventsRaised, itemId));
						}
					}
				}
			}
		}
	}
}
