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
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Physics fixes ported from Visual Pinball, mostly its FIX_PHYSICS path.
	/// </summary>
	public class PhysicsAlignmentTests
	{
		private const float Tolerance = 1e-5f;

		#region Wall touch band

		[Test]
		public void SlowWallTouchIsAContact()
		{
			// wall along y, facing +x
			var wall = new LineCollider(new float2(0f, 100f), new float2(0f, -100f), 0f, 50f, new ColliderInfo { ItemId = 1 });
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
				Position = new float3(25.02f, 0f, 25f), // 0.02 off the wall: inside the touch band
				Velocity = new float3(-0.05f, 0f, 0f), // slower than ContactVel
			};
			var insideOfs = default(InsideOfs);
			var collEvent = new CollisionEventData();

			var hitTime = wall.HitTest(ref collEvent, ref insideOfs, in ball, PhysicsConstants.PhysFactor);

			Assert.That(hitTime, Is.Zero, "a slow touch is a contact now, not a collision at a remapped time");
			Assert.That(collEvent.IsContact, Is.True);
			Assert.That(collEvent.HitOrgNormalVelocity, Is.EqualTo(-0.05f).Within(Tolerance));
			Assert.That(collEvent.HitDistance, Is.EqualTo(0.02f).Within(1e-4f));
		}

		#endregion

		#region Contact friction

		private static readonly float3 DownGravity = new(0f, 0f, -1f);

		[TestCase(1f)]
		[TestCase(4f)]
		public void RestingContactVelocityGainIsMassIndependent(float mass)
		{
			var ball = new BallState { Mass = mass, Radius = 25f };
			var floor = new CollisionEventData { ColliderId = 1, HitNormal = new float3(0f, 0f, 1f) };

			BallCollider.HandleStaticContact(ref ball, in floor, 0f, 0.1f, in DownGravity, float3.zero);

			Assert.That(ball.Velocity.z, Is.EqualTo(0.1f).Within(Tolerance),
				"the contact cancels one step of gravity, it must not throw a heavy ball off its support");
		}

		[Test]
		public void BallSurfaceAccelerationIsMassIndependent()
		{
			var ball = new BallState { Mass = 4f, Radius = 25f };

			var acceleration = BallState.SurfaceAcceleration(in ball, new float3(0f, 0f, -25f), in DownGravity);

			AssertFloat3(acceleration, DownGravity);
		}

		[Test]
		public void BallSlidingAlongAWallIsBrakedByWallFriction()
		{
			// the ball approaches the wall slowly while sliding along it; gravity is parallel to
			// the wall, so a cone bounded by gravity alone has no budget at all, and the static
			// branch would only push along gravity
			var ball = new BallState { Mass = 1f, Radius = 25f, Velocity = new float3(-0.05f, 5f, 0f) };
			var wall = new CollisionEventData {
				ColliderId = 1,
				HitNormal = new float3(1f, 0f, 0f),
				HitOrgNormalVelocity = -0.05f,
			};
			var frictionVelocity = ball.Velocity;
			var frictionAngularMomentum = ball.AngularMomentum;

			BallCollider.HandleStaticContact(ref ball, in wall, 0.3f, 0.1f, in DownGravity, float3.zero,
				in DownGravity, in frictionVelocity, in frictionAngularMomentum);

			Assert.That(ball.Velocity.x, Is.EqualTo(0f).Within(Tolerance), "the approach is cancelled");
			Assert.That(ball.Velocity.y, Is.EqualTo(5f - 0.3f * 0.05f).Within(Tolerance),
				"kinetic friction bounded by mu times the normal impulse brakes the slide");
			Assert.That(ball.Velocity.z, Is.EqualTo(0f).Within(Tolerance), "no static friction along gravity");
			Assert.That(ball.AngularMomentum.z, Is.Not.EqualTo(0f), "wall friction spins the ball");
		}

		[Test]
		public void SupportContactKeepsTheStaticFrictionQuench()
		{
			// on a support, a residual normal velocity below 0.025 after the normal solve still
			// selects the static branch: friction counters the tangential gravity of the 6 degree
			// slope, not the slide (VPX's spin quench, kept for supports)
			var slope = math.radians(6f);
			var gravity = new float3(0f, math.sin(slope), -math.cos(slope));
			var ball = new BallState { Mass = 1f, Radius = 25f, Velocity = new float3(2f, 0f, -0.2f) };
			var floor = new CollisionEventData { ColliderId = 1, HitNormal = new float3(0f, 0f, 1f) };
			var frictionVelocity = ball.Velocity;
			var frictionAngularMomentum = ball.AngularMomentum;

			BallCollider.HandleStaticContact(ref ball, in floor, 0.3f, 0.1f, in gravity, float3.zero,
				in gravity, in frictionVelocity, in frictionAngularMomentum);

			Assert.That(ball.Velocity.x, Is.EqualTo(2f).Within(Tolerance));
			// with I = 2/5 m r^2 the impulse denominator is 3.5 / m
			Assert.That(ball.Velocity.y, Is.EqualTo(-0.1f * math.sin(slope) / 3.5f).Within(Tolerance));
		}

		[Test]
		public void SlidingFrictionRemovesASmallSlipInOneStep()
		{
			// VPX applied a tenth of the slip-removing impulse per 0.1 step (dTime times it)
			var ball = new BallState { Mass = 1f, Radius = 25f, Velocity = new float3(0.05f, 0f, 0f) };
			var floor = new CollisionEventData { ColliderId = 1, HitNormal = new float3(0f, 0f, 1f) };
			var frictionVelocity = ball.Velocity;
			var frictionAngularMomentum = ball.AngularMomentum;

			BallCollider.HandleStaticContact(ref ball, in floor, 1f, 0.1f, in DownGravity, float3.zero,
				in DownGravity, in frictionVelocity, in frictionAngularMomentum);

			Assert.That(TangentialSlipSpeed(in ball, new float3(0f, 0f, 1f)), Is.LessThan(1e-4f),
				"a slip within the Coulomb cone is removed at once: the ball rolls");
			Assert.That(ball.Velocity.x, Is.EqualTo(0.05f * (1f - 1f / 3.5f)).Within(Tolerance));
		}

		[Test]
		public void LaterContactsOfTheSameBallSeeTheFrictionOfEarlierOnes()
		{
			var normal = new float3(0f, 0f, 1f);
			var contacts = new NativeList<ContactBufferElement>(3, Allocator.Temp);
			try {
				contacts.Add(new ContactBufferElement(7, new CollisionEventData { HitNormal = normal }));
				contacts.Add(new ContactBufferElement(7, new CollisionEventData { HitNormal = normal }));
				contacts.Add(new ContactBufferElement(8, new CollisionEventData { HitNormal = normal }));

				PhysicsCycle.PassOn(ref contacts, 0, new float3(1f, 2f, 3f), new float3(4f, 5f, 6f));

				AssertFloat3(contacts[0].FrictionVelocity, float3.zero);
				AssertFloat3(contacts[1].FrictionVelocity, new float3(1f, 2f, 0f), "the normal part is not slip");
				AssertFloat3(contacts[1].FrictionAngularMomentum, new float3(4f, 5f, 6f));
				AssertFloat3(contacts[1].PriorContactVelocity, new float3(1f, 2f, 0f), "a static contact passes on its friction");
				AssertFloat3(contacts[2].FrictionVelocity, float3.zero, "another ball");
			} finally {
				contacts.Dispose();
			}
		}

		#endregion

		#region Flipper contact

		private const float FlipperInertia = 1f / 3f * 80f * 80f;

		// a resting flipper with its base at the origin, the ball touching a face whose normal is +x
		private static void FlipperContact(ref BallState ball, float friction, in float3 acceleration)
		{
			var flipper = new FlipperCollider(50f, 80f, 30f, 15f, 0f, 0f, new ColliderInfo { ItemId = 1 });
			flipper.Header.Material.Friction = friction;
			var movement = new FlipperMovementState();
			var staticData = new FlipperStaticData { Inertia = FlipperInertia };
			var velocityData = new FlipperVelocityData();
			var collEvent = new CollisionEventData {
				HitNormal = new float3(1f, 0f, 0f),
				HitDistance = 0.02f,
				HitOrgNormalVelocity = math.dot(ball.Velocity, new float3(1f, 0f, 0f)),
			};
			var frictionVelocity = ball.Velocity;
			var frictionAngularMomentum = ball.AngularMomentum;
			flipper.Contact(ref ball, ref movement, in collEvent, in staticData, in velocityData, 0.1f,
				in acceleration, in acceleration, in frictionVelocity, in frictionAngularMomentum);
		}

		[Test]
		public void SpinningBallKeepsFlipperContactSupport()
		{
			var acceleration = new float3(-1f, 0f, -1f); // pressing the ball onto the face
			var still = new BallState { Mass = 1f, Radius = 25f, Position = new float3(55f, -40f, 25f), Velocity = new float3(-0.05f, 0f, 0f) };
			FlipperContact(ref still, 0f, in acceleration);
			Assert.That(still.Velocity.x, Is.GreaterThan(-0.01f), "sanity: the contact kills the approach");

			// w = 4 about z: a centripetal acceleration of w^2 r = 400 along the normal at the material point
			var spinning = still;
			spinning.Velocity = new float3(-0.05f, 0f, 0f);
			spinning.AngularMomentum = new float3(0f, 0f, 4f * spinning.Inertia);
			FlipperContact(ref spinning, 0f, in acceleration);

			Assert.That(spinning.Velocity.x, Is.GreaterThan(-0.01f), "spin must not release the contact");
		}

		[Test]
		public void FlipperStaticFrictionAddsTheFlipperInertia()
		{
			// the ball rests on the face and gravity pulls it along the face (-y): static friction
			var acceleration = new float3(-1f, -1f, 0f);
			var ball = new BallState { Mass = 1f, Radius = 25f, Position = new float3(55f, -40f, 25f), Velocity = new float3(-0.05f, 0f, 0f) };
			FlipperContact(ref ball, 10f, in acceleration);

			// rF = (30, -40), slip direction -y: the flipper term is |rF.x|^2 / I = 900 / I, the ball's 2.5
			Assert.That(ball.Velocity.y, Is.EqualTo(0.1f / (3.5f + 900f / FlipperInertia)).Within(Tolerance));
		}

		#endregion

		#region Flipper inertia

		[TestCase(21.5f, 13f, 130f)] // VPX defaults
		[TestCase(30f, 15f, 80f)]
		[TestCase(15f, 15f, 100f)]
		[TestCase(40f, 8f, 60f)]
		public void FlipperInertiaShapeMatchesTheConvexHullOfItsCircles(float baseRadius, float endRadius, float flipperRadius)
		{
			// numerically integrate r^2 over the hull of the two circles: a point is inside if it is
			// within the interpolated circle at some point between the two centers
			const float cell = 0.25f;
			double area = 0, moment = 0;
			for (var x = -baseRadius; x <= flipperRadius + endRadius; x += cell) {
				for (var y = -baseRadius; y <= baseRadius; y += cell) {
					var inside = false;
					for (var i = 0; i <= 200 && !inside; i++) {
						var t = i / 200f;
						var cx = t * flipperRadius;
						var r = math.lerp(baseRadius, endRadius, t);
						inside = (x - cx) * (x - cx) + y * y <= r * r;
					}
					if (inside) {
						area += cell * cell;
						moment += cell * cell * (x * x + y * y);
					}
				}
			}

			var shape = FlipperComponent.InertiaShape(baseRadius, endRadius, flipperRadius);

			Assert.That(shape, Is.EqualTo((float)(moment / area)).Within(1).Percent);
		}

		#endregion

		#region Ball/ball

		[Test]
		public void BallBallCollisionNormalIsTakenAtHitTime()
		{
			// an oblique hit on a resting ball: the moving ball touches it side by side at t = 1
			var resting = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f) };
			var moving = new BallState { Id = 2, Mass = 1f, Radius = 25f, Position = new float3(-60f, -10f, 25f), Velocity = new float3(10f, 10f, 0f) };
			var collEvent = new CollisionEventData();

			var hitTime = BallCollider.HitTest(ref collEvent, ref moving, in resting, 2f);

			Assert.That(hitTime, Is.EqualTo(1f).Within(1e-4f));
			AssertFloat3(collEvent.HitNormal, new float3(-1f, 0f, 0f), "from the hit ball towards the moving one");
		}

		[Test]
		public void SlowlyTouchingBallPairIsAContact()
		{
			var still = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(50.02f, 0f, 25f) }; // 0.02 apart
			var moving = new BallState { Id = 2, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f), Velocity = new float3(0.05f, 0f, 0f) };
			var collEvent = new CollisionEventData();

			Assert.That(BallCollider.HitTest(ref collEvent, ref moving, in still, 0.1f), Is.Zero);
			Assert.That(collEvent.IsContact, Is.True);
			Assert.That(collEvent.HitOrgNormalVelocity, Is.EqualTo(-0.05f).Within(Tolerance));
			Assert.That(collEvent.HitDistance, Is.EqualTo(0.02f).Within(1e-4f));
		}

		[Test]
		public void OnlyAPathologicalBallOverlapIsACollision()
		{
			var moving = new BallState { Id = 2, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f), Velocity = new float3(0.05f, 0f, 0f) };

			// the resting band: the contact drains the overlap
			var still = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(49.94f, 0f, 25f) };
			var collEvent = new CollisionEventData();
			Assert.That(BallCollider.HitTest(ref collEvent, ref moving, in still, 0.1f), Is.Zero);
			Assert.That(collEvent.IsContact, Is.True);

			// deep past it: a collision, so the displacement correction digs the balls out
			still.Position = new float3(49.4f, 0f, 25f);
			collEvent = new CollisionEventData();
			Assert.That(BallCollider.HitTest(ref collEvent, ref moving, in still, 0.1f), Is.Zero);
			Assert.That(collEvent.IsContact, Is.False);
		}

		[Test]
		public void BallContactSupportsTheBallOnTop()
		{
			var gravity = new float3(0f, 0f, -PhysicsConstants.GravityConst);
			var top = new BallState { Id = 1, Mass = 1f, Radius = 25f, Velocity = gravity * PhysicsConstants.PhysFactor };
			var bottom = new BallState { Id = 2, Mass = 1f, Radius = 25f };
			var contact = new CollisionEventData { HitNormal = new float3(0f, 0f, 1f) };

			HandleBallContact(ref top, in bottom, in contact, in gravity);

			Assert.That(top.Velocity.z, Is.EqualTo(0f).Within(Tolerance), "the step of gravity is cancelled");
		}

		[Test]
		public void TwoFreeBallsInSlowContactDoNotCancelTheirApproachTwice()
		{
			var gravity = new float3(0f, 0f, -PhysicsConstants.GravityConst); // flat: none along the contact normal
			var a = new BallState { Id = 1, Mass = 1f, Radius = 25f, Velocity = new float3(0.025f, 0f, 0f) };
			var b = new BallState { Id = 2, Mass = 1f, Radius = 25f, Velocity = new float3(-0.025f, 0f, 0f) };
			var aAgainstB = new CollisionEventData { HitNormal = new float3(-1f, 0f, 0f) }; // from b towards a
			var bAgainstA = new CollisionEventData { HitNormal = new float3(1f, 0f, 0f) };

			HandleBallContact(ref a, in b, in aAgainstB, in gravity);
			HandleBallContact(ref b, in a, in bAgainstA, in gravity);

			Assert.That(a.Velocity.x - b.Velocity.x, Is.EqualTo(0f).Within(Tolerance), "no separation");
			Assert.That(a.Velocity.x + b.Velocity.x, Is.EqualTo(0f).Within(Tolerance), "momentum conserved");
		}

		[Test]
		public void FreeBallAgainstAFrozenBallTakesTheWholeResponse()
		{
			var gravity = new float3(0f, 0f, -PhysicsConstants.GravityConst);
			var a = new BallState { Id = 1, Mass = 1f, Radius = 25f, Velocity = new float3(0.05f, 0f, 0f) };
			var locked = new BallState { Id = 2, Mass = 1f, Radius = 25f, IsFrozen = true };
			var contact = new CollisionEventData { HitNormal = new float3(-1f, 0f, 0f) };

			HandleBallContact(ref a, in locked, in contact, in gravity);

			Assert.That(a.Velocity.x, Is.EqualTo(0f).Within(Tolerance));
		}

		[Test]
		public void TwoBallsRollingSideBySideAreNotBrakedByTheirContact()
		{
			var gravity = new float3(0f, 0f, -PhysicsConstants.GravityConst);
			const float speed = 10f;
			var a = new BallState { Id = 1, Mass = 1f, Radius = 25f, Velocity = new float3(0.025f, speed, 0f) };
			var b = new BallState { Id = 2, Mass = 1f, Radius = 25f, Velocity = new float3(-0.025f, speed, 0f) };
			// rolling on the playfield along +y: their surfaces move together where they touch
			a.AngularMomentum = new float3(-speed / a.Radius * a.Inertia, 0f, 0f);
			b.AngularMomentum = a.AngularMomentum;
			var aAgainstB = new CollisionEventData { HitNormal = new float3(-1f, 0f, 0f) };
			var bAgainstA = new CollisionEventData { HitNormal = new float3(1f, 0f, 0f) };

			HandleBallContact(ref a, in b, in aAgainstB, in gravity);
			HandleBallContact(ref b, in a, in bAgainstA, in gravity);

			Assert.That(a.Velocity.y, Is.EqualTo(speed).Within(1e-5f), "a static ball would see a slip of 10 and brake");
			Assert.That(b.Velocity.y, Is.EqualTo(speed).Within(1e-5f));
			Assert.That(a.AngularMomentum.x, Is.EqualTo(-speed / a.Radius * a.Inertia).Within(1e-3f));
		}

		[Test]
		public void BallContactPassesAnImpactOn()
		{
			// a ball hit into a resting pair earlier in the cycle: the contact cancels a contact-scale
			// approach only, the rest reaches the other ball through their collision (Newton's cradle)
			var gravity = new float3(0f, 0f, -PhysicsConstants.GravityConst);
			var a = new BallState { Id = 1, Mass = 1f, Radius = 25f, Velocity = new float3(2f, 0f, 0f) };
			var b = new BallState { Id = 2, Mass = 1f, Radius = 25f };
			var contact = new CollisionEventData { HitNormal = new float3(-1f, 0f, 0f) };

			HandleBallContact(ref a, in b, in contact, in gravity);

			Assert.That(a.Velocity.x, Is.EqualTo(2f - PhysicsConstants.ContactVel).Within(Tolerance));
		}

		[Test]
		public void StaticContactCancelsTheLiftOfEarlierContacts()
		{
			// the friction of a ball/ball or wall contact resolved before lifted the ball by 0.05
			var ball = new BallState { Mass = 1f, Radius = 25f, Velocity = new float3(0f, 0f, 0.05f) };
			var floor = new CollisionEventData { ColliderId = 1, HitNormal = new float3(0f, 0f, 1f) };
			var priorContactVelocity = new float3(0f, 0f, 0.05f);
			var frictionVelocity = ball.Velocity;
			var frictionAngularMomentum = ball.AngularMomentum;

			BallCollider.HandleStaticContact(ref ball, in floor, 0f, 0.1f, in DownGravity, float3.zero,
				in DownGravity, in frictionVelocity, in frictionAngularMomentum, true, in priorContactVelocity);

			Assert.That(ball.Velocity.z, Is.EqualTo(0.1f).Within(Tolerance), "only the gravity compensation, no lift");
		}

		[Test]
		public void BallContactsPassOnAllOfTheirVelocityChange()
		{
			var contacts = new NativeList<ContactBufferElement>(2, Allocator.Temp);
			try {
				var ballContact = new CollisionEventData { HitNormal = new float3(1f, 0f, 0f) };
				ballContact.SetBallItem(9);
				contacts.Add(new ContactBufferElement(7, ballContact));
				contacts.Add(new ContactBufferElement(7, new CollisionEventData { ColliderId = 1, HitNormal = new float3(0f, 0f, 1f) }));

				PhysicsCycle.PassOn(ref contacts, 0, new float3(1f, 0f, 2f), float3.zero);

				AssertFloat3(contacts[1].PriorContactVelocity, new float3(1f, 0f, 2f));
				AssertFloat3(contacts[1].FrictionVelocity, new float3(0f, 0f, 2f), "friction still ignores the normal solve");
			} finally {
				contacts.Dispose();
			}
		}

		[Test]
		public void BallContactsAreResolvedFirst()
		{
			var contacts = new NativeList<ContactBufferElement>(4, Allocator.Temp);
			try {
				var ballContact = new CollisionEventData();
				ballContact.SetBallItem(9);
				contacts.Add(new ContactBufferElement(1, new CollisionEventData { ColliderId = 10 }));
				contacts.Add(new ContactBufferElement(2, ballContact));
				contacts.Add(new ContactBufferElement(3, new CollisionEventData { ColliderId = 11 }));
				contacts.Add(new ContactBufferElement(4, ballContact));

				PhysicsCycle.SortBallContactsFirst(ref contacts);

				Assert.That(new[] { contacts[0].BallId, contacts[1].BallId, contacts[2].BallId, contacts[3].BallId },
					Is.EqualTo(new[] { 2, 4, 1, 3 }));
			} finally {
				contacts.Dispose();
			}
		}

		[Test]
		public void SlowBallImpactIsInelasticAndKeepsPositions()
		{
			// a resting-scale impact on a shallow overlap: no restitution bounce and no displacement
			// pop, both kept the balls of a resting stack bouncing forever
			var hit = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f) };
			var moving = new BallState { Id = 2, Mass = 1f, Radius = 25f, Position = new float3(49.9f, 0f, 25f), Velocity = new float3(-0.2f, 0f, 0f) };
			var hitEvent = new CollisionEventData { HitNormal = new float3(-1f, 0f, 0f), HitDistance = -0.1f };
			var movingEvent = new CollisionEventData { HitNormal = new float3(1f, 0f, 0f), HitDistance = -0.1f };

			Assert.That(BallCollider.Collide(ref hit, ref moving, in hitEvent, in movingEvent, false), Is.True);

			Assert.That(hit.Velocity.x, Is.EqualTo(-0.1f).Within(Tolerance));
			Assert.That(moving.Velocity.x, Is.EqualTo(-0.1f).Within(Tolerance));
			Assert.That(hit.Position.x, Is.EqualTo(0f));
			Assert.That(moving.Position.x, Is.EqualTo(49.9f));
		}

		[Test]
		public void BallImpactKeepsItsRestitution()
		{
			var hit = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f) };
			var moving = new BallState { Id = 2, Mass = 1f, Radius = 25f, Position = new float3(50f, 0f, 25f), Velocity = new float3(-2f, 0f, 0f) };
			var hitEvent = new CollisionEventData { HitNormal = new float3(-1f, 0f, 0f) };
			var movingEvent = new CollisionEventData { HitNormal = new float3(1f, 0f, 0f) };

			BallCollider.Collide(ref hit, ref moving, in hitEvent, in movingEvent, false);

			Assert.That(hit.Velocity.x, Is.EqualTo(-1.8f).Within(Tolerance));
			Assert.That(moving.Velocity.x, Is.EqualTo(-0.2f).Within(Tolerance));
		}

		[Test]
		public void PackedBallsGetNoFrictionFromEachOthersNormalResponse()
		{
			// four balls in a square, moving together: A (0, 0), B (50, 0), C (50, 50), D (0, 50).
			// The earlier contacts cancel the y motion of B and A; at A against B nothing slips.
			var a = new BallState { Id = 1, Mass = 1f, Radius = 25f, Velocity = new float3(0.05f, 0.05f, 0f) };
			var b = a;
			b.Id = 2;
			var c = a;
			c.Id = 3;
			var d = a;
			d.Id = 4;
			var noLoad = float3.zero;

			HandleBallContact(ref b, in c, new CollisionEventData { HitNormal = new float3(0f, -1f, 0f) }, in noLoad);
			HandleBallContact(ref b, in a, new CollisionEventData { HitNormal = new float3(1f, 0f, 0f) }, in noLoad);
			HandleBallContact(ref a, in d, new CollisionEventData { HitNormal = new float3(0f, -1f, 0f) }, in noLoad);
			HandleBallContact(ref a, in b, new CollisionEventData { HitNormal = new float3(-1f, 0f, 0f) }, in noLoad);

			Assert.That(a.Velocity.y, Is.EqualTo(0f).Within(Tolerance), "no sideways friction");
			Assert.That(a.AngularMomentum.z, Is.EqualTo(0f).Within(Tolerance), "no spin");
		}

		private static void HandleBallContact(ref BallState ball, in BallState other, in CollisionEventData contact, in float3 gravity)
		{
			BallCollider.HandleBallContact(ref ball, in other, in contact, PhysicsConstants.PhysFactor, in gravity, in gravity);
		}

		#endregion

		#region Collider generation

		[Test]
		public void RampEntranceHasASingleJoint()
		{
			var go = new GameObject("ramp");
			var nonTransformable = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var colliders = new ColliderReference(ref nonTransformable, Allocator.Persistent);
			try {
				var ramp = go.AddComponent<RampComponent>();
				var rampCollider = go.AddComponent<RampColliderComponent>();
				ramp.DragPoints = new[] { new DragPointData(500f, 1500f), new DragPointData(500f, 1000f) };
				var api = new RampApi(go, null, null);

				new RampColliderGenerator(api, ramp, rampCollider, float4x4.identity).GenerateColliders(0f, ref colliders);

				// a flat ramp: the edge joints and the start and end joints, each once
				var joints = colliders.Line3DColliders;
				Assert.That(joints.Length, Is.GreaterThan(0));
				for (var i = 0; i < joints.Length; i++) {
					for (var j = i + 1; j < joints.Length; j++) {
						var a = joints[i].Bounds.Aabb;
						var b = joints[j].Bounds.Aabb;
						Assert.That(a.Min == b.Min && a.Max == b.Max, Is.False,
							$"joints {i} and {j} are the same edge");
					}
				}
			} finally {
				colliders.Dispose();
				nonTransformable.Dispose();
				Object.DestroyImmediate(go);
			}
		}

		#endregion

		private static float TangentialSlipSpeed(in BallState ball, in float3 hitNormal)
		{
			var contactOffset = -ball.Radius * hitNormal;
			var surfaceVelocity = BallState.SurfaceVelocity(in ball, in contactOffset);
			var slip = surfaceVelocity - hitNormal * math.dot(surfaceVelocity, hitNormal);
			return math.length(slip);
		}

		private static void AssertFloat3(float3 actual, float3 expected, string message = null)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), message);
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), message);
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), message);
		}
	}
}
