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
	/// One lane's underdamped coefficients, in VPE units.
	/// </summary>
	internal readonly struct RubberModalCoefficients
	{
		internal readonly double Mass;
		internal readonly double Stiffness;
		internal readonly double Damping;

		/// <summary>Undamped natural frequency, rad per VPE time unit.</summary>
		internal readonly double OmegaN;

		internal readonly double Zeta;

		/// <summary>Damped frequency, rad per VPE time unit.</summary>
		internal readonly double OmegaD;

		internal RubberModalCoefficients(double mass, double stiffness, double damping)
		{
			if (!(mass > 0.0) || !(stiffness > 0.0) || double.IsNaN(damping)) {
				throw new ArgumentException("Modal coefficients must be positive and finite.");
			}
			Mass = mass;
			Stiffness = stiffness;
			Damping = damping;
			OmegaN = Math.Sqrt(stiffness / mass);
			Zeta = damping / (2.0 * Math.Sqrt(stiffness * mass));
			if (!(Zeta >= 0.0) || Zeta >= 1.0) {
				throw new ArgumentException($"Damping ratio {Zeta} outside the validated domain [0, 1).");
			}
			OmegaD = OmegaN * Math.Sqrt(1.0 - Zeta * Zeta);
		}

		internal static RubberModalCoefficients FromRatio(double mass, double stiffness, double zeta)
			=> new RubberModalCoefficients(mass, stiffness, 2.0 * zeta * Math.Sqrt(stiffness * mass));
	}

	internal readonly struct RubberModalState
	{
		internal readonly double Q;
		internal readonly double QDot;

		internal RubberModalState(double q, double qDot)
		{
			Q = q;
			QDot = qDot;
		}
	}

	internal enum RubberCapOutcome
	{
		/// <summary>No crossing of either cap inside the window.</summary>
		None,

		/// <summary>The lane is already at or beyond the cap and moving further outward.</summary>
		AlreadyAtCap,

		/// <summary>A crossing was isolated strictly inside the window.</summary>
		Arrival,
	}

	internal readonly struct RubberCapPrediction
	{
		internal readonly RubberCapOutcome Outcome;
		internal readonly double Time;

		/// <summary>+1 for the +cap, -1 for the -cap, 0 when there is no arrival.</summary>
		internal readonly int Sign;

		internal RubberCapPrediction(RubberCapOutcome outcome, double time, int sign)
		{
			Outcome = outcome;
			Time = time;
			Sign = sign;
		}

		internal bool HasArrival => Outcome != RubberCapOutcome.None;

		internal static readonly RubberCapPrediction NoArrival =
			new RubberCapPrediction(RubberCapOutcome.None, double.PositiveInfinity, 0);
	}

	/// <summary>
	/// Section 2.5: the single-mode closed form, its energy, its rate bound, and the cap
	/// root isolation built on its analytic extrema.
	/// </summary>
	internal static class RubberModalMath
	{
		/// <summary>Bisection tolerance for the cap crossing, in VPE time units (2.5).</summary>
		internal const double CapBisectionTolerance = 1e-6;

		/// <summary>
		/// Exact free advancement of the underdamped oscillator over <paramref name="dt"/>.
		///
		/// <code>
		/// q(t)    = e^(-a t) [ q0 cos(wd t) + (qdot0 + a q0)/wd sin(wd t) ]
		/// qdot(t) = e^(-a t) [ qdot0 cos(wd t) - (wn^2 q0 + a qdot0)/wd sin(wd t) ]
		/// </code>
		///
		/// with <c>a = zeta * wn</c>.
		/// </summary>
		internal static RubberModalState Advance(in RubberModalCoefficients c, in RubberModalState s, double dt)
		{
			if (dt == 0.0) {
				return s;
			}
			var a = c.Zeta * c.OmegaN;
			var wd = c.OmegaD;
			var e = Math.Exp(-a * dt);
			var co = Math.Cos(wd * dt);
			var si = Math.Sin(wd * dt);

			var q = e * (s.Q * co + (s.QDot + a * s.Q) / wd * si);
			var qd = e * (s.QDot * co - (c.OmegaN * c.OmegaN * s.Q + a * s.QDot) / wd * si);
			return new RubberModalState(q, qd);
		}

		/// <summary>The same step in single precision, to pin what the production code will do.</summary>
		internal static void AdvanceFloat(float mass, float stiffness, float damping,
			ref float q, ref float qDot, float dt)
		{
			var omegaN = (float)Math.Sqrt(stiffness / mass);
			var zeta = damping / (2f * (float)Math.Sqrt(stiffness * (double)mass));
			var omegaD = omegaN * (float)Math.Sqrt(1f - zeta * zeta);
			var a = zeta * omegaN;
			var e = (float)Math.Exp(-a * dt);
			var co = (float)Math.Cos(omegaD * dt);
			var si = (float)Math.Sin(omegaD * dt);
			var q0 = q;
			var v0 = qDot;
			q = e * (q0 * co + (v0 + a * q0) / omegaD * si);
			qDot = e * (v0 * co - (omegaN * omegaN * q0 + a * v0) / omegaD * si);
		}

		internal static double Energy(in RubberModalCoefficients c, in RubberModalState s)
			=> 0.5 * c.Mass * s.QDot * s.QDot + 0.5 * c.Stiffness * s.Q * s.Q;

		/// <summary>
		/// Section 3.4's per-lane rate bound: <c>sqrt(qdot^2 + wn^2 q^2)</c>. It is a
		/// Lipschitz bound on the lane's contribution to the chain speed for the whole
		/// window because <c>d/dt (qdot^2 + wn^2 q^2) = -2 (C/M) qdot^2 &lt;= 0</c>.
		/// </summary>
		internal static double RateBound(in RubberModalCoefficients c, in RubberModalState s)
			=> Math.Sqrt(s.QDot * s.QDot + c.OmegaN * c.OmegaN * s.Q * s.Q);

		/// <summary>
		/// The times in (0, window] at which qdot vanishes, i.e. the analytic extrema of q.
		/// Between consecutive extrema q is strictly monotone.
		/// </summary>
		internal static void Extrema(in RubberModalCoefficients c, in RubberModalState s,
			double window, List<double> into)
		{
			into.Clear();
			var a = c.Zeta * c.OmegaN;
			var wd = c.OmegaD;
			// qdot(t) = e^(-a t) [ qdot0 cos(wd t) + S sin(wd t) ], zero when tan(wd t) = -qdot0 / S
			var sCoefficient = -(c.OmegaN * c.OmegaN * s.Q + a * s.QDot) / wd;
			if (sCoefficient == 0.0 && s.QDot == 0.0) {
				return; // identically at rest
			}
			var phase = Math.Atan2(-s.QDot, sCoefficient);
			var period = Math.PI / wd;
			var t = phase / wd;
			// step up to the first strictly positive root
			var k = (long)Math.Floor(-t / period);
			t += k * period;
			while (t <= 0.0) {
				t += period;
			}
			while (t < window) {
				into.Add(t);
				t += period;
			}
		}

		/// <summary>
		/// Section 2.5's cap arrival: split the window at the analytic extrema, look for a
		/// sign change of <c>q - cap</c> and <c>q + cap</c> on each monotone interval in
		/// order, and refine the first bracket by bisection to
		/// <see cref="CapBisectionTolerance"/>. The validity check at t = 0 reports an
		/// immediate arrival when the lane already sits at the cap and is still moving
		/// outward, and otherwise ignores the root at t = 0 so a lane released exactly at
		/// the cap is not re-captured.
		/// </summary>
		internal static RubberCapPrediction PredictCapArrival(in RubberModalCoefficients c,
			in RubberModalState s, double cap, double window, List<double> scratch = null)
		{
			if (!(cap > 0.0) || !(window > 0.0)) {
				return RubberCapPrediction.NoArrival;
			}

			// validity check at t = 0
			if (Math.Abs(s.Q) >= cap) {
				if (s.Q * s.QDot > 0.0) {
					return new RubberCapPrediction(RubberCapOutcome.AlreadyAtCap, 0.0, Math.Sign(s.Q));
				}
			}

			var extrema = scratch ?? new List<double>();
			Extrema(in c, in s, window, extrema);

			var previous = 0.0;
			for (var i = 0; i <= extrema.Count; i++) {
				var next = i < extrema.Count ? extrema[i] : window;
				if (next <= previous) {
					previous = next;
					continue;
				}
				var hit = ScanMonotoneInterval(in c, in s, cap, previous, next);
				if (hit.HasArrival) {
					return hit;
				}
				previous = next;
			}
			return RubberCapPrediction.NoArrival;
		}

		private static RubberCapPrediction ScanMonotoneInterval(in RubberModalCoefficients c,
			in RubberModalState s, double cap, double lo, double hi)
		{
			var qLo = Advance(in c, in s, lo).Q;
			var qHi = Advance(in c, in s, hi).Q;

			// on a monotone interval at most one of the two caps can be crossed, and the
			// interval is monotone so the crossing is unique
			for (var sign = 1; sign >= -1; sign -= 2) {
				var target = sign * cap;
				var fLo = qLo - target;
				var fHi = qHi - target;
				if (fLo == 0.0 && lo > 0.0) {
					return new RubberCapPrediction(RubberCapOutcome.Arrival, lo, sign);
				}
				if (fLo * fHi > 0.0) {
					continue;
				}
				if (fLo * fHi == 0.0 && fHi != 0.0) {
					continue; // the zero is at lo == 0, excluded by the validity check
				}
				var t = Bisect(in c, in s, target, lo, hi);
				return new RubberCapPrediction(RubberCapOutcome.Arrival, t, sign);
			}
			return RubberCapPrediction.NoArrival;
		}

		private static double Bisect(in RubberModalCoefficients c, in RubberModalState s,
			double target, double lo, double hi)
		{
			var fLo = Advance(in c, in s, lo).Q - target;
			for (var i = 0; i < 200 && hi - lo > CapBisectionTolerance; i++) {
				var mid = 0.5 * (lo + hi);
				var fMid = Advance(in c, in s, mid).Q - target;
				if (fMid == 0.0) {
					return mid;
				}
				if (fLo * fMid < 0.0) {
					hi = mid;
				} else {
					lo = mid;
					fLo = fMid;
				}
			}
			return 0.5 * (lo + hi);
		}
	}
}
