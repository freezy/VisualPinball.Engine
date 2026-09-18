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

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// Section 2.2 of the deformable-rubber plan: the SI &lt;-&gt; VPE unit contract.
	///
	/// <para>
	/// VPE physics runs in VPX length units, in units of the default step time, and in
	/// units of one 80 g ball. Every conversion in this class is derived from those three
	/// scales alone, so a mistake anywhere shows up as a broken free-fall or impulse test.
	/// </para>
	///
	/// <para>
	/// All fixture math in this folder is <c>double</c>. The production code will be
	/// <c>float</c>; the fixtures are the reference the production code is measured against,
	/// so they are deliberately a precision step above it. A separate test pins that a
	/// float evaluation of the closed form still matches the double reference.
	/// </para>
	/// </summary>
	internal static class RubberDeformationUnits
	{
		/// <summary>VPX units per metre — <c>Physics.ScaleToVpx(1f)</c>.</summary>
		internal const double VpxPerMetre = 1852.71;

		/// <summary>Seconds per VPE time unit — <c>PhysicsConstants.DefaultStepTimeS</c>.</summary>
		internal const double SecondsPerTimeUnit = 0.01;

		/// <summary>Kilograms per VPE mass unit — <c>BallComponent.Mass = 1</c> is one 80 g ball.</summary>
		internal const double KilogramsPerMassUnit = 0.080;

		/// <summary>
		/// One physics substep of the engine's 1 kHz tick, expressed in VPE time units:
		/// <c>PhysicsConstants.PhysFactor</c> = PhysicsStepTimeS / DefaultStepTimeS = 0.1.
		/// </summary>
		internal const double TickTime = 0.1;

		/// <summary>Standard gravity, SI.</summary>
		internal const double StandardGravitySi = 9.81;

		#region Scalar conversions

		internal static double MassToVpe(double kilograms) => kilograms / KilogramsPerMassUnit;
		internal static double MassToSi(double vpe) => vpe * KilogramsPerMassUnit;

		internal static double LengthToVpe(double metres) => metres * VpxPerMetre;
		internal static double LengthToSi(double vpe) => vpe / VpxPerMetre;

		internal static double VelocityToVpe(double metresPerSecond)
			=> metresPerSecond * VpxPerMetre * SecondsPerTimeUnit;

		internal static double VelocityToSi(double vpe)
			=> vpe / (VpxPerMetre * SecondsPerTimeUnit);

		internal static double AccelerationToVpe(double metresPerSecondSquared)
			=> metresPerSecondSquared * VpxPerMetre * SecondsPerTimeUnit * SecondsPerTimeUnit;

		internal static double AccelerationToSi(double vpe)
			=> vpe / (VpxPerMetre * SecondsPerTimeUnit * SecondsPerTimeUnit);

		internal static double ForceToVpe(double newtons)
			=> newtons * VpxPerMetre * SecondsPerTimeUnit * SecondsPerTimeUnit / KilogramsPerMassUnit;

		internal static double ForceToSi(double vpe)
			=> vpe * KilogramsPerMassUnit / (VpxPerMetre * SecondsPerTimeUnit * SecondsPerTimeUnit);

		/// <summary>
		/// A modal stiffness is a force per generalized coordinate, and the generalized
		/// coordinate is a length, so the VPX scale cancels: K_vpe = K_si * t0^2 / m0.
		/// </summary>
		internal static double StiffnessToVpe(double newtonsPerMetre)
			=> newtonsPerMetre * SecondsPerTimeUnit * SecondsPerTimeUnit / KilogramsPerMassUnit;

		internal static double StiffnessToSi(double vpe)
			=> vpe * KilogramsPerMassUnit / (SecondsPerTimeUnit * SecondsPerTimeUnit);

		internal static double DampingToVpe(double newtonSecondsPerMetre)
			=> newtonSecondsPerMetre * SecondsPerTimeUnit / KilogramsPerMassUnit;

		internal static double DampingToSi(double vpe)
			=> vpe * KilogramsPerMassUnit / SecondsPerTimeUnit;

		#endregion

		/// <summary>
		/// Gravity in VPE units, straight from the SI constant. Must reproduce
		/// <c>PhysicsConstants.GravityConst</c> (1.81751) to the printed precision.
		/// </summary>
		internal static double GravityVpe => AccelerationToVpe(StandardGravitySi);

		/// <summary>Cross-section area of a cord of the given VPX thickness (diameter), in m^2.</summary>
		internal static double CordAreaSquareMetres(double thicknessVpx)
		{
			var radius = LengthToSi(thicknessVpx) * 0.5;
			return Math.PI * radius * radius;
		}
	}
}
