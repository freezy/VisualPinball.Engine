// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

// ReSharper disable CommentTypo

namespace VisualPinball.Engine.Common
{
	public static class Constants
	{
		public const int NewSoundFormatVersion = 1031;

		public const float FloatMin = 1.175494350822287507968736537222245677819e-038f;
		public const float FloatMax = 340282346638528859811704183484516925440f;

		public const float Gravity = 1.81751f;
	}

	public static class PhysicsConstants
	{
		public const float HitShapeDetailLevel = 7.0f;
		public const float ContactVel = 0.099f;                                // C_CONTACTVEL

		/// <summary>
		/// seems like this mimics the radius of the ball -> replace with radius where possible?
		/// </summary>
		public const float PhysSkin = 25.0f;

		/// <summary>
		/// Layer outside object which increases it's size for contact measurements. Used to determine clearances.
		/// Setting this value during testing to 0.1 will insure clearance. After testing set the value to 0.005
		/// Default 0.01
		/// </summary>
		public const float PhysTouch = 0.05f;                                  // PHYS_TOUCH

		/// <summary>
		/// usecs to go between each physics update
		/// </summary>
		public const int PhysicsStepTime = 1000;                               // PHYSICS_STEPTIME

		/// <summary>
		/// step time in seconds
		/// </summary>
		public const double PhysicsStepTimeS = PhysicsStepTime * 1e-6;         // PHYSICS_STEPTIME_S

		/// <summary>
		/// default physics rate: 1000Hz
		/// </summary>
		public const double DefaultStepTime = 10000;                           // DEFAULT_STEPTIME

		/// <summary>
		/// default physics rate: 1000Hz
		/// </summary>
		public const float DefaultStepTimeS = 0.01f;                           // DEFAULT_STEPTIME_S


		public const double PhysFactor = PhysicsStepTimeS / DefaultStepTimeS;  // PHYS_FACTOR


		public const float LowNormVel = 0.0001f;                               // C_LOWNORMVEL
		public const float Embedded = 0.0f;                                    // C_EMBEDDED
		public const float EmbedShot = 0.05f;                                  // C_EMBEDSHOT
		public const float DispGain = 0.9875f;                                 // C_DISP_GAIN
		public const float DispLimit = 5.0f;                                   // C_DISP_LIMIT

		/// <summary>
		/// test near zero conditions in linear, well behaved, conditions
		/// </summary>
		public const float Precision = 0.01f;                                  // C_PRECISION

		public const float EmbedVelLimit = 5.0f;                               // C_EMBEDVELLIMIT

		public const float DefaultTableMinSlope = 6.0f;                        // DEFAULT_TABLE_MIN_SLOPE
		public const float DefaultTableMaxSlope = 6.0f;                        // DEFAULT_TABLE_MAX_SLOPE
		public const float DefaultTableGravity = 0.97f;                        // DEFAULT_TABLE_GRAVITY

		/// <summary>
		/// trigger/kicker boundary crossing hysterisis
		/// </summary>
		public const float StaticTime = 0.005f;                                // STATICTIME
		public const float StaticCnts = 10f;                                   // STATICCNTS

		/// <summary>
		/// amount of msecs to wait (at least) until same timer can be
		/// triggered again (e.g. they can fall behind, if set to > 1, as
		/// update cycle is 1000Hz)
		/// </summary>
		public const int MaxTimerMsecInterval = 1;                             // MAX_TIMER_MSEC_INTERVAL

		/// <summary>
		/// Amount of msecs that all timers combined can take per frame (e.g.
		/// they can fall behind, if set to < somelargevalue)
		/// </summary>
		public const int MaxTimersMsecOverall = 5;                             // MAX_TIMERS_MSEC_OVERALL

		/// <summary>
		/// tolerance for line segment endpoint and point radii collisions
		/// </summary>
		public const float ToleranceEndPoints = 0.0f;                          // C_TOL_ENDPNTS
		public const float ToleranceRadius =  0.005f;                          // C_TOL_RADIUS

		/// <summary>
		/// Precision level and cycles for interative calculations // acceptable contact time ... near zero time
		/// </summary>
		public const int Internations = 20;                                    // C_INTERATIONS
	}
}
