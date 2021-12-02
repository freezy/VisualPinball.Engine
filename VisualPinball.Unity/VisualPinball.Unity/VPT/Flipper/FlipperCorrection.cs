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

using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	internal static class FlipperCorrection
	{
		public static void OnBallLeaveFlipper(ref BallData ballData, ref FlipperCorrectionBlob flipperCorrectionBlob,
			in FlipperMovementData flipperMovementData, in FlipperStaticData flipperStaticData, uint timeMs)
		{

			var timeSinceFlipperStartedRotatingToEndMs = timeMs - flipperMovementData.StartRotateToEndTime;

			// Time delay overrun test
			if (timeSinceFlipperStartedRotatingToEndMs > flipperCorrectionBlob.TimeDelayMs)
				return;

			ref var velocities = ref flipperCorrectionBlob.Velocities;
			ref var polarities = ref flipperCorrectionBlob.Polarities;
			var angleCur = flipperMovementData.Angle;

			var ballPosition = ballData.Position;
			var ballVelocity = ballData.Velocity;
			var uncorrectedVel = ballVelocity;
			if (ballVelocity.y > -8F) // ball going down
			{
				return;
			}

			var angleAtFire = flipperMovementData.AngleAtRotateToEnd;
			var angleStart = flipperStaticData.AngleStart;
			var angleEnd = flipperStaticData.AngleEnd;

			var flipPos = flipperStaticData.Position;

			var flipEnd = flipperStaticData.Position;
			flipEnd.x += math.sin(angleCur) * flipperStaticData.FlipperRadius;
			flipEnd.y += -math.cos(angleCur) * flipperStaticData.FlipperRadius;

			// Compute ball distance on Flipper (normalized from start to end)
			//var dir = flipEnd - flipPos;
			var ballPos = (ballPosition.x - flipPos.x) / (flipEnd.x - flipPos.x);

			// Safety coeffcient: has been disabled in all curves of nFozzy. Not using;
			float Ycoef = 1F;

			// Normalized flipper course since fire (can be > 1 if rebounding...)
			float partialFlipCoef = (angleStart - angleAtFire) / (angleStart - angleEnd);
			partialFlipCoef = math.abs(partialFlipCoef - 1F);

			// Velocity correction
			var velCoef = CurveUtils.LinearEnvelopeEven(ballPos, ref velocities);
			var velCoefInit = velCoef;
			if (partialFlipCoef < 1)
			{
				velCoef = CurveUtils.PSlope(partialFlipCoef, 0, 1, 1, velCoef);
			}
			ballVelocity *= velCoef;

			// Polarity Correction
			bool isLeft = angleEnd < angleStart; // TODO: better if not classic flippers (trigonometry problems)

			float AddX = CurveUtils.LinearEnvelopeEven(ballPos, ref polarities, 0F);
			if(!isLeft) {
				AddX = -AddX;
			}
			ballVelocity.x += AddX * Ycoef * partialFlipCoef;



			// Apply all corrections
			ballData.Velocity = ballVelocity;

			//ballData.IsFrozen = true;

#if UNITY_EDITOR
			Global.FlipperCorrectionDebug.flipPos = flipPos;
			Global.FlipperCorrectionDebug.flipEnd = flipEnd;
			Global.FlipperCorrectionDebug.endRadius = flipperStaticData.EndRadius;
			Global.FlipperCorrectionDebug.outPos = ballPosition;
			Global.FlipperCorrectionDebug.uncorrectedVel = uncorrectedVel;
			Global.FlipperCorrectionDebug.outVel = ballVelocity;
			Global.FlipperCorrectionDebug.ballPos = ballPos;
			Global.FlipperCorrectionDebug.justOut = true;
			DebugInfo("Normalized angle:" + partialFlipCoef + " Velocity coef:" + velCoefInit + " => " + velCoef + " Polarity correction:" + AddX + " Time Since Power:" + timeSinceFlipperStartedRotatingToEndMs, true);
#endif
		}

		[BurstDiscard]
		public static void DebugInfo(string message = "", bool aditionnal = false)
		{
			if (message != "")
				Debug.Log("<b> <size=13> <color=#9DF155>Debug : " + message + "</color> </size> </b>");
			if (aditionnal)
			{
				Debug.Log("<b> <size=13> <color=#9DF155>Debug : OnBallLeaveFlipper.</color> </size> </b>" + Global.FlipperCorrectionDebug.outPos + " " + Global.FlipperCorrectionDebug.outVel);
				Debug.Log("<b> <size=13> <color=#9DF155>Debug : BallPos :" + Global.FlipperCorrectionDebug.ballPos + "</color> </size> </b>");
			}
		}
	}
}
