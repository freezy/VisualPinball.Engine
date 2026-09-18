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

using NUnit.Framework;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// Section 6.1 of the static rebound plan, response half: the transactional path of
	/// 2.2 against the wall it has to reproduce at the centre, its end blend, its
	/// passivity and its non-approach guard.
	/// </summary>
	public class RubberSpanReboundResponseTests
	{
		private const float BallRadius = 25f;
		private const float CordRadius = 4f;

		#region Wall equivalence

		[Test]
		public void CentreNormalIncidenceShouldReproduceTheWallForEveryRestitution()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var guardActive = 0;
			for (var step = 0; step <= 10; step++) {
				var elasticity = step / 10f;
				var material = new PhysicsMaterialData {
					Elasticity = elasticity,
					ElasticityFalloff = 0f,
					Friction = 0.3f,
				};
				var collEvent = Impact();
				var wall = Ball(0.5f, new float3(0f, -18f, 0f), new float3(0f, 0f, -0.4f));
				var rebound = wall;
				var wallState = new PhysicsState();
				var reboundState = new PhysicsState();

				BallCollider.Collide3DWall(ref wall, in material, in collEvent,
					in collEvent.HitNormal, 1f, ref wallState);
				RubberSpanRebound.Respond(ref rebound, in material, in collEvent, in record,
					0.5f, 1f, ref reboundState, out var outcome);

				TestContext.Out.WriteLine(
					$"wallCentre|e={elasticity:F1}|wall={wall.Velocity}|rebound={rebound.Velocity}|guard={outcome.GuardFired}|steps={outcome.Steps}");
				if (outcome.GuardFired) {
					guardActive++;
					continue;
				}
				AssertClose(rebound.Velocity, wall.Velocity, 18f * 2e-3f,
					$"velocity at e={elasticity}");
				AssertClose(rebound.AngularMomentum, wall.AngularMomentum,
					math.max(1e-3f, math.length(wall.AngularMomentum) * 2e-3f),
					$"angular momentum at e={elasticity}");
			}
			Assert.That(guardActive, Is.LessThanOrEqualTo(1),
				"the guard must stay out of the way of a plain centre hit");
		}

		[Test]
		public void EmbeddedBallShouldNotGetTheWallKick()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var material = new PhysicsMaterialData { Elasticity = 0.8f };
			var collEvent = Impact();
			collEvent.HitDistance = -0.5f;
			var wall = Ball(0.5f, float3.zero, float3.zero);
			var rebound = wall;
			var wallState = new PhysicsState();
			var reboundState = new PhysicsState();

			BallCollider.Collide3DWall(ref wall, in material, in collEvent,
				in collEvent.HitNormal, 1f, ref wallState);
			RubberSpanRebound.Respond(ref rebound, in material, in collEvent, in record,
				0.5f, 1f, ref reboundState, out var outcome);

			// The documented difference of 2.2 step 1: the wall kicks an embedded ball out
			// at EmbedShot, the compliant path leaves it to the static-contact recovery.
			Assert.That(wall.Velocity.y, Is.GreaterThan(0f));
			Assert.That(rebound.Velocity, Is.EqualTo(float3.zero));
			Assert.That(rebound.Position, Is.EqualTo(Ball(0.5f, float3.zero, float3.zero).Position));
			Assert.That(outcome.Applied, Is.False);
		}

		[Test]
		public void GuardActiveScatterCaseIsAnExpectedDifferenceFromTheWall()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var material = new PhysicsMaterialData {
				Elasticity = 0f,
				Friction = 0.6f,
				ScatterAngleRad = 0.5f,
			};
			var found = false;
			for (var index = 1; index <= 400 && !found; index++) {
				// a small seed leaves Unity's xorshift near the bottom of its range for a
				// draw or two, which produces a scatter angle of a few microradians, so the
				// seeds are spread out
				var seed = (uint)(index * 2654435761u) | 1u;
				var collEvent = Impact();
				var wall = Ball(0.5f, new float3(-6f, -18f, 0f), new float3(0f, 0f, -2f));
				var rebound = wall;
				var wallState = SeededState(seed);
				var reboundState = SeededState(seed);

				BallCollider.Collide3DWall(ref wall, in material, in collEvent,
					in collEvent.HitNormal, 1f, ref wallState);
				RubberSpanRebound.Respond(ref rebound, in material, in collEvent, in record,
					0.5f, 1f, ref reboundState, out var outcome);

				if (!outcome.GuardFired) {
					continue;
				}
				found = true;
				TestContext.Out.WriteLine(
					$"guardScatter|index={index}|seed={seed}|wall={wall.Velocity}|rebound={rebound.Velocity}|wallNormal={wall.Velocity.y:F6}|reboundNormal={rebound.Velocity.y:F6}");
				Assert.That(wall.Velocity.y, Is.LessThan(0f),
					"the wall's own scatter leaves the ball slightly approaching");
				Assert.That(rebound.Velocity.y, Is.GreaterThanOrEqualTo(0f),
					"the guard must stop the approach");
			}
			Assert.That(found, Is.True, "no seed produced an approaching scatter result");
		}

		[Test]
		public void ObliqueCentreIncidenceShouldDifferFromTheWallByRecordedAmounts()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var material = new PhysicsMaterialData { Elasticity = 1f };
			foreach (var incidence in new[] { 30.0, -30.0, 60.0 }) {
				var velocity = RubberSpanReboundFixture.Velocity(18.0, incidence);
				var approach = new float3((float)velocity.Chord, (float)velocity.Normal, 0f);
				var collEvent = Impact();
				var wall = Ball(0.5f, approach, float3.zero);
				var rebound = wall;
				var wallState = new PhysicsState();
				var reboundState = new PhysicsState();

				BallCollider.Collide3DWall(ref wall, in material, in collEvent,
					in collEvent.HitNormal, 1f, ref wallState);
				RubberSpanRebound.Respond(ref rebound, in material, in collEvent, in record,
					0.5f, 1f, ref reboundState, out var outcome);

				TestContext.Out.WriteLine(
					$"obliqueCentre|inc={incidence:+0;-0;0}|wall=({wall.Velocity.x:F4},{wall.Velocity.y:F4})|rebound=({rebound.Velocity.x:F4},{rebound.Velocity.y:F4})|dChord={rebound.Velocity.x - wall.Velocity.x:F4}|dNormal={rebound.Velocity.y - wall.Velocity.y:F4}|failure={outcome.Failure}");
			}
			Assert.Pass("recorded");
		}

		#endregion

		#region End blend

		[Test]
		public void EndBlendShouldBeContinuousAcrossBothKnots()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var material = new PhysicsMaterialData { Elasticity = 0.8f, Friction = 0.1f };
			foreach (var knot in new[] { RubberSpanRebound.BlendStart, RubberSpanRebound.BlendEnd }) {
				const float epsilon = 1e-4f;
				var below = RespondAt(in record, in material, knot - epsilon,
					new float3(4f, -18f, 0f), float3.zero, out var outcomeBelow);
				var above = RespondAt(in record, in material, knot + epsilon,
					new float3(4f, -18f, 0f), float3.zero, out var outcomeAbove);

				TestContext.Out.WriteLine(
					$"blend|u={knot}|sBelow={outcomeBelow.Blend:F6}|sAbove={outcomeAbove.Blend:F6}|delta={math.distance(below, above):E3}");
				Assert.That(math.distance(below, above), Is.LessThan(0.05f),
					$"the response jumps across u = {knot}");
			}
		}

		[Test]
		public void EffectiveDepthShouldBeTheBlendTimesTheDepth()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var material = new PhysicsMaterialData { Elasticity = 0.8f };
			foreach (var u in new[] { 0.01f, 0.02f, 0.05f, 0.08f, 0.25f, 0.5f }) {
				RespondAt(in record, in material, u, new float3(0f, -18f, 0f), float3.zero,
					out var outcome);
				Assert.That(outcome.EffectiveDepth,
					Is.EqualTo(outcome.Blend * outcome.Depth).Within(1e-6f), $"u={u}");
				if (u <= RubberSpanRebound.BlendStart) {
					Assert.That(outcome.Blend, Is.Zero, $"u={u} must be purely rigid");
					Assert.That(outcome.EffectiveDepth, Is.Zero, $"u={u}");
				}
			}
		}

		[Test]
		public void TheSpinCounterexampleShouldStayPassive()
		{
			// The review's counterexample: linear material, inside the blend, oblique, with
			// a tangential surface spin velocity of -19.3 units per step.
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.LinearRampCurve);
			var material = new PhysicsMaterialData { Elasticity = 0.8f, Friction = 0.1f };
			var velocity = RubberSpanReboundFixture.Velocity(30.0, 30.0);
			var approach = new float3((float)velocity.Chord, (float)velocity.Normal, 0f);
			var ball = Ball(0.05f, approach, float3.zero);
			// surface velocity from spin at the contact point is (w.z * R, 0, -w.x * R)
			var spin = new float3(0f, 0f, -19.3f / BallRadius);
			ball.AngularMomentum = spin * ball.Inertia;
			Assert.That(BallState.SurfaceVelocity(in ball,
					new float3(0f, -BallRadius, 0f)).x - approach.x,
				Is.EqualTo(-19.3f).Within(1e-3f));

			var before = TotalEnergy(in ball);
			var collEvent = Impact();
			var state = new PhysicsState();
			RubberSpanRebound.Respond(ref ball, in material, in collEvent, in record, 0.05f,
				1f, ref state, out var outcome);
			var after = TotalEnergy(in ball);

			TestContext.Out.WriteLine(
				$"spinCounterexample|s={outcome.Blend:F4}|before={before:F6}|after={after:F6}|failure={outcome.Failure}|guard={outcome.GuardFired}");
			Assert.That(after, Is.LessThanOrEqualTo(before * (1f + 1e-4f)));
		}

		#endregion

		#region Passivity

		[Test]
		public void TenThousandRandomImpactsShouldNeverAddEnergy()
		{
			var records = new[] {
				RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve),
				RubberSpanReboundFixture.Record(RubberSpanReboundFixture.LinearRampCurve),
			};
			var random = new Random(0x5eed1234);
			var worstGain = 0.0;
			var guardFirings = 0;
			var integrated = 0;
			for (var i = 0; i < 10000; i++) {
				var record = records[random.NextInt(0, records.Length)];
				var u = random.NextFloat(0f, 1f);
				var material = new PhysicsMaterialData {
					Elasticity = random.NextFloat(0f, 1f),
					ElasticityFalloff = 0f,
					Friction = random.NextFloat(0f, 1f),
				};
				var ball = Ball(u,
					new float3(random.NextFloat(-25f, 25f), -random.NextFloat(0.5f, 40f),
						random.NextFloat(-25f, 25f)),
					random.NextFloat3(new float3(-3f), new float3(3f)));
				var before = TotalEnergy(in ball);
				var collEvent = Impact();
				var state = new PhysicsState();

				RubberSpanRebound.Respond(ref ball, in material, in collEvent, in record, u,
					random.NextFloat(0f, 1f), ref state, out var outcome);

				var after = TotalEnergy(in ball);
				worstGain = Math.Max(worstGain, (after - before) / Math.Max(before, 1e-9));
				if (outcome.GuardFired) {
					guardFirings++;
				}
				if (outcome.Failure == RubberSpanReboundFailure.None) {
					integrated++;
				}
				Assert.That(after, Is.LessThanOrEqualTo(before * (1f + 1e-3f)),
					$"impact {i} added energy (u={u})");
			}
			TestContext.Out.WriteLine(
				$"random|worstRelativeGain={worstGain:E3}|guardFirings={guardFirings}|integrated={integrated}/10000");
		}

		[Test]
		public void TheGuardShouldNotFireWithoutFrictionOrScatter()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var random = new Random(0xC0FFEE);
			for (var i = 0; i < 2000; i++) {
				var u = random.NextFloat(0f, 1f);
				var material = new PhysicsMaterialData {
					Elasticity = random.NextFloat(0f, 1f),
				};
				var ball = Ball(u,
					new float3(random.NextFloat(-25f, 25f), -random.NextFloat(0.5f, 40f),
						random.NextFloat(-25f, 25f)),
					random.NextFloat3(new float3(-3f), new float3(3f)));
				var collEvent = Impact();
				var state = new PhysicsState();

				RubberSpanRebound.Respond(ref ball, in material, in collEvent, in record, u,
					1f, ref state, out var outcome);

				Assert.That(outcome.GuardFired, Is.False,
					$"impact {i} needed the guard although nothing dissipative ran");
			}
		}

		[Test]
		public void ANonApproachingContactShouldReturnWithoutAnyEffect()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var material = new PhysicsMaterialData { Elasticity = 0.8f, Friction = 0.5f };
			var ball = Ball(0.5f, new float3(10f, -0.5f * PhysicsConstants.LowNormVel, 0f),
				new float3(0f, 0f, 1f));
			var before = ball;
			var collEvent = Impact();
			var state = new PhysicsState();

			RubberSpanRebound.Respond(ref ball, in material, in collEvent, in record, 0.5f,
				1f, ref state, out var outcome);

			Assert.That(outcome.Applied, Is.False);
			Assert.That(ball.Velocity, Is.EqualTo(before.Velocity));
			Assert.That(ball.AngularMomentum, Is.EqualTo(before.AngularMomentum));
			Assert.That(ball.Position, Is.EqualTo(before.Position));
		}

		#endregion

		#region Helpers

		private static float3 RespondAt(in RubberSpanRebound record,
			in PhysicsMaterialData material, float u, float3 velocity, float3 angularMomentum,
			out RubberSpanReboundOutcome outcome)
		{
			var ball = Ball(u, velocity, angularMomentum);
			var collEvent = Impact();
			var state = new PhysicsState();
			RubberSpanRebound.Respond(ref ball, in material, in collEvent, in record, u, 1f,
				ref state, out outcome);
			return ball.Velocity;
		}

		private static BallState Ball(float u, float3 velocity, float3 angularMomentum)
		{
			return new BallState {
				Id = 1,
				Position = new float3((float)(u * RubberSpanReboundFixture.LengthVpx),
					BallRadius + CordRadius, 0f),
				Velocity = velocity,
				AngularMomentum = angularMomentum,
				Radius = BallRadius,
				Mass = 1f,
			};
		}

		private static CollisionEventData Impact()
		{
			return new CollisionEventData {
				ColliderId = 0,
				HitNormal = new float3(0f, 1f, 0f),
				HitDistance = 0f,
			};
		}

		private static PhysicsState SeededState(uint seed)
		{
			var env = default(PhysicsEnv);
			env.Random = new Random(seed);
			return new PhysicsState { Env = env };
		}

		private static double TotalEnergy(in BallState ball)
		{
			var translational = 0.5 * ball.Mass * math.lengthsq(ball.Velocity);
			var rotational = math.lengthsq(ball.AngularMomentum) / (2.0 * ball.Inertia);
			return translational + rotational;
		}

		private static void AssertClose(float3 actual, float3 expected, float tolerance,
			string what)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), $"{what}.x");
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), $"{what}.y");
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), $"{what}.z");
		}

		#endregion
	}
}
