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
using NUnit.Framework;
using Unity.Collections;
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

		[Test]
		public void MotorOffCoastsDownWithSpinDown()
		{
			var turntable = new TurntableState {
				Speed = 50f,
				MaxSpeed = 100f,
				TargetSpeed = 100f,
				SpinUp = 100f,
				SpinDown = 4f,
				MotorOn = false
			};

			TurntablePhysics.UpdateSpeed(ref turntable, 1f);

			Assert.That(turntable.Speed, Is.EqualTo(50f - 4f * 0.01f).Within(1e-5f));
		}

		[Test]
		public void DirectionReversalRampsWithSpinUp()
		{
			// the motor drives the reversal all the way through, so the motor ramp
			// applies while decelerating toward zero as well
			var turntable = new TurntableState {
				Speed = 100f,
				MaxSpeed = 100f,
				TargetSpeed = -100f,
				SpinUp = 10f,
				SpinDown = 4f,
				MotorOn = true
			};

			TurntablePhysics.UpdateSpeed(ref turntable, 1f);

			Assert.That(turntable.Speed, Is.EqualTo(100f - 10f * 0.01f).Within(1e-5f));
		}

		[Test]
		public void KinematicTransformUpdatesTurntableCenterAndHeight()
		{
			var turntable = new TurntableState {
				Position = float2.zero,
				Height = 1f
			};
			var matrix = float4x4.Translate(new float3(12f, -8f, 3f));

			TurntablePhysics.ApplyKinematicTransform(ref turntable, in matrix);

			Assert.That(turntable.Position, Is.EqualTo(new float2(12f, -8f)));
			Assert.That(turntable.Height, Is.EqualTo(3f).Within(1e-5f));
		}

		[Test]
		public void MovementPublishesSpeedAndAngleTogether()
		{
			var states = new NativeParallelHashMap<int, TurntableState>(1, Allocator.Temp);
			try {
				states.Add(17, new TurntableState {
					Speed = 42f,
					RotationAngle = 123f
				});
				var emitter = new Float2Emitter();
				var emitters = new Dictionary<int, IAnimationValueEmitter<float2>> {
					{ 17, emitter }
				};
				var movements = new PhysicsMovements();

				movements.ApplyTurntableMovement(ref states, emitters);

				Assert.That(emitter.Value, Is.EqualTo(new float2(42f, 123f)));
			} finally {
				states.Dispose();
			}
		}

		private static BallState CreateBall()
		{
			return new BallState {
				Id = 1,
				Position = new float3(50f, 0f, 10f),
				Velocity = new float3(0f, 0f, 5f)
			};
		}

		private sealed class Float2Emitter : IAnimationValueEmitter<float2>
		{
			public float2 Value;
			public event Action<float2> OnAnimationValueChanged;

			public void UpdateAnimationValue(float2 value)
			{
				Value = value;
				OnAnimationValueChanged?.Invoke(value);
			}
		}
	}
}
