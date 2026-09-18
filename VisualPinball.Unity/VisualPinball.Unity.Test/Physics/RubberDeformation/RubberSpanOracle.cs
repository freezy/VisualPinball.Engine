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
	/// Section 7.1's oracle, which is also the "compliant formulation" cross-check of
	/// section 2.4.4: a pinned span carrying 8 modes (16 for the refinement study), a
	/// penalty contact stiff enough to hold penetration under one percent of the ball
	/// radius with damping ratio 0.1, integrated by fourth-order Runge-Kutta at 10
	/// microseconds -- 1e-3 VPE time units, because one VPE time unit is 0.01 s.
	///
	/// <para>
	/// Everything is planar: the span lies along x from 0 to L, deflection is along +y, and
	/// the ball comes up from below. That is the plan's model exactly -- deflection is
	/// confined to the bake plane and the bake-normal direction is rigid.
	/// </para>
	/// </summary>
	internal sealed class RubberSpanOracle
	{
		/// <summary>10 microseconds, in VPE time units.</summary>
		internal const double DefaultStep = 1e-3;

		internal const int DefaultModeCount = 8;
		internal const double PenaltyDampingRatio = 0.1;

		/// <summary>The penalty contact is sized so the penetration stays under this.</summary>
		internal const double PenetrationFractionOfBallRadius = 0.01;

		/// <summary>
		/// The sizing target, kept below the budget so the measured peak has margin. The
		/// contact is stiff against the ball, not against the truncation's point-load mass:
		/// sizing it on the latter produces a soft bumper that throws the ball back before
		/// the cord's slow mode ever loads, which is not a contact at all.
		/// </summary>
		internal const double SizingFractionOfBallRadius = 0.008;

		/// <summary>Clearance between the ball surface and the cord surface at t = 0.</summary>
		internal const double StartGap = 0.5;

		internal struct Shot
		{
			/// <summary>Arc-length fraction of the nominal impact point.</summary>
			internal double U;

			/// <summary>Approach speed, VPX units per VPE time unit.</summary>
			internal double Speed;

			/// <summary>Tangential component of the approach, same units.</summary>
			internal double TangentialSpeed;

			internal double BallMass;
			internal double BallRadius;

			internal static Shot Centre(double speed) => new Shot {
				U = 0.5, Speed = speed, TangentialSpeed = 0.0, BallMass = 1.0, BallRadius = 25.0,
			};

			internal static Shot At(double u, double speed) => new Shot {
				U = u, Speed = speed, TangentialSpeed = 0.0, BallMass = 1.0, BallRadius = 25.0,
			};
		}

		internal struct Result
		{
			internal double ReboundSpeed;
			internal double ReboundAngleDegrees;
			internal double ContactDuration;
			internal double PeakDeflection;
			internal double PeakPenetration;
			internal double PenaltyStiffness;
			internal double Step;
			internal bool Separated;
			internal int Steps;

			public override string ToString()
				=> $"rebound {ReboundSpeed:F4} u/step, angle {ReboundAngleDegrees:F3} deg, " +
				   $"duration {ContactDuration:F4} VPE t, peak {PeakDeflection:F4} u, " +
				   $"penetration {PeakPenetration:F4} u, step {Step:E2} VPE t, separated {Separated}";
		}

		/// <summary>
		/// The step is subdivided until the fastest mode in the system -- the truncation's
		/// point-load mass against the penalty spring -- satisfies <c>omega * dt &lt;= this</c>.
		/// Section 7.1 fixes the nominal step at 10 microseconds, but for an 8 mode
		/// truncation of a 0.59 g cord that mode runs near 2100 rad per VPE time unit at 18
		/// units per step, which is <c>omega * dt = 2.1</c>, right at the stability edge of
		/// RK4. Subdividing is what the section's own refinement instruction asks for.
		/// </summary>
		internal const double StabilityBudget = 0.5;

		private readonly int _modeCount;
		private readonly double _requestedStep;
		private double _step;
		private readonly double _length;
		private readonly double _cordRadius;

		private readonly double[] _modalMass;
		private readonly double[] _modalStiffness;
		private readonly double[] _modalDamping;
		private readonly double[] _sin;
		private readonly double[] _cos;

		private double _penaltyStiffness;
		private double _penaltyDamping;
		private double _ballMass;
		private double _ballRadius;

		internal int ModeCount => _modeCount;
		internal double Step_ => _step;

		internal RubberSpanOracle(in RubberSpanCoefficients coefficients, int modeCount = DefaultModeCount,
			double step = DefaultStep)
		{
			_modeCount = modeCount;
			_requestedStep = step;
			_step = step;
			_length = coefficients.InstalledLengthVpx;
			_cordRadius = coefficients.ThicknessVpx * 0.5;

			_modalMass = new double[modeCount];
			_modalStiffness = new double[modeCount];
			_modalDamping = new double[modeCount];
			_sin = new double[modeCount];
			_cos = new double[modeCount];
			for (var n = 1; n <= modeCount; n++) {
				_modalMass[n - 1] = coefficients.ModalMassVpe;
				_modalStiffness[n - 1] = coefficients.ModalStiffnessVpe(n);
				_modalDamping[n - 1] = coefficients.ModalDampingVpe(n);
			}
		}

		/// <summary>
		/// Reduced mass seen by the penalty contact at fraction u: the ball in series with
		/// the truncation's point-load effective mass. Sizing the penalty on it puts the
		/// contact frequency at <c>speed / (0.01 * ballRadius)</c>, which is the "order 120
		/// per VPE time unit" of section 2.4.4 at the top of the shot envelope.
		/// </summary>
		internal double ReducedMass(double u, double ballMass)
		{
			var inv = 1.0 / ballMass;
			for (var n = 1; n <= _modeCount; n++) {
				var s = RubberSpanCoefficients.Shape(n, u);
				inv += s * s / _modalMass[n - 1];
			}
			return 1.0 / inv;
		}

		internal void SetPenalty(double stiffness, double reducedMass)
		{
			_penaltyStiffness = stiffness;
			_penaltyDamping = 2.0 * PenaltyDampingRatio * Math.Sqrt(stiffness * reducedMass);
		}

		internal double SizePenalty(in Shot shot)
		{
			var reduced = ReducedMass(shot.U, shot.BallMass);
			var target = SizingFractionOfBallRadius * shot.BallRadius;
			// stiff against the ball's own momentum: p_peak = v * sqrt(m_ball / k) = target,
			// which puts the contact frequency at speed / target -- section 2.4.4's "order
			// 120 per VPE time unit" at the top of the shot envelope. The damper is rated on
			// the reduced mass so the fastest mode in the system, the near massless cord
			// against the penalty spring, is the one damped at 0.1.
			var k = shot.BallMass * (shot.Speed / target) * (shot.Speed / target);
			SetPenalty(k, reduced);
			return k;
		}

		internal Result Run(in Shot shot, double? penaltyStiffness = null, double? penaltyReducedMass = null)
		{
			_ballMass = shot.BallMass;
			_ballRadius = shot.BallRadius;
			if (penaltyStiffness.HasValue) {
				SetPenalty(penaltyStiffness.Value, penaltyReducedMass ?? ReducedMass(shot.U, shot.BallMass));
			} else {
				SizePenalty(in shot);
			}

			// subdivide the nominal step until the contact micro mode is resolved
			var microMass = ReducedMass(shot.U, shot.BallMass);
			var microOmega = Math.Sqrt(_penaltyStiffness / microMass);
			var subdivisions = Math.Max(1, (int)Math.Ceiling(_requestedStep * microOmega / StabilityBudget));
			_step = _requestedStep / subdivisions;

			var m = _modeCount;
			var dim = 2 * m + 4;
			var y = new double[dim];
			y[2 * m + 0] = shot.U * _length;
			y[2 * m + 1] = -(shot.BallRadius + _cordRadius + StartGap);
			y[2 * m + 2] = shot.TangentialSpeed;
			y[2 * m + 3] = shot.Speed;

			var k1 = new double[dim];
			var k2 = new double[dim];
			var k3 = new double[dim];
			var k4 = new double[dim];
			var tmp = new double[dim];

			var t = 0.0;
			const double maxTime = 80.0;
			var contactStart = double.NaN;
			var contactEnd = double.NaN;
			var inContact = false;
			var peakDeflection = 0.0;
			var peakPenetration = 0.0;
			var steps = 0;
			var separated = false;
			var quiet = 0;

			// A stiff penalty rings: the force drops to zero for a step or two several times
			// inside one contact. Separation is only declared after the force has stayed at
			// zero for a long stretch and the ball is moving away.
			const double quietSpan = 0.2;
			var quietSteps = (int)(quietSpan / _step);

			while (t < maxTime) {
				Derivative(y, k1, out var force, out var penetration);
				if (force > 0.0) {
					inContact = true;
					quiet = 0;
					if (double.IsNaN(contactStart)) {
						contactStart = t;
					}
					contactEnd = t;
					peakPenetration = Math.Max(peakPenetration, penetration);
				} else if (inContact) {
					quiet++;
					if (quiet >= quietSteps && y[2 * m + 3] < 0.0) {
						separated = true;
					}
				}
				peakDeflection = Math.Max(peakDeflection, Math.Abs(Deflection(y, shot.U)));

				if (separated) {
					break;
				}
				if (!inContact && t > 5.0) {
					break;
				}

				Integrate(y, k1, k2, k3, k4, tmp);
				t += _step;
				steps++;
			}

			var vx = y[2 * m + 2];
			var vy = y[2 * m + 3];
			return new Result {
				ReboundSpeed = Math.Sqrt(vx * vx + vy * vy),
				ReboundAngleDegrees = Math.Atan2(vx, -vy) * 180.0 / Math.PI,
				ContactDuration = double.IsNaN(contactStart) ? 0.0 : contactEnd - contactStart + _step,
				PeakDeflection = peakDeflection,
				PeakPenetration = peakPenetration,
				PenaltyStiffness = _penaltyStiffness,
				Step = _step,
				Separated = separated,
				Steps = steps,
			};
		}

		private void Integrate(double[] y, double[] k1, double[] k2, double[] k3, double[] k4, double[] tmp)
		{
			var dim = y.Length;
			for (var i = 0; i < dim; i++) {
				tmp[i] = y[i] + 0.5 * _step * k1[i];
			}
			Derivative(tmp, k2, out _, out _);
			for (var i = 0; i < dim; i++) {
				tmp[i] = y[i] + 0.5 * _step * k2[i];
			}
			Derivative(tmp, k3, out _, out _);
			for (var i = 0; i < dim; i++) {
				tmp[i] = y[i] + _step * k3[i];
			}
			Derivative(tmp, k4, out _, out _);
			for (var i = 0; i < dim; i++) {
				y[i] += _step / 6.0 * (k1[i] + 2.0 * k2[i] + 2.0 * k3[i] + k4[i]);
			}
		}

		/// <summary>sin(n*pi*s/L) and cos(n*pi*s/L) for n = 1..N by Chebyshev recurrence.</summary>
		private void FillShape(double s)
		{
			var theta = Math.PI * s / _length;
			var c = Math.Cos(theta);
			_sin[0] = Math.Sin(theta);
			_cos[0] = c;
			for (var n = 2; n <= _modeCount; n++) {
				_sin[n - 1] = 2.0 * c * _sin[n - 2] - (n >= 3 ? _sin[n - 3] : 0.0);
				_cos[n - 1] = 2.0 * c * _cos[n - 2] - (n >= 3 ? _cos[n - 3] : 1.0);
			}
		}

		private double Deflection(double[] y, double u)
		{
			var w = 0.0;
			for (var n = 1; n <= _modeCount; n++) {
				w += y[n - 1] * RubberSpanCoefficients.Shape(n, u);
			}
			return w;
		}

		private void Curve(double[] y, double s, out double w, out double wp, out double wpp)
		{
			FillShape(s);
			w = 0.0;
			wp = 0.0;
			wpp = 0.0;
			var k0 = Math.PI / _length;
			for (var n = 1; n <= _modeCount; n++) {
				var q = y[n - 1];
				w += q * _sin[n - 1];
				wp += q * (n * k0) * _cos[n - 1];
				wpp -= q * (n * k0) * (n * k0) * _sin[n - 1];
			}
		}

		private double ClosestParameter(double[] y, double bx, double by)
		{
			// Newton from the projection onto the rest line, cross-checked against a coarse
			// scan so a strongly deformed cord cannot trap it in a wrong local minimum.
			var best = double.MaxValue;
			var bestS = 0.0;
			const int samples = 16;
			for (var i = 0; i <= samples; i++) {
				var candidate = _length * i / samples;
				Curve(y, candidate, out var w, out _, out _);
				var dx = bx - candidate;
				var dyc = by - w;
				var d2 = dx * dx + dyc * dyc;
				if (d2 < best) {
					best = d2;
					bestS = candidate;
				}
			}

			foreach (var start in new[] { bestS, Math.Max(0.0, Math.Min(_length, bx)) }) {
				var s = start;
				for (var it = 0; it < 12; it++) {
					Curve(y, s, out var w, out var wp, out var wpp);
					var g = bx - s + (by - w) * wp;
					var gp = -1.0 - wp * wp + (by - w) * wpp;
					if (Math.Abs(gp) < 1e-12) {
						break;
					}
					var delta = g / gp;
					s -= delta;
					if (s < 0.0) {
						s = 0.0;
					} else if (s > _length) {
						s = _length;
					}
					if (Math.Abs(delta) < 1e-12) {
						break;
					}
				}
				Curve(y, s, out var wf, out _, out _);
				var dx = bx - s;
				var dyf = by - wf;
				var d2 = dx * dx + dyf * dyf;
				if (d2 < best) {
					best = d2;
					bestS = s;
				}
			}
			return bestS;
		}

		private void Derivative(double[] y, double[] dy, out double force, out double penetration)
		{
			var m = _modeCount;
			var bx = y[2 * m + 0];
			var by = y[2 * m + 1];
			var vx = y[2 * m + 2];
			var vy = y[2 * m + 3];

			var s = ClosestParameter(y, bx, by);
			Curve(y, s, out var wS, out _, out _);

			var nx = bx - s;
			var ny = by - wS;
			var dist = Math.Sqrt(nx * nx + ny * ny);
			if (dist > 1e-12) {
				nx /= dist;
				ny /= dist;
			} else {
				nx = 0.0;
				ny = -1.0;
			}

			var separation = dist - (_ballRadius + _cordRadius);
			penetration = Math.Max(0.0, -separation);

			force = 0.0;
			if (penetration > 0.0) {
				// FillShape already holds sin(n pi s / L) from the Curve call above
				var cordVy = 0.0;
				for (var n = 1; n <= m; n++) {
					cordVy += y[m + n - 1] * _sin[n - 1];
				}
				var separationRate = vx * nx + (vy - cordVy) * ny;
				force = Math.Max(0.0, _penaltyStiffness * penetration - _penaltyDamping * separationRate);
			}

			for (var n = 1; n <= m; n++) {
				var generalized = -force * ny * _sin[n - 1];
				dy[n - 1] = y[m + n - 1];
				dy[m + n - 1] = (generalized - _modalDamping[n - 1] * y[m + n - 1]
					- _modalStiffness[n - 1] * y[n - 1]) / _modalMass[n - 1];
			}

			dy[2 * m + 0] = vx;
			dy[2 * m + 1] = vy;
			dy[2 * m + 2] = force * nx / _ballMass;
			dy[2 * m + 3] = force * ny / _ballMass;
		}
	}
}
