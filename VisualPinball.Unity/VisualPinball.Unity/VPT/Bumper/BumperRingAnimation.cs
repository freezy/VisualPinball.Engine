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

namespace VisualPinball.Unity
{
	internal static class BumperRingAnimation
	{
		internal static void Update(ref BumperRingAnimationState state, float dTimeMs)
		{
			// todo visibility - skip if invisible

			var limit = state.DropOffset + state.HeightScale * 0.5f;
			if (state.IsHit) {
				state.DoAnimate = true;
				state.AnimateDown = true;
				state.IsHit = false;
			}
			if (state.DoAnimate) {
				var step = state.Speed;
				if (state.AnimateDown) {
					step = -step;
				}
				state.Offset += step * dTimeMs;
				if (state.AnimateDown) {
					if (state.Offset <= -limit) {
						state.Offset = -limit;
						state.AnimateDown = false;
					}
				} else {
					if (state.Offset >= 0.0f) {
						state.Offset = 0.0f;
						state.DoAnimate = false;
					}
				}
			}
		}
	}
}
