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
using NUnit.Framework;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// Section 2.2 and section 12 of the deformable-rubber plan: the unit contract and the
	/// worked span's coefficients, analytic and table-derived.
	/// </summary>
	public class RubberDeformationUnitsTests
	{
		private const double MegaPascal = 1e6;

		[Test]
		public void ShouldMatchTheEngineLengthScale()
		{
			// the engine keeps the scale as a float, so it only carries seven digits
			Assert.That(RubberDeformationUnits.VpxPerMetre, Is.EqualTo((double)Physics.ScaleToVpx(1f)).Within(1e-3));
			Assert.That(RubberDeformationUnits.SecondsPerTimeUnit,
				Is.EqualTo((double)PhysicsConstants.DefaultStepTimeS).Within(1e-9));
			Assert.That(RubberDeformationUnits.TickTime,
				Is.EqualTo((double)PhysicsConstants.PhysFactor).Within(1e-7));
			// MToVpu is the same scale expressed from the VP source constants
			Assert.That(RubberDeformationUnits.VpxPerMetre,
				Is.EqualTo((double)PhysicsConstants.MToVpu).Within(1e-3));
		}

		[Test]
		public void ShouldRoundTripEveryConversion()
		{
			foreach (var v in new[] { 1e-6, 0.25, 1.0, 137.0, 98765.0 }) {
				Assert.That(RubberDeformationUnits.MassToSi(RubberDeformationUnits.MassToVpe(v)), Is.EqualTo(v).Within(1e-12 * v));
				Assert.That(RubberDeformationUnits.LengthToSi(RubberDeformationUnits.LengthToVpe(v)), Is.EqualTo(v).Within(1e-12 * v));
				Assert.That(RubberDeformationUnits.VelocityToSi(RubberDeformationUnits.VelocityToVpe(v)), Is.EqualTo(v).Within(1e-12 * v));
				Assert.That(RubberDeformationUnits.AccelerationToSi(RubberDeformationUnits.AccelerationToVpe(v)), Is.EqualTo(v).Within(1e-12 * v));
				Assert.That(RubberDeformationUnits.ForceToSi(RubberDeformationUnits.ForceToVpe(v)), Is.EqualTo(v).Within(1e-12 * v));
				Assert.That(RubberDeformationUnits.StiffnessToSi(RubberDeformationUnits.StiffnessToVpe(v)), Is.EqualTo(v).Within(1e-12 * v));
				Assert.That(RubberDeformationUnits.DampingToSi(RubberDeformationUnits.DampingToVpe(v)), Is.EqualTo(v).Within(1e-12 * v));
			}
		}

		[Test]
		public void ShouldKeepNewtonsSecondLawConsistentAcrossUnits()
		{
			// F = m a, K x = F, C v = F must all hold after conversion
			const double massKg = 0.37;
			const double accelSi = 4.2;
			var forceSi = massKg * accelSi;
			Assert.That(RubberDeformationUnits.MassToVpe(massKg) * RubberDeformationUnits.AccelerationToVpe(accelSi),
				Is.EqualTo(RubberDeformationUnits.ForceToVpe(forceSi)).Within(1e-12 * RubberDeformationUnits.ForceToVpe(forceSi)));

			const double stiffnessSi = 53.1;
			const double displacementM = 0.011;
			var springForce = stiffnessSi * displacementM;
			Assert.That(RubberDeformationUnits.StiffnessToVpe(stiffnessSi) * RubberDeformationUnits.LengthToVpe(displacementM),
				Is.EqualTo(RubberDeformationUnits.ForceToVpe(springForce)).Within(1e-9 * RubberDeformationUnits.ForceToVpe(springForce)));

			const double dampingSi = 0.031;
			const double speedSi = 2.7;
			var damperForce = dampingSi * speedSi;
			Assert.That(RubberDeformationUnits.DampingToVpe(dampingSi) * RubberDeformationUnits.VelocityToVpe(speedSi),
				Is.EqualTo(RubberDeformationUnits.ForceToVpe(damperForce)).Within(1e-9 * RubberDeformationUnits.ForceToVpe(damperForce)));
		}

		[Test]
		public void ShouldReproduceTheEngineGravityConstant()
		{
			var gravity = RubberDeformationUnits.GravityVpe;
			Assert.That(gravity, Is.EqualTo((double)Constants.Gravity).Within(5e-6),
				$"9.81 m/s^2 converts to {gravity:F6} VPE units, the engine constant is {Constants.Gravity}");
			Assert.That(gravity, Is.EqualTo((double)PhysicsConstants.GravityConst).Within(5e-6));
		}

		/// <summary>
		/// Section 2.2's free-fall check: drop a ball with the engine's own gravity constant
		/// and its own kick-then-drift tick order, and compare against the closed form in SI.
		/// </summary>
		[Test]
		public void ShouldFreeFallAtTheEngineGravity()
		{
			const double seconds = 1.0;
			var ticks = (int)Math.Round(seconds / PhysicsConstants.PhysicsStepTimeS);
			var h = RubberDeformationUnits.TickTime; // VPE time units per 1 ms tick
			var g = (double)Constants.Gravity;      // VPE units per VPE time unit squared

			var v = 0.0;
			var x = 0.0;
			for (var i = 0; i < ticks; i++) {
				v += h * g;   // BallVelocityPhysics.UpdateVelocities: PhysFactor * gravity
				x += v * h;   // displacement inside the time-of-impact loop
			}

			// exact sum of the semi-implicit scheme: x = g h^2 * n(n+1)/2
			var expectedDiscrete = g * h * h * ticks * (ticks + 1) / 2.0;
			Assert.That(x, Is.EqualTo(expectedDiscrete).Within(1e-6 * expectedDiscrete));

			// and that converts back to the SI drop, up to the scheme's O(h) offset
			var dropMetres = RubberDeformationUnits.LengthToSi(x);
			var analytic = 0.5 * RubberDeformationUnits.StandardGravitySi * seconds * seconds;
			var offset = 0.5 * RubberDeformationUnits.StandardGravitySi * PhysicsConstants.PhysicsStepTimeS * seconds;
			Assert.That(dropMetres, Is.EqualTo(analytic + offset).Within(1e-4 * analytic),
				$"dropped {dropMetres:F6} m in 1 s, analytic {analytic:F6} m, scheme offset {offset:F6} m");
		}

		/// <summary>
		/// Section 2.2's two-body impulse check: an 80 g ball (VPE mass 1) against a 1 kg
		/// body (VPE mass 12.5), resolved in VPE units and in SI, must agree.
		/// </summary>
		[Test]
		public void ShouldResolveATwoBodyImpulseIdenticallyInBothUnitSystems()
		{
			const double ballKg = 0.080;
			const double bodyKg = 1.0;
			const double approachSi = 3.0; // m/s, head on

			// SI: inelastic normal impulse that zeroes the relative normal velocity
			var reducedSi = 1.0 / (1.0 / ballKg + 1.0 / bodyKg);
			var impulseSi = reducedSi * approachSi;               // N s
			var ballAfterSi = approachSi - impulseSi / ballKg;    // m/s
			var bodyAfterSi = impulseSi / bodyKg;

			// VPE
			var ballVpe = RubberDeformationUnits.MassToVpe(ballKg);
			var bodyVpe = RubberDeformationUnits.MassToVpe(bodyKg);
			Assert.That(ballVpe, Is.EqualTo(1.0).Within(1e-12));
			Assert.That(bodyVpe, Is.EqualTo(12.5).Within(1e-12));

			var approachVpe = RubberDeformationUnits.VelocityToVpe(approachSi);
			var reducedVpe = 1.0 / (1.0 / ballVpe + 1.0 / bodyVpe);
			var impulseVpe = reducedVpe * approachVpe;
			var ballAfterVpe = approachVpe - impulseVpe / ballVpe;
			var bodyAfterVpe = impulseVpe / bodyVpe;

			Assert.That(RubberDeformationUnits.VelocityToSi(ballAfterVpe), Is.EqualTo(ballAfterSi).Within(1e-9));
			Assert.That(RubberDeformationUnits.VelocityToSi(bodyAfterVpe), Is.EqualTo(bodyAfterSi).Within(1e-9));

			// momentum and energy bookkeeping
			var momentumBefore = ballVpe * approachVpe;
			var momentumAfter = ballVpe * ballAfterVpe + bodyVpe * bodyAfterVpe;
			Assert.That(momentumAfter, Is.EqualTo(momentumBefore).Within(1e-9 * momentumBefore));
			var energyBefore = 0.5 * ballVpe * approachVpe * approachVpe;
			var energyAfter = 0.5 * ballVpe * ballAfterVpe * ballAfterVpe + 0.5 * bodyVpe * bodyAfterVpe * bodyAfterVpe;
			Assert.That(energyAfter, Is.LessThanOrEqualTo(energyBefore));
		}

		/// <summary>
		/// Section 2.1: <c>M_n = mu0 * L / (2 * lambda0)</c>. Half the unloaded mass of the
		/// span, so stretching the same piece of rubber further does not invent mass -- and
		/// that is exactly what the brief's <c>mu * L / 2</c> would do.
		/// </summary>
		[Test]
		public void ShouldConserveModalMassAcrossStretch()
		{
			const double unloadedLengthVpx = 136.0;
			var curve = new RubberDefaultStressCurve();
			double? reference = null;
			foreach (var stretch in new[] { 1.02, 1.10, 1.25, 1.50, 1.90 }) {
				var installed = unloadedLengthVpx * stretch;
				var c = new RubberSpanCoefficients(8.0, installed, 1100.0, stretch, 0.08, curve);
				reference ??= c.ModalMassKilograms;
				Assert.That(c.ModalMassKilograms, Is.EqualTo(reference.Value).Within(1e-15),
					$"modal mass changed at stretch {stretch}");

				// the naive mu * L / 2 form grows with stretch and is what the plan corrects
				var naive = c.UnloadedLinearDensity * c.InstalledLengthMetres * 0.5;
				Assert.That(naive, Is.EqualTo(c.ModalMassKilograms * stretch).Within(1e-15 * naive));
			}

			// and the conserved quantity is literally half the unloaded span mass
			var reference2 = new RubberSpanCoefficients(8.0, unloadedLengthVpx * 1.10, 1100.0, 1.10, 0.08, curve);
			var unloadedMass = reference2.UnloadedLinearDensity * RubberDeformationUnits.LengthToSi(unloadedLengthVpx);
			Assert.That(reference2.ModalMassKilograms, Is.EqualTo(0.5 * unloadedMass).Within(1e-15 * unloadedMass));
		}

		#region section 12 worked magnitudes

		[Test]
		public void ShouldReproduceTheWorkedSpanWithTheDefaultCurve()
		{
			var c = RubberSpanCoefficients.WorkedDefaultCurve();

			Assert.That(c.ThicknessVpx / RubberDeformationUnits.VpxPerMetre * 1000.0, Is.EqualTo(4.32).Within(0.01));
			Assert.That(c.InstalledLengthMetres * 1000.0, Is.EqualTo(81.0).Within(0.5));

			Assert.That(c.StressPascals / MegaPascal, Is.EqualTo(0.056).Within(5e-4),
				$"stress at stretch 1.10 is {c.StressPascals / MegaPascal:F6} MPa");
			Assert.That(c.TensionNewtons, Is.EqualTo(0.82).Within(0.01),
				$"tension is {c.TensionNewtons:F4} N");
			Assert.That(c.ModalMassKilograms * 1000.0, Is.EqualTo(0.593).Within(5e-4),
				$"M1 is {c.ModalMassKilograms * 1000.0:F4} g");
			Assert.That(c.ModalStiffnessSi(1), Is.EqualTo(50.0).Within(0.05),
				$"K1 is {c.ModalStiffnessSi(1):F3} N/m");
			Assert.That(c.FrequencyHz(1), Is.EqualTo(46.2).Within(0.05),
				$"f1 is {c.FrequencyHz(1):F3} Hz");

			// period 21.6 ms, i.e. 2.16 VPE time units
			Assert.That(2.0 * Math.PI / c.OmegaVpe(1), Is.EqualTo(2.16).Within(0.01));
		}

		[Test]
		public void ShouldReproduceTheWorkedSpanWithTheLinearRamp()
		{
			var c = RubberSpanCoefficients.WorkedLinearRamp();

			Assert.That(c.StressPascals / MegaPascal, Is.EqualTo(0.20).Within(1e-9));
			Assert.That(c.TensionNewtons, Is.EqualTo(2.9).Within(0.05),
				$"tension is {c.TensionNewtons:F4} N");
			Assert.That(c.ModalMassKilograms * 1000.0, Is.EqualTo(0.593).Within(5e-4));
			Assert.That(c.ModalStiffnessSi(1), Is.EqualTo(178.5).Within(0.2),
				$"K1 is {c.ModalStiffnessSi(1):F3} N/m");
			Assert.That(c.FrequencyHz(1), Is.EqualTo(87.3).Within(0.1),
				$"f1 is {c.FrequencyHz(1):F3} Hz");
		}

		[Test]
		public void ShouldGiveTheSameModalMassForEveryMode()
		{
			var c = RubberSpanCoefficients.WorkedDefaultCurve();
			for (var n = 1; n <= 8; n++) {
				Assert.That(c.Modal(n).Mass, Is.EqualTo(c.ModalMassVpe).Within(1e-15));
				// K_n scales with n^2, so omega_n scales with n
				Assert.That(c.OmegaVpe(n), Is.EqualTo(n * c.OmegaVpe(1)).Within(1e-9 * n * c.OmegaVpe(1)));
			}
		}

		/// <summary>
		/// Section 4.2 and 7.1: the baker samples the curve into 128 points and reads it
		/// back by linear interpolation. The resulting coefficients must sit within 0.12
		/// percent of the analytic ones.
		/// </summary>
		[Test]
		public void ShouldAgreeWithTheTableDerivedCoefficients()
		{
			var report = "";
			foreach (var pair in new (string name, IRubberStressCurve curve)[] {
				("default zero-tangent", new RubberDefaultStressCurve()),
				("linear ramp", new RubberLinearRampStressCurve()),
			}) {
				var analytic = RubberSpanCoefficients.Worked(pair.curve);
				var sampled = new RubberSampledStressCurve(pair.curve);
				var baked = RubberSpanCoefficients.Worked(sampled);

				Assert.That(sampled.SampleCount, Is.EqualTo(128));

				var stressError = Math.Abs(baked.StressPascals - analytic.StressPascals) / analytic.StressPascals;
				var stiffnessError = Math.Abs(baked.ModalStiffnessSi(1) - analytic.ModalStiffnessSi(1)) / analytic.ModalStiffnessSi(1);
				var frequencyError = Math.Abs(baked.FrequencyHz(1) - analytic.FrequencyHz(1)) / analytic.FrequencyHz(1);
				report += $"{pair.name}: stress {stressError * 100:F4}%, K1 {stiffnessError * 100:F4}%, f1 {frequencyError * 100:F4}%\n";

				Assert.That(stressError, Is.LessThan(0.0012), $"{pair.name} stress error {stressError * 100:F4}% exceeds 0.12%");
				Assert.That(stiffnessError, Is.LessThan(0.0012), $"{pair.name} K1 error {stiffnessError * 100:F4}% exceeds 0.12%");
				Assert.That(frequencyError, Is.LessThan(0.0012), $"{pair.name} f1 error {frequencyError * 100:F4}% exceeds 0.12%");
				Assert.That(baked.ModalMassKilograms, Is.EqualTo(analytic.ModalMassKilograms).Within(1e-15));
			}
			TestContext.WriteLine("128-sample table error:\n" + report);
		}

		/// <summary>Section 4.2's claim that the error scales with the inverse square of the sample count.</summary>
		[Test]
		public void ShouldShowQuadraticTableErrorScaling()
		{
			var curve = new RubberDefaultStressCurve();
			var analytic = RubberSpanCoefficients.Worked(curve);
			var e64 = Math.Abs(RubberSpanCoefficients.Worked(new RubberSampledStressCurve(curve, 64)).StressPascals
				- analytic.StressPascals) / analytic.StressPascals;
			var e128 = Math.Abs(RubberSpanCoefficients.Worked(new RubberSampledStressCurve(curve, 128)).StressPascals
				- analytic.StressPascals) / analytic.StressPascals;
			TestContext.WriteLine($"64 samples: {e64 * 100:F4}%, 128 samples: {e128 * 100:F4}%, ratio {e64 / e128:F2}");
			Assert.That(e64 / e128, Is.GreaterThan(3.0).And.LessThan(5.0));
			Assert.That(e64, Is.LessThan(0.005), $"64-sample error {e64 * 100:F4}% (the plan quotes 0.45%)");
		}

		#endregion
	}
}
