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

	internal enum RubberRowKind
	{
		/// <summary>Unilateral ball-against-cord row, lambda &gt;= 0, post-solve v &gt;= 0.</summary>
		Normal,

		/// <summary>Boxed row, |lambda| &lt;= mu * lambda of <see cref="RubberSolverRow.NormalRow"/>.</summary>
		Friction,

		/// <summary>Unilateral row against a lane resting at its cap.</summary>
		Stop,
	}

	internal struct RubberSolverRow
	{
		internal RubberRowKind Kind;

		/// <summary>Friction rows only: the row whose impulse bounds this one.</summary>
		internal int NormalRow;

		/// <summary>Friction rows only.</summary>
		internal double FrictionCoefficient;

		internal static RubberSolverRow NormalRowOf() => new RubberSolverRow { Kind = RubberRowKind.Normal, NormalRow = -1 };
		internal static RubberSolverRow StopRowOf() => new RubberSolverRow { Kind = RubberRowKind.Stop, NormalRow = -1 };

		internal static RubberSolverRow FrictionRowOf(int normalRow, double mu)
			=> new RubberSolverRow { Kind = RubberRowKind.Friction, NormalRow = normalRow, FrictionCoefficient = mu };

		internal bool IsUnilateral => Kind != RubberRowKind.Friction;
	}

	internal sealed class RubberGroupSolution
	{
		internal double[] Impulses;
		internal double[] PostVelocities;
		internal double[] FrictionBounds;

		/// <summary>Per-row residual of the committed solution, in VPE velocity units.</summary>
		internal double[] RowResiduals;

		/// <summary>How many normal/friction alternations ran (1..4).</summary>
		internal int Alternations;

		/// <summary>Section 2.4.3 step 4 fired: friction halved and the normals re-solved.</summary>
		internal bool TerminalPolicy;

		/// <summary>How many halvings the terminal policy needed (the last one zeroes).</summary>
		internal int TerminalHalvings;

		/// <summary>Residual of the committed solution, in VPE velocity units.</summary>
		internal double Residual;

		/// <summary>Total Dantzig pivots across every pass, the cost measure of section 13.</summary>
		internal int Pivots;

		/// <summary>A pass ran out of pivots or hit a singular active set.</summary>
		internal bool PivotLimitHit;
	}

	/// <summary>
	/// Section 2.4.3: the per-span contact group. The Delassus matrix is assembled in full,
	/// regularized on the diagonal, and solved by an alternating exact scheme -- a
	/// Dantzig-style boxed active-set pivot on the normal and stop rows with the friction
	/// impulses fixed, then the same pivot on the friction rows with bounds frozen from the
	/// normal impulses, alternating at most four times, with a terminal policy that halves
	/// friction and re-solves the normals so the committed normal and stop rows are always
	/// exactly feasible and complementary.
	/// </summary>
	internal static class RubberContactGroupSolver
	{
		/// <summary>Section 2.4.3 step 3.</summary>
		internal const double ComplementarityTolerance = 1e-5;

		/// <summary>Section 2.4.3 step 1: diagonal regularization relative to the trace.</summary>
		internal const double DiagonalRegularizationRelative = 1e-9;

		internal const int MaxAlternations = 4;

		/// <summary>
		/// How many times the terminal policy may halve the friction before it zeroes it.
		/// Zeroing always fits, so the committed solution is always feasible on every row.
		/// </summary>
		internal const int MaxTerminalHalvings = 4;

		internal const int MaxRows = 12;

		private const double Tiny = 1e-13;

		/// <summary>
		/// Solve one group. <paramref name="a"/> is the assembled Delassus matrix (it is
		/// copied, not modified), <paramref name="b"/> the pre-solve relative velocities
		/// along each row, positive meaning separating for unilateral rows.
		/// </summary>
		internal static RubberGroupSolution Solve(double[,] a, double[] b, RubberSolverRow[] rows)
		{
			var n = rows.Length;
			if (n == 0) {
				return new RubberGroupSolution {
					Impulses = Array.Empty<double>(),
					PostVelocities = Array.Empty<double>(),
					FrictionBounds = Array.Empty<double>(),
					RowResiduals = Array.Empty<double>(),
				};
			}

			var m = Regularize(a, n);
			var lambda = new double[n];
			var lo = new double[n];
			var hi = new double[n];
			var bounds = new double[n];

			var unilateral = new List<int>();
			var friction = new List<int>();
			for (var i = 0; i < n; i++) {
				if (rows[i].IsUnilateral) {
					unilateral.Add(i);
					lo[i] = 0.0;
					hi[i] = double.PositiveInfinity;
				} else {
					friction.Add(i);
				}
			}

			var result = new RubberGroupSolution {
				Impulses = lambda,
				FrictionBounds = bounds,
			};

			var normalIdx = unilateral.ToArray();
			var frictionIdx = friction.ToArray();

			var alternations = 0;
			double residual;
			while (true) {
				alternations++;

				// 1. normal pass, friction held fixed
				RunPass(m, b, lambda, lo, hi, normalIdx, result);

				// 2. friction pass, bounds frozen from the normal impulses
				if (frictionIdx.Length > 0) {
					FreezeFrictionBounds(rows, lambda, bounds, lo, hi, frictionIdx);
					RunPass(m, b, lambda, lo, hi, frictionIdx, result);
				}

				residual = Residual(m, b, lambda, rows, lo, hi, out _);
				if (residual <= ComplementarityTolerance || alternations >= MaxAlternations) {
					break;
				}
			}

			result.Alternations = alternations;

			// 3. terminal policy: halve the friction of the last pass, run the normal pass
			// once more with it fixed, and commit. The normal and stop rows are then exactly
			// feasible and complementary by construction; friction stays inside the box it
			// was solved against and is only approximate against the new normal impulses.
			if (residual > ComplementarityTolerance) {
				result.TerminalPolicy = true;
				// The halving is repeated until the committed friction fits the box the
				// committed normal impulses imply, with a final zeroing that always does.
				// Halving once can leave friction above a bound that the re-solved normals
				// shrank, and section 7.1 asks for friction inside its bounds as well as
				// exactly complementary normal rows.
				for (var round = 0; round <= MaxTerminalHalvings; round++) {
					var last = round == MaxTerminalHalvings;
					for (var k = 0; k < frictionIdx.Length; k++) {
						lambda[frictionIdx[k]] = last ? 0.0 : lambda[frictionIdx[k]] * 0.5;
					}
					RunPass(m, b, lambda, lo, hi, normalIdx, result);
					FreezeFrictionBounds(rows, lambda, bounds, lo, hi, frictionIdx);
					result.TerminalHalvings = round + 1;

					var fits = true;
					for (var k = 0; k < frictionIdx.Length && fits; k++) {
						var i = frictionIdx[k];
						fits = Math.Abs(lambda[i]) <= hi[i] + Tiny * Math.Max(1.0, hi[i]);
					}
					if (fits) {
						break;
					}
				}
			}

			// bounds reported to the caller always come from the committed normal impulses
			FreezeFrictionBounds(rows, lambda, bounds, lo, hi, frictionIdx);
			result.Residual = Residual(m, b, lambda, rows, lo, hi, out var post);
			result.RowResiduals = RowResiduals(m, b, lambda, rows, lo, hi);
			result.PostVelocities = post;
			return result;
		}

		private static void FreezeFrictionBounds(RubberSolverRow[] rows, double[] lambda,
			double[] bounds, double[] lo, double[] hi, int[] frictionIdx)
		{
			for (var k = 0; k < frictionIdx.Length; k++) {
				var i = frictionIdx[k];
				var normal = rows[i].NormalRow;
				var bound = normal >= 0
					? Math.Max(0.0, rows[i].FrictionCoefficient * lambda[normal])
					: 0.0;
				bounds[i] = bound;
				lo[i] = -bound;
				hi[i] = bound;
			}
		}

		private static void RunPass(double[,] m, double[] b, double[] lambda, double[] lo, double[] hi,
			int[] idx, RubberGroupSolution result)
		{
			if (idx.Length == 0) {
				return;
			}
			var pivots = BoxedDantzig(m, b, lambda, lo, hi, idx, out var limited);
			result.Pivots += pivots;
			result.PivotLimitHit |= limited;
		}

		#region Delassus helpers

		internal static double[,] Regularize(double[,] a, int n)
		{
			var trace = 0.0;
			for (var i = 0; i < n; i++) {
				trace += a[i, i];
			}
			var reg = DiagonalRegularizationRelative * Math.Max(trace, 0.0);
			var m = new double[n, n];
			for (var i = 0; i < n; i++) {
				for (var j = 0; j < n; j++) {
					m[i, j] = 0.5 * (a[i, j] + a[j, i]);
				}
				m[i, i] += reg;
			}
			return m;
		}

		internal static double[] Multiply(double[,] m, double[] lambda, double[] b)
		{
			var n = lambda.Length;
			var v = new double[n];
			for (var i = 0; i < n; i++) {
				var acc = b[i];
				for (var j = 0; j < n; j++) {
					acc += m[i, j] * lambda[j];
				}
				v[i] = acc;
			}
			return v;
		}

		/// <summary>
		/// Complementarity residual of a candidate solution, expressed in VPE velocity
		/// units so the tolerance means the same thing for every row. Bound violations are
		/// converted to velocity through the row's own diagonal.
		/// </summary>
		internal static double Residual(double[,] m, double[] b, double[] lambda,
			RubberSolverRow[] rows, double[] lo, double[] hi, out double[] velocities)
		{
			velocities = Multiply(m, lambda, b);
			var worst = 0.0;
			for (var i = 0; i < rows.Length; i++) {
				worst = Math.Max(worst, RowResidual(m, velocities, lambda, rows, lo, hi, i));
			}
			return worst;
		}

		internal static double[] RowResiduals(double[,] m, double[] b, double[] lambda,
			RubberSolverRow[] rows, double[] lo, double[] hi)
		{
			var velocities = Multiply(m, lambda, b);
			var res = new double[rows.Length];
			for (var i = 0; i < rows.Length; i++) {
				res[i] = RowResidual(m, velocities, lambda, rows, lo, hi, i);
			}
			return res;
		}

		private static double RowResidual(double[,] m, double[] velocities, double[] lambda,
			RubberSolverRow[] rows, double[] lo, double[] hi, int i)
		{
			var v = velocities[i];
			var l = lambda[i];
			var diag = m[i, i];
			if (rows[i].IsUnilateral) {
				var bound = Math.Max(0.0, -l) * diag;
				return bound + (l > Tiny ? Math.Abs(v) : Math.Max(0.0, -v));
			}
			var hiI = hi[i];
			var loI = lo[i];
			var span = Math.Max(1.0, Math.Abs(hiI));
			var violation = Math.Max(0.0, Math.Max(l - hiI, loI - l)) * diag;
			if (l > loI + Tiny * span && l < hiI - Tiny * span) {
				return violation + Math.Abs(v);
			}
			if (l <= loI + Tiny * span) {
				return violation + Math.Max(0.0, -v);
			}
			return violation + Math.Max(0.0, v);
		}

		/// <summary>
		/// Kinetic-energy change produced by an impulse set on a Delassus system:
		/// <c>dT = lambda . b + 0.5 lambda^T A lambda</c>. A passive group solve must not
		/// make this positive.
		/// </summary>
		internal static double EnergyChange(double[,] m, double[] b, double[] lambda)
		{
			var n = lambda.Length;
			var dt = 0.0;
			for (var i = 0; i < n; i++) {
				dt += lambda[i] * b[i];
				for (var j = 0; j < n; j++) {
					dt += 0.5 * lambda[i] * m[i, j] * lambda[j];
				}
			}
			return dt;
		}

		#endregion

		#region Boxed Dantzig pivot

		private enum State { Unprocessed, AtLo, AtHi, Free }

		private enum Block { None, DriveComplementary, DriveBound, FreeLeaves, ClampEnters }

		/// <summary>
		/// Dantzig-style boxed active-set pivot over the rows listed in <paramref name="idx"/>,
		/// with every other row's impulse held fixed and folded into the right-hand side.
		/// Returns the pivot count.
		/// </summary>
		internal static int BoxedDantzig(double[,] a, double[] b, double[] lambda,
			double[] lo, double[] hi, int[] idx, out bool limited)
		{
			limited = false;
			var n = idx.Length;
			var m = new double[n, n];
			var q = new double[n];
			for (var k = 0; k < n; k++) {
				var i = idx[k];
				var acc = b[i];
				for (var j = 0; j < lambda.Length; j++) {
					acc += a[i, j] * lambda[j];
				}
				// remove the contribution of the rows we are about to re-solve
				for (var l = 0; l < n; l++) {
					acc -= a[i, idx[l]] * lambda[idx[l]];
					m[k, l] = a[i, idx[l]];
				}
				q[k] = acc;
			}

			var z = new double[n];
			var w = new double[n];
			var dz = new double[n];
			var dw = new double[n];
			var state = new State[n];
			var free = new List<int>();

			var scale = 0.0;
			for (var k = 0; k < n; k++) {
				scale = Math.Max(scale, Math.Abs(q[k]));
				scale = Math.Max(scale, m[k, k]);
			}
			var eps = Tiny * Math.Max(1.0, scale);

			var pivots = 0;
			var maxPivots = 8 * n + 16;

			for (var d = 0; d < n && !limited; d++) {
				var loD = lo[idx[d]];
				var hiD = hi[idx[d]];

				w[d] = q[d];
				for (var j = 0; j < d; j++) {
					w[d] += m[d, j] * z[j];
				}

				if (hiD - loD <= eps) {
					// frozen row (zero friction bound): it stays at zero
					z[d] = Math.Max(loD, Math.Min(hiD, 0.0));
					state[d] = State.AtLo;
					continue;
				}

				var dir = 0;
				if (w[d] < -eps && z[d] < hiD) {
					dir = 1;
				} else if (w[d] > eps && z[d] > loD) {
					dir = -1;
				}
				if (dir == 0) {
					state[d] = z[d] <= loD + eps ? State.AtLo : z[d] >= hiD - eps ? State.AtHi : State.Free;
					if (state[d] == State.Free) {
						free.Add(d);
					}
					continue;
				}

				var settled = false;
				while (!settled) {
					if (++pivots > maxPivots) {
						limited = true;
						break;
					}

					// direction
					Array.Clear(dz, 0, n);
					dz[d] = dir;
					if (free.Count > 0) {
						var size = free.Count;
						var ff = new double[size, size];
						var rhs = new double[size];
						for (var r = 0; r < size; r++) {
							for (var c = 0; c < size; c++) {
								ff[r, c] = m[free[r], free[c]];
							}
							rhs[r] = -dir * m[free[r], d];
						}
						if (!SolveDense(ff, rhs, size)) {
							limited = true;
							break;
						}
						for (var r = 0; r < size; r++) {
							dz[free[r]] = rhs[r];
						}
					}
					for (var k = 0; k <= d; k++) {
						var acc = m[k, d] * dz[d];
						for (var r = 0; r < free.Count; r++) {
							acc += m[k, free[r]] * dz[free[r]];
						}
						dw[k] = acc;
					}

					var best = double.PositiveInfinity;
					var block = Block.None;
					var blockIdx = -1;

					// (a) w[d] reaches zero
					if (dir * dw[d] > eps) {
						var s = -w[d] / dw[d];
						if (s < best) {
							best = Math.Max(0.0, s);
							block = Block.DriveComplementary;
							blockIdx = d;
						}
					}
					// (b) z[d] reaches its own bound
					{
						var s = dir > 0 ? hiD - z[d] : z[d] - loD;
						if (s < best) {
							best = Math.Max(0.0, s);
							block = Block.DriveBound;
							blockIdx = d;
						}
					}
					// (c) a free row leaves its box
					for (var r = 0; r < free.Count; r++) {
						var f = free[r];
						var s = double.PositiveInfinity;
						if (dz[f] > eps) {
							s = (hi[idx[f]] - z[f]) / dz[f];
						} else if (dz[f] < -eps) {
							s = (lo[idx[f]] - z[f]) / dz[f];
						}
						if (s < best) {
							best = Math.Max(0.0, s);
							block = Block.FreeLeaves;
							blockIdx = f;
						}
					}
					// (d) a clamped row's w changes sign
					for (var k = 0; k < d; k++) {
						double s;
						if (state[k] == State.AtLo && dw[k] < -eps) {
							s = -w[k] / dw[k];
						} else if (state[k] == State.AtHi && dw[k] > eps) {
							s = -w[k] / dw[k];
						} else {
							continue;
						}
						if (s < best) {
							best = Math.Max(0.0, s);
							block = Block.ClampEnters;
							blockIdx = k;
						}
					}

					if (block == Block.None || double.IsInfinity(best)) {
						limited = true;
						break;
					}

					z[d] += best * dz[d];
					for (var r = 0; r < free.Count; r++) {
						z[free[r]] += best * dz[free[r]];
					}
					for (var k = 0; k <= d; k++) {
						w[k] += best * dw[k];
					}

					switch (block) {
						case Block.DriveComplementary:
							w[d] = 0.0;
							state[d] = State.Free;
							free.Add(d);
							settled = true;
							break;
						case Block.DriveBound:
							z[d] = dir > 0 ? hiD : loD;
							state[d] = dir > 0 ? State.AtHi : State.AtLo;
							settled = true;
							break;
						case Block.FreeLeaves: {
							var f = blockIdx;
							free.Remove(f);
							if (dz[f] > 0.0) {
								z[f] = hi[idx[f]];
								state[f] = State.AtHi;
							} else {
								z[f] = lo[idx[f]];
								state[f] = State.AtLo;
							}
							break;
						}
						case Block.ClampEnters: {
							var k = blockIdx;
							w[k] = 0.0;
							state[k] = State.Free;
							free.Add(k);
							break;
						}
					}
				}
			}

			for (var k = 0; k < n; k++) {
				var i = idx[k];
				lambda[i] = Math.Max(lo[i], Math.Min(hi[i], z[k]));
			}
			return pivots;
		}

		/// <summary>Gaussian elimination with partial pivoting; rhs is overwritten with x.</summary>
		private static bool SolveDense(double[,] m, double[] rhs, int n)
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
				if (best < 1e-300) {
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

		#endregion
	}
}
