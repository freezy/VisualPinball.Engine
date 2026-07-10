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
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[BurstCompile(FloatPrecision.Medium, FloatMode.Fast, CompileSynchronously = true)]
	internal static class TurntablePhysics
	{
		private const float VpxTurntableForceDivisor = 8000f;
		private const float SecondsPerVpxUpdate = PhysicsConstants.DefaultStepTimeS;

		[BurstCompile]
		internal static void Update(int itemId, ref TurntableState turntable, ref PhysicsState state, float physicsDiffTime)
		{
			RefreshKinematicState(itemId, ref turntable, ref state);
			UpdateSpeed(ref turntable, physicsDiffTime);
			// Speed is a VPX-arbitrary force scale, not a rotation rate; VisualSpeedFactor
			// maps it to degrees per second for the visual disc.
			turntable.RotationAngle = math.fmod(turntable.RotationAngle + turntable.Speed * turntable.VisualSpeedFactor * physicsDiffTime * SecondsPerVpxUpdate, 360f);

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

		internal static void RefreshKinematicState(int itemId, ref TurntableState turntable, ref PhysicsState state)
		{
			if (turntable.IsKinematic && state.KinematicTransforms.TryGetValue(itemId, out var matrix)) {
				ApplyKinematicTransform(ref turntable, in matrix);
			}
		}

		internal static void ApplyKinematicTransform(ref TurntableState turntable, in float4x4 matrix)
		{
			turntable.Position = matrix.c3.xy;
			turntable.Height = matrix.c3.z;
		}

		internal static void UpdateSpeed(ref TurntableState turntable, float physicsDiffTime)
		{
			// cvpmTurnTable semantics: the motor ramp applies whenever the motor drives
			// the disc — including through a direction reversal — and the friction ramp
			// applies when it coasts down
			var targetSpeed = turntable.MotorOn ? turntable.TargetSpeed : 0f;
			var acceleration = turntable.MotorOn ? turntable.SpinUp : turntable.SpinDown;
			turntable.Speed = MoveTowards(turntable.Speed, targetSpeed, math.max(0f, acceleration) * physicsDiffTime * SecondsPerVpxUpdate);
		}

		internal static void ApplyVpxCompatibleForce(ref BallState ball, in TurntableState turntable, float physicsDiffTime)
		{
			var delta = ball.Position.xy - turntable.Position;
			var impulse = new float2(-delta.y, delta.x) * (turntable.Speed / VpxTurntableForceDivisor) * physicsDiffTime;
			ball.Velocity = new float3(ball.Velocity.x + impulse.x, ball.Velocity.y + impulse.y, ball.Velocity.z);
		}

		internal static bool IsBallInRange(in BallState ball, in TurntableState turntable)
		{
			// balls on ramps or upper playfields above the disc are not affected; VPX got
			// this implicitly from the z-bounded trigger that tracked the balls
			if (turntable.HeightRange > 0f && (ball.Position.z < turntable.Height || ball.Position.z > turntable.Height + turntable.HeightRange)) {
				return false;
			}
			return math.lengthsq(ball.Position.xy - turntable.Position) <= turntable.Radius * turntable.Radius;
		}

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
