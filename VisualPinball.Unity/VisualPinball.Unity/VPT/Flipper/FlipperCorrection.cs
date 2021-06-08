// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using UnityEngine;

namespace VisualPinball.Unity
{
	internal static class FlipperCorrection
	{
		public static void OnBallLeaveFlipper(ref BallData ballData, ref FlipperCorrectionBlob flipperCorrectionBlob,
			in FlipperMovementData flipperMovementData, in FlipperStaticData flipperStaticData, uint timeMs)
		{
			ref var velocities = ref flipperCorrectionBlob.Velocities;
			ref var polarities = ref flipperCorrectionBlob.Polarities;
			var flipperAngleRad = flipperMovementData.Angle;
			var flipperStrength = flipperStaticData.Strength;
			var ballPosition = ballData.Position;
			var ballVelocity = ballData.Velocity;
			var timeSinceFlipperStartedRotatingToEndMs = timeMs - flipperMovementData.StartRotateToEndTime;

		}
	}
}
