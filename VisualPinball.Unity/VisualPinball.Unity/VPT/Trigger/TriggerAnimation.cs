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

using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	internal static class TriggerAnimation
	{
		internal static void Update(ref TriggerAnimationState animation, ref TriggerMovementState movement, in TriggerStaticState staticState,
			float dTimeMs)
		{
			// var oldTimeMsec = animation.TimeMsec < dTimeMs ? animation.TimeMsec : dTimeMs;
			// animation.TimeMsec = dTimeMs;
			// var diffTimeMsec = dTimeMs - oldTimeMsec;

			var animLimit = staticState.Shape == TriggerShape.TriggerStar ? staticState.Radius * (float)(1.0 / 5.0) : 32.0f;
			if (staticState.Shape == TriggerShape.TriggerButton) {
				animLimit = staticState.Radius * (float)(1.0 / 10.0);
			}
			if (staticState.Shape == TriggerShape.TriggerWireC) {
				animLimit = 60.0f;
			}
			if (staticState.Shape == TriggerShape.TriggerWireD) {
				animLimit = 25.0f;
			}

			var limit = animLimit * staticState.TableScaleZ;

			if (animation.HitEvent) {
				animation.DoAnimation = true;
				animation.HitEvent = false;
				// unhitEvent = false;   // Bugfix: If HitEvent and unhitEvent happen at the same time, you want to favor the unhit, otherwise the switch gets stuck down.
				movement.HeightOffset = 0.0f;
				animation.MoveDown = true;
			}
			if (animation.UnHitEvent) {
				animation.DoAnimation = true;
				animation.UnHitEvent = false;
				animation.HitEvent = false;
				//movement.HeightOffset = limit;
				animation.MoveDown = false;
			}

			if (animation.DoAnimation) {
				var step = dTimeMs * staticState.AnimSpeed * staticState.TableScaleZ;
				if (animation.MoveDown) {
					step = -step;
				}
				movement.HeightOffset += step;

				if (animation.MoveDown) {
					if (movement.HeightOffset <= -limit) {
						movement.HeightOffset = -limit;
						animation.DoAnimation = false;
						animation.MoveDown = false;
					}

				} else {
					if (movement.HeightOffset >= 0.0f) {
						movement.HeightOffset = 0.0f;
						animation.DoAnimation = false;
						animation.MoveDown = true;
					}
				}
			}
		}
	}
}
