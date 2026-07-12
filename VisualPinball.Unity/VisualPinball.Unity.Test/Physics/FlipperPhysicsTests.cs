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

			FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 105);

			Assert.That(ball.Velocity, Is.EqualTo(float3.zero));
		}

		[Test]
		public void LiveCatchRequiresValidEosTimestamp()
		{
			var ball = CreateCatchBall();
			var collEvent = CreateCatchEvent();
			var tricks = CreateLiveCatchData();
			tricks.HasLiveCatchEosTime = false;

			FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 105);

			Assert.That(ball.Velocity, Is.EqualTo(new float3(0f, 4f, 0f)));
		}

		[Test]
		public void LiveCatchWindowHandlesUintRollover()
		{
			var ball = CreateCatchBall();
			var collEvent = CreateCatchEvent();
			var tricks = CreateLiveCatchData();
			tricks.LiveCatchEosTimeMsec = uint.MaxValue - 4;

			FlipperCollider.LiveCatch(ref ball, in collEvent, in tricks, float3.zero, 5f, 3);

			Assert.That(ball.Velocity, Is.EqualTo(float3.zero));
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
