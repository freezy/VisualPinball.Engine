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
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	internal enum RubberHitOutcome
	{
		/// <summary>The window is covered: certifiably no contact in it.</summary>
		None,

		/// <summary>A sustained contact row for this substep (t = 0, slow).</summary>
		Contact,

		/// <summary>An impact, at t = 0 or at the reported time.</summary>
		Hit,

		/// <summary>32 iterations exhausted with covered time below the window.</summary>
		SearchLimit,
	}

	internal readonly struct RubberHitResult
	{
		internal readonly RubberHitOutcome Outcome;
		internal readonly double Time;

		/// <summary>The certified interval, for a search limit.</summary>
		internal readonly double CoveredTime;

		internal readonly int Iterations;

		internal RubberHitResult(RubberHitOutcome outcome, double time, double coveredTime, int iterations)
		{
			Outcome = outcome;
			Time = time;
			CoveredTime = coveredTime;
			Iterations = iterations;
		}
	}

	/// <summary>
	/// A ball against a chain driven by the closed form, as section 3.4 specifies it, as a
	/// pure function. Only lane 1 is active in phase 3b.
	/// </summary>
	internal static class RubberCordHitSearch
	{
		internal const int MaxIterations = 32;
		internal const double SafetyFactor = 0.8;
		internal const double MinimumStep = 1e-7;

		/// <summary>Section 3.4's per-tick cap on search-limit substeps from cords.</summary>
		internal const int SearchLimitSubstepsPerTick = 64;

		internal static double PhysTouch => PhysicsConstants.PhysTouch;
		internal static double ContactVel => PhysicsConstants.ContactVel;

		internal struct BallShot
		{
			internal double3 Position;
			internal double3 Velocity;
			internal double Radius;
		}

		/// <summary>
		/// Section 3.4's rate bound. An at-cap lane is constant within the window, so it
		/// contributes nothing and the bound is the ball speed alone.
		/// </summary>
		internal static double RateBound(in BallShot ball, in RubberModalCoefficients c,
			in RubberModalState s, bool laneAtCap)
			=> math.length(ball.Velocity) + (laneAtCap ? 0.0 : RubberModalMath.RateBound(in c, in s));

		private static double4 Q(in RubberModalState s) => new double4(s.Q, 0, 0, 0);
		private static double4 QDot(in RubberModalState s) => new double4(s.QDot, 0, 0, 0);

		internal static RubberModalState StateAt(in RubberModalCoefficients c, in RubberModalState s,
			double t, bool laneAtCap)
			=> laneAtCap ? s : RubberModalMath.Advance(in c, in s, t);

		/// <summary>Signed separation to the cord surface at time t within the window.</summary>
		internal static double SeparationAt(RubberCordChain chain, in BallShot ball,
			in RubberModalCoefficients c, in RubberModalState s, double t, bool laneAtCap)
		{
			var lane = StateAt(in c, in s, t, laneAtCap);
			var centre = ball.Position + ball.Velocity * t;
			return chain.Closest(centre, ball.Radius, Q(in lane)).Separation;
		}

		/// <summary>Relative normal velocity at time t, positive when separating.</summary>
		internal static double NormalVelocityAt(RubberCordChain chain, in BallShot ball,
			in RubberModalCoefficients c, in RubberModalState s, double t, bool laneAtCap)
		{
			var lane = StateAt(in c, in s, t, laneAtCap);
			var centre = ball.Position + ball.Velocity * t;
			var w = chain.Closest(centre, ball.Radius, Q(in lane));
			var surface = laneAtCap ? double3.zero : chain.SurfaceVelocity(in w, QDot(in lane));
			return math.dot(ball.Velocity - surface, w.Normal);
		}

		/// <summary>
		/// Conservative advancement with the classification rules of section 3.4.
		/// </summary>
		internal static RubberHitResult HitTest(RubberCordChain chain, in BallShot ball,
			in RubberModalCoefficients c, in RubberModalState s, double window, bool laneAtCap)
		{
			var rate = RateBound(in ball, in c, in s, laneAtCap);
			if (rate <= 0.0) {
				rate = 1e-12;
			}

			var sep0 = SeparationAt(chain, in ball, in c, in s, 0.0, laneAtCap);
			if (sep0 <= PhysTouch) {
				var vn0 = NormalVelocityAt(chain, in ball, in c, in s, 0.0, laneAtCap);
				if (math.abs(vn0) <= ContactVel) {
					return new RubberHitResult(RubberHitOutcome.Contact, 0.0, 0.0, 0);
				}
				if (vn0 < -ContactVel) {
					return new RubberHitResult(RubberHitOutcome.Hit, 0.0, 0.0, 0);
				}
				// in the band but separating: no zero-time event, advance with the surface
				// as the target so a return inside the window is found
			}

			var t = 0.0;
			var sep = sep0;
			for (var i = 1; i <= MaxIterations; i++) {
				var step = math.max(MinimumStep, SafetyFactor * sep / rate);
				var next = t + step;
				if (next >= window) {
					return new RubberHitResult(RubberHitOutcome.None, double.PositiveInfinity, window, i);
				}
				t = next;
				sep = SeparationAt(chain, in ball, in c, in s, t, laneAtCap);
				if (sep <= PhysTouch) {
					var vn = NormalVelocityAt(chain, in ball, in c, in s, t, laneAtCap);
					if (vn < 0.0) {
						return new RubberHitResult(RubberHitOutcome.Hit, t, t, i);
					}
				}
			}
			return new RubberHitResult(RubberHitOutcome.SearchLimit, double.PositiveInfinity, t, MaxIterations);
		}

		/// <summary>
		/// Reference scan of the same closed form at the oracle's resolution, used to look
		/// for penetrations the conservative advancement might have skipped.
		/// </summary>
		internal static double FirstPenetration(RubberCordChain chain, in BallShot ball,
			in RubberModalCoefficients c, in RubberModalState s, double window, bool laneAtCap,
			double resolution)
		{
			for (var t = 0.0; t <= window; t += resolution) {
				if (SeparationAt(chain, in ball, in c, in s, math.min(t, window), laneAtCap) <= 0.0) {
					return math.min(t, window);
				}
			}
			return double.PositiveInfinity;
		}

		/// <summary>
		/// Section 3.4's search-limit contract: limits accumulate during the ball pass and
		/// are applied as the final minimum; a tick allows at most 64 of them, beyond which
		/// further limits are ignored and the tick is counted.
		/// </summary>
		internal sealed class SearchLimitBudget
		{
			internal int SubstepsThisTick;
			internal int TotalSubsteps;
			internal int CapHitTicks;
			private bool _cappedThisTick;

			internal void BeginTick()
			{
				SubstepsThisTick = 0;
				_cappedThisTick = false;
			}

			/// <summary>Returns true when the limit may shorten the substep.</summary>
			internal bool Accept()
			{
				if (SubstepsThisTick >= SearchLimitSubstepsPerTick) {
					if (!_cappedThisTick) {
						_cappedThisTick = true;
						CapHitTicks++;
					}
					return false;
				}
				SubstepsThisTick++;
				TotalSubsteps++;
				return true;
			}
		}
	}
}
