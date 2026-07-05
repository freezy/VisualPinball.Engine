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

using Unity.Burst;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	[BurstCompile(FloatPrecision.Medium, FloatMode.Fast, CompileSynchronously = true)]
	internal static class TurntablePhysics
	{
		private const float VpxTurntableForceDivisor = 8000f;
		private const float SecondsPerVpxUpdate = MagnetPhysics.VpxMagnetUpdateMs * 0.001f;

		[BurstCompile]
		internal static void Update(ref TurntableState turntable, ref PhysicsState state, float physicsDiffTime)
		{
			UpdateSpeed(ref turntable, physicsDiffTime);
			turntable.RotationAngle = math.fmod(turntable.RotationAngle + turntable.Speed * physicsDiffTime * SecondsPerVpxUpdate * 360f, 360f);

			if (turntable.Radius <= 0f || math.abs(turntable.Speed) <= 0.0001f) {
				return;
			}

			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					if (ball.IsFrozen || !IsBallInRange(in ball, in turntable)) {
						continue;
					}
					ApplyVpxCompatibleForce(ref ball, in turntable, physicsDiffTime);
				}
			}
		}

		internal static void UpdateSpeed(ref TurntableState turntable, float physicsDiffTime)
		{
			var targetSpeed = turntable.MotorOn ? turntable.TargetSpeed : 0f;
			var acceleration = math.abs(targetSpeed) > math.abs(turntable.Speed) ? turntable.SpinUp : turntable.SpinDown;
			turntable.Speed = MoveTowards(turntable.Speed, targetSpeed, math.max(0f, acceleration) * physicsDiffTime * SecondsPerVpxUpdate);
		}

		internal static void ApplyVpxCompatibleForce(ref BallState ball, in TurntableState turntable, float physicsDiffTime)
		{
			var delta = ball.Position.xy - turntable.Position;
			var impulse = new float2(-delta.y, delta.x) * (turntable.Speed / VpxTurntableForceDivisor) * physicsDiffTime;
			ball.Velocity = new float3(ball.Velocity.x + impulse.x, ball.Velocity.y + impulse.y, ball.Velocity.z);
		}

		internal static bool IsBallInRange(in BallState ball, in TurntableState turntable)
			=> math.lengthsq(ball.Position.xy - turntable.Position) <= turntable.Radius * turntable.Radius;

		internal static void RefreshTargetSpeed(ref TurntableState turntable)
		{
			turntable.TargetSpeed = (turntable.SpinClockwise ? 1f : -1f) * turntable.MaxSpeed;
		}

		private static float MoveTowards(float current, float target, float maxDelta)
		{
			var delta = target - current;
			if (math.abs(delta) <= maxDelta) {
				return target;
			}
			return current + math.sign(delta) * maxDelta;
		}
	}
}
