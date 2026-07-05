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
	public class TurntablePhysicsTests
	{
		[Test]
		public void VpxCompatibleForceScalesToOneMillisecondTicks()
		{
			var oneTickBall = CreateBall();
			var tenTickBall = CreateBall();
			var turntable = new TurntableState {
				Position = float2.zero,
				Radius = 100f,
				Speed = 80f
			};

			TurntablePhysics.ApplyVpxCompatibleForce(ref oneTickBall, in turntable, 1f);
			for (var i = 0; i < 10; i++) {
				TurntablePhysics.ApplyVpxCompatibleForce(ref tenTickBall, in turntable, 0.1f);
			}

			Assert.That(tenTickBall.Velocity.x, Is.EqualTo(oneTickBall.Velocity.x).Within(1e-5f));
			Assert.That(tenTickBall.Velocity.y, Is.EqualTo(oneTickBall.Velocity.y).Within(1e-5f));
		}

		[Test]
		public void PositiveSpeedAppliesVpxTangentialKick()
		{
			var ball = CreateBall();
			var turntable = new TurntableState {
				Position = float2.zero,
				Radius = 100f,
				Speed = 80f
			};

			TurntablePhysics.ApplyVpxCompatibleForce(ref ball, in turntable, 1f);

			Assert.That(ball.Velocity.x, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0.5f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.EqualTo(5f).Within(1e-5f));
		}

		[Test]
		public void SpeedRampsTowardMotorTarget()
		{
			var turntable = new TurntableState {
				MaxSpeed = 100f,
				TargetSpeed = 100f,
				SpinUp = 100f,
				SpinDown = 100f,
				MotorOn = true
			};

			TurntablePhysics.UpdateSpeed(ref turntable, 1f);

			Assert.That(turntable.Speed, Is.EqualTo(1f).Within(1e-5f));
		}

		private static BallState CreateBall()
		{
			return new BallState {
				Id = 1,
				Position = new float3(50f, 0f, 10f),
				Velocity = new float3(0f, 0f, 5f)
			};
		}
	}
}
