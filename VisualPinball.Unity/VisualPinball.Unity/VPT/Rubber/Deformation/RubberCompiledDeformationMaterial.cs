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
	/// Result of the editor-side deformation bake of a Physical rubber.
	/// </summary>
	/// <remarks>
	/// The enum order is fixed by the plan. <see cref="Deformable"/> is the zero
	/// value, so every serialized default and every reset explicitly writes
	/// <see cref="Invalid"/> instead of relying on default initialization.
	/// </remarks>
	public enum RubberDeformationBakeOutcome
	{
		/// <summary>Free spans are eligible for the deformable cord runtime.</summary>
		Deformable = 0,

		/// <summary>
		/// The authoring state is legitimate but not deformable; the rubber keeps
		/// static Physical capsules built from the baked backed descriptors.
		/// </summary>
		StaticFallback = 1,

		/// <summary>
		/// Material or geometry outside the model domain, or no bake at all. The
		/// rubber falls back to Legacy collision.
		/// </summary>
		Invalid = 2,
	}

	/// <summary>
	/// The SI deformation material asset plus the rubber's cord thickness, compiled
	/// into the scalars and lookup tables the deformation model consumes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Everything here is in VPE physics units (see the plan's section 2.2), so the
	/// runtime never converts:
	/// </para>
	/// <list type="bullet">
	/// <item><description>length: VPX units, <c>S = 1852.71</c> per meter</description></item>
	/// <item><description>time: <c>t0 = 0.01</c> s per unit</description></item>
	/// <item><description>mass: <c>m0 = 0.08</c> kg per unit</description></item>
	/// </list>
	/// <para>
	/// The two tables hold <see cref="LutSampleCount"/> samples uniformly spaced in
	/// stretch ratio over <c>[1, MaximumStretchRatio]</c>, inclusive at both ends, and
	/// are read by linear interpolation between the bracketing samples.
	/// </para>
	/// </remarks>
	[Serializable]
	public struct RubberCompiledDeformationMaterial
	{
		/// <summary>Number of samples in <see cref="StressLut"/> and <see cref="TangentStiffnessLut"/>.</summary>
		public const int LutSampleCount = 128;

		/// <summary>Version of the compiler that produced this record.</summary>
		public int CompilerVersion;

		/// <summary>
		/// Unloaded linear density <c>mu0 = rho * A0</c>, in VPE mass units per VPX unit.
		/// </summary>
		public float LinearDensity;

		/// <summary>Modal damping ratio, dimensionless, strictly between zero and one.</summary>
		public float DampingRatio;

		/// <summary>Coulomb friction coefficient between the ball and the cord surface.</summary>
		public float SurfaceFriction;

		/// <summary>Largest stretch ratio the material is defined for.</summary>
		public float MaximumStretchRatio;

		/// <summary>
		/// Nominal stress over stretch ratio, in VPE force units per square VPX unit.
		/// </summary>
		public float[] StressLut;

		/// <summary>
		/// Derivative of the nominal stress with respect to the stretch ratio, in the
		/// same units as <see cref="StressLut"/>.
		/// </summary>
		public float[] TangentStiffnessLut;

		/// <summary>
		/// True when both tables are populated and every scalar is inside its domain.
		/// </summary>
		public bool IsPopulated =>
			StressLut != null && StressLut.Length == LutSampleCount
			&& TangentStiffnessLut != null && TangentStiffnessLut.Length == LutSampleCount
			&& float.IsFinite(LinearDensity) && LinearDensity > 0f
			&& float.IsFinite(DampingRatio) && DampingRatio > 0f && DampingRatio < 1f
			&& float.IsFinite(MaximumStretchRatio) && MaximumStretchRatio > 1f;

		/// <summary>
		/// Reads the nominal stress at a stretch ratio by linear interpolation between
		/// the bracketing table samples. The ratio is clamped to the table domain.
		/// </summary>
		public float SampleStress(float stretchRatio) => Sample(StressLut, stretchRatio);

		/// <summary>
		/// Reads the tangent stiffness at a stretch ratio by linear interpolation
		/// between the bracketing table samples. The ratio is clamped to the domain.
		/// </summary>
		public float SampleTangentStiffness(float stretchRatio)
			=> Sample(TangentStiffnessLut, stretchRatio);

		/// <summary>
		/// The stretch ratio of the table sample at <paramref name="index"/>.
		/// </summary>
		public float StretchRatioAt(int index)
		{
			var maximum = math.max(1f, MaximumStretchRatio);
			return math.lerp(1f, maximum, index / (LutSampleCount - 1f));
		}

		private float Sample(float[] lut, float stretchRatio)
		{
			if (lut == null || lut.Length == 0) {
				return 0f;
			}
			if (lut.Length == 1 || !float.IsFinite(stretchRatio)) {
				return lut[0];
			}
			var maximum = math.max(1f + 1e-6f, MaximumStretchRatio);
			var position = math.saturate((stretchRatio - 1f) / (maximum - 1f)) * (lut.Length - 1);
			var lower = (int)math.floor(position);
			if (lower >= lut.Length - 1) {
				return lut[lut.Length - 1];
			}
			return math.lerp(lut[lower], lut[lower + 1], position - lower);
		}
	}
}
