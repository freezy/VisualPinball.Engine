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
using System.Collections.Generic;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// Section 2.5 and 7.1: the single-mode closed form and the cap root isolation.
	/// </summary>
	public class RubberModalMathTests
	{
		private const int ReferenceSubstepsPerPeriod = 1000;
		private const int ReferencePeriods = 100;

		#region closed form versus a reference integrator

		[TestCase(0.001)]
		[TestCase(0.08)]
		[TestCase(0.30)]
		public void ShouldMatchAReferenceIntegratorOverAHundredPeriods(double zeta)
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var c = RubberModalCoefficients.FromRatio(span.ModalMassVpe, span.ModalStiffnessVpe(1), zeta);
			var period = 2.0 * Math.PI / c.OmegaD;
			var dt = period / ReferenceSubstepsPerPeriod;
			var steps = ReferenceSubstepsPerPeriod * ReferencePeriods;

			var q = 12.0;
			var v = 0.0;
			var a = c.Zeta * c.OmegaN;
			var envelope0 = Envelope(c, q, v);

			// half-period zero crossings of q, which the closed form places exactly pi/wd apart
			var crossings = new List<double>();
			var previous = q;
			for (var i = 0; i < steps; i++) {
				Rk4(c, ref q, ref v, dt);
				if (previous > 0.0 != q > 0.0 && q != 0.0) {
					var t = (i + previous / (previous - q)) * dt;
					crossings.Add(t);
				}
				previous = q;
			}

			Assert.That(crossings.Count, Is.GreaterThanOrEqualTo(2 * ReferencePeriods),
				"the reference did not produce enough zero crossings");

			// the zeros of the closed form are exactly pi/wd apart, so the 200th sits 199
			// half periods after the first whatever the initial phase is
			var index = 2 * ReferencePeriods - 1;
			var measuredSpacing = (crossings[index] - crossings[0]) / index;
			var analyticSpacing = Math.PI / c.OmegaD;
			var frequencyError = Math.Abs(measuredSpacing - analyticSpacing) / analyticSpacing;

			var tEnd = steps * dt;
			var envelopeEnd = Envelope(c, q, v);
			var analyticEnvelope = envelope0 * Math.Exp(-a * tEnd);
			var decayError = Math.Abs(envelopeEnd - analyticEnvelope) / analyticEnvelope;

			// and the closed form taken in one single step must land on the same state
			var closed = RubberModalMath.Advance(in c, new RubberModalState(12.0, 0.0), tEnd);
			var stateError = Math.Sqrt((closed.Q - q) * (closed.Q - q)
				+ (closed.QDot - v) * (closed.QDot - v) / (c.OmegaN * c.OmegaN)) / analyticEnvelope;

			TestContext.WriteLine($"zeta {zeta}: frequency error {frequencyError:E3}, " +
				$"decay error {decayError:E3}, state error {stateError:E3}");

			Assert.That(frequencyError, Is.LessThan(1e-4),
				$"frequency differs from the reference by {frequencyError:E3} relative");
			Assert.That(decayError, Is.LessThan(1e-4),
				$"decay differs from the reference by {decayError:E3} relative");
			Assert.That(stateError, Is.LessThan(1e-4),
				$"final state differs from the reference by {stateError:E3} relative");
		}

		private static double Envelope(in RubberModalCoefficients c, double q, double v)
		{
			var a = c.Zeta * c.OmegaN;
			var b = (v + a * q) / c.OmegaD;
			return Math.Sqrt(q * q + b * b);
		}

		private static void Rk4(RubberModalCoefficients c, ref double q, ref double v, double dt)
		{
			var damping = c.Damping;
			var stiffness = c.Stiffness;
			var mass = c.Mass;
			double Acc(double qq, double vv) => -(damping * vv + stiffness * qq) / mass;

			var k1Q = v;
			var k1V = Acc(q, v);
			var k2Q = v + 0.5 * dt * k1V;
			var k2V = Acc(q + 0.5 * dt * k1Q, v + 0.5 * dt * k1V);
			var k3Q = v + 0.5 * dt * k2V;
			var k3V = Acc(q + 0.5 * dt * k2Q, v + 0.5 * dt * k2V);
			var k4Q = v + dt * k3V;
			var k4V = Acc(q + dt * k3Q, v + dt * k3V);

			q += dt / 6.0 * (k1Q + 2 * k2Q + 2 * k3Q + k4Q);
			v += dt / 6.0 * (k1V + 2 * k2V + 2 * k3V + k4V);
		}

		#endregion

		[Test]
		public void ShouldComposeSteps()
		{
			var random = new Random(20260901);
			var worst = 0.0;
			for (var i = 0; i < 20000; i++) {
				var c = RandomCoefficients(random);
				var s = new RubberModalState(random.NextDouble() * 60.0 - 30.0, random.NextDouble() * 400.0 - 200.0);
				var a = random.NextDouble() * 0.2;
				var b = random.NextDouble() * 0.2;

				var composed = RubberModalMath.Advance(in c, RubberModalMath.Advance(in c, in s, a), b);
				var direct = RubberModalMath.Advance(in c, in s, a + b);
				var scale = Math.Max(1.0, Math.Abs(direct.Q) + Math.Abs(direct.QDot) / c.OmegaN);
				var error = (Math.Abs(composed.Q - direct.Q) + Math.Abs(composed.QDot - direct.QDot) / c.OmegaN) / scale;
				worst = Math.Max(worst, error);
			}
			TestContext.WriteLine($"worst composition error over 20000 draws: {worst:E3}");
			Assert.That(worst, Is.LessThan(1e-6), $"step(a+b) differs from step(b) after step(a) by {worst:E3}");
		}

		[Test]
		public void ShouldNeverGainEnergyOverAHundredThousandRandomSteps()
		{
			var random = new Random(20260902);
			var c = RubberModalCoefficients.FromRatio(0.0074, 0.0625, 0.08);
			var s = new RubberModalState(30.0, 0.0);
			var worstGain = 0.0;
			var violations = 0;

			for (var i = 0; i < 100000; i++) {
				if (i % 500 == 0) {
					s = new RubberModalState(random.NextDouble() * 60.0 - 30.0, random.NextDouble() * 200.0 - 100.0);
				}
				var before = RubberModalMath.Energy(in c, in s);
				var dt = random.NextDouble() * 0.2;
				s = RubberModalMath.Advance(in c, in s, dt);
				var after = RubberModalMath.Energy(in c, in s);
				var gain = after - before;
				if (gain > 0.0) {
					violations++;
					worstGain = Math.Max(worstGain, gain / Math.Max(before, 1e-300));
				}
			}
			TestContext.WriteLine($"energy gains: {violations} of 100000 steps, worst relative gain {worstGain:E3}");
			Assert.That(worstGain, Is.LessThan(1e-12),
				$"the free step gained {worstGain:E3} relative energy on {violations} of 100000 steps");
		}

		[Test]
		public void ShouldAgreeWithASinglePrecisionStep()
		{
			var random = new Random(20260903);
			var worst = 0.0;
			for (var i = 0; i < 10000; i++) {
				var c = RandomCoefficients(random);
				var q = random.NextDouble() * 60.0 - 30.0;
				var v = random.NextDouble() * 200.0 - 100.0;
				var dt = random.NextDouble() * 0.1;

				var expected = RubberModalMath.Advance(in c, new RubberModalState(q, v), dt);
				float fq = (float)q, fv = (float)v;
				RubberModalMath.AdvanceFloat((float)c.Mass, (float)c.Stiffness, (float)c.Damping, ref fq, ref fv, (float)dt);

				var scale = Math.Max(1.0, Math.Abs(expected.Q) + Math.Abs(expected.QDot) / c.OmegaN);
				var error = (Math.Abs(fq - expected.Q) + Math.Abs(fv - expected.QDot) / c.OmegaN) / scale;
				worst = Math.Max(worst, error);
			}
			TestContext.WriteLine($"worst float-versus-double step error: {worst:E3}");
			Assert.That(worst, Is.LessThan(1e-4), $"the float step drifts from the double reference by {worst:E3}");
		}

		private static RubberModalCoefficients RandomCoefficients(Random random)
		{
			var mass = 0.001 + random.NextDouble() * 0.05;
			var stiffness = 0.005 + random.NextDouble() * 2.0;
			var zeta = random.NextDouble() * 0.5;
			return RubberModalCoefficients.FromRatio(mass, stiffness, zeta);
		}

		#region cap root isolation

		[Test]
		public void ShouldIsolateTheFirstCapCrossing()
		{
			var random = new Random(20260904);
			var scratch = new List<double>();
			var arrivals = 0;
			var grazing = 0;
			var worstBracket = 0.0;

			for (var trial = 0; trial < 10000; trial++) {
				var c = RandomCoefficients(random);
				var cap = 1.0 + random.NextDouble() * 80.0;
				var q0 = (random.NextDouble() * 2.0 - 1.0) * cap;
				var v0 = (random.NextDouble() * 2.0 - 1.0) * 600.0;
				var window = 0.001 + random.NextDouble() * 0.4;
				var s = new RubberModalState(q0, v0);

				var prediction = RubberModalMath.PredictCapArrival(in c, in s, cap, window, scratch);

				if (prediction.Outcome == RubberCapOutcome.Arrival) {
					arrivals++;
					var t = prediction.Time;
					Assert.That(t, Is.GreaterThan(0.0).And.LessThanOrEqualTo(window));

					// brackets a sign change of the closed form at t +/- 1e-6
					var before = RubberModalMath.Advance(in c, in s, Math.Max(0.0, t - 1e-6)).Q - prediction.Sign * cap;
					var after = RubberModalMath.Advance(in c, in s, Math.Min(window, t + 1e-6)).Q - prediction.Sign * cap;
					Assert.That(before * after, Is.LessThanOrEqualTo(1e-10 * cap * cap),
						$"trial {trial}: no sign change bracketed at t = {t} " +
						$"(q - cap is {before:E3} before and {after:E3} after)");
					worstBracket = Math.Max(worstBracket, Math.Abs(after - before));

					// no earlier crossing exists. Within the bisection tolerance of the root
					// the closed form may already be above the cap by rate * tolerance, so
					// that much is allowed and nothing more.
					var slack = RubberModalMath.RateBound(in c, in s) * 4.0 * RubberModalMath.CapBisectionTolerance
						+ 1e-9 * cap;
					var excess = MaximumExcess(in c, in s, cap, 0.0, t, 2000);
					if (excess > slack) {
						Assert.Fail($"trial {trial}: |q| exceeded the cap by {excess} (slack {slack}) " +
							$"before the reported arrival at {t}");
					}
				} else {
					// nothing may cross the cap anywhere in the window
					var excess = MaximumExcess(in c, in s, cap, 0.0, window, 4000);
					if (excess > 1e-6 * cap) {
						grazing++;
						Assert.That(excess, Is.LessThan(1e-3 * cap),
							$"trial {trial}: a crossing of {excess} above the cap was missed over the window");
					}
				}
			}
			TestContext.WriteLine($"cap isolation: {arrivals} arrivals in 10000 trials, {grazing} near-tangency cases");
			Assert.That(arrivals, Is.GreaterThan(1000), "the random draw produced too few arrivals to be meaningful");
			Assert.That(grazing, Is.EqualTo(0), $"{grazing} windows crossed the cap without an arrival being reported");
		}

		private static double MaximumExcess(in RubberModalCoefficients c, in RubberModalState s,
			double cap, double from, double to, int samples)
		{
			var worst = 0.0;
			for (var i = 0; i <= samples; i++) {
				var t = from + (to - from) * i / samples;
				if (t <= 1e-9) {
					continue;
				}
				var q = RubberModalMath.Advance(in c, in s, t).Q;
				worst = Math.Max(worst, Math.Abs(q) - cap);
			}
			return worst;
		}

		[Test]
		public void ShouldReportAnImmediateArrivalWhenAlreadyAtTheCapAndStillMovingOutwards()
		{
			var c = RubberModalCoefficients.FromRatio(0.0074, 0.0625, 0.08);
			const double cap = 20.0;

			var outward = RubberModalMath.PredictCapArrival(in c, new RubberModalState(cap, 5.0), cap, 0.1);
			Assert.That(outward.Outcome, Is.EqualTo(RubberCapOutcome.AlreadyAtCap));
			Assert.That(outward.Time, Is.EqualTo(0.0));
			Assert.That(outward.Sign, Is.EqualTo(1));

			// released exactly at the cap and heading back: no immediate re-capture
			var inward = RubberModalMath.PredictCapArrival(in c, new RubberModalState(cap, -5.0), cap, 0.1);
			Assert.That(inward.Outcome, Is.Not.EqualTo(RubberCapOutcome.AlreadyAtCap));
			if (inward.Outcome == RubberCapOutcome.Arrival) {
				Assert.That(inward.Time, Is.GreaterThan(0.0));
			}

			// and at rest exactly on the cap it is not an arrival either
			var resting = RubberModalMath.PredictCapArrival(in c, new RubberModalState(cap, 0.0), cap, 0.1);
			Assert.That(resting.Outcome, Is.Not.EqualTo(RubberCapOutcome.AlreadyAtCap));
		}

		[Test]
		public void ShouldFindTheFirstOfSeveralCrossingsInsideOneWindow()
		{
			// a lightly damped lane whose amplitude is well over the cap crosses out, comes
			// back and crosses out again inside a single window
			var c = RubberModalCoefficients.FromRatio(0.0074, 0.0625, 0.005);
			const double cap = 5.0;
			var s = new RubberModalState(0.0, 60.0);
			var window = 2.0 * Math.PI / c.OmegaD * 1.5;

			var prediction = RubberModalMath.PredictCapArrival(in c, in s, cap, window);
			Assert.That(prediction.Outcome, Is.EqualTo(RubberCapOutcome.Arrival));
			Assert.That(prediction.Sign, Is.EqualTo(1));

			// the analytic first crossing, bracketed by a coarse scan and then bisected
			var first = double.PositiveInfinity;
			const int samples = 40000;
			var lo = 0.0;
			for (var i = 1; i <= samples; i++) {
				var t = window * i / samples;
				if (Math.Abs(RubberModalMath.Advance(in c, in s, t).Q) >= cap) {
					var hi = t;
					for (var k = 0; k < 60 && hi - lo > 1e-9; k++) {
						var mid = 0.5 * (lo + hi);
						if (Math.Abs(RubberModalMath.Advance(in c, in s, mid).Q) >= cap) {
							hi = mid;
						} else {
							lo = mid;
						}
					}
					first = 0.5 * (lo + hi);
					break;
				}
				lo = t;
			}
			Assert.That(prediction.Time, Is.EqualTo(first).Within(2e-6),
				$"reported {prediction.Time}, scan found {first}");

			// and there is more than one crossing in this window, so the "first" matters
			var extrema = new List<double>();
			RubberModalMath.Extrema(in c, in s, window, extrema);
			Assert.That(extrema.Count, Is.GreaterThanOrEqualTo(3));
		}

		[Test]
		public void ShouldReportNoArrivalWhenTheLaneStaysInside()
		{
			var c = RubberModalCoefficients.FromRatio(0.0074, 0.0625, 0.08);
			var s = new RubberModalState(1.0, 0.0);
			var prediction = RubberModalMath.PredictCapArrival(in c, in s, 40.0, 1.0);
			Assert.That(prediction.Outcome, Is.EqualTo(RubberCapOutcome.None));
			Assert.That(prediction.HasArrival, Is.False);
		}

		#endregion
	}
}
