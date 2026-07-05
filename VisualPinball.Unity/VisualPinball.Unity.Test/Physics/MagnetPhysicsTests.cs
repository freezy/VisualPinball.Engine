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
	public class MagnetPhysicsTests
	{
		[Test]
		public void VpxCompatibleForceScalesToOneMillisecondTicks()
		{
			var oneTickBall = CreateBall();
			var tenTickBall = CreateBall();
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 10f,
				PlanarDamping = 1f
			};

			MagnetPhysics.ApplyVpxCompatibleForce(ref oneTickBall, in magnet, 1f);
			for (var i = 0; i < 10; i++) {
				MagnetPhysics.ApplyVpxCompatibleForce(ref tenTickBall, in magnet, 0.1f);
			}

			Assert.That(tenTickBall.Velocity.x, Is.EqualTo(oneTickBall.Velocity.x).Within(1e-5f));
			Assert.That(tenTickBall.Velocity.y, Is.EqualTo(oneTickBall.Velocity.y).Within(1e-5f));
		}

		[Test]
		public void PlanarDampingUsesFrameFractionExponent()
		{
			var ball = CreateBall();
			ball.Velocity = new float3(3f, -4f, 5f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 0f,
				PlanarDamping = 0.985f
			};

			for (var i = 0; i < 10; i++) {
				MagnetPhysics.ApplyVpxCompatibleForce(ref ball, in magnet, 0.1f);
			}

			Assert.That(ball.Velocity.x, Is.EqualTo(3f * 0.985f).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(-4f * 0.985f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.EqualTo(5f).Within(1e-5f));
		}

		[Test]
		public void VpxCompatibleGrabClampsBallToMagnetCenter()
		{
			var ball = CreateBall();
			ball.EventPosition = new float3(49f, -2f, 10f);
			ball.Velocity = new float3(3f, -4f, 5f);
			ball.OldVelocity = new float3(2f, 1f, -1f);
			ball.AngularMomentum = new float3(1f, 2f, 3f);
			var magnet = new MagnetState {
				Position = new float2(12f, -8f)
			};

			MagnetPhysics.ApplyVpxCompatibleGrab(ref ball, in magnet);

			Assert.That(ball.Position.xy, Is.EqualTo(magnet.Position));
			Assert.That(ball.Position.z, Is.EqualTo(10f));
			Assert.That(ball.EventPosition.xy, Is.EqualTo(magnet.Position));
			Assert.That(ball.Velocity, Is.EqualTo(new float3(0f, 0f, 5f)));
			Assert.That(ball.OldVelocity, Is.EqualTo(new float3(0f, 0f, -1f)));
			Assert.That(ball.AngularMomentum, Is.EqualTo(float3.zero));
		}

		private static BallState CreateBall()
		{
			return new BallState {
				Id = 1,
				Position = new float3(50f, 0f, 10f),
				Velocity = new float3(0f, 0f, 0f)
			};
		}
	}
}
