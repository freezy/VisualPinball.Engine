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
	/// Section 6.1's reference: the massless-string surrogate of the static rebound
	/// plan's 2.1, in double precision at 10 microseconds (1e-3 VPE time units).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The model, the domain rules and the return refinement are the runtime's; only the
	/// precision and the step size differ, so a comparison isolates the single-precision
	/// half-millisecond integration the engine runs. The stress table is the same
	/// 128-sample compiled table the runtime reads, so the material model is shared too.
	/// </para>
	/// <para>
	/// Everything is in VPE physics units: lengths in VPX units, time in units of 10 ms,
	/// mass in units of one 80 g ball.
	/// </para>
	/// </remarks>
	internal sealed class RubberSpanReboundReference
	{
		/// <summary>10 microseconds, in VPE time units.</summary>
		internal const double Step = 1e-3;

		/// <summary>
		/// The runtime's 300 ms budget expressed at the reference step, so both solvers
		/// give up after the same amount of simulated time.
		/// </summary>
		internal const int StepBudget = 30000;

		internal const double MinimumSpanCoordinate = -0.02;
		internal const double MaximumSpanCoordinate = 1.02;
		internal const double EnergyResidualTolerance = 0.01;

		internal struct Result
		{
			internal bool Success;
			internal RubberSpanReboundFailure Failure;
			internal double ExitChord;
			internal double ExitNormal;
			internal double Depth;
			internal double ApexFraction;
			internal int Steps;
			internal double ReturnTime;
			internal double EnergyResidual;

			internal double ExitSpeed => Math.Sqrt(ExitChord * ExitChord + ExitNormal * ExitNormal);

			/// <summary>Exit angle from the outward normal, positive toward anchor B.</summary>
			internal double ExitAngleDegrees
				=> Math.Atan2(ExitChord, ExitNormal) * 180.0 / Math.PI;

			public override string ToString()
				=> Success
					? $"speed {ExitSpeed:F4}, angle {ExitAngleDegrees:F4} deg, depth {Depth:F4}, apex {ApexFraction:F4}, steps {Steps}, return {ReturnTime:F4}, residual {EnergyResidual:E2}"
					: $"failed: {Failure} after {Steps} steps";
		}

		private readonly double _length;
		private readonly double _unloadedLength;
		private readonly double _area;
		private readonly double _maximumStretchRatio;
		private readonly double[] _stress;
		private readonly double _step;
		private readonly int _budget;

		internal RubberSpanReboundReference(double length, double unloadedLength, double area,
			double maximumStretchRatio, double[] stress, double step = Step,
			int budget = StepBudget)
		{
			if (stress == null || stress.Length < 2) {
				throw new ArgumentException("the reference needs a compiled stress table",
					nameof(stress));
			}
			_length = length;
			_unloadedLength = unloadedLength;
			_area = area;
			_maximumStretchRatio = maximumStretchRatio;
			_stress = stress;
			_step = step;
			_budget = budget;
		}

		/// <summary>
		/// The compiled table's linear interpolation, in the layout the compiled material
		/// and the runtime record share.
		/// </summary>
		internal double SampleStress(double stretchRatio)
		{
			if (double.IsNaN(stretchRatio)) {
				return _stress[0];
			}
			var maximum = Math.Max(1.0 + 1e-6, _maximumStretchRatio);
			var position = Math.Max(0.0, Math.Min(1.0, (stretchRatio - 1.0) / (maximum - 1.0)))
				* (_stress.Length - 1);
			var lower = (int)Math.Floor(position);
			if (lower >= _stress.Length - 1) {
				return _stress[_stress.Length - 1];
			}
			return _stress[lower] + (_stress[lower + 1] - _stress[lower]) * (position - lower);
		}

		/// <summary>
		/// Integrates one shot. <paramref name="u"/> is the contact's arc-length
		/// fraction, the velocity is in the span plane with the chord in x and the
		/// outward normal in y, and the normal component is negative on approach.
		/// </summary>
		internal Result Run(double u, double velocityChord, double velocityNormal,
			double mass = 1.0)
		{
			var result = new Result {
				ExitChord = velocityChord,
				ExitNormal = velocityNormal,
				ApexFraction = u,
			};
			var anchorA = -u * _length;
			var anchorB = (1.0 - u) * _length;
			double px = 0.0, py = 0.0;
			var vx = velocityChord;
			var vy = velocityNormal;
			var initialEnergy = 0.5 * mass * (vx * vx + vy * vy);
			var entered = false;
			var depth = 0.0;
			var apex = u;

			for (var step = 1; step <= _budget; step++) {
				var previousY = py;
				var previousVx = vx;
				var previousVy = vy;
				Integrate(anchorA, anchorB, mass, ref px, ref py, ref vx, ref vy);
				result.Steps = step;

				if (double.IsNaN(px) || double.IsNaN(py) || double.IsInfinity(px)
					|| double.IsInfinity(py) || double.IsNaN(vx) || double.IsNaN(vy)
					|| double.IsInfinity(vx) || double.IsInfinity(vy)) {
					result.Failure = RubberSpanReboundFailure.NonFinite;
					return result;
				}

				var spanCoordinate = u + px / _length;
				if (spanCoordinate < MinimumSpanCoordinate
					|| spanCoordinate > MaximumSpanCoordinate) {
					result.Failure = RubberSpanReboundFailure.ApexOutsideSpan;
					return result;
				}

				var cordLength = Distance(px - anchorA, py) + Distance(px - anchorB, py);
				if (cordLength / _unloadedLength > _maximumStretchRatio) {
					result.Failure = RubberSpanReboundFailure.TableExhausted;
					return result;
				}

				if (-py > depth) {
					depth = -py;
					apex = spanCoordinate;
				}

				if (!entered) {
					entered = py < 0.0;
					continue;
				}
				if (!(previousY < 0.0) || py < 0.0) {
					continue;
				}

				var rise = py - previousY;
				var fraction = rise > 0.0
					? Math.Max(0.0, Math.Min(1.0, -previousY / rise))
					: 0.0;
				var exitX = previousVx + (vx - previousVx) * fraction;
				var exitY = previousVy + (vy - previousVy) * fraction;
				var exitEnergy = 0.5 * mass * (exitX * exitX + exitY * exitY);
				result.EnergyResidual = (exitEnergy - initialEnergy) / initialEnergy;
				if (Math.Abs(result.EnergyResidual) > EnergyResidualTolerance) {
					result.Failure = RubberSpanReboundFailure.EnergyResidual;
					return result;
				}
				result.ExitChord = exitX;
				result.ExitNormal = exitY;
				result.Depth = depth;
				result.ApexFraction = apex;
				result.ReturnTime = (step - 1 + fraction) * _step;
				result.Success = true;
				return result;
			}

			result.Failure = RubberSpanReboundFailure.StepBudget;
			return result;
		}

		private void Integrate(double anchorA, double anchorB, double mass,
			ref double px, ref double py, ref double vx, ref double vy)
		{
			// the runtime's Runge-Kutta stage layout, in double precision
			var h = _step;
			var p1x = vx;
			var p1y = vy;
			Force(anchorA, anchorB, px, py, out var f1x, out var f1y);
			var v1x = f1x / mass;
			var v1y = f1y / mass;

			var p2x = vx + 0.5 * h * v1x;
			var p2y = vy + 0.5 * h * v1y;
			Force(anchorA, anchorB, px + 0.5 * h * p1x, py + 0.5 * h * p1y,
				out var f2x, out var f2y);
			var v2x = f2x / mass;
			var v2y = f2y / mass;

			var p3x = vx + 0.5 * h * v2x;
			var p3y = vy + 0.5 * h * v2y;
			Force(anchorA, anchorB, px + 0.5 * h * p2x, py + 0.5 * h * p2y,
				out var f3x, out var f3y);
			var v3x = f3x / mass;
			var v3y = f3y / mass;

			var p4x = vx + h * v3x;
			var p4y = vy + h * v3y;
			Force(anchorA, anchorB, px + h * p3x, py + h * p3y, out var f4x, out var f4y);
			var v4x = f4x / mass;
			var v4y = f4y / mass;

			px += h / 6.0 * (p1x + 2.0 * p2x + 2.0 * p3x + p4x);
			py += h / 6.0 * (p1y + 2.0 * p2y + 2.0 * p3y + p4y);
			vx += h / 6.0 * (v1x + 2.0 * v2x + 2.0 * v3x + v4x);
			vy += h / 6.0 * (v1y + 2.0 * v2y + 2.0 * v3y + v4y);
		}

		private void Force(double anchorA, double anchorB, double px, double py,
			out double fx, out double fy)
		{
			fx = 0.0;
			fy = 0.0;
			var ax = anchorA - px;
			var bx = anchorB - px;
			var ay = -py;
			var distanceA = Distance(ax, ay);
			var distanceB = Distance(bx, ay);
			if (!(distanceA > 1e-9) || !(distanceB > 1e-9)) {
				return;
			}
			var cordLength = distanceA + distanceB;
			if (cordLength <= _length) {
				return;
			}
			var tension = SampleStress(cordLength / _unloadedLength) * _area;
			if (!(tension > 0.0)) {
				return;
			}
			fx = tension * (ax / distanceA + bx / distanceB);
			fy = tension * (ay / distanceA + ay / distanceB);
		}

		private static double Distance(double x, double y) => Math.Sqrt(x * x + y * y);
	}
}
