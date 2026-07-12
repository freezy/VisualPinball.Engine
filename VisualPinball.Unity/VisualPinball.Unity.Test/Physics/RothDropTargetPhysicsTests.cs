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

namespace VisualPinball.Unity.Test
{
	public class RothDropTargetPhysicsTests
	{
		private const float Tolerance = 1e-5f;

		[Test]
		public void ProductionMassCorrectionMatchesEveryGoldenCase()
		{
			foreach (var golden in RothDropTargetGoldenData.MassCases) {
				var ball = new BallState {
					Mass = golden.BallMass,
					Velocity = new float3(-golden.IncomingNormalVelocity, 7f, 3f),
				};
				var preImpactVelocity = ball.Velocity;
				RothDropTargetPhysics.ApplyMassCorrection(ref ball, in preImpactVelocity,
					new float3(1f, 0f, 0f), RothDropTargetGoldenData.TargetMass);

				Assert.That(ball.Velocity.x, Is.EqualTo(-golden.ExpectedNormalVelocity).Within(Tolerance));
				Assert.That(ball.Velocity.y, Is.EqualTo(7f).Within(Tolerance));
				Assert.That(ball.Velocity.z, Is.EqualTo(3f).Within(Tolerance));
			}
		}

		[Test]
		public void ShippedConfigurationNeverClassifiesABrick()
		{
			var config = DropTargetRothConfig.Default;
			config.EnableBrick = RothDropTargetGoldenData.EnableBrick;

			var outcome = RothDropTargetPhysics.Classify(in config, ColliderRole.DropTargetFrontSensor,
				100f, 1f, 0f);

			Assert.That(outcome, Is.EqualTo(RothDropTargetOutcome.FaceDrop));
		}

		[Test]
		public void SyntheticBrickRequiresSpeedAndCentralContact()
		{
			var config = DropTargetRothConfig.Default;
			config.EnableBrick = true;

			Assert.That(RothDropTargetPhysics.Classify(in config, ColliderRole.DropTargetFrontSensor,
				31f, 1f, 7.9f), Is.EqualTo(RothDropTargetOutcome.Brick));
			Assert.That(RothDropTargetPhysics.Classify(in config, ColliderRole.DropTargetFrontSensor,
				30f, 1f, 7.9f), Is.EqualTo(RothDropTargetOutcome.FaceDrop));
			Assert.That(RothDropTargetPhysics.Classify(in config, ColliderRole.DropTargetFrontSensor,
				31f, 1f, 8f), Is.EqualTo(RothDropTargetOutcome.FaceDrop));
		}

		[Test]
		public void BacksideAndSideClassificationAreExplicit()
		{
			var config = DropTargetRothConfig.Default;
			config.EnableBacksideDrop = true;

			Assert.That(RothDropTargetPhysics.Classify(in config, ColliderRole.DropTargetBackFace,
				16f, 1f, 0f), Is.EqualTo(RothDropTargetOutcome.BacksideDrop));
			Assert.That(RothDropTargetPhysics.Classify(in config, ColliderRole.DropTargetBackFace,
				15f, 1f, 0f), Is.EqualTo(RothDropTargetOutcome.BacksideBounce));
			Assert.That(RothDropTargetPhysics.Classify(in config, ColliderRole.DropTargetFrontSensor,
				20f, 0.49f, 0f), Is.EqualTo(RothDropTargetOutcome.SideHit));
		}

		[Test]
		public void VerticalBouncerIsDeterministicAndPreservesSpeed()
		{
			var config = DropTargetRothConfig.Default;
			config.EnableVerticalBouncer = true;
			var first = new BallState { Id = 7, Velocity = new float3(3f, 4f, 0f) };
			var second = first;

			RothDropTargetPhysics.ApplyVerticalBouncer(ref first, in config, 42, 3);
			RothDropTargetPhysics.ApplyVerticalBouncer(ref second, in config, 42, 3);

			Assert.That(first.Velocity, Is.EqualTo(second.Velocity));
			Assert.That(math.length(first.Velocity), Is.EqualTo(5f).Within(Tolerance));
			Assert.That(first.Velocity.z, Is.GreaterThan(0f));
		}

		[Test]
		public void VerticalBouncerMatchesReferenceByReplacingExistingZ()
		{
			var config = DropTargetRothConfig.Default;
			config.EnableVerticalBouncer = true;
			var ball = new BallState { Id = 7, Velocity = new float3(3f, 4f, 12f) };

			RothDropTargetPhysics.ApplyVerticalBouncer(ref ball, in config, 42, 3);

			Assert.That(math.length(ball.Velocity), Is.EqualTo(5f).Within(Tolerance));
			Assert.That(ball.Velocity.z, Is.GreaterThan(0f).And.LessThan(5f));
		}
	}
}
