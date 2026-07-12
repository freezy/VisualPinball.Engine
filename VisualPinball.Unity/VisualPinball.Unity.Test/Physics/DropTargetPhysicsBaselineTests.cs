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

namespace VisualPinball.Unity.Test
{
	public class DropTargetPhysicsBaselineTests
	{
		private const float Tolerance = 1e-5f;

		[Test]
		public void StandardBallAndPhysicsTimeBaseStayCompatibleWithRoth()
		{
			var createBall = typeof(BallManager).GetMethod(nameof(BallManager.CreateBall));
			var massParameter = createBall?.GetParameters()[2];

			Assert.That(massParameter, Is.Not.Null);
			Assert.That(massParameter.DefaultValue, Is.EqualTo(1f));
			Assert.That(PhysicsConstants.DefaultStepTimeS, Is.EqualTo(0.01f));
			Assert.That(PhysicsConstants.PhysicsStepTimeS, Is.EqualTo(0.001d).Within(Tolerance));
			Assert.That(PhysicsConstants.PhysFactor, Is.EqualTo(0.1f));
		}

		[Test]
		public void MovingTriangleDetectsStationaryBallFromRelativeVelocity()
		{
			var triangle = new TriangleCollider(
				new float3(-10f, -10f, 0f),
				new float3(-10f, 10f, 0f),
				new float3(10f, -10f, 0f),
				new ColliderInfo { ItemId = 1 }
			);
			var ball = new BallState {
				Id = 1,
				Position = new float3(0f, 0f, 2f),
				Velocity = float3.zero,
				Radius = 1f,
				Mass = 1f,
			};
			var staticEvent = new CollisionEventData();
			var relativeEvent = new CollisionEventData();

			var staticHit = triangle.HitTest(ref staticEvent, default, in ball, 1f);
			// The generic kinematic narrow phase tests in the collider's rest frame.
			// A face moving +Z at 2 therefore sees a stationary ball moving -Z at 2.
			ball.Velocity -= new float3(0f, 0f, 2f);
			var relativeHit = triangle.HitTest(ref relativeEvent, default, in ball, 1f);

			Assert.That(staticHit, Is.LessThan(0f));
			Assert.That(relativeHit, Is.EqualTo(0.5f).Within(Tolerance));
			Assert.That(relativeEvent.HitNormal, Is.EqualTo(new float3(0f, 0f, 1f)));
		}

		[Test]
		public void KinematicVelocityUsesTheVpxTenMillisecondTimeBase()
		{
			var previousMatrix = float4x4.identity;
			var currentMatrix = float4x4.Translate(new float3(10f, 0f, 0f));
			var previous = new KinematicVelocityState { LastUpdateUsec = 1000 };

			var velocity = PhysicsKinematics.DeriveVelocity(
				in previous,
				in previousMatrix,
				in currentMatrix,
				11000,
				out var isIsolated
			);

			Assert.That(isIsolated, Is.False);
			Assert.That(velocity.LinearVelocity, Is.EqualTo(new float3(10f, 0f, 0f)));
		}
	}
}
