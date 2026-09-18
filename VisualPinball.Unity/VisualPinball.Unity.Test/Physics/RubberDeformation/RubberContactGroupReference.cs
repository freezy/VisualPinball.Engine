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

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// Brute-force active-set reference for the group solver, used only by tests. It
	/// enumerates every active set of the subproblem and keeps the feasible,
	/// complementary ones, so it says both what the answer is and whether it is unique.
	/// </summary>
	internal static class RubberContactGroupReference
	{
		internal const double FeasibilityTolerance = 1e-9;

		internal sealed class Enumeration
		{
			internal bool Found;
			internal bool Unique;
			internal double[] Impulses;
			internal int SolutionCount;
		}

		/// <summary>
		/// Enumerate the unilateral subproblem (normal and stop rows) with every other row
		/// held fixed: 2^k active sets, k = number of unilateral rows.
		/// </summary>
		internal static Enumeration EnumerateUnilateral(double[,] m, double[] b, double[] fixedLambda, int[] idx)
		{
			var n = idx.Length;
			var q = ReducedRhs(m, b, fixedLambda, idx);
			var result = new Enumeration { Impulses = new double[n] };
			var found = new List<double[]>();

			for (var mask = 0; mask < 1 << n; mask++) {
				var active = new List<int>();
				for (var k = 0; k < n; k++) {
					if ((mask & (1 << k)) != 0) {
						active.Add(k);
					}
				}
				var z = new double[n];
				if (active.Count > 0) {
					var size = active.Count;
					var a = new double[size, size];
					var rhs = new double[size];
					for (var r = 0; r < size; r++) {
						for (var c = 0; c < size; c++) {
							a[r, c] = m[idx[active[r]], idx[active[c]]];
						}
						rhs[r] = -q[active[r]];
					}
					if (!Dense.Solve(a, rhs, size)) {
						continue;
					}
					for (var r = 0; r < size; r++) {
						z[active[r]] = rhs[r];
					}
				}
				// feasibility
				var ok = true;
				for (var k = 0; k < n && ok; k++) {
					if (z[k] < -FeasibilityTolerance) {
						ok = false;
					}
				}
				if (!ok) {
					continue;
				}
				for (var k = 0; k < n && ok; k++) {
					var w = q[k];
					for (var l = 0; l < n; l++) {
						w += m[idx[k], idx[l]] * z[l];
					}
					if (w < -FeasibilityTolerance) {
						ok = false;
					}
					if (z[k] > FeasibilityTolerance && Math.Abs(w) > 1e-6 * Math.Max(1.0, Math.Abs(z[k]))) {
						ok = false;
					}
				}
				if (!ok) {
					continue;
				}
				var duplicate = false;
				foreach (var prior in found) {
					var same = true;
					for (var k = 0; k < n && same; k++) {
						if (Math.Abs(prior[k] - z[k]) > 1e-7 * Math.Max(1.0, Math.Abs(prior[k]))) {
							same = false;
						}
					}
					if (same) {
						duplicate = true;
						break;
					}
				}
				if (!duplicate) {
					found.Add(z);
				}
			}

			result.SolutionCount = found.Count;
			result.Found = found.Count > 0;
			result.Unique = found.Count == 1;
			if (result.Found) {
				result.Impulses = found[0];
			}
			return result;
		}

		/// <summary>
		/// Enumerate the boxed subproblem (friction rows with frozen bounds) with every
		/// other row held fixed: 3^k states, each row at its lower bound, at its upper
		/// bound, or free.
		/// </summary>
		internal static Enumeration EnumerateBoxed(double[,] m, double[] b, double[] fixedLambda,
			int[] idx, double[] lo, double[] hi)
		{
			var n = idx.Length;
			var q = ReducedRhs(m, b, fixedLambda, idx);
			var result = new Enumeration { Impulses = new double[n] };
			var found = new List<double[]>();

			var states = 1;
			for (var k = 0; k < n; k++) {
				states *= 3;
			}

			for (var code = 0; code < states; code++) {
				var z = new double[n];
				var freeRows = new List<int>();
				var c = code;
				var ok = true;
				for (var k = 0; k < n; k++) {
					var s = c % 3;
					c /= 3;
					switch (s) {
						case 0: z[k] = lo[idx[k]]; break;
						case 1: z[k] = hi[idx[k]]; break;
						default: freeRows.Add(k); break;
					}
				}
				if (freeRows.Count > 0) {
					var size = freeRows.Count;
					var a = new double[size, size];
					var rhs = new double[size];
					for (var r = 0; r < size; r++) {
						for (var cc = 0; cc < size; cc++) {
							a[r, cc] = m[idx[freeRows[r]], idx[freeRows[cc]]];
						}
						var acc = q[freeRows[r]];
						for (var l = 0; l < n; l++) {
							if (!freeRows.Contains(l)) {
								acc += m[idx[freeRows[r]], idx[l]] * z[l];
							}
						}
						rhs[r] = -acc;
					}
					if (!Dense.Solve(a, rhs, size)) {
						continue;
					}
					for (var r = 0; r < size; r++) {
						z[freeRows[r]] = rhs[r];
					}
				}
				for (var k = 0; k < n && ok; k++) {
					if (z[k] < lo[idx[k]] - FeasibilityTolerance || z[k] > hi[idx[k]] + FeasibilityTolerance) {
						ok = false;
					}
				}
				if (!ok) {
					continue;
				}
				for (var k = 0; k < n && ok; k++) {
					var w = q[k];
					for (var l = 0; l < n; l++) {
						w += m[idx[k], idx[l]] * z[l];
					}
					var span = Math.Max(1.0, Math.Abs(hi[idx[k]]));
					if (z[k] > lo[idx[k]] + 1e-10 * span && z[k] < hi[idx[k]] - 1e-10 * span) {
						if (Math.Abs(w) > 1e-6) {
							ok = false;
						}
					} else if (z[k] <= lo[idx[k]] + 1e-10 * span) {
						if (w < -1e-6) {
							ok = false;
						}
					} else if (w > 1e-6) {
						ok = false;
					}
				}
				if (!ok) {
					continue;
				}
				var duplicate = false;
				foreach (var prior in found) {
					var same = true;
					for (var k = 0; k < n && same; k++) {
						if (Math.Abs(prior[k] - z[k]) > 1e-7 * Math.Max(1.0, Math.Abs(prior[k]))) {
							same = false;
						}
					}
					if (same) {
						duplicate = true;
						break;
					}
				}
				if (!duplicate) {
					found.Add(z);
				}
			}

			result.SolutionCount = found.Count;
			result.Found = found.Count > 0;
			result.Unique = found.Count == 1;
			if (result.Found) {
				result.Impulses = found[0];
			}
			return result;
		}

		private static double[] ReducedRhs(double[,] m, double[] b, double[] fixedLambda, int[] idx)
		{
			var n = idx.Length;
			var q = new double[n];
			for (var k = 0; k < n; k++) {
				var i = idx[k];
				var acc = b[i];
				for (var j = 0; j < fixedLambda.Length; j++) {
					acc += m[i, j] * fixedLambda[j];
				}
				for (var l = 0; l < n; l++) {
					acc -= m[i, idx[l]] * fixedLambda[idx[l]];
				}
				q[k] = acc;
			}
			return q;
		}

		internal static class Dense
		{
			internal static bool Solve(double[,] m, double[] rhs, int n)
			{
				for (var c = 0; c < n; c++) {
					var pivot = c;
					var best = Math.Abs(m[c, c]);
					for (var r = c + 1; r < n; r++) {
						var v = Math.Abs(m[r, c]);
						if (v > best) {
							best = v;
							pivot = r;
						}
					}
					if (best < 1e-14) {
						return false;
					}
					if (pivot != c) {
						for (var k = 0; k < n; k++) {
							(m[c, k], m[pivot, k]) = (m[pivot, k], m[c, k]);
						}
						(rhs[c], rhs[pivot]) = (rhs[pivot], rhs[c]);
					}
					var inv = 1.0 / m[c, c];
					for (var r = c + 1; r < n; r++) {
						var f = m[r, c] * inv;
						if (f == 0.0) {
							continue;
						}
						for (var k = c; k < n; k++) {
							m[r, k] -= f * m[c, k];
						}
						rhs[r] -= f * rhs[c];
					}
				}
				for (var r = n - 1; r >= 0; r--) {
					var acc = rhs[r];
					for (var k = r + 1; k < n; k++) {
						acc -= m[r, k] * rhs[k];
					}
					rhs[r] = acc / m[r, r];
				}
				return true;
			}
		}
	}
}
