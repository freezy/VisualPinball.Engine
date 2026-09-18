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
	/// Section 2.4.3 and 7.1: the per-span contact group solver.
	/// </summary>
	public class RubberContactGroupSolverTests
	{
		private const int RandomGroups = 10000;

		[Test]
		public void ShouldSolveRandomGroupsFeasiblyAndComplementarily()
		{
			var random = new Random(20260905);

			var terminalPolicies = 0;
			var pivotLimits = 0;
			var maxPivots = 0;
			var maxRows = 0;
			var worstUnilateralResidual = 0.0;
			var worstFrictionExcess = 0.0;
			var worstEnergyGain = 0.0;
			var worstEnumerationVelocity = 0.0;
			var enumeratedUnique = 0;
			var enumeratedBoxedUnique = 0;
			var worstBoxedEnumeration = 0.0;
			var alternationHistogram = new int[RubberContactGroupSolver.MaxAlternations + 1];

			for (var trial = 0; trial < RandomGroups; trial++) {
				BuildRandomGroup(random, out var a, out var b, out var rows);
				var n = rows.Length;
				maxRows = Math.Max(maxRows, n);

				var solution = RubberContactGroupSolver.Solve(a, b, rows);
				var m = RubberContactGroupSolver.Regularize(a, n);

				alternationHistogram[solution.Alternations]++;
				if (solution.TerminalPolicy) {
					terminalPolicies++;
				}
				if (solution.PivotLimitHit) {
					pivotLimits++;
				}
				maxPivots = Math.Max(maxPivots, solution.Pivots);

				var unilateral = new List<int>();
				var friction = new List<int>();
				for (var i = 0; i < n; i++) {
					if (rows[i].IsUnilateral) {
						unilateral.Add(i);
					} else {
						friction.Add(i);
					}
				}

				// feasibility and complementarity of the normal and stop rows, after the
				// friction pass, as section 7.1 demands
				foreach (var i in unilateral) {
					Assert.That(solution.Impulses[i], Is.GreaterThanOrEqualTo(-1e-9),
						$"trial {trial} row {i}: negative normal impulse {solution.Impulses[i]}");
					Assert.That(solution.PostVelocities[i], Is.GreaterThanOrEqualTo(-RubberContactGroupSolver.ComplementarityTolerance),
						$"trial {trial} row {i}: post-solve relative normal velocity {solution.PostVelocities[i]}");
					worstUnilateralResidual = Math.Max(worstUnilateralResidual, solution.RowResiduals[i]);
					Assert.That(solution.RowResiduals[i], Is.LessThanOrEqualTo(RubberContactGroupSolver.ComplementarityTolerance),
						$"trial {trial} row {i}: complementarity residual {solution.RowResiduals[i]:E3}");
				}

				// friction within bounds
				foreach (var i in friction) {
					var excess = Math.Abs(solution.Impulses[i]) - solution.FrictionBounds[i];
					worstFrictionExcess = Math.Max(worstFrictionExcess, excess);
					Assert.That(excess, Is.LessThanOrEqualTo(1e-9 * Math.Max(1.0, solution.FrictionBounds[i])),
						$"trial {trial} row {i}: friction {solution.Impulses[i]} exceeds its bound {solution.FrictionBounds[i]} by {excess:E3}");
				}

				// the group is passive
				var energy = RubberContactGroupSolver.EnergyChange(m, b, solution.Impulses);
				worstEnergyGain = Math.Max(worstEnergyGain, energy);
				Assert.That(energy, Is.LessThanOrEqualTo(1e-9 * Scale(b)),
					$"trial {trial}: the group added {energy:E3} of energy");

				// agreement with an enumerated active set where the solution is unique
				if (unilateral.Count <= 6) {
					var enumeration = RubberContactGroupReference.EnumerateUnilateral(m, b, solution.Impulses, unilateral.ToArray());
					if (enumeration.Unique) {
						enumeratedUnique++;
						var reference = new double[n];
						Array.Copy(solution.Impulses, reference, n);
						for (var k = 0; k < unilateral.Count; k++) {
							reference[unilateral[k]] = enumeration.Impulses[k];
						}
						var referenceVelocities = RubberContactGroupSolver.Multiply(m, reference, b);
						foreach (var i in unilateral) {
							var delta = Math.Abs(referenceVelocities[i] - solution.PostVelocities[i]);
							worstEnumerationVelocity = Math.Max(worstEnumerationVelocity, delta);
							Assert.That(delta, Is.LessThanOrEqualTo(1e-4),
								$"trial {trial} row {i}: post-solve velocity {solution.PostVelocities[i]} " +
								$"versus the enumerated {referenceVelocities[i]}");
						}
					}
				}

				// and the friction pass against the enumerated boxed solution
				if (friction.Count >= 1 && friction.Count <= 4) {
					var lo = new double[n];
					var hi = new double[n];
					foreach (var i in friction) {
						lo[i] = -solution.FrictionBounds[i];
						hi[i] = solution.FrictionBounds[i];
					}
					var boxed = RubberContactGroupReference.EnumerateBoxed(m, b, solution.Impulses, friction.ToArray(), lo, hi);
					if (boxed.Unique && !solution.TerminalPolicy) {
						enumeratedBoxedUnique++;
						var reference = new double[n];
						Array.Copy(solution.Impulses, reference, n);
						for (var k = 0; k < friction.Count; k++) {
							reference[friction[k]] = boxed.Impulses[k];
						}
						var referenceVelocities = RubberContactGroupSolver.Multiply(m, reference, b);
						foreach (var i in friction) {
							var delta = Math.Abs(referenceVelocities[i] - solution.PostVelocities[i]);
							worstBoxedEnumeration = Math.Max(worstBoxedEnumeration, delta);
						}
					}
				}
			}

			TestContext.WriteLine(
				$"random groups: {RandomGroups}, max rows {maxRows}\n" +
				$"  terminal policy: {terminalPolicies} ({100.0 * terminalPolicies / RandomGroups:F2}%)\n" +
				$"  alternations 1/2/3/4: {alternationHistogram[1]}/{alternationHistogram[2]}/" +
				$"{alternationHistogram[3]}/{alternationHistogram[4]}\n" +
				$"  pivot-limit hits: {pivotLimits}, max pivots in one group: {maxPivots}\n" +
				$"  worst unilateral residual: {worstUnilateralResidual:E3}\n" +
				$"  worst friction excess: {worstFrictionExcess:E3}\n" +
				$"  worst energy gain: {worstEnergyGain:E3}\n" +
				$"  enumerated unique (unilateral): {enumeratedUnique}, worst velocity delta {worstEnumerationVelocity:E3}\n" +
				$"  enumerated unique (boxed): {enumeratedBoxedUnique}, worst velocity delta {worstBoxedEnumeration:E3}");

			Assert.That(pivotLimits, Is.EqualTo(0), $"{pivotLimits} groups exhausted the pivot budget");
			Assert.That(worstBoxedEnumeration, Is.LessThanOrEqualTo(1e-4),
				$"the friction pass disagrees with the enumerated boxed solution by {worstBoxedEnumeration:E3}");
		}

		[Test]
		public void ShouldSolveTheTwoBallSymmetricCaseExactly()
		{
			// two balls striking one span at mirrored fractions: identical diagonals,
			// identical modal coupling, mirrored pre-solve velocities
			const double invMass = 1.0;
			const double modalMass = 0.0074;
			const double g = 0.7071067811865476; // sin(pi/4) == sin(3 pi/4)

			var rows = new[] { RubberSolverRow.NormalRowOf(), RubberSolverRow.NormalRowOf() };
			var a = new double[2, 2];
			a[0, 0] = invMass + g * g / modalMass;
			a[1, 1] = invMass + g * g / modalMass;
			a[0, 1] = a[1, 0] = g * g / modalMass;
			var b = new[] { -14.0, -14.0 };

			var solution = RubberContactGroupSolver.Solve(a, b, rows);

			Assert.That(solution.Impulses[0], Is.EqualTo(solution.Impulses[1]).Within(1e-15),
				$"the two impulses are {solution.Impulses[0]} and {solution.Impulses[1]}");
			Assert.That(solution.PostVelocities[0], Is.EqualTo(0.0).Within(1e-12));
			Assert.That(solution.PostVelocities[1], Is.EqualTo(0.0).Within(1e-12));
			Assert.That(solution.Alternations, Is.EqualTo(1), "the symmetric case must fall out of the first normal pass");
			Assert.That(solution.TerminalPolicy, Is.False);
		}

		[Test]
		public void ShouldEndNormalFeasibleOnTheReviewCyclingCase()
		{
			foreach (var cross in new[] { -6.066714, 6.066714 }) {
				var a = new double[2, 2];
				a[0, 0] = 1.273279;
				a[1, 1] = 138.179305;
				a[0, 1] = a[1, 0] = cross;
				var b = new[] { -0.7, 20.0 };
				var rows = new[] {
					RubberSolverRow.NormalRowOf(),
					RubberSolverRow.FrictionRowOf(0, 0.3),
				};

				var m = RubberContactGroupSolver.Regularize(a, 2);
				var solution = RubberContactGroupSolver.Solve(a, b, rows);
				var energy = RubberContactGroupSolver.EnergyChange(m, b, solution.Impulses);

				TestContext.WriteLine(
					$"cross {cross}: lambda_n {solution.Impulses[0]:F6}, lambda_f {solution.Impulses[1]:F6}, " +
					$"bound {solution.FrictionBounds[1]:F6}, v_n {solution.PostVelocities[0]:E3}, " +
					$"v_f {solution.PostVelocities[1]:E3}, alternations {solution.Alternations}, " +
					$"terminal {solution.TerminalPolicy}, dT {energy:E3}");

				Assert.That(solution.Impulses[0], Is.GreaterThanOrEqualTo(-1e-12),
					$"cross {cross}: the normal impulse went negative");
				Assert.That(solution.PostVelocities[0], Is.GreaterThanOrEqualTo(-RubberContactGroupSolver.ComplementarityTolerance),
					$"cross {cross}: the normal row is infeasible, v_n = {solution.PostVelocities[0]:E3}");
				Assert.That(solution.RowResiduals[0], Is.LessThanOrEqualTo(RubberContactGroupSolver.ComplementarityTolerance),
					$"cross {cross}: normal residual {solution.RowResiduals[0]:E3}");
				Assert.That(Math.Abs(solution.Impulses[1]), Is.LessThanOrEqualTo(solution.FrictionBounds[1] + 1e-9),
					$"cross {cross}: friction {solution.Impulses[1]} outside its bound {solution.FrictionBounds[1]}");
				Assert.That(energy, Is.LessThanOrEqualTo(1e-9),
					$"cross {cross}: the group added {energy:E3} of energy");
			}
		}

		[Test]
		public void ShouldHoldALaneAtItsCapWithAStopRow()
		{
			const double modalMass = 0.0074;
			const double g = 1.0;

			// one ball pressing the cord outwards plus the stop row of a lane at its cap
			var rows = new[] { RubberSolverRow.NormalRowOf(), RubberSolverRow.StopRowOf() };
			var a = new double[2, 2];
			a[0, 0] = 1.0 + g * g / modalMass;
			a[1, 1] = 1.0 / modalMass;
			// J_lane is +1 for the normal row (the ball drives the lane outwards) and -1 for
			// the stop row, so the cross term is negative
			a[0, 1] = a[1, 0] = -g / modalMass;
			var b = new[] { -9.0, 0.0 };

			var solution = RubberContactGroupSolver.Solve(a, b, rows);

			Assert.That(solution.Impulses[0], Is.GreaterThan(0.0));
			Assert.That(solution.Impulses[1], Is.GreaterThanOrEqualTo(-1e-12));
			Assert.That(solution.PostVelocities[0], Is.GreaterThanOrEqualTo(-1e-9));
			Assert.That(solution.PostVelocities[1], Is.GreaterThanOrEqualTo(-1e-9),
				"the lane moved further past its cap");
			// with the lane pinned, the ball response is the rigid-wall one
			// 1e-4 rather than float noise: the 1e-9 relative diagonal regularization of
			// section 2.4.3 perturbs a stop-backed solve by about 5e-6 at this scale
			Assert.That(solution.Impulses[0], Is.EqualTo(9.0).Within(1e-4),
				$"the stop-backed impulse was {solution.Impulses[0]}");
		}

		[Test]
		public void ShouldLeaveSeparatingRowsAtZero()
		{
			var rows = new[] { RubberSolverRow.NormalRowOf(), RubberSolverRow.FrictionRowOf(0, 0.4) };
			var a = new double[2, 2];
			a[0, 0] = 1.1;
			a[1, 1] = 3.5;
			a[0, 1] = a[1, 0] = 0.2;
			var b = new[] { 4.0, 1.0 };

			var solution = RubberContactGroupSolver.Solve(a, b, rows);
			Assert.That(solution.Impulses[0], Is.EqualTo(0.0).Within(1e-12));
			Assert.That(solution.Impulses[1], Is.EqualTo(0.0).Within(1e-12), "friction with a zero bound must stay at zero");
			Assert.That(solution.PostVelocities[0], Is.EqualTo(4.0).Within(1e-12));
		}

		#region helpers

		private static double Scale(double[] b)
		{
			var s = 1.0;
			foreach (var v in b) {
				s = Math.Max(s, Math.Abs(v));
			}
			return s * s;
		}

		/// <summary>
		/// A random positive semi-definite group built the way the plan assembles one:
		/// <c>A = J W J^T</c> from random Jacobian rows and positive inverse masses, plus
		/// friction rows that depend on their normal row and an occasional stop row.
		/// </summary>
		private static void BuildRandomGroup(Random random, out double[,] a, out double[] b, out RubberSolverRow[] rows)
		{
			var withStop = random.NextDouble() < 0.35;
			var maxPairs = withStop ? 5 : 6;
			var pairs = 1 + random.Next(maxPairs);
			var rowList = new List<RubberSolverRow>();
			for (var p = 0; p < pairs; p++) {
				var normal = rowList.Count;
				rowList.Add(RubberSolverRow.NormalRowOf());
				rowList.Add(RubberSolverRow.FrictionRowOf(normal, random.NextDouble() * 1.2));
			}
			if (withStop) {
				rowList.Add(RubberSolverRow.StopRowOf());
			}
			rows = rowList.ToArray();
			var n = rows.Length;

			const int dof = 10;
			var j = new double[n, dof];
			var w = new double[dof];
			for (var d = 0; d < dof; d++) {
				w[d] = 0.02 + random.NextDouble() * 140.0;   // inverse masses, ball and modal alike
			}
			for (var i = 0; i < n; i++) {
				for (var d = 0; d < dof; d++) {
					// sparse Jacobians, as real rows are
					j[i, d] = random.NextDouble() < 0.45 ? random.NextDouble() * 2.0 - 1.0 : 0.0;
				}
				var any = false;
				for (var d = 0; d < dof; d++) {
					any |= j[i, d] != 0.0;
				}
				if (!any) {
					j[i, random.Next(dof)] = 1.0;
				}
			}

			a = new double[n, n];
			for (var i = 0; i < n; i++) {
				for (var k = 0; k < n; k++) {
					var acc = 0.0;
					for (var d = 0; d < dof; d++) {
						acc += j[i, d] * w[d] * j[k, d];
					}
					a[i, k] = acc;
				}
			}

			b = new double[n];
			for (var i = 0; i < n; i++) {
				b[i] = rows[i].Kind switch {
					// normals mostly approaching, some separating
					RubberRowKind.Normal => random.NextDouble() < 0.8
						? -random.NextDouble() * 40.0
						: random.NextDouble() * 10.0,
					RubberRowKind.Stop => random.NextDouble() < 0.5 ? 0.0 : -random.NextDouble() * 20.0,
					_ => random.NextDouble() * 60.0 - 30.0,
				};
			}
		}

		#endregion
	}
}
