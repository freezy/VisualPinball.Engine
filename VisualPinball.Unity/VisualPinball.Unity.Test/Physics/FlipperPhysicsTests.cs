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

namespace VisualPinball.Unity.Test
{
	public class FlipperPhysicsTests
	{
		[Test]
		public void EosTimestampUsesPhysicsClock()
		{
			var movement = new FlipperMovementState {
				Angle = 0f,
				AngleSpeed = 1f,
				AngularMomentum = 1f,
			};
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				AngleEnd = 0.5f,
			};
			var staticData = new FlipperStaticData {
				AngleStart = 0f,
				AngleEnd = 0.5f,
				Inertia = 1f,
			};
			using var events = new NativeQueue<EventData>(Allocator.Temp);
			var writer = events.AsParallelWriter();

			FlipperDisplacementPhysics.UpdateDisplacement(1, ref movement, ref tricks, in staticData,
				true, 1234, 0.5f, ref writer);

			Assert.That(tricks.HasLiveCatchEosTime, Is.True);
			Assert.That(tricks.LiveCatchEosTimeMsec, Is.EqualTo(1234));
		}

		[Test]
		public void EosTimestampUsesPhysicsClockForNegativeRotation()
		{
			var movement = new FlipperMovementState {
				Angle = 1f,
				AngleSpeed = -1f,
				AngularMomentum = -1f,
			};
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				AngleEnd = 0.5f,
			};
			var staticData = new FlipperStaticData {
				AngleStart = 1f,
				AngleEnd = 0.5f,
				Inertia = 1f,
			};
			using var events = new NativeQueue<EventData>(Allocator.Temp);
			var writer = events.AsParallelWriter();

			FlipperDisplacementPhysics.UpdateDisplacement(1, ref movement, ref tricks, in staticData,
				true, 4321, 0.5f, ref writer);

