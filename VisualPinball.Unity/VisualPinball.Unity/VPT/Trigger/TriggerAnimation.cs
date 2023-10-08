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
		internal static void Update(ref TriggerAnimationData data, ref TriggerMovementData movementData, in TriggerStaticData staticData,
			float dTimeMs)
		{
			var oldTimeMsec = data.TimeMsec < dTimeMs ? data.TimeMsec : dTimeMs;
			data.TimeMsec = dTimeMs;
			var diffTimeMsec = dTimeMs - oldTimeMsec;

			var animLimit = staticData.Shape == TriggerShape.TriggerStar ? staticData.Radius * (float)(1.0 / 5.0) : 32.0f;
			if (staticData.Shape == TriggerShape.TriggerButton) {
				animLimit = staticData.Radius * (float)(1.0 / 10.0);
			}
			if (staticData.Shape == TriggerShape.TriggerWireC) {
				animLimit = 60.0f;
			}
			if (staticData.Shape == TriggerShape.TriggerWireD) {
				animLimit = 25.0f;
			}

			var limit = animLimit * staticData.TableScaleZ;

			if (data.HitEvent) {
				data.DoAnimation = true;
				data.HitEvent = false;
				// unhitEvent = false;   // Bugfix: If HitEvent and unhitEvent happen at the same time, you want to favor the unhit, otherwise the switch gets stuck down.
				movementData.HeightOffset = 0.0f;
				data.MoveDown = true;
			}
			if (data.UnHitEvent) {
				data.DoAnimation = true;
				data.UnHitEvent = false;
				data.HitEvent = false;
				movementData.HeightOffset = limit;
				data.MoveDown = false;
			}

			if (data.DoAnimation) {
				var step = diffTimeMsec * staticData.AnimSpeed * staticData.TableScaleZ;
				if (data.MoveDown) {
					step = -step;
				}
				movementData.HeightOffset += step;

				if (data.MoveDown) {
					if (movementData.HeightOffset <= -limit) {
						movementData.HeightOffset = -limit;
						data.DoAnimation = false;
						data.MoveDown = false;
					}

				} else {
					if (movementData.HeightOffset >= 0.0f) {
						movementData.HeightOffset = 0.0f;
						data.DoAnimation = false;
						data.MoveDown = true;
					}
				}
			}
		}
	}
}
