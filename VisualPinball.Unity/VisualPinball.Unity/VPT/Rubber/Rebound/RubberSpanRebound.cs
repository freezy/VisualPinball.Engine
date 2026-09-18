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

using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Why a surrogate integration did not produce a usable exit velocity. Every
	/// value other than <see cref="None"/> falls back to the rigid response.
	/// </summary>
	internal enum RubberSpanReboundFailure : byte
	{
		/// <summary>The integration returned inside the domain with a valid residual.</summary>
		None = 0,

		/// <summary>The 600-step budget ran out before the apex crossed the chord.</summary>
		StepBudget = 1,

		/// <summary>The state left the finite range.</summary>
		NonFinite = 2,

		/// <summary>The cord length left the compiled stress table.</summary>
		TableExhausted = 3,

		/// <summary>The apex left <c>[-0.02, 1.02]</c> in span coordinates.</summary>
		ApexOutsideSpan = 4,

		/// <summary>The return energy left the one percent residual band.</summary>
		EnergyResidual = 5,

		/// <summary>
		/// The span record or the contact geometry is degenerate, so no plane could be
		/// built. The contact takes the wall path unchanged.
		/// </summary>
		Degenerate = 6,
	}

	/// <summary>
	/// Result of one surrogate integration, in the span's contact plane.
	/// </summary>
	internal struct RubberSpanReboundTrajectory
	{
		/// <summary>True when the apex returned to the chord inside the domain.</summary>
		public bool Success;

		public RubberSpanReboundFailure Failure;

		/// <summary>Exit velocity, chord component in x and normal component in y.</summary>
		public float2 ExitVelocity;

		/// <summary>Largest depth below the chord, in VPX units.</summary>
		public float Depth;

		/// <summary>Span coordinate of the deepest point.</summary>
		public float ApexFraction;

		/// <summary>Number of integration steps taken.</summary>
		public int Steps;

		/// <summary>Return time in VPE time units, after linear refinement.</summary>
		public float ReturnTime;

		/// <summary>Relative energy change at the return, before any correction.</summary>
		public float EnergyResidual;

		/// <summary>Whether the downward energy correction was applied.</summary>
		public bool EnergyCorrected;
	}

	/// <summary>
	/// What the compliant response did, for the callers that animate and switch on it.
	/// </summary>
	internal struct RubberSpanReboundOutcome
	{
		/// <summary>False when the low-normal-velocity gate returned without an effect.</summary>
		public bool Applied;

		/// <summary>True when the contact was handed to the wall response unchanged.</summary>
		public bool FellBackToWall;

		/// <summary>The end blend <c>s</c> of the plan's 2.2 step 5.</summary>
		public float Blend;

		/// <summary>The resolved, clamped restitution.</summary>
		public float Elasticity;

		/// <summary>Span coordinate of the deepest point, <c>u_h</c>.</summary>
		public float ApexFraction;

		/// <summary>The blended depth <c>s * delta</c>, zero for a rigid or failed response.</summary>
		public float EffectiveDepth;

		/// <summary>The unblended depth <c>delta</c>.</summary>
		public float Depth;

		public RubberSpanReboundFailure Failure;
		public int Steps;
		public float ReturnTime;
		public float EnergyResidual;
		public bool EnergyCorrected;

		/// <summary>Whether the non-approach guard had to add an impulse.</summary>
		public bool GuardFired;
	}

	/// <summary>
	/// One straight free span of a Physical rubber, reduced to a massless string pinned
	/// at two anchors, plus the transactional contact response that integrates it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Constant data, written once at registration and read only by the collide pass.
	/// Everything is in VPE physics units and in the same space as the span's capsules,
	/// which for the static rubbers this applies to is playfield space.
	/// </para>
	/// <para>
	/// The model is the plan's section 2.1: the ball's mass is placed at the apex of a
	/// massless string pinned at <see cref="AnchorA"/> and <see cref="AnchorB"/>, with
	/// the string force
	/// <c>F(P) = T(l) * ((A - P) / |A - P| + (B - P) / |B - P|)</c>,
	/// <c>l = |A - P| + |B - P|</c>, <c>T(l) = sigma(l / L0) * A0</c> and <c>T = 0</c>
	/// while <c>l &lt;= L</c>.
	/// </para>
	/// </remarks>
	internal struct RubberSpanRebound
	{
		#region Constants

		/// <summary>Fixed integration step, 0.5 ms of simulation time.</summary>
		internal const float IntegrationStep = 0.05f;

		/// <summary>Largest number of steps one integration may take (300 ms).</summary>
		internal const int StepBudget = 600;

		/// <summary>Lower bound of the valid span coordinate of the apex.</summary>
		internal const float MinimumSpanCoordinate = -0.02f;

		/// <summary>Upper bound of the valid span coordinate of the apex.</summary>
		internal const float MaximumSpanCoordinate = 1.02f;

		/// <summary>Accepted band of the return energy residual.</summary>
		internal const float EnergyResidualTolerance = 0.01f;

		/// <summary>
		/// Residual below which the downward energy correction is float noise and is
		/// not applied.
		/// </summary>
		internal const float EnergyCorrectionThreshold = 1e-6f;

		/// <summary>Span coordinate below which the response is purely rigid.</summary>
		internal const float BlendStart = 0.02f;

		/// <summary>Span coordinate above which the response is fully compliant.</summary>
		internal const float BlendEnd = 0.08f;

		#endregion

		#region Record

		/// <summary>The anchor at span coordinate zero.</summary>
		public float3 AnchorA;

		/// <summary>The anchor at span coordinate one.</summary>
		public float3 AnchorB;

		/// <summary>Unit chord direction, from <see cref="AnchorA"/> toward <see cref="AnchorB"/>.</summary>
		public float3 ChordDirection;

		/// <summary>Unit outward normal of the rest chord, pointing away from the loop interior.</summary>
		public float3 OutwardNormal;

		/// <summary>Installed chord length <c>L</c>, in VPX units.</summary>
		public float Length;

		/// <summary>Unloaded length <c>L0</c>, in VPX units.</summary>
		public float UnloadedLength;

		/// <summary>Cross-section area <c>A0</c>, in square VPX units.</summary>
		public float CrossSectionArea;

		/// <summary>Largest stretch ratio the stress table is defined for.</summary>
		public float MaximumStretchRatio;

		/// <summary>The rubber's item id.</summary>
		public int ItemId;

		/// <summary>Index of the span's free-span element in the rubber's baked path.</summary>
		public int PathElementIndex;

		/// <summary>
		/// The compiled material's 128-sample stress table over
		/// <c>[1, MaximumStretchRatio]</c>, both endpoints included.
		/// </summary>
		public FixedList4096Bytes<float> StressTable;

		/// <summary>Installed stretch ratio <c>L / L0</c>.</summary>
		public float InstalledStretch => UnloadedLength > 0f ? Length / UnloadedLength : 0f;

		/// <summary>
		/// Reads the nominal stress at a stretch ratio, with exactly the layout and the
		/// linear interpolation of <see cref="RubberCompiledDeformationMaterial"/>.
		/// </summary>
		public float SampleStress(float stretchRatio)
		{
			var count = StressTable.Length;
			if (count == 0) {
				return 0f;
			}
			if (count == 1 || !math.isfinite(stretchRatio)) {
				return StressTable[0];
			}
			var maximum = math.max(1f + 1e-6f, MaximumStretchRatio);
			var position = math.saturate((stretchRatio - 1f) / (maximum - 1f)) * (count - 1);
			var lower = (int)math.floor(position);
			if (lower >= count - 1) {
				return StressTable[count - 1];
			}
			return math.lerp(StressTable[lower], StressTable[lower + 1], position - lower);
		}

		/// <summary>True when the record carries enough data to integrate.</summary>
		public bool IsUsable => Length > 0f && UnloadedLength > 0f && CrossSectionArea > 0f
			&& MaximumStretchRatio > 1f && StressTable.Length > 1
			&& math.lengthsq(ChordDirection) > 0.5f;

		#endregion

		#region Surrogate integration

		/// <summary>
		/// Integrates the plan's 2.1 surrogate: a virtual point mass at the apex of the
		/// massless string, started at the foot of the contact with the in-plane part of
		/// the relative velocity, until the apex returns to the chord.
		/// </summary>
		/// <param name="span">The span record.</param>
		/// <param name="u">Arc-length fraction of the contact along the span.</param>
		/// <param name="velocity">In-plane velocity, chord in x, outward normal in y.</param>
		/// <param name="mass">The ball's mass.</param>
		internal static RubberSpanReboundTrajectory Integrate(in RubberSpanRebound span, float u,
			float2 velocity, float mass)
		{
			var result = new RubberSpanReboundTrajectory {
				ExitVelocity = velocity,
				ApexFraction = u,
			};
			if (!span.IsUsable || !(mass > 0f) || !math.all(math.isfinite(velocity))) {
				result.Failure = RubberSpanReboundFailure.Degenerate;
				return result;
			}
			var length = span.Length;
			var initialEnergy = 0.5f * mass * math.lengthsq(velocity);
			if (!(initialEnergy > 0f)) {
				result.Failure = RubberSpanReboundFailure.Degenerate;
				return result;
			}

			// The integrator's origin is the foot of the contact on the chord, so the
			// anchors sit at -u * L and (1 - u) * L along x.
			var anchorA = -u * length;
			var anchorB = (1f - u) * length;
			var position = float2.zero;
			var entered = false;
			var depth = 0f;
			var apex = u;

			for (var step = 1; step <= StepBudget; step++) {
				var previousPosition = position;
				var previousVelocity = velocity;
				Step(in span, anchorA, anchorB, mass, ref position, ref velocity);
				result.Steps = step;

				if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity))) {
					result.Failure = RubberSpanReboundFailure.NonFinite;
					return result;
				}

				var spanCoordinate = u + position.x / length;
				if (spanCoordinate < MinimumSpanCoordinate
					|| spanCoordinate > MaximumSpanCoordinate) {
					result.Failure = RubberSpanReboundFailure.ApexOutsideSpan;
					return result;
				}

				var cordLength = math.distance(position, new float2(anchorA, 0f))
					+ math.distance(position, new float2(anchorB, 0f));
				if (cordLength / span.UnloadedLength > span.MaximumStretchRatio) {
					result.Failure = RubberSpanReboundFailure.TableExhausted;
					return result;
				}

				if (-position.y > depth) {
					depth = -position.y;
					apex = spanCoordinate;
				}

				if (!entered) {
					entered = position.y < 0f;
					continue;
				}
				if (!(previousPosition.y < 0f) || position.y < 0f) {
					continue;
				}

				// the return: refine the crossing linearly between the two steps
				var rise = position.y - previousPosition.y;
				var fraction = rise > 0f
					? math.saturate(-previousPosition.y / rise)
					: 0f;
				var exit = math.lerp(previousVelocity, velocity, fraction);
				var exitEnergy = 0.5f * mass * math.lengthsq(exit);
				var residual = (exitEnergy - initialEnergy) / initialEnergy;
				result.EnergyResidual = residual;
				if (!math.isfinite(residual) || math.abs(residual) > EnergyResidualTolerance) {
					result.Failure = RubberSpanReboundFailure.EnergyResidual;
					return result;
				}
				if (residual > EnergyCorrectionThreshold) {
					// RK4 is not exactly conservative; never let it hand back energy
					exit *= math.sqrt(initialEnergy / exitEnergy);
					result.EnergyCorrected = true;
				}
				result.ExitVelocity = exit;
				result.Depth = depth;
				result.ApexFraction = apex;
				result.ReturnTime = (step - 1 + fraction) * IntegrationStep;
				result.Success = true;
				return result;
			}

			result.Failure = RubberSpanReboundFailure.StepBudget;
			return result;
		}

		/// <summary>One fixed-step fourth-order Runge-Kutta step of <c>m P'' = F(P)</c>.</summary>
		private static void Step(in RubberSpanRebound span, float anchorA, float anchorB,
			float mass, ref float2 position, ref float2 velocity)
		{
			const float h = IntegrationStep;
			var p1 = velocity;
			var v1 = Force(in span, anchorA, anchorB, position) / mass;
			var p2 = velocity + 0.5f * h * v1;
			var v2 = Force(in span, anchorA, anchorB, position + 0.5f * h * p1) / mass;
			var p3 = velocity + 0.5f * h * v2;
			var v3 = Force(in span, anchorA, anchorB, position + 0.5f * h * p2) / mass;
			var p4 = velocity + h * v3;
			var v4 = Force(in span, anchorA, anchorB, position + h * p3) / mass;
			position += h / 6f * (p1 + 2f * p2 + 2f * p3 + p4);
			velocity += h / 6f * (v1 + 2f * v2 + 2f * v3 + v4);
		}

		/// <summary>The string force on the surrogate at an apex position.</summary>
		private static float2 Force(in RubberSpanRebound span, float anchorA, float anchorB,
			float2 position)
		{
			var toA = new float2(anchorA, 0f) - position;
			var toB = new float2(anchorB, 0f) - position;
			var distanceA = math.length(toA);
			var distanceB = math.length(toB);
			if (!(distanceA > 1e-6f) || !(distanceB > 1e-6f)) {
				return float2.zero;
			}
			var cordLength = distanceA + distanceB;
			if (cordLength <= span.Length) {
				return float2.zero;
			}
			var tension = span.SampleStress(cordLength / span.UnloadedLength)
				* span.CrossSectionArea;
			if (!(tension > 0f)) {
				return float2.zero;
			}
			return tension * (toA / distanceA + toB / distanceB);
		}

		#endregion

		#region Response

		/// <summary>
		/// The transactional response of the plan's 2.2. Everything up to the end blend
		/// is computed on copies; only the commit touches the ball, the random state or
		/// the event queue.
		/// </summary>
		/// <param name="ball">The colliding ball.</param>
		/// <param name="material">The capsule's contact material.</param>
		/// <param name="collEvent">The collision event of this contact.</param>
		/// <param name="span">The span record.</param>
		/// <param name="u">Arc-length fraction of the contact along the span.</param>
		/// <param name="elasticityScale">The capsule's guide-distance elasticity scale.</param>
		/// <param name="state">The physics state.</param>
		/// <param name="outcome">What the response did, for animation and switches.</param>
		internal static void Respond(ref BallState ball, in PhysicsMaterialData material,
			in CollisionEventData collEvent, in RubberSpanRebound span, float u,
			float elasticityScale, ref PhysicsState state,
			out RubberSpanReboundOutcome outcome)
		{
			outcome = new RubberSpanReboundOutcome { ApexFraction = u };

			// The response plane contains the chord and the transverse part of the
			// contact normal. For an in-plane contact that part is the span's own
			// outward normal, which is what the plan's model assumes; for a contact from
			// outside the bake plane the pinned-string geometry is unchanged, because the
			// anchors lie on the chord either way.
			var chord = span.ChordDirection;
			var transverse = collEvent.HitNormal - chord * math.dot(collEvent.HitNormal, chord);
			var transverseLength = math.length(transverse);
			if (!span.IsUsable || !(transverseLength > 1e-4f)) {
				outcome.Failure = RubberSpanReboundFailure.Degenerate;
				outcome.FellBackToWall = true;
				BallCollider.Collide3DWall(ref ball, in material, in collEvent,
					in collEvent.HitNormal, elasticityScale, ref state);
				return;
			}
			var normal = transverse / transverseLength;

			// 1. relative velocity against the capsule's surface velocity. Unlike
			// Collide3DWall this path deliberately omits the embedded-ball kick; the
			// capsule's touch band and the static-contact recovery handle that case.
			var colliderVelocity = state.GetKinematicSurfaceVelocity(in collEvent,
				ball.Position - ball.Radius * normal);
			var relative = ball.Velocity - colliderVelocity;
			var normalVelocity = math.dot(relative, normal);
			if (normalVelocity >= -PhysicsConstants.LowNormVel) {
				return;
			}

			// 2. restitution, resolved as the wall does and clamped to [0, 1]
			var elasticity = math.saturate(BallCollider.ResolveElasticity(in material,
				in collEvent, normalVelocity, ref state) * elasticityScale);
			outcome.Elasticity = elasticity;

			// 3. rigid branch
			var normalImpulse = -(1f + elasticity) * normalVelocity;
			var rigid = relative + normal * normalImpulse;

			// 4. compliant branch
			var binormal = math.cross(chord, normal);
			var trajectory = Integrate(in span, u,
				new float2(math.dot(relative, chord), normalVelocity), ball.Mass);
			outcome.Failure = trajectory.Failure;
			outcome.Steps = trajectory.Steps;
			outcome.ReturnTime = trajectory.ReturnTime;
			outcome.EnergyResidual = trajectory.EnergyResidual;
			outcome.EnergyCorrected = trajectory.EnergyCorrected;
			var compliant = rigid;
			if (trajectory.Success) {
				var exit = chord * trajectory.ExitVelocity.x + normal * trajectory.ExitVelocity.y
					+ binormal * math.dot(relative, binormal);
				var exitNormal = math.dot(exit, normal);
				compliant = exit - normal * exitNormal + normal * (elasticity * exitNormal);
				outcome.Depth = trajectory.Depth;
				outcome.ApexFraction = trajectory.ApexFraction;
			}

			// 5. end blend, still on copies and still passive
			var blend = math.smoothstep(BlendStart, BlendEnd, math.min(u, 1f - u));
			var preFriction = math.lerp(rigid, compliant, blend);
			outcome.Blend = blend;
			outcome.EffectiveDepth = blend * outcome.Depth;

			// 6. commit, once, in the wall's order
			var displacement = -PhysicsConstants.DispGain * collEvent.HitDistance;
			if (displacement > 1.0e-4f) {
				if (displacement > PhysicsConstants.DispLimit) {
					displacement = PhysicsConstants.DispLimit;
				}
				ball.Position += normal * displacement;
			}
			ball.Velocity += preFriction - relative;

			BallCollider.ApplyImpactFriction(ref ball, in material, in collEvent, in normal,
				in colliderVelocity, ball.Mass * math.abs(normalVelocity), ref state);

			var scatterAngle = material.ScatterAngleRad;
			if (scatterAngle < 0f) {
				scatterAngle = BallCollider.HardScatter; // the wall's global value
			}
			scatterAngle *= 0.2f; // difficulty weighting, as the wall does
			if (normalImpulse > 1f && scatterAngle > 1.0e-5f) {
				var scatter = state.Env.Random.NextFloat(-1f, 1f);
				scatter *= (1f - scatter * scatter) * 2.59808f * scatterAngle;
				var radSin = math.sin(scatter);
				var radCos = math.cos(scatter);
				var vxt = ball.Velocity.x;
				var vyt = ball.Velocity.y;
				ball.Velocity.x = vxt * radCos - vyt * radSin;
				ball.Velocity.y = vyt * radCos + vxt * radSin;
			}

			// the guard: friction, spin and scatter must not leave the ball approaching
			var residualNormal = math.dot(ball.Velocity - colliderVelocity, normal);
			if (residualNormal < 0f) {
				ball.Velocity -= normal * residualNormal;
				outcome.GuardFired = true;
			}

			outcome.Applied = true;

			// HOOK (commit 2): the impact event of the plan's section 4 is emitted here,
			// carrying outcome.ApexFraction (u_h) and outcome.EffectiveDepth (delta_eff),
			// and only when outcome.EffectiveDepth > 0.
		}

		#endregion
	}
}
