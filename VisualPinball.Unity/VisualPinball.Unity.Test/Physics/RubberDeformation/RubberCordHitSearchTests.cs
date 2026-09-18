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
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;
	using Random = System.Random;

	/// <summary>
	/// Section 3.4 and 7.1: conservative advancement against a chain driven by the closed
	/// form, its classification rules, and the search-limit contract.
	/// </summary>
	public class RubberCordHitSearchTests
	{
		private const double SpanLength = RubberSpanCoefficients.WorkedInstalledLengthVpx;
		private const double CordRadius = RubberSpanCoefficients.WorkedThicknessVpx * 0.5;
		private const double BallRadius = 25.0;

		/// <summary>The oracle's 10 microsecond scan, in VPE time units.</summary>
		private const double ScanResolution = 1e-3;

		private static RubberCordChain Chain(int segments = 16) => RubberCordChain.Straight(
			new double3(0, 0, 0), new double3(SpanLength, 0, 0), new double3(0, 1, 0), segments, CordRadius);

		[Test]
		public void ShouldNotTunnelOnTenThousandRandomisedShots()
		{
			var random = new Random(20260906);
			var chain = Chain();
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var c = span.Modal(1);
			var window = RubberDeformationUnits.TickTime;

			var misses = 0;
			var late = 0;
			var worstLateness = 0.0;
			var hits = 0;
			var contacts = 0;
			var covered = 0;
			var limits = 0;

			for (var trial = 0; trial < 10000; trial++) {
				// ball anywhere around the span, at up to 100 units per step
				var speed = random.NextDouble() * 100.0;
				var angle = random.NextDouble() * 2.0 * Math.PI;
				var ball = new RubberCordHitSearch.BallShot {
					Position = new double3(
						random.NextDouble() * SpanLength * 1.2 - 0.1 * SpanLength,
						(random.NextDouble() * 2.0 - 1.0) * 90.0,
						0.0),
					Velocity = new double3(Math.Cos(angle) * speed, Math.Sin(angle) * speed, 0.0),
					Radius = BallRadius,
				};
				// modal speeds up to the post-impact envelope of section 3.3
				var s = new RubberModalState(
					(random.NextDouble() * 2.0 - 1.0) * 60.0,
					(random.NextDouble() * 2.0 - 1.0) * 600.0);

				var result = RubberCordHitSearch.HitTest(chain, in ball, in c, in s, window, false);
				var firstPenetration = RubberCordHitSearch.FirstPenetration(
					chain, in ball, in c, in s, window, false, ScanResolution);

				switch (result.Outcome) {
					case RubberHitOutcome.Hit:
						hits++;
						if (!double.IsPositiveInfinity(firstPenetration) && result.Time > firstPenetration + 1e-9) {
							late++;
							worstLateness = Math.Max(worstLateness, result.Time - firstPenetration);
						}
						break;
					case RubberHitOutcome.Contact:
						contacts++;
						break;
					case RubberHitOutcome.None:
						covered++;
						if (!double.IsPositiveInfinity(firstPenetration)) {
							misses++;
						}
						break;
					default:
						limits++;
						break;
				}
			}

			TestContext.WriteLine(
				$"10000 shots to 100 u/step: {hits} hits, {contacts} contacts, {covered} covered, {limits} search limits\n" +
				$"  missed penetrations: {misses}\n" +
				$"  reported later than the scan's first contact: {late}, worst lateness {worstLateness:E3} VPE t");

			Assert.That(misses, Is.EqualTo(0), $"{misses} shots penetrated in a window the search certified as clear");
			Assert.That(late, Is.EqualTo(0),
				$"{late} hits were reported after the scan's first contact, worst by {worstLateness:E3}");
			Assert.That(hits, Is.GreaterThan(100), "the random draw produced too few hits to be meaningful");
		}

		/// <summary>
		/// The review's in-band separating return: a single mode with zeta 0.08 and
		/// <c>omega_d = 2 pi / 0.05</c>, cap 0.01, <c>q(0) = 0</c>, <c>qdot(0) = -1</c> and
		/// a stationary ball 0.001 away. The cord recedes first, comes back, and the search
		/// has to report the return rather than certifying the window as clear.
		/// </summary>
		[Test]
		public void ShouldFindTheReturnOfAnInBandSeparatingCord()
		{
			const double zeta = 0.08;
			var omegaD = 2.0 * Math.PI / 0.05;
			var omegaN = omegaD / Math.Sqrt(1.0 - zeta * zeta);
			const double mass = 0.0074;
			var stiffness = mass * omegaN * omegaN;
			var c = RubberModalCoefficients.FromRatio(mass, stiffness, zeta);
			Assert.That(c.OmegaD, Is.EqualTo(omegaD).Within(1e-9));

			var chain = Chain();
			var s = new RubberModalState(0.0, -1.0);
			const double gap = 0.001;
			// the ball sits on the inward side, so qdot = -1 first takes the cord away from
			// it and the swing back is what has to be found
			var ball = new RubberCordHitSearch.BallShot {
				Position = new double3(SpanLength * 0.5, BallRadius + CordRadius + gap, 0.0),
				Velocity = double3.zero,
				Radius = BallRadius,
			};

			var sep0 = RubberCordHitSearch.SeparationAt(chain, in ball, in c, in s, 0.0, false);
			var vn0 = RubberCordHitSearch.NormalVelocityAt(chain, in ball, in c, in s, 0.0, false);
			Assert.That(sep0, Is.EqualTo(gap).Within(1e-9));
			Assert.That(sep0, Is.LessThanOrEqualTo(RubberCordHitSearch.PhysTouch), "the case must start inside the touch band");
			Assert.That(vn0, Is.GreaterThan(RubberCordHitSearch.ContactVel), "the cord must be separating at t = 0");

			const double window = 0.1;
			var result = RubberCordHitSearch.HitTest(chain, in ball, in c, in s, window, false);
			var firstPenetration = RubberCordHitSearch.FirstPenetration(
				chain, in ball, in c, in s, window, false, 1e-5);

			TestContext.WriteLine($"in-band separating return: outcome {result.Outcome} at t = {result.Time:F6} " +
				$"after {result.Iterations} iterations; the scan first penetrates at {firstPenetration:F6}");

			Assert.That(firstPenetration, Is.LessThan(window), "the fixture itself must produce a penetration");
			Assert.That(result.Outcome, Is.EqualTo(RubberHitOutcome.Hit),
				$"the search returned {result.Outcome} instead of finding the return");
			Assert.That(result.Time, Is.GreaterThan(0.0));
			Assert.That(result.Time, Is.LessThanOrEqualTo(firstPenetration + 1e-9),
				$"the return was reported at {result.Time:F6}, after the first penetration at {firstPenetration:F6}");
		}

		[Test]
		public void ShouldClassifyTheThreeZeroTimeCases()
		{
			var chain = Chain();
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var c = span.Modal(1);
			var s = new RubberModalState(0.0, 0.0);
			var touching = new double3(SpanLength * 0.5, -(BallRadius + CordRadius + 0.5 * RubberCordHitSearch.PhysTouch), 0.0);

			// slow: a sustained contact row
			var slow = new RubberCordHitSearch.BallShot {
				Position = touching, Velocity = new double3(0, 0.05, 0), Radius = BallRadius,
			};
			Assert.That(RubberCordHitSearch.HitTest(chain, in slow, in c, in s, 0.1, false).Outcome,
				Is.EqualTo(RubberHitOutcome.Contact));

			// fast and approaching: an impact at t = 0
			var fast = new RubberCordHitSearch.BallShot {
				Position = touching, Velocity = new double3(0, 12.0, 0), Radius = BallRadius,
			};
			var impact = RubberCordHitSearch.HitTest(chain, in fast, in c, in s, 0.1, false);
			Assert.That(impact.Outcome, Is.EqualTo(RubberHitOutcome.Hit));
			Assert.That(impact.Time, Is.EqualTo(0.0));

			// fast and separating: no zero-time event, and with a still cord no event at all
			var leaving = new RubberCordHitSearch.BallShot {
				Position = touching, Velocity = new double3(0, -12.0, 0), Radius = BallRadius,
			};
			Assert.That(RubberCordHitSearch.HitTest(chain, in leaving, in c, in s, 0.1, false).Outcome,
				Is.EqualTo(RubberHitOutcome.None));
		}

		[Test]
		public void ShouldCertifyAClearWindow()
		{
			var chain = Chain();
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var c = span.Modal(1);
			var s = new RubberModalState(0.0, 0.0);
			var ball = new RubberCordHitSearch.BallShot {
				Position = new double3(SpanLength * 0.5, -400.0, 0.0),
				Velocity = new double3(0, 1.0, 0),
				Radius = BallRadius,
			};
			var result = RubberCordHitSearch.HitTest(chain, in ball, in c, in s, 0.1, false);
			Assert.That(result.Outcome, Is.EqualTo(RubberHitOutcome.None));
			Assert.That(result.CoveredTime, Is.EqualTo(0.1).Within(1e-12));
		}

		/// <summary>
		/// Section 3.4's worst case: a ball skimming along inside the touch band. It records
		/// search-limit substeps tick after tick, and the per-tick cap of 64 must stop the
		/// accounting from running away.
		/// </summary>
		[Test]
		public void ShouldRecordSearchLimitsOnGrazingShotsAndHonourThePerTickCap()
		{
			var chain = Chain();
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var c = span.Modal(1);
			var budget = new RubberCordHitSearch.SearchLimitBudget();

			var worstPerTick = 0;
			var ticksWithLimits = 0;
			var limitedSubsteps = 0;
			var ignoredSubsteps = 0;
			const int ticks = 20;
			const double tangentialSpeed = 100.0;
			// just above ContactVel, so t = 0 is neither a contact nor an impact and the
			// advancement has to crawl out of the touch band
			const double separatingSpeed = 0.12;

			for (var tick = 0; tick < ticks; tick++) {
				budget.BeginTick();
				var s = new RubberModalState(0.0, 0.0);
				var y = -(BallRadius + CordRadius + 0.02 * RubberCordHitSearch.PhysTouch);
				var x = 10.0;
				var remaining = RubberDeformationUnits.TickTime;
				var guard = 0;
				while (remaining > 1e-9 && guard++ < 20000) {
					var ball = new RubberCordHitSearch.BallShot {
						Position = new double3(x, y, 0.0),
						Velocity = new double3(tangentialSpeed, -separatingSpeed, 0.0),
						Radius = BallRadius,
					};
					var result = RubberCordHitSearch.HitTest(chain, in ball, in c, in s, remaining, false);
					double taken;
					if (result.Outcome == RubberHitOutcome.SearchLimit) {
						limitedSubsteps++;
						if (budget.Accept()) {
							taken = result.CoveredTime;
						} else {
							ignoredSubsteps++;
							taken = remaining;
						}
					} else if (result.Outcome == RubberHitOutcome.Hit || result.Outcome == RubberHitOutcome.Contact) {
						taken = Math.Max(result.Time, 1e-6);
					} else {
						taken = remaining;
					}
					taken = Math.Min(Math.Max(taken, 1e-9), remaining);
					x += tangentialSpeed * taken;
					y -= separatingSpeed * taken;
					remaining -= taken;
				}
				if (budget.SubstepsThisTick > 0) {
					ticksWithLimits++;
				}
				worstPerTick = Math.Max(worstPerTick, budget.SubstepsThisTick);
			}

			TestContext.WriteLine(
				$"grazing shot at {tangentialSpeed} u/step tangential, {separatingSpeed} separating, over {ticks} ticks:\n" +
				$"  substeps that returned a search limit: {limitedSubsteps} " +
				$"({(double)limitedSubsteps / ticks:F1} per tick)\n" +
				$"  accepted: {budget.TotalSubsteps}, ignored past the cap: {ignoredSubsteps}\n" +
				$"  worst accepted in one tick: {worstPerTick}, ticks with limits: {ticksWithLimits}, " +
				$"ticks that hit the 64 cap: {budget.CapHitTicks}");

			Assert.That(limitedSubsteps, Is.GreaterThan(0), "the grazing shot produced no search limits at all");
			Assert.That(worstPerTick, Is.LessThanOrEqualTo(RubberCordHitSearch.SearchLimitSubstepsPerTick),
				"the per-tick cap did not hold");
			Assert.That(budget.CapHitTicks, Is.GreaterThan(0),
				"the grazing shot never reached the 64 per tick cap, so the cap is untested here");
		}

		[Test]
		public void ShouldExerciseThePerTickCap()
		{
			var budget = new RubberCordHitSearch.SearchLimitBudget();
			budget.BeginTick();
			for (var i = 0; i < 200; i++) {
				var accepted = budget.Accept();
				Assert.That(accepted, Is.EqualTo(i < RubberCordHitSearch.SearchLimitSubstepsPerTick));
			}
			Assert.That(budget.SubstepsThisTick, Is.EqualTo(RubberCordHitSearch.SearchLimitSubstepsPerTick));
			Assert.That(budget.CapHitTicks, Is.EqualTo(1), "the tick must be counted exactly once");

			budget.BeginTick();
			Assert.That(budget.Accept(), Is.True, "a fresh tick starts with a fresh budget");
			Assert.That(budget.CapHitTicks, Is.EqualTo(1));
		}

		[Test]
		public void ShouldUseTheBallSpeedAloneAsTheRateBoundAtTheCap()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var c = span.Modal(1);
			var s = new RubberModalState(60.0, 0.0);
			var ball = new RubberCordHitSearch.BallShot {
				Position = new double3(0, -100, 0), Velocity = new double3(0, 7.0, 0), Radius = BallRadius,
			};
			Assert.That(RubberCordHitSearch.RateBound(in ball, in c, in s, true), Is.EqualTo(7.0).Within(1e-12));
			Assert.That(RubberCordHitSearch.RateBound(in ball, in c, in s, false),
				Is.EqualTo(7.0 + 60.0 * c.OmegaN).Within(1e-9));
		}
	}
}
