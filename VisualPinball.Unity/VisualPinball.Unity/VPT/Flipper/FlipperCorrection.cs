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
	internal static class FlipperCorrection
	{
		public static void OnBallLeaveFlipper(ref BallState ballState, ref FlipperCorrectionState flipperCorrectionState,
			in FlipperMovementState flipperMovementState, in FlipperTricksData tricks, float3 flipperPos,
			in FlipperStaticData flipperStaticData, uint timeMs)
		{

			var timeSinceFlipperStartedRotatingToEndMs = timeMs - flipperMovementState.StartRotateToEndTime;

			// Time delay overrun test
			if (timeSinceFlipperStartedRotatingToEndMs > flipperCorrectionState.TimeDelayMs)
				return;

			var angleCur = flipperMovementState.Angle;

			var ballPosition = ballState.Position;
			var ballVelocity = ballState.Velocity;
			var uncorrectedVel = ballVelocity;
			if (ballVelocity.y > -8F) // ball going down
			{
				return;
			}

			var angleAtFire = flipperMovementState.AngleAtRotateToEnd;
			var angleStart = flipperStaticData.AngleStart;
			var angleEnd = tricks.AngleEnd;

			var flipPos = flipperPos;

			var flipEnd = flipperPos;
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
			var velCoef = LinearEnvelopeEven(ballPos, flipperCorrectionState.Velocities);
			var velCoefInit = velCoef;
			if (partialFlipCoef < 1)
			{
				velCoef = PSlope(partialFlipCoef, 0, 1, 1, velCoef);
			}
			ballVelocity *= velCoef;

			// Polarity Correction
			bool isLeft = angleEnd < angleStart; // TODO: better if not classic flippers (trigonometry problems)

			float AddX = LinearEnvelopeEven(ballPos, flipperCorrectionState.Polarities, 0F);
			if(!isLeft) {
				AddX = -AddX;
			}
			ballVelocity.x += AddX * Ycoef * partialFlipCoef;



			// Apply all corrections
			ballState.Velocity = ballVelocity;
		}

		// awful linear interpolations : TODO: replace by AnimationCurve equivalent...
		private static float PSlope(float x, float x1, float y1, float x2, float y2)  //Set up line via two points, no clamping. Input X, output Y
		{
			float m = (y2 - y1) / (y2 - x1);
			float b = y2 - m * y2;
			return m * x + b;
		}

		private static float LinearEnvelope(float xInput, ref UnmanagedArray<float2> curve, float defaultValue = 1F)
		{
			if (curve.Length <= 0)
				return defaultValue;

			if (xInput <= curve[0].x) //Clamp lower
				return curve[0].y;
			if (xInput >= curve[curve.Length - 1].x) //Clamp upper
				return curve[curve.Length - 1].y;

			int L = -1;
			for (int ii = 1; ii < curve.Length; ii++)    //find active line
				if (xInput <= curve[ii].x)
				{
					L = ii;
					break;
				}

			if (L < 0)
				return defaultValue;

			if (xInput > curve[curve.Length - 1].x) // catch line overrun
				L = curve.Length - 1;

			float y = PSlope(xInput, curve[L - 1].x, curve[L - 1].y, curve[L].x, curve[L - 1].y);

			return y;
		}

		// if segments are even on x axis, no need to iterate: faster
		private static float LinearEnvelopeEven(float xInput, UnmanagedArray<float2> curve, float defaultValue = 1F)
		{
			if (curve.Length <= 0)
				return defaultValue;

			if (xInput <= curve[0].x) //Clamp lower
				return curve[0].y;
			if (xInput >= curve[curve.Length - 1].x) //Clamp upper
				return curve[curve.Length - 1].y;

			int L = (int)((xInput-curve[0].x) * (curve.Length-1) / (curve[curve.Length - 1].x - curve[0].x));

			float y = PSlope(xInput, curve[L - 1].x, curve[L - 1].y, curve[L].x, curve[L - 1].y);

			return y;
		}
	}
}
