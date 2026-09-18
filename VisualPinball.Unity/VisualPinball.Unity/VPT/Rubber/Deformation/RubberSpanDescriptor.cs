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

using System;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// One baked free span of a Physical rubber, reduced to a pinned, damped
	/// transverse oscillator with up to four modes.
	/// </summary>
	/// <remarks>
	/// All quantities are in VPE physics units: lengths in VPX units, mass in units
	/// of 80 g, time in units of 10 ms. Phase 3b activates lane one only; lanes two
	/// to four are populated at bake time so phase 3c can raise the active mode
	/// count without a package migration.
	/// </remarks>
	[Serializable]
	public struct RubberSpanDescriptor
	{
		/// <summary>Index of the free-span element in <c>RubberComponent.BakedPath</c>.</summary>
		public int PathElementIndex;

		/// <summary>Guide binding index of the anchor at the start of the span.</summary>
		public int StartBindingIndex;

		/// <summary>Guide binding index of the anchor at the end of the span.</summary>
		public int EndBindingIndex;

		/// <summary>Installed arc length <c>L</c> in VPX units.</summary>
		public float InstalledLength;

		/// <summary>Unloaded arc length <c>L0 = L / lambda0</c> in VPX units.</summary>
		public float UnloadedLength;

		/// <summary>Installed tension <c>T = sigma(lambda0) * A0</c>, in VPE force units.</summary>
		public float InstalledTension;

		/// <summary>
		/// Axial tangent stiffness <c>dT/dlambda = A0 * dsigma/dlambda</c> at the
		/// installed stretch, in VPE force units. Unused in phase 3b; it is baked for
		/// the tension exchange of phase 3c.
		/// </summary>
		public float TangentStiffness;

		/// <summary>Modal mass per lane, <c>M_n = mu0 * L0 / 2</c>, equal for all lanes.</summary>
		public float4 ModalMass;

		/// <summary>Modal stiffness per lane, <c>K_n = T * (n * pi)^2 / (2 * L)</c>.</summary>
		public float4 ModalStiffness;

		/// <summary>Modal damping per lane, <c>C_n = 2 * zeta * sqrt(K_n * M_n)</c>.</summary>
		public float4 ModalDamping;

		/// <summary>
		/// Largest single-mode deflection in VPX units: the smaller of the rubber's
		/// deflection clearance and the deflection at which the span reaches the
		/// material's maximum stretch ratio.
		/// </summary>
		public float MaxDeflection;

		/// <summary>Coulomb friction coefficient of the cord surface.</summary>
		public float SurfaceFriction;

		/// <summary>
		/// Orientation of the positive deflection direction: <c>+1</c> when the loop
		/// interior is to the left of the rest tangent, <c>-1</c> when it is to the
		/// right. The inward direction at a point is
		/// <c>InwardSign * (-tangent.y, tangent.x)</c> in the bake plane.
		/// </summary>
		public sbyte InwardSign;

		/// <summary>Certified number of chain segments, a multiple of four from 8 to 64.</summary>
		public int SegmentCount;

		/// <summary>
		/// Curve parameters of the <see cref="SegmentCount"/> + 1 chain points, so that
		/// consecutive points are spaced uniformly in arc length.
		/// <c>ArcLengthFractions[k]</c> is the parameter <c>t</c> to pass to
		/// <c>RubberPathMath.PointAt</c> for the point whose arc-length fraction along
		/// the span is <c>k / SegmentCount</c>. The table is monotone from 0 to 1.
		/// </summary>
		public float[] ArcLengthFractions;

		/// <summary>Installed stretch ratio <c>lambda0 = L / L0</c> of this span.</summary>
		public float InstalledStretch
			=> UnloadedLength > 0f ? InstalledLength / UnloadedLength : 0f;
	}
}
