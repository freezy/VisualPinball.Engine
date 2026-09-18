// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
	/// <summary>
	/// The two numbers <see cref="VisualPinball.Engine.Game.EventId.RubberSpanImpact"/>
	/// carries in <see cref="EventData.FloatParam2"/>: the apex fraction <c>u_h</c> and
	/// the signed effective depth <c>delta_eff</c>, packed as two half-precision floats.
	/// </summary>
	/// <remarks>
	/// The apex fraction lives in <c>[0, 1]</c> and the depth in a few VPX units, so
	/// half precision (about three decimal digits) is far more than the renderer and
	/// the switch zones need, and one float keeps the event struct blittable and small.
	/// The low 16 bits hold <c>u_h</c>, the high 16 bits <c>delta_eff</c>.
	/// </remarks>
	public static class RubberSpanImpactPayload
	{
		/// <summary>Packs the apex fraction and the signed effective depth into one float.</summary>
		public static float Pack(float apexFraction, float effectiveDepth)
			=> math.asfloat(math.f32tof16(apexFraction) | (math.f32tof16(effectiveDepth) << 16));

		/// <summary>Reads back what <see cref="Pack"/> wrote.</summary>
		public static void Unpack(float payload, out float apexFraction, out float effectiveDepth)
		{
			var bits = math.asuint(payload);
			apexFraction = math.f16tof32(bits & 0xffffu);
			effectiveDepth = math.f16tof32(bits >> 16);
		}
	}
}
