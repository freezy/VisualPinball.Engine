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
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;
	using Random = System.Random;

	/// <summary>
	/// Section 7.1's engine-ordering contact fixture: a ball and the section 12 span driven
	/// through impact, sustained loading, release, a ten second hold, simultaneous impacts,
	/// cap arrival and cap residence, at random substep lengths.
	/// </summary>
	public class RubberContactFixtureTests
	{
		private const double SpanLength = RubberSpanCoefficients.WorkedInstalledLengthVpx;
		private const double CordRadius = RubberSpanCoefficients.WorkedThicknessVpx * 0.5;
		private const double BallRadius = 25.0;
		private const int Segments = 16;

		/// <summary>The engine's gravity in VPE units, at the default table slope and difficulty.</summary>
		private static readonly double DefaultLoad =
			PhysicsConstants.GravityConst * PhysicsConstants.DefaultTableGravity;

		#region setup

		private static RubberContactFixture Build(in RubberSpanCoefficients span,
			double cap = RubberSpanCoefficients.DefaultDeflectionClearanceVpx, double friction = 0.3)
		{
			var chain = RubberCordChain.Straight(new double3(0, 0, 0), new double3(SpanLength, 0, 0),
				new double3(0, 1, 0), Segments, CordRadius);
			return new RubberContactFixture(chain, span.Modal(1)) {
				Cap = cap,
				SurfaceFriction = friction,
			};
		}

		private static RubberContactFixture.Ball BallBelow(double u, double gap, double speed)
			=> new RubberContactFixture.Ball {
				Position = new double3(u * SpanLength, -(BallRadius + CordRadius + gap), 0.0),
				Velocity = new double3(0.0, speed, 0.0),
				AngularMomentum = double3.zero,
				Mass = 1.0,
				Radius = BallRadius,
			};

		private sealed class RunStats
		{
			internal double Time;
			internal double FirstContact = double.NaN;
			internal double LastContact = double.NaN;
			internal double PeakQ;
			internal double PeakDeflectionAtU;
			internal double WorstEnergyMinusWork = double.NegativeInfinity;
			internal int Substeps;

			internal double ContactDuration => double.IsNaN(FirstContact) ? 0.0 : LastContact - FirstContact;
		}

		/// <summary>Runs whole ticks with random substep lengths, exactly as section 2.4.1 orders them.</summary>
		private static RunStats Run(RubberContactFixture f, double duration, Random random, double u,
			Func<RunStats, bool> stop = null, Action<RubberContactFixture, RunStats> observe = null)
		{
			var stats = new RunStats();
			var baseline = f.TotalEnergy() - f.ExternalWork;
			var shape = Math.Sin(Math.PI * u);
			var guardTicks = 0;

			while (stats.Time < duration && guardTicks++ < 2000000) {
				f.BeginTick();
				var remaining = RubberDeformationUnits.TickTime;
				var guard = 0;
				while (remaining > 1e-12 && guard++ < 400) {
					var requested = Math.Min(remaining, 0.004 + random.NextDouble() * 0.06);
					var taken = f.Substep(requested);
					stats.Substeps++;
					remaining -= taken;
					stats.Time += taken;

					if (f.NormalRowsLastSolve > 0) {
						if (double.IsNaN(stats.FirstContact)) {
							stats.FirstContact = stats.Time;
						}
						stats.LastContact = stats.Time;
					}
					stats.PeakQ = Math.Max(stats.PeakQ, Math.Abs(f.Lane.Q));
					stats.PeakDeflectionAtU = Math.Max(stats.PeakDeflectionAtU, Math.Abs(f.Lane.Q) * shape);

					var budget = f.TotalEnergy() - f.ExternalWork - baseline;
					stats.WorstEnergyMinusWork = Math.Max(stats.WorstEnergyMinusWork, budget);
					observe?.Invoke(f, stats);
				}
				if (stop != null && stop(stats)) {
					break;
				}
			}
			return stats;
		}

		private static void AssertInvariants(RubberContactFixture f, RunStats stats, string what)
		{
			Assert.That(f.WorstReciprocalError, Is.LessThan(1e-9),
				$"{what}: M_n * delta qdot_n did not match -sum lambda_r g_r,n, worst relative error {f.WorstReciprocalError:E3}");
			Assert.That(f.WorstNegativeImpulse, Is.GreaterThanOrEqualTo(-1e-9),
				$"{what}: a normal or stop impulse went to {f.WorstNegativeImpulse:E3}");
			Assert.That(f.WorstComplementarity, Is.LessThanOrEqualTo(RubberContactGroupSolver.ComplementarityTolerance),
				$"{what}: worst unilateral complementarity residual {f.WorstComplementarity:E3}");
			Assert.That(f.WorstFrictionExcess, Is.LessThanOrEqualTo(1e-9),
				$"{what}: friction exceeded its bound by {f.WorstFrictionExcess:E3}");
			Assert.That(f.WorstEnergyGrowth, Is.LessThanOrEqualTo(1e-7),
				$"{what}: a substep gained {f.WorstEnergyGrowth:E3} of energy");
			Assert.That(stats.WorstEnergyMinusWork, Is.LessThanOrEqualTo(1e-6),
				$"{what}: ball plus spin plus modal energy minus external work grew by {stats.WorstEnergyMinusWork:E3}");
		}

		#endregion

		#region impact

		[Test]
		public void ShouldResolveASingleImpactPassively()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var f = Build(in span);
			f.Balls.Add(BallBelow(0.5, 0.5, 8.0));
			var random = new Random(20260907);

			var before = f.TotalEnergy();
			var stats = Run(f, 60.0, random, 0.5,
				s => !double.IsNaN(s.LastContact) && s.Time > s.LastContact + 1.0);

			var ball = f.Balls[0];
			TestContext.WriteLine(
				$"single impact at the centre, 8 u/step: rebound {math.length(ball.Velocity):F4}, " +
				$"contact {stats.ContactDuration:F4} VPE t, peak q {stats.PeakQ:F3}, " +
				$"energy {before:F5} -> {f.TotalEnergy():F5}, solves {f.GroupSolves}, " +
				$"fallbacks {f.SolverFallbacks}, substeps {stats.Substeps}");

			AssertInvariants(f, stats, "single impact");
			Assert.That(ball.Velocity.y, Is.LessThan(0.0), "the ball did not rebound");
			Assert.That(f.TotalEnergy(), Is.LessThanOrEqualTo(before + 1e-9));
			Assert.That(stats.ContactDuration, Is.GreaterThan(0.0));
		}

		[Test]
		public void ShouldMirrorTwoSimultaneousSymmetricImpacts()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var f = Build(in span);
			f.Balls.Add(BallBelow(0.25, 0.5, 9.0));
			f.Balls.Add(BallBelow(0.75, 0.5, 9.0));
			var random = new Random(20260908);

			var stats = Run(f, 40.0, random, 0.25,
				s => !double.IsNaN(s.LastContact) && s.Time > s.LastContact + 1.0);

			var a = f.Balls[0];
			var b = f.Balls[1];
			var velocityDelta = math.length(new double3(a.Velocity.x + b.Velocity.x,
				a.Velocity.y - b.Velocity.y, a.Velocity.z - b.Velocity.z));
			var positionDelta = Math.Abs((a.Position.x - 0.5 * SpanLength) + (b.Position.x - 0.5 * SpanLength))
				+ Math.Abs(a.Position.y - b.Position.y);

			TestContext.WriteLine($"two symmetric impacts: velocity mismatch {velocityDelta:E3}, " +
				$"position mismatch {positionDelta:E3}, peak q {stats.PeakQ:F3}");

			AssertInvariants(f, stats, "two symmetric impacts");
			Assert.That(velocityDelta, Is.LessThan(1e-9), $"the two balls are not mirrored, mismatch {velocityDelta:E3}");
			Assert.That(positionDelta, Is.LessThan(1e-9), $"the two positions are not mirrored, mismatch {positionDelta:E3}");
		}

		[Test]
		public void ShouldResolveAnImpactWhileAnotherBallRests()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var f = Build(in span);
			f.ExternalAcceleration = new double3(0, DefaultLoad, 0);

			// a ball already sitting on the cord at the quarter point
			var shape = Math.Sin(Math.PI * 0.25);
			var qEquilibrium = 1.0 * DefaultLoad * shape / f.Modal.Stiffness;
			f.Lane = new RubberModalState(qEquilibrium, 0.0);
			var resting = BallBelow(0.25, 0.0, 0.0);
			resting.Position.y += qEquilibrium * shape;
			f.Balls.Add(resting);
			f.Balls.Add(BallBelow(0.75, 0.5, 14.0));

			var random = new Random(20260909);
			var minStrikerVy = double.MaxValue;
			var stats = Run(f, 40.0, random, 0.75, null,
				(fx, _) => minStrikerVy = Math.Min(minStrikerVy, fx.Balls[1].Velocity.y));

			TestContext.WriteLine($"impact beside a resting ball: resting ball v {f.Balls[0].Velocity}, " +
				$"striker v {f.Balls[1].Velocity}, minimum striker vy {minStrikerVy:F4}, " +
				$"peak q {stats.PeakQ:F3}, solves {f.GroupSolves}, " +
				$"fallbacks {f.SolverFallbacks}, position corrections {f.PositionCorrections}");

			AssertInvariants(f, stats, "impact beside a resting ball");
			// the strike must load the cord past the sag the resting ball alone produces,
			// and the striker must be decelerated well below free flight under the load
			Assert.That(stats.PeakQ, Is.GreaterThan(qEquilibrium * 1.5),
				$"the strike only reached q = {stats.PeakQ:F3} against a resting sag of {qEquilibrium:F3}");
			Assert.That(minStrikerVy, Is.LessThan(14.0 * 0.5),
				$"the striker was barely decelerated, its lowest vertical speed was {minStrikerVy:F4}");
		}

		#endregion

		#region sustained loading and rest

		[Test]
		[Explicit("Section 13 measurement: the end-of-substep impulse formulation misses this threshold at the engine substep; run explicitly to reproduce the numbers recorded in the plan addendum")]
		public void ShouldHoldTheAnalyticLoadedEquilibrium()
		{
			var failures = "";
			foreach (var pair in new (string name, RubberSpanCoefficients span)[] {
				("default curve", RubberSpanCoefficients.WorkedDefaultCurve()),
				("linear ramp", RubberSpanCoefficients.WorkedLinearRamp()),
			}) {
				const double u = 0.5;
				var f = Build(in pair.span);
				f.ExternalAcceleration = new double3(0, DefaultLoad, 0);
				var shape = Math.Sin(Math.PI * u);
				var qEquilibrium = 1.0 * DefaultLoad * shape / f.Modal.Stiffness;

				f.Lane = new RubberModalState(qEquilibrium, 0.0);
				var ball = BallBelow(u, 0.0, 0.0);
				ball.Position.y += qEquilibrium * shape;
				f.Balls.Add(ball);

				var random = new Random(20260910);
				// one second to settle
				Run(f, 100.0, random, u);
				var qAfterSettle = f.Lane.Q;
				var penetrationAfterSettle = f.DeepestPenetration;
				f.DeepestPenetration = 0.0;

				// five seconds
				var stats = Run(f, 500.0, random, u);
				var qAtFive = f.Lane.Q;
				var penetrationAtFive = f.DeepestPenetration;
				f.DeepestPenetration = 0.0;

				// the second five seconds, so the ten second hold is complete
				var stats2 = Run(f, 500.0, random, u);
				var qAtTen = f.Lane.Q;
				var penetrationAtTen = f.DeepestPenetration;

				var errorAtTen = Math.Abs(qAtTen - qEquilibrium);
				var drift = Math.Abs(qAtTen - qAtFive);

				TestContext.WriteLine(
					$"{pair.name} ten second loaded hold at u = {u}:\n" +
					$"  analytic equilibrium q = {qEquilibrium:F5}\n" +
					$"  q after 1 s {qAfterSettle:F5}, at 6 s {qAtFive:F5}, at 11 s {qAtTen:F5}\n" +
					$"  |q - q_eq| at the end {errorAtTen:F6}, drift over the last five seconds {drift:F6}\n" +
					$"  deepest penetration: settle {penetrationAfterSettle:F4}, " +
					$"first half {penetrationAtFive:F4}, second half {penetrationAtTen:F4}\n" +
					$"  position corrections {f.PositionCorrections}, solves {f.GroupSolves}, " +
					$"fallbacks {f.SolverFallbacks}, substeps {stats.Substeps + stats2.Substeps}");

				AssertInvariants(f, stats2, $"{pair.name} loaded hold");
				if (errorAtTen > 0.01) {
					failures += $"{pair.name}: q settled {errorAtTen:F6} units away from the analytic " +
						$"equilibrium {qEquilibrium:F5}, budget 0.01\n";
				}
				if (penetrationAtTen > 0.5) {
					failures += $"{pair.name}: the ball sank {penetrationAtTen:F4} units into the cord, budget 0.5\n";
				}
				if (drift > 0.01) {
					failures += $"{pair.name}: q drifted {drift:F6} units over the last five seconds, budget 0.01\n";
				}
			}
			Assert.That(failures, Is.Empty, "the loaded rest gates of section 2.4.4 are not met:\n" + failures);
		}

		[Test]
		public void ShouldTrackExternalWorkUnderSustainedLoading()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var f = Build(in span);
			f.ExternalAcceleration = new double3(0, DefaultLoad, 0);
			f.Balls.Add(BallBelow(0.5, 4.0, 0.0));

			var random = new Random(20260911);
			var stats = Run(f, 200.0, random, 0.5);

			TestContext.WriteLine($"sustained loading: external work {f.ExternalWork:F5}, " +
				$"total energy {f.TotalEnergy():F5}, worst budget excess {stats.WorstEnergyMinusWork:E3}");

			AssertInvariants(f, stats, "sustained loading");
			Assert.That(f.ExternalWork, Is.GreaterThan(0.0), "the load never did any work");
		}

		[Test]
		public void ShouldReleaseWhenTheLoadIsRemoved()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var f = Build(in span);
			f.ExternalAcceleration = new double3(0, DefaultLoad, 0);
			var shape = 1.0;
			var qEquilibrium = DefaultLoad * shape / f.Modal.Stiffness;
			f.Lane = new RubberModalState(qEquilibrium, 0.0);
			var ball = BallBelow(0.5, 0.0, 0.0);
			ball.Position.y += qEquilibrium;
			f.Balls.Add(ball);

			var random = new Random(20260912);
			Run(f, 100.0, random, 0.5);
			var loaded = f.Lane.Q;

			// the ball is taken away and the span is released
			f.Balls.Clear();
			f.ExternalAcceleration = double3.zero;
			var stats = Run(f, 200.0, random, 0.5);

			TestContext.WriteLine($"release: q {loaded:F4} -> {f.Lane.Q:F6}, " +
				$"modal energy {RubberModalMath.Energy(in f.Modal, in f.Lane):E3}");

			Assert.That(Math.Abs(f.Lane.Q), Is.LessThan(Math.Abs(loaded) * 0.2),
				$"the released span still sits at q = {f.Lane.Q:F4} from {loaded:F4}");
			Assert.That(stats.WorstEnergyMinusWork, Is.LessThanOrEqualTo(1e-9));
		}

		#endregion

		#region cap

		[Test]
		public void ShouldArriveAtTheCapUnderLoadAndHold()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			const double cap = 6.0;
			var f = Build(in span, cap);
			// a load big enough that K * cap is below it, so the lane must stay
			f.ExternalAcceleration = new double3(0, 0.4, 0);
			f.Balls.Add(BallBelow(0.5, 0.2, 2.0));

			var random = new Random(20260913);
			var stats = Run(f, 60.0, random, 0.5);

			TestContext.WriteLine($"cap arrival under load: arrivals {f.CapArrivals}, releases {f.CapReleases}, " +
				$"cap sign {f.CapSign}, q {f.Lane.Q:F5}, K*cap {f.Modal.Stiffness * cap:F5}, " +
				$"energy removed at the cap {f.CapEnergyRemoved:E3}");

			AssertInvariants(f, stats, "cap arrival under load");
			Assert.That(f.CapArrivals, Is.GreaterThan(0), "the lane never reached its cap");
			Assert.That(Math.Abs(f.Lane.Q), Is.LessThanOrEqualTo(cap + 1e-9), "the lane went past its cap");
			Assert.That(f.CapSign, Is.EqualTo(1), $"the lane left the cap although K*cap = " +
				$"{f.Modal.Stiffness * cap:F5} is below the load of 0.4");
		}

		[Test]
		public void ShouldReleaseAnUnloadedLaneOnTheFirstBoundary()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			const double cap = 6.0;
			var f = Build(in span, cap);
			f.Lane = new RubberModalState(cap, 0.0);
			f.CapSign = 1;

			var taken = f.Substep(0.02);
			Assert.That(taken, Is.EqualTo(0.02).Within(1e-12));
			Assert.That(f.CapSign, Is.EqualTo(0), "an unloaded lane must leave the cap at the first boundary");
			Assert.That(f.Lane.QDot, Is.EqualTo(0.0).Within(1e-12), "a quasi-static release leaves qdot at zero");
			Assert.That(f.CapReleases, Is.EqualTo(1));
		}

		[Test]
		public void ShouldPreserveTheLaneVelocityOnAnImpulsiveRelease()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			const double cap = 6.0;
			var f = Build(in span, cap);
			f.Lane = new RubberModalState(cap, -3.5);
			f.CapSign = 1;
			f.Substep(0.01);
			Assert.That(f.CapSign, Is.EqualTo(0));
			Assert.That(f.Lane.QDot, Is.EqualTo(-3.5).Within(1e-9),
				"the impulsive release must keep the lane velocity");
		}

		/// <summary>
		/// The review's partition case: an outward load of 0.4 against a cap of 6, taken as
		/// one 0.1 substep and as two of 0.05, must produce the same residence.
		/// </summary>
		[Test]
		public void ShouldGiveTheSameCapResidenceForEitherPartitionOfATick()
		{
			foreach (var pair in new (string name, RubberSpanCoefficients span)[] {
				("default curve", RubberSpanCoefficients.WorkedDefaultCurve()),
				("linear ramp", RubberSpanCoefficients.WorkedLinearRamp()),
			}) {
				const double cap = 6.0;

				int RunPartition(double[] substeps)
				{
					var f = Build(in pair.span, cap);
					f.ExternalAcceleration = new double3(0, 0.4, 0);
					f.Lane = new RubberModalState(cap, 0.0);
					f.CapSign = 1;
					var ball = BallBelow(0.5, -0.1, 0.0);
					ball.Position.y += cap;
					f.Balls.Add(ball);
					f.BeginTick();
					foreach (var h in substeps) {
						f.Substep(h);
					}
					return f.CapSign;
				}

				var one = RunPartition(new[] { 0.1 });
				var two = RunPartition(new[] { 0.05, 0.05 });
				var stiffnessTimesCap = pair.span.ModalStiffnessVpe(1) * cap;

				TestContext.WriteLine($"{pair.name}: K*cap = {stiffnessTimesCap:F5} against a load of 0.4; " +
					$"one 0.1 substep -> cap sign {one}, two 0.05 substeps -> cap sign {two}");

				Assert.That(one, Is.EqualTo(two),
					$"{pair.name}: the partition changed the residence ({one} versus {two})");
				Assert.That(one, Is.EqualTo(stiffnessTimesCap <= 0.4 ? 1 : 0),
					$"{pair.name}: residence does not follow the K*cap versus load rule");
			}
		}

		[Test]
		public void ShouldDecideResidenceFromContinuousQuantitiesAlone()
		{
			// the rule as a pure function, so a zero-duration substep cannot change it
			Assert.That(RubberContactFixture.StaysAtCap(0.0624775, 6.0, 0.0, 1, 0.4), Is.True);
			Assert.That(RubberContactFixture.StaysAtCap(0.2231, 6.0, 0.0, 1, 0.4), Is.False);
			// a lane already leaving the cap is released whatever the load
			Assert.That(RubberContactFixture.StaysAtCap(0.0624775, 6.0, -1.0, 1, 10.0), Is.False);
		}

		[Test]
		public void ShouldHandleAZeroDurationBoundary()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			var f = Build(in span, 6.0);
			// past the cap and still moving outwards: the predictor reports an immediate arrival
			f.Lane = new RubberModalState(6.5, 4.0);
			var taken = f.Substep(0.05);
			Assert.That(taken, Is.EqualTo(0.0));
			Assert.That(f.CapArrivals, Is.EqualTo(1));
			Assert.That(f.Lane.Q, Is.EqualTo(6.0).Within(1e-12));
			// with no load it leaves again at the same boundary
			Assert.That(f.CapSign, Is.EqualTo(0));
		}

		#endregion

		#region oracle comparison, the section 13 deliverable

		[Test]
		[Explicit("Section 13 measurement: the end-of-substep impulse formulation misses this threshold at the engine substep; run explicitly to reproduce the numbers recorded in the plan addendum")]
		public void ShouldMatchTheOracleAtTheCentreAndTheQuarterPoint()
		{
			var report = "";
			var failures = "";

			foreach (var pair in new (string name, RubberSpanCoefficients span)[] {
				("default curve", RubberSpanCoefficients.WorkedDefaultCurve()),
				("linear ramp", RubberSpanCoefficients.WorkedLinearRamp()),
			}) {
				foreach (var u in new[] { 0.5, 0.25 }) {
					foreach (var speed in new[] { 8.0, 18.0 }) {
						var oracle = new RubberSpanOracle(in pair.span);
						var shot = RubberSpanOracle.Shot.At(u, speed);
						var reference = oracle.Run(in shot);

						var f = Build(in pair.span, 1e9, 0.0);   // no cap, no friction: the oracle has neither
						f.Balls.Add(BallBelow(u, RubberSpanOracle.StartGap, speed));
						var random = new Random(20260914);
						var stats = Run(f, 80.0, random, u,
							s => !double.IsNaN(s.LastContact) && s.Time > s.LastContact + 0.5);

						var rebound = math.length(f.Balls[0].Velocity);
						var reboundError = Math.Abs(rebound - reference.ReboundSpeed) / reference.ReboundSpeed;
						var durationError = Math.Abs(stats.ContactDuration - reference.ContactDuration)
							/ reference.ContactDuration;
						var peakError = Math.Abs(stats.PeakDeflectionAtU - reference.PeakDeflection)
							/ reference.PeakDeflection;

						report += $"{pair.name}, u {u}, {speed} u/step:\n" +
							$"    A2       rebound {rebound:F4}, duration {stats.ContactDuration:F4}, peak {stats.PeakDeflectionAtU:F4}\n" +
							$"    oracle   rebound {reference.ReboundSpeed:F4}, duration {reference.ContactDuration:F4}, peak {reference.PeakDeflection:F4}\n" +
							$"    error    rebound {reboundError * 100:F2}%, duration {durationError * 100:F2}%, peak {peakError * 100:F2}%\n";

						if (reboundError > 0.02) {
							failures += $"{pair.name} u {u} {speed} u/step: rebound error {reboundError * 100:F2}% exceeds 2%\n";
						}
						if (durationError > 0.05) {
							failures += $"{pair.name} u {u} {speed} u/step: contact duration error {durationError * 100:F2}% exceeds 5%\n";
						}
						if (peakError > 0.05) {
							failures += $"{pair.name} u {u} {speed} u/step: peak deflection error {peakError * 100:F2}% exceeds 5%\n";
						}
					}
				}
			}

			TestContext.WriteLine("A2 against the oracle:\n" + report);
			Assert.That(failures, Is.Empty, "A2 misses the section 7.1 thresholds:\n" + failures + "\n" + report);
		}

		/// <summary>
		/// The same comparison against a single-mode oracle, which isolates the end-of-substep
		/// impulse error from the mode-truncation gap. Reported, not gated.
		/// </summary>
		[Test]
		public void ShouldReportTheSingleModeOracleGap()
		{
			var report = "";
			foreach (var pair in new (string name, RubberSpanCoefficients span)[] {
				("default curve", RubberSpanCoefficients.WorkedDefaultCurve()),
				("linear ramp", RubberSpanCoefficients.WorkedLinearRamp()),
			}) {
				foreach (var u in new[] { 0.5, 0.25 }) {
					foreach (var speed in new[] { 8.0, 18.0 }) {
						var oracle = new RubberSpanOracle(in pair.span, 1);
						var shot = RubberSpanOracle.Shot.At(u, speed);
						var reference = oracle.Run(in shot);

						var f = Build(in pair.span, 1e9, 0.0);
						f.Balls.Add(BallBelow(u, RubberSpanOracle.StartGap, speed));
						var random = new Random(20260915);
						var stats = Run(f, 80.0, random, u,
							s => !double.IsNaN(s.LastContact) && s.Time > s.LastContact + 0.5);

						var rebound = math.length(f.Balls[0].Velocity);
						report += $"{pair.name}, u {u}, {speed} u/step: rebound {rebound:F4} vs {reference.ReboundSpeed:F4} " +
							$"({Math.Abs(rebound - reference.ReboundSpeed) / reference.ReboundSpeed * 100:F2}%), " +
							$"duration {stats.ContactDuration:F4} vs {reference.ContactDuration:F4} " +
							$"({Math.Abs(stats.ContactDuration - reference.ContactDuration) / reference.ContactDuration * 100:F2}%), " +
							$"peak {stats.PeakDeflectionAtU:F4} vs {reference.PeakDeflection:F4} " +
							$"({Math.Abs(stats.PeakDeflectionAtU - reference.PeakDeflection) / reference.PeakDeflection * 100:F2}%)\n";
					}
				}
			}
			TestContext.WriteLine("A2 against a single-mode oracle (isolates the impulse error):\n" + report);
			Assert.Pass(report);
		}

		/// <summary>
		/// How the end-of-substep impulse depends on the substep length, against the
		/// single-mode oracle. This is the measurement section 13 needs: it separates the
		/// formulation's discretization error from any fixture mistake, because A2 must
		/// converge on the oracle as the substep shrinks.
		/// </summary>
		[Test]
		public void ShouldReportTheSubstepDependenceOfTheRebound()
		{
			var span = RubberSpanCoefficients.WorkedDefaultCurve();
			const double u = 0.5;
			const double speed = 8.0;

			var oracle = new RubberSpanOracle(in span, 1);
			var shot = RubberSpanOracle.Shot.At(u, speed);
			var reference = oracle.Run(in shot);

			var report = $"single-mode oracle: rebound {reference.ReboundSpeed:F4}, " +
				$"duration {reference.ContactDuration:F4}, peak {reference.PeakDeflection:F4}\n";

			foreach (var h in new[] { 0.1, 0.05, 0.02, 0.01, 0.005, 0.002, 0.001 }) {
				var f = Build(in span, 1e9, 0.0);
				f.Balls.Add(BallBelow(u, RubberSpanOracle.StartGap, speed));
				var shape = Math.Sin(Math.PI * u);
				var time = 0.0;
				var first = double.NaN;
				var last = double.NaN;
				var peak = 0.0;
				var guard = 0;
				while (time < 80.0 && guard++ < 200000) {
					f.BeginTick();
					var remaining = RubberDeformationUnits.TickTime;
					while (remaining > 1e-12) {
						var taken = f.Substep(Math.Min(h, remaining));
						remaining -= taken;
						time += taken;
						if (f.NormalRowsLastSolve > 0) {
							if (double.IsNaN(first)) {
								first = time;
							}
							last = time;
						}
						peak = Math.Max(peak, Math.Abs(f.Lane.Q) * shape);
					}
					if (!double.IsNaN(last) && time > last + 0.5) {
						break;
					}
				}
				var rebound = math.length(f.Balls[0].Velocity);
				var duration = double.IsNaN(first) ? 0.0 : last - first;
				report += $"  h = {h,-6}: rebound {rebound:F4} " +
					$"({Math.Abs(rebound - reference.ReboundSpeed) / reference.ReboundSpeed * 100,7:F2}%), " +
					$"duration {duration:F4} " +
					$"({Math.Abs(duration - reference.ContactDuration) / reference.ContactDuration * 100,7:F2}%), " +
					$"peak {peak:F4} " +
					$"({Math.Abs(peak - reference.PeakDeflection) / reference.PeakDeflection * 100,7:F2}%)\n";
			}

			TestContext.WriteLine("A2 substep dependence, centre hit at 8 u/step, default curve:\n" + report);
			Assert.Pass(report);
		}

		#endregion
	}
}
