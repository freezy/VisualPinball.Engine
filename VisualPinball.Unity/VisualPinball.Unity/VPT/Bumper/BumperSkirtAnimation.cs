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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal static class BumperSkirtAnimation
	{
		internal static void Update(ref BumperSkirtAnimationState state, float dTimeMs)
		{
			// todo visibility - skip if invisible

			var isHit = state.HitEvent;
			state.HitEvent = false;

			if (state.EnableAnimation) {
				if (isHit) {
					state.DoAnimate = true;
					UpdateSkirt(ref state);
					state.AnimationCounter = 0.0f;
				}
				if (state.DoAnimate) {
					state.AnimationCounter += dTimeMs;
					if (state.AnimationCounter > state.Duration * 1000) {
						state.DoAnimate = false;
						ResetSkirt(ref state);
					}
				}
			} else if (state.DoUpdate) { // do a single update if the animation was turned off via script
				state.DoUpdate = false;
				ResetSkirt(ref state);
			}
		}

		private static void UpdateSkirt(ref BumperSkirtAnimationState state)
		{
			const float skirtTilt = 5.0f;

			var hitX = state.BallPosition.x;
			var hitY = state.BallPosition.y;
			var dy = math.abs(hitY - state.Center.y);
			if (dy == 0.0f) {
				dy = 0.000001f;
			}

			var dx = math.abs(hitX - state.Center.x);
			var skirtA = math.atan(dx / dy);
			state.Rotation.x = math.cos(skirtA) * skirtTilt;
			state.Rotation.y = math.sin(skirtA) * skirtTilt;
			if (state.Center.y < hitY) {
				state.Rotation.x = -state.Rotation.x;
			}

			if (state.Center.x > hitX) {
				state.Rotation.y = -state.Rotation.y;
			}
		}

		private static void ResetSkirt(ref BumperSkirtAnimationState state)
		{
			state.Rotation = new float2(0, 0);
		}
	}
}
