﻿using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VisualPinball.Unity.Common
{
	public static class Math
	{
		public static float3 CrossZ(float rz, in float3 v)
		{
			return new float3(-rz * v.y, rz * v.x, 0);
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
				return elasticity / (1.0f + falloff * math.abs(vel) * (1.0f / 18.53f));
			}

			return elasticity;
		}

		public static bool SolveQuadraticEq(float a, float b, float c, out float u, out float v)
		{
			var discr = b * b - 4.0f * a * c;
			if (discr < 0) {
				u = -1;
				v = -1;
				return false;
			}
			discr = math.sqrt(discr);

			var invA = (-0.5f) / a;

			u = (b + discr) * invA;
			v = (b - discr) * invA;

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong NextMultipleOf16(ulong input) => ((input + 15) >> 4) << 4;
	}
}
