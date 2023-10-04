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
		internal static void Update(ref BumperSkirtAnimationData data, float dTime)
		{
			// todo visibility - skip if invisible

			var isHit = data.HitEvent;
			data.HitEvent = false;

			if (data.EnableAnimation) {
				if (isHit) {
					data.DoAnimate = true;
					UpdateSkirt(ref data);
					data.AnimationCounter = 0.0f;
				}
				if (data.DoAnimate) {
					data.AnimationCounter += dTime;
					if (data.AnimationCounter > 160.0f) {
						data.DoAnimate = false;
						ResetSkirt(ref data);
					}
				}
			} else if (data.DoUpdate) { // do a single update if the animation was turned off via script
				data.DoUpdate = false;
				ResetSkirt(ref data);
			}
		}

		private static void UpdateSkirt(ref BumperSkirtAnimationData data)
		{
			const float skirtTilt = 5.0f;

			var hitX = data.BallPosition.x;
			var hitY = data.BallPosition.y;
			var dy = math.abs(hitY - data.Center.y);
			if (dy == 0.0f) {
				dy = 0.000001f;
			}

			var dx = math.abs(hitX - data.Center.x);
			var skirtA = math.atan(dx / dy);
			data.Rotation.x = math.cos(skirtA) * skirtTilt;
			data.Rotation.y = math.sin(skirtA) * skirtTilt;
			if (data.Center.y < hitY) {
				data.Rotation.x = -data.Rotation.x;
			}

			if (data.Center.x > hitX) {
				data.Rotation.y = -data.Rotation.y;
			}
		}

		private static void ResetSkirt(ref BumperSkirtAnimationData data)
		{
			data.Rotation = new float2(0, 0);
		}
	}
}
