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

using System;

namespace VisualPinball.Engine.Math
{
	public static class Functions
	{
		public static Tuple<float, float> SolveQuadraticEq(float a, float b, float c)
		{
			var discr = b * b - 4.0f * a * c;
			if (discr < 0) {
				return null;
			}
			discr = MathF.Sqrt(discr);

			var invA = -0.5f / a;

			return new Tuple<float, float>(
				(b + discr) * invA,
				(b - discr) * invA
			);
		}

		public static float Clamp(float x, float min, float max)
		{
			if (x < min) {
				return min;
			}
			return x > max ? max : x;
		}

		/// <summary>
		/// Rubber has a coefficient of restitution which decreases with the impact velocity.
		/// We use a heuristic model which decreases the COR according to a falloff parameter:
		/// 	0 = no falloff, 1 = half the COR at 1 m/s (18.53 speed units)
		/// </summary>
		/// <param name="elasticity"></param>
		/// <param name="falloff"></param>
		/// <param name="vel"></param>
		/// <returns></returns>
		public static float ElasticityWithFalloff(float elasticity, float falloff, float vel)
		{
			if (falloff > 0) {
				return elasticity / (1.0f + falloff * MathF.Abs(vel) * (1.0f / 18.53f));
			}

			return elasticity;
		}

		/// <summary>
		/// Returns number of seconds.
		/// </summary>
		/// <returns></returns>
		public static double Now()
		{
			var ticks = System.Diagnostics.Stopwatch.GetTimestamp();
			return ticks / (double)TimeSpan.TicksPerSecond;
		}

		public static long NowUsec()
		{
			var ticks = System.Diagnostics.Stopwatch.GetTimestamp();
			return (long)(ticks / (TimeSpan.TicksPerMillisecond / 1000d));
		}
	}
}
