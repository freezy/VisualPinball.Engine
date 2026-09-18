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
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// A pure C# replica of the engine's per-substep order as sections 2.4.1, 2.4.4, 2.4.6
	/// and 2.5 define it for a deformable span, with no engine types beyond
	/// <c>PhysicsConstants</c>:
	///
	/// <list type="number">
	/// <item>once per tick, the whole tick's external velocity change is applied to every ball;</item>
	/// <item>per substep, cap arrival shortens the window;</item>
	/// <item>balls are displaced linearly;</item>
	/// <item>the free lane advances by the closed form, or stays put at its cap;</item>
	/// <item>the contact group is assembled and solved once;</item>
	/// <item>the position policy corrects penetration beyond 0.5 units;</item>
	/// <item>cap release is decided from continuous quantities only.</item>
	/// </list>
	/// </summary>
	internal sealed class RubberContactFixture
	{
		/// <summary>Mirrors the private constant in <c>BallCollider</c>.</summary>
		internal const double StaticContactPenetrationTolerance = 0.5;

		internal struct Ball
		{
			internal double3 Position;
			internal double3 Velocity;
			internal double3 AngularMomentum;
			internal double Mass;
			internal double Radius;

			/// <summary>Solid sphere: I = 2 m R^2 / 5.</summary>
			internal double Inertia => 0.4 * Mass * Radius * Radius;
		}

		private struct Row
		{
			internal RubberRowKind Kind;
			internal int BallIndex;
			internal double3 Direction;
			internal double3 Lever;
			internal double LaneJ;
			internal int NormalRow;
			internal double Mu;
			internal double3 Normal;
			internal double Separation;
		}

		internal RubberCordChain Chain;
		internal RubberModalCoefficients Modal;
		internal double Cap = RubberSpanCoefficients.DefaultDeflectionClearanceVpx;
		internal double SurfaceFriction = 0.3;

		internal RubberModalState Lane;
		internal int CapSign;

		internal readonly List<Ball> Balls = new List<Ball>();

		/// <summary>Tick-level external acceleration (gravity plus nudge plus magnets).</summary>
		internal double3 ExternalAcceleration;

		#region diagnostics

		internal double ExternalWork;
		internal int GroupSolves;
		internal int SolverFallbacks;
		internal int CapArrivals;
		internal int CapReleases;
		internal int PositionCorrections;
		internal double CapEnergyRemoved;
		internal double WorstReciprocalError;
		internal double WorstNegativeImpulse;
		internal double WorstComplementarity;
		internal double WorstFrictionExcess;
		internal double WorstEnergyGrowth;
		internal int MaxPivots;

		/// <summary>Normal rows collected by the most recent group solve.</summary>
		internal int NormalRowsLastSolve;

		/// <summary>Deepest penetration of any normal row seen at a group solve.</summary>
		internal double DeepestPenetration;

		#endregion

		internal RubberContactFixture(RubberCordChain chain, in RubberModalCoefficients modal)
		{
			Chain = chain;
			Modal = modal;
		}

		internal double4 Q => new double4(Lane.Q, 0, 0, 0);
		internal double4 QDot => new double4(Lane.QDot, 0, 0, 0);

		internal double TotalEnergy()
		{
			var e = RubberModalMath.Energy(in Modal, in Lane);
			foreach (var b in Balls) {
				e += 0.5 * b.Mass * math.lengthsq(b.Velocity);
				e += 0.5 * math.lengthsq(b.AngularMomentum) / b.Inertia;
			}
			return e;
		}

		/// <summary>
		/// Step 1 of section 2.4.1: the whole tick's external velocity change, applied once
		/// before the time-of-impact loop, exactly as <c>BallVelocityPhysics</c> does with
		/// <c>PhysFactor * gravity</c>. The energy it injects is the fixture's external work.
		/// </summary>
		internal void BeginTick()
		{
			var dv = ExternalAcceleration * RubberDeformationUnits.TickTime;
			for (var i = 0; i < Balls.Count; i++) {
				var b = Balls[i];
				var before = 0.5 * b.Mass * math.lengthsq(b.Velocity);
				b.Velocity += dv;
				var after = 0.5 * b.Mass * math.lengthsq(b.Velocity);
				ExternalWork += after - before;
				Balls[i] = b;
			}
		}

		/// <summary>
		/// One substep. Returns the substep length actually taken, which may be shorter
		/// than requested because of cap arrival.
		/// </summary>
		internal double Substep(double requested)
		{
			var energyBefore = TotalEnergy();
			var h = requested;
			var arrivalSign = 0;

			if (CapSign == 0 && h > 0.0) {
				var prediction = RubberModalMath.PredictCapArrival(in Modal, in Lane, Cap, h);
				if (prediction.Outcome == RubberCapOutcome.AlreadyAtCap) {
					arrivalSign = prediction.Sign;
					h = 0.0;
				} else if (prediction.Outcome == RubberCapOutcome.Arrival) {
					h = prediction.Time;
					arrivalSign = prediction.Sign;
				}
			}

			for (var i = 0; i < Balls.Count; i++) {
				var b = Balls[i];
				b.Position += b.Velocity * h;
				Balls[i] = b;
			}

			if (CapSign == 0) {
				Lane = RubberModalMath.Advance(in Modal, in Lane, h);
				if (arrivalSign != 0) {
					var removed = 0.5 * Modal.Mass * Lane.QDot * Lane.QDot;
					var keep = arrivalSign * Lane.QDot > 0.0 ? 0.0 : Lane.QDot;
					CapEnergyRemoved += removed - 0.5 * Modal.Mass * keep * keep;
					Lane = new RubberModalState(arrivalSign * Cap, keep);
					CapSign = arrivalSign;
					CapArrivals++;
				}
			}

			SolveGroup();

			var energyAfter = TotalEnergy();
			WorstEnergyGrowth = math.max(WorstEnergyGrowth, energyAfter - energyBefore);
			return h;
		}

		private void SolveGroup()
		{
			var rows = new List<Row>();
			var witnesses = new List<RubberCordWitness>();
			NormalRowsLastSolve = 0;

			for (var i = 0; i < Balls.Count; i++) {
				var ball = Balls[i];
				var w = Chain.Closest(ball.Position, ball.Radius, Q);
				if (w.Separation > PhysicsConstants.PhysTouch) {
					continue;
				}
				var g = w.Participation.x;
				if (math.abs(g) < 1e-3) {
					continue; // pinned anchor, section 2.4.5
				}

				var lever = -ball.Radius * w.Normal;
				var surface = Chain.SurfaceVelocity(in w, QDot);
				var pointVelocity = ball.Velocity + math.cross(ball.AngularMomentum / ball.Inertia, lever);
				var relative = pointVelocity - surface;

				NormalRowsLastSolve++;
				DeepestPenetration = math.max(DeepestPenetration, -w.Separation);
				var normalRow = rows.Count;
				rows.Add(new Row {
					Kind = RubberRowKind.Normal,
					BallIndex = i,
					Direction = w.Normal,
					Lever = lever,
					LaneJ = -g,
					NormalRow = -1,
					Normal = w.Normal,
					Separation = w.Separation,
				});
				witnesses.Add(w);

				var tangentVelocity = relative - math.dot(relative, w.Normal) * w.Normal;
				double3 tangent;
				if (math.lengthsq(tangentVelocity) > 1e-18) {
					tangent = math.normalize(tangentVelocity);
				} else {
					var axis = math.abs(w.Normal.x) < 0.9 ? new double3(1, 0, 0) : new double3(0, 1, 0);
					tangent = math.normalize(math.cross(w.Normal, axis));
				}
				// friction opposes the relative surface motion
				rows.Add(new Row {
					Kind = RubberRowKind.Friction,
					BallIndex = i,
					Direction = tangent,
					Lever = lever,
					LaneJ = -math.dot(w.BasisLane0, tangent),
					NormalRow = normalRow,
					Mu = SurfaceFriction,
					Normal = w.Normal,
					Separation = w.Separation,
				});
				witnesses.Add(w);
			}

			if (CapSign != 0) {
				rows.Add(new Row {
					Kind = RubberRowKind.Stop,
					BallIndex = -1,
					Direction = double3.zero,
					Lever = double3.zero,
					LaneJ = -CapSign,
					NormalRow = -1,
				});
				witnesses.Add(default);
			}

			if (rows.Count == 0) {
				EvaluateCapRelease(new List<Row>(), Array.Empty<double>());
				return;
			}

			var n = rows.Count;
			var a = new double[n, n];
			var b = new double[n];
			var solverRows = new RubberSolverRow[n];

			for (var i = 0; i < n; i++) {
				solverRows[i] = rows[i].Kind switch {
					RubberRowKind.Normal => RubberSolverRow.NormalRowOf(),
					RubberRowKind.Stop => RubberSolverRow.StopRowOf(),
					_ => RubberSolverRow.FrictionRowOf(rows[i].NormalRow, rows[i].Mu),
				};
				b[i] = RowVelocity(rows[i]);
				for (var j = 0; j < n; j++) {
					a[i, j] = Delassus(rows[i], rows[j]);
				}
			}

			var solution = RubberContactGroupSolver.Solve(a, b, solverRows);
			GroupSolves++;
			MaxPivots = math.max(MaxPivots, solution.Pivots);
			if (solution.TerminalPolicy) {
				SolverFallbacks++;
			}

			var laneBefore = Lane.QDot;
			var laneDelta = 0.0;
			var deltaV = new double3[Balls.Count];
			var deltaL = new double3[Balls.Count];

			for (var i = 0; i < n; i++) {
				var lambda = solution.Impulses[i];
				var row = rows[i];
				if (row.BallIndex >= 0) {
					var ball = Balls[row.BallIndex];
					deltaV[row.BallIndex] += lambda * row.Direction / ball.Mass;
					deltaL[row.BallIndex] += lambda * math.cross(row.Lever, row.Direction);
				}
				laneDelta += lambda * row.LaneJ;

				if (solverRows[i].IsUnilateral) {
					WorstNegativeImpulse = math.min(WorstNegativeImpulse, lambda);
					WorstComplementarity = math.max(WorstComplementarity, solution.RowResiduals[i]);
				} else {
					var excess = math.abs(lambda) - solution.FrictionBounds[i];
					WorstFrictionExcess = math.max(WorstFrictionExcess, excess);
				}
			}

			for (var i = 0; i < Balls.Count; i++) {
				var ball = Balls[i];
				ball.Velocity += deltaV[i];
				ball.AngularMomentum += deltaL[i];
				Balls[i] = ball;
			}
			Lane = new RubberModalState(Lane.Q, Lane.QDot + laneDelta / Modal.Mass);

			// section 7.1: M_n * delta qdot_n == -sum_rows lambda_r * g_r,n
			var reciprocal = Modal.Mass * (Lane.QDot - laneBefore) - laneDelta;
			WorstReciprocalError = math.max(WorstReciprocalError,
				math.abs(reciprocal) / math.max(1.0, math.abs(laneDelta)));

			ApplyPositionPolicy(rows, solution.Impulses);
			EvaluateCapRelease(rows, solution.Impulses);
		}

		private double RowVelocity(in Row row)
		{
			if (row.BallIndex < 0) {
				return row.LaneJ * Lane.QDot;
			}
			var ball = Balls[row.BallIndex];
			var surface = ChainSurfaceVelocity(row);
			var pointVelocity = ball.Velocity + math.cross(ball.AngularMomentum / ball.Inertia, row.Lever);
			return math.dot(pointVelocity - surface, row.Direction);
		}

		private double3 ChainSurfaceVelocity(in Row row)
		{
			// -LaneJ is the participation along the row direction, and the surface velocity
			// along that direction is qdot times it
			return row.Direction * (-row.LaneJ * Lane.QDot);
		}

		private double Delassus(in Row i, in Row j)
		{
			var acc = 0.0;
			if (i.BallIndex >= 0 && i.BallIndex == j.BallIndex) {
				var ball = Balls[i.BallIndex];
				acc += math.dot(i.Direction, j.Direction) / ball.Mass;
				acc += math.dot(math.cross(i.Lever, i.Direction), math.cross(j.Lever, j.Direction)) / ball.Inertia;
			}
			acc += i.LaneJ * j.LaneJ / Modal.Mass;
			return acc;
		}

		/// <summary>
		/// Section 2.4.4's position policy, reproducing the threshold, correction, gravity
		/// guard and ordering of <c>BallCollider.HandleStaticContact</c>.
		/// </summary>
		private void ApplyPositionPolicy(List<Row> rows, double[] impulses)
		{
			var gravity = ExternalAcceleration;
			var gravityLength = math.length(gravity);
			for (var i = 0; i < rows.Count; i++) {
				var row = rows[i];
				if (row.Kind != RubberRowKind.Normal) {
					continue;
				}
				var penetration = -row.Separation;
				if (penetration <= StaticContactPenetrationTolerance) {
					continue;
				}
				if (gravityLength > 0.0 && math.dot(row.Normal, gravity) > 0.2 * gravityLength) {
					continue;
				}
				var correction = math.min(PhysicsConstants.DispGain * penetration, PhysicsConstants.DispLimit);
				var ball = Balls[row.BallIndex];
				ball.Position += row.Normal * correction;
				Balls[row.BallIndex] = ball;
				PositionCorrections++;
			}
		}

		/// <summary>
		/// Section 2.5's release rules, both stated on continuous quantities so any
		/// partition of a tick gives the same answer.
		/// </summary>
		private void EvaluateCapRelease(List<Row> rows, double[] impulses)
		{
			if (CapSign == 0) {
				return;
			}
			if (CapSign * Lane.QDot < 0.0) {
				CapSign = 0;
				CapReleases++;
				return;
			}
			var load = ExternalLoadIntoCap(rows);
			if (Modal.Stiffness * Cap > load) {
				Lane = new RubberModalState(Lane.Q, 0.0);
				CapSign = 0;
				CapReleases++;
			}
		}

		private double ExternalLoadIntoCap(List<Row> rows)
		{
			var load = 0.0;
			for (var i = 0; i < rows.Count; i++) {
				var row = rows[i];
				if (row.Kind != RubberRowKind.Normal || row.BallIndex < 0) {
					continue;
				}
				var ball = Balls[row.BallIndex];
				var press = ball.Mass * math.dot(ExternalAcceleration, -row.Normal);
				if (press <= 0.0) {
					continue;
				}
				var g = -row.LaneJ; // participation of the lane along the normal
				load += -CapSign * press * g;
			}
			return load;
		}

		/// <summary>
		/// The same release decision as a pure function, for the partition case of section
		/// 2.5 where the load is prescribed rather than produced by a ball.
		/// </summary>
		internal static bool StaysAtCap(double stiffness, double cap, double qDotPost, int capSign, double load)
		{
			if (capSign * qDotPost < 0.0) {
				return false;
			}
			return stiffness * cap <= load;
		}
	}
}