			Assert.That(tricks.HasLiveCatchEosTime, Is.True);
			Assert.That(tricks.LiveCatchEosTimeMsec, Is.EqualTo(4321));
		}

		[Test]
		public void SolenoidEdgeInvalidatesEosTimestampWithoutFlipperTricks()
		{
			var state = new FlipperState(
				new FlipperStaticData {
					AngleStart = 0f,
					AngleEnd = 1f,
					Inertia = 1f,
					ReturnRatio = 1f,
				},
				new FlipperMovementState(),
				new FlipperVelocityData { Direction = true },
				new FlipperHitData(),
				new FlipperTricksData {
					AngleEnd = 1f,
					HasLiveCatchEosTime = true,
				},
				new SolenoidState { Value = true }
			);

			FlipperVelocityPhysics.UpdateVelocities(ref state);

			Assert.That(state.Tricks.HasLiveCatchEosTime, Is.False);
			Assert.That(state.Tricks.lastSolState, Is.True);
		}

		[Test]
		public void LiveCatchUsesCapturedPreImpactSpeed()
		{
			var ball = new BallState {
				Position = new float3(-50f, 0f, 0f),
				Velocity = new float3(0f, 4f, 0f),
			};
			var collEvent = new CollisionEventData {
				HitNormal = new float3(0f, 1f, 0f),
				HitOrgNormalVelocity = 0f,
			};
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				LiveCatchDistanceMin = 5f,
				LiveCatchDistanceMax = 114f,
				LiveCatchMinimalBallSpeed = 3f,
				LiveCatchPerfectTime = 8f,
				LiveCatchFullTime = 16f,
				LiveCatchMinimalBounceSpeedMultiplier = 0f,
				LiveCatchInaccurateBounceSpeedMultiplier = 32f,
				LiveCatchBaseDampenDistance = 30f,
				LiveCatchBaseDampen = 0.55f,
				LiveCatchEosTimeMsec = 100,
				HasLiveCatchEosTime = true,
			};

			var outcome = FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 105);

			Assert.That(outcome, Is.EqualTo(LiveCatchOutcome.Caught));
			Assert.That(ball.Velocity, Is.EqualTo(float3.zero));
		}

		[Test]
		public void LiveCatchRequiresValidEosTimestamp()
		{
			var ball = CreateCatchBall();
			var collEvent = CreateCatchEvent();
			var tricks = CreateLiveCatchData();
			tricks.HasLiveCatchEosTime = false;

			var outcome = FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 105);

			Assert.That(outcome, Is.EqualTo(LiveCatchOutcome.NotEligible));
			Assert.That(ball.Velocity, Is.EqualTo(new float3(0f, 4f, 0f)));
		}

		[Test]
		public void LiveCatchWindowHandlesUintRollover()
		{
			var ball = CreateCatchBall();
			var collEvent = CreateCatchEvent();
			var tricks = CreateLiveCatchData();
			tricks.LiveCatchEosTimeMsec = uint.MaxValue - 4;

			var outcome = FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 3);

			Assert.That(outcome, Is.EqualTo(LiveCatchOutcome.Caught));
			Assert.That(ball.Velocity, Is.EqualTo(float3.zero));
		}

		[Test]
		public void EligibleBaseZoneDoesNotFallThroughWhenNoVelocityChanges()
		{
			var ball = CreateCatchBall();
			ball.Position = new float3(-20f, 0f, 0f);
			var collEvent = CreateCatchEvent();
			var tricks = CreateLiveCatchData();

			var outcome = FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 105);

			Assert.That(outcome, Is.EqualTo(LiveCatchOutcome.EligibleNoChange));
			Assert.That(ball.Velocity, Is.EqualTo(new float3(0f, 4f, 0f)));
		}

		[Test]
		public void EligibleBaseZoneDampensBallMovingTowardTip()
		{
			var ball = CreateCatchBall();
			ball.Position = new float3(-20f, 0f, 0f);
			ball.Velocity = new float3(-2f, 4f, 0f);
			ball.AngularMomentum = new float3(1f, 2f, 3f);
			var collEvent = CreateCatchEvent();
			var tricks = CreateLiveCatchData();

			var outcome = FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 105);

			Assert.That(outcome, Is.EqualTo(LiveCatchOutcome.BaseDampened));
			Assert.That(ball.Velocity, Is.EqualTo(new float3(-1.1f, 2.2f, 0f)));
			Assert.That(ball.AngularMomentum, Is.EqualTo(new float3(0.55f, 1.1f, 1.65f)));
		}

		[TestCase(0f, 1.1f)]
		[TestCase(3.77f, 0.99f)]
		[TestCase(6f, 0.99f)]
		public void EosRubberCurveMatchesVpwProfile(float incomingSpeed, float expectedCor)
		{
			Assert.That(FlipperCollider.EosRubberDesiredCor(incomingSpeed), Is.EqualTo(expectedCor).Within(1e-5f));
		}

		[Test]
		public void EosRubberCurveInterpolatesAtLowSpeed()
		{
			var expected = math.lerp(1.1f, 0.99f, 2f / 3.77f);

			Assert.That(FlipperCollider.EosRubberDesiredCor(2f), Is.EqualTo(expected).Within(1e-5f));
		}

		[Test]
		public void HeldEosRubberDampenerScalesLinearVelocityOnly()
		{
			var ball = new BallState {
				Velocity = new float3(1f, -1f, 2f),
				AngularMomentum = new float3(3f, 4f, 5f),
			};
			var movement = new FlipperMovementState { Angle = 0.5f };
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				OriginalAngleEnd = 0.5f,
			};
			var postPlayfieldVelocity = ball.Velocity;
			var incomingSpeed = 3.77f;
			var expectedCoefficient = 0.99f * incomingSpeed / math.length(postPlayfieldVelocity);

			var applied = FlipperCollider.TryApplyEosRubberDampener(ref ball, in postPlayfieldVelocity,
				incomingSpeed, true, in movement, in tricks);

			Assert.That(applied, Is.True);
			Assert.That(ball.Velocity, Is.EqualTo(postPlayfieldVelocity * expectedCoefficient));
			Assert.That(ball.AngularMomentum, Is.EqualTo(new float3(3f, 4f, 5f)));
		}

		[TestCase(2f, -1f)]
		[TestCase(0f, 0f)]
		[TestCase(0f, -3.75f)]
		[TestCase(0f, -4f)]
		public void EosRubberDampenerUsesStrictVelocityWindow(float x, float y)
		{
			var ball = new BallState { Velocity = new float3(x, y, 1f) };
			var movement = new FlipperMovementState { Angle = 0.5f };
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				OriginalAngleEnd = 0.5f,
			};
			var postPlayfieldVelocity = ball.Velocity;

			var applied = FlipperCollider.TryApplyEosRubberDampener(ref ball, in postPlayfieldVelocity,
				3f, true, in movement, in tricks);

			Assert.That(applied, Is.False);
		}

		[Test]
		public void EosRubberDampenerRequiresEnabledHeldFlipperAtEos()
		{
			var velocity = new float3(0f, -1f, 1f);
			var movement = new FlipperMovementState { Angle = 0.5f };
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				OriginalAngleEnd = 0.5f,
			};

			var disabledBall = new BallState { Velocity = velocity };
			var disabledTricks = tricks;
			disabledTricks.UseFlipperLiveCatch = false;
			Assert.That(FlipperCollider.TryApplyEosRubberDampener(ref disabledBall, in velocity,
				3f, true, in movement, in disabledTricks), Is.False);

			var releasedBall = new BallState { Velocity = velocity };
			Assert.That(FlipperCollider.TryApplyEosRubberDampener(ref releasedBall, in velocity,
				3f, false, in movement, in tricks), Is.False);

			var movingBall = new BallState { Velocity = velocity };
			var awayFromEos = new FlipperMovementState { Angle = math.radians(2f) + tricks.OriginalAngleEnd };
			Assert.That(FlipperCollider.TryApplyEosRubberDampener(ref movingBall, in velocity,
				3f, true, in awayFromEos, in tricks), Is.False);
		}

		[Test]
		public void EosRubberDampenerRejectsZeroSpeedWithoutNonFiniteValues()
		{
			var ball = new BallState { Velocity = float3.zero };
			var movement = new FlipperMovementState { Angle = 0.5f };
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				OriginalAngleEnd = 0.5f,
			};
			var postPlayfieldVelocity = float3.zero;

			var applied = FlipperCollider.TryApplyEosRubberDampener(ref ball, in postPlayfieldVelocity,
				0f, true, in movement, in tricks);

			Assert.That(applied, Is.False);
			Assert.That(ball.Velocity, Is.EqualTo(float3.zero));
		}

		[Test]
		public void EosRubberDampenerUsesPlayfieldVelocityForGateAndCurrentFrameForScaling()
		{
			var currentFrameVelocity = new float3(5f, 5f, 1f);
			var ball = new BallState { Velocity = currentFrameVelocity };
			var movement = new FlipperMovementState { Angle = 0.5f };
			var tricks = new FlipperTricksData {
				UseFlipperLiveCatch = true,
				OriginalAngleEnd = 0.5f,
			};
			var postPlayfieldVelocity = new float3(0f, -1f, 0f);
			var incomingSpeed = 3f;
			var expectedCoefficient = FlipperCollider.EosRubberDesiredCor(incomingSpeed) * incomingSpeed;

			var applied = FlipperCollider.TryApplyEosRubberDampener(ref ball, in postPlayfieldVelocity,
				incomingSpeed, true, in movement, in tricks);

			Assert.That(applied, Is.True);
			Assert.That(ball.Velocity, Is.EqualTo(currentFrameVelocity * expectedCoefficient));
		}

		private static BallState CreateCatchBall()
		{
			return new BallState {
				Position = new float3(-50f, 0f, 0f),
				Velocity = new float3(0f, 4f, 0f),
			};
		}

		private static CollisionEventData CreateCatchEvent()
		{
			return new CollisionEventData {
				HitNormal = new float3(0f, 1f, 0f),
				HitOrgNormalVelocity = 0f,
			};
		}

		private static FlipperTricksData CreateLiveCatchData()
		{
			return new FlipperTricksData {
				UseFlipperLiveCatch = true,
				LiveCatchDistanceMin = 5f,
				LiveCatchDistanceMax = 114f,
				LiveCatchMinimalBallSpeed = 3f,
				LiveCatchPerfectTime = 8f,
				LiveCatchFullTime = 16f,
				LiveCatchMinimalBounceSpeedMultiplier = 0f,
				LiveCatchInaccurateBounceSpeedMultiplier = 32f,
				LiveCatchBaseDampenDistance = 30f,
				LiveCatchBaseDampen = 0.55f,
				LiveCatchEosTimeMsec = 100,
				HasLiveCatchEosTime = true,
			};
		}
	}
}
