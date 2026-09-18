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
	/// A cord stress curve, sigma(lambda), in pascals.
	/// </summary>
	internal interface IRubberStressCurve
	{
		double StressPascals(double stretch);
		double MaximumStretchRatio { get; }
	}

	/// <summary>
	/// The material asset's default curve: an <c>AnimationCurve</c> with keys (1, 0) and
	/// (2, 2) MPa and zero tangents, which is the cubic Hermite <c>3x^2 - 2x^3</c> scaled
	/// to 2 MPa, with <c>x = stretch - 1</c>. Section 12: 0.056 MPa at stretch 1.10.
	/// </summary>
	internal sealed class RubberDefaultStressCurve : IRubberStressCurve
	{
		internal const double PeakMegapascals = 2.0;

		public double MaximumStretchRatio => 2.0;

		public double StressPascals(double stretch)
		{
			var x = Math.Max(0.0, Math.Min(1.0, stretch - 1.0));
			return PeakMegapascals * 1e6 * (3.0 * x * x - 2.0 * x * x * x);
		}
	}

	/// <summary>
	/// Section 12's comparison material: a linear ramp to 2 MPa at stretch 2.
	/// </summary>
	internal sealed class RubberLinearRampStressCurve : IRubberStressCurve
	{
		internal const double PeakMegapascals = 2.0;

		public double MaximumStretchRatio => 2.0;

		public double StressPascals(double stretch)
		{
			var x = Math.Max(0.0, Math.Min(1.0, stretch - 1.0));
			return PeakMegapascals * 1e6 * x;
		}
	}

	/// <summary>
	/// The baker's compiled stress table (section 4.2): 128 uniformly spaced samples over
	/// stretch 1..MaximumStretchRatio, read back by linear interpolation between the
	/// bracketing samples. Sampling a curve and interpolating it is the only difference
	/// between the fixture's analytic coefficients and the baked ones, and section 7.1
	/// requires the two to agree within the table error of 4.2.
	/// </summary>
	internal sealed class RubberSampledStressCurve : IRubberStressCurve
	{
		internal const int DefaultSampleCount = 128;

		private readonly double[] _samples;
		private readonly double _min;
		private readonly double _max;

		public double MaximumStretchRatio => _max;

		internal int SampleCount => _samples.Length;

		internal RubberSampledStressCurve(IRubberStressCurve source, int sampleCount = DefaultSampleCount)
		{
			if (sampleCount < 2) {
				throw new ArgumentOutOfRangeException(nameof(sampleCount));
			}
			_min = 1.0;
			_max = source.MaximumStretchRatio;
			_samples = new double[sampleCount];
			for (var i = 0; i < sampleCount; i++) {
				var stretch = _min + (_max - _min) * i / (sampleCount - 1);
				_samples[i] = source.StressPascals(stretch);
			}
		}

		public double StressPascals(double stretch)
		{
			var t = (stretch - _min) / (_max - _min) * (_samples.Length - 1);
			if (t <= 0.0) {
				return _samples[0];
			}
			if (t >= _samples.Length - 1) {
				return _samples[_samples.Length - 1];
			}
			var i = (int)Math.Floor(t);
			var f = t - i;
			return _samples[i] * (1.0 - f) + _samples[i + 1] * f;
		}
	}

	/// <summary>
	/// Section 2.1 and 2.3: the reduced pinned-span oscillator coefficients of one free
	/// span, in both SI and VPE units.
	///
	/// <para>
	/// <c>M_n = mu0 * L0 / 2 = mu0 * L / (2 * lambda0)</c> is the mass-conserving form the
	/// plan corrects the brief to; it is independent of the mode index, which is what makes
	/// the point-load effective mass of an N-mode truncation fall like 1/N.
	/// </para>
	/// </summary>
	internal readonly struct RubberSpanCoefficients
	{
		// inputs
		internal readonly double ThicknessVpx;
		internal readonly double InstalledLengthVpx;
		internal readonly double DensityKgPerCubicMetre;
		internal readonly double InstalledStretch;
		internal readonly double DampingRatio;

		// SI
		internal readonly double AreaSquareMetres;
		internal readonly double InstalledLengthMetres;
		internal readonly double UnloadedLengthMetres;
		internal readonly double UnloadedLinearDensity;   // kg/m
		internal readonly double StressPascals;
		internal readonly double TensionNewtons;
		internal readonly double ModalMassKilograms;      // same for every mode

		// VPE
		internal readonly double ModalMassVpe;

		internal RubberSpanCoefficients(double thicknessVpx, double installedLengthVpx,
			double densityKgPerCubicMetre, double installedStretch, double dampingRatio,
			IRubberStressCurve curve)
		{
			ThicknessVpx = thicknessVpx;
			InstalledLengthVpx = installedLengthVpx;
			DensityKgPerCubicMetre = densityKgPerCubicMetre;
			InstalledStretch = installedStretch;
			DampingRatio = dampingRatio;

			AreaSquareMetres = RubberDeformationUnits.CordAreaSquareMetres(thicknessVpx);
			InstalledLengthMetres = RubberDeformationUnits.LengthToSi(installedLengthVpx);
			UnloadedLengthMetres = InstalledLengthMetres / installedStretch;
			UnloadedLinearDensity = densityKgPerCubicMetre * AreaSquareMetres;
			StressPascals = curve.StressPascals(installedStretch);
			TensionNewtons = StressPascals * AreaSquareMetres;
			ModalMassKilograms = UnloadedLinearDensity * UnloadedLengthMetres * 0.5;
			ModalMassVpe = RubberDeformationUnits.MassToVpe(ModalMassKilograms);
		}

		/// <summary>K_n = T * (n * pi)^2 / (2 * L), in N/m, with L the installed length.</summary>
		internal double ModalStiffnessSi(int mode)
		{
			var np = mode * Math.PI;
			return TensionNewtons * np * np / (2.0 * InstalledLengthMetres);
		}

		internal double ModalStiffnessVpe(int mode)
			=> RubberDeformationUnits.StiffnessToVpe(ModalStiffnessSi(mode));

		internal double ModalDampingSi(int mode)
			=> 2.0 * DampingRatio * Math.Sqrt(ModalStiffnessSi(mode) * ModalMassKilograms);

		internal double ModalDampingVpe(int mode)
			=> RubberDeformationUnits.DampingToVpe(ModalDampingSi(mode));

		/// <summary>Undamped natural frequency in rad/s.</summary>
		internal double OmegaSi(int mode) => Math.Sqrt(ModalStiffnessSi(mode) / ModalMassKilograms);

		/// <summary>Undamped natural frequency in rad per VPE time unit.</summary>
		internal double OmegaVpe(int mode) => OmegaSi(mode) * RubberDeformationUnits.SecondsPerTimeUnit;

		internal double FrequencyHz(int mode) => OmegaSi(mode) / (2.0 * Math.PI);

		internal RubberModalCoefficients Modal(int mode) => new RubberModalCoefficients(
			ModalMassVpe, ModalStiffnessVpe(mode), ModalDampingVpe(mode));

		/// <summary>Mode shape at arc-length fraction u.</summary>
		internal static double Shape(int mode, double u) => Math.Sin(mode * Math.PI * u);

		#region Section 12 worked span

		internal const double WorkedThicknessVpx = 8.0;
		internal const double WorkedInstalledLengthVpx = 150.0;
		internal const double WorkedDensity = 1100.0;
		internal const double WorkedStretch = 1.10;
		internal const double WorkedDampingRatio = 0.08;

		/// <summary>Section 3.3's default Deflection Clearance, in VPX units.</summary>
		internal const double DefaultDeflectionClearanceVpx = 60.0;

		internal static RubberSpanCoefficients Worked(IRubberStressCurve curve)
			=> new RubberSpanCoefficients(WorkedThicknessVpx, WorkedInstalledLengthVpx,
				WorkedDensity, WorkedStretch, WorkedDampingRatio, curve);

		internal static RubberSpanCoefficients WorkedDefaultCurve()
			=> Worked(new RubberDefaultStressCurve());

		internal static RubberSpanCoefficients WorkedLinearRamp()
			=> Worked(new RubberLinearRampStressCurve());

		#endregion
	}
}
