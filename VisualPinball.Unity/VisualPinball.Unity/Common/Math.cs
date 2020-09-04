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

using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public static class Math
	{
		private static Random _random = new Random(6429845);

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

		/// <summary>
		/// Returns a random number between 0f and 1f.
		/// </summary>
		/// <returns></returns>
		public static float Random()
		{
			return (float) _random.NextDouble();
		}

		public static bool Sign(float f)
		{
			var floats = new NativeArray<float>(1, Allocator.Persistent) { [0] = f };
			var b = floats.Reinterpret<byte>(UnsafeUtility.SizeOf<float>());
			var sign = (b[3] & 0x80) == 0x80 && (b[2] & 0x00) == 0x00 && (b[1] & 0x00) == 0x00 && (b[0] & 0x00) == 0x00;
			floats.Dispose();
			return sign;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong NextMultipleOf16(ulong input) => ((input + 15) >> 4) << 4;
	}
}
