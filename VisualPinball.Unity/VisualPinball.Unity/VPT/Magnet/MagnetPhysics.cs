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
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	[BurstCompile(FloatPrecision.Medium, FloatMode.Fast, CompileSynchronously = true)]
	internal static class MagnetPhysics
	{
		internal const float VpxMagnetUpdateMs = 10f;
		private const float MinDistance = 0.0001f;
		private const float MinDistanceSq = MinDistance * MinDistance;
		private const float PhysicalCoreRadiusRatio = 0.2f;
		private const float PhysicalMinimumCoreRadius = 5f;
		private const float PhysicalVelocityDamping = 0.02f;
		private const float PhysicalMinimumHoldStrength = 1f;

		[BurstCompile]
		internal static void Update(int itemId, ref MagnetState magnet, ref PhysicsState state, float physicsDiffTime)
		{
			var magnetVelocity = RefreshKinematicState(itemId, ref magnet, ref state);

			if (!magnet.IsEnabled) {
				ReleaseGrabbedBalls(itemId, ref magnet, ref state, false);
				if (!state.InsideOfs.IsEmpty(itemId)) {
					ReleaseMembership(itemId, ref state);
				}
				return;
			}

			// constant within the tick; hoisted so the transcendental isn't paid per ball
			var vpxDamping = math.pow(magnet.PlanarDamping, physicsDiffTime);

			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					if (ball.IsFrozen) {
						ReleaseGrabbedBall(itemId, ref magnet, ref state, ball.Id);
						UpdateMembership(itemId, ball.Id, false, ref state);
						continue;
					}

					var affectsBall = IsBallInRange(in ball, in magnet);
					if (!affectsBall) {
						ReleaseGrabbedBall(itemId, ref magnet, ref state, ball.Id);
						ClearReleasedBall(ref magnet, ref state, ball.Id);
						UpdateMembership(itemId, ball.Id, false, ref state);
						continue;
					}

					UpdateMembership(itemId, ball.Id, true, ref state);
					if (UpdateGrab(itemId, ref magnet, ref state, ref ball, physicsDiffTime, magnetVelocity)) {
						continue;
					}

					switch (magnet.Profile) {
						case MagnetForceProfile.VpxCompatible:
							ApplyVpxCompatibleForce(ref ball, in magnet, physicsDiffTime, vpxDamping);
							break;
						case MagnetForceProfile.Physical:
							ApplyPhysicalForce(ref ball, in magnet, physicsDiffTime, magnetVelocity);
							break;
					}
				}
			}
		}

		internal static float2 RefreshKinematicState(int itemId, ref MagnetState magnet, ref PhysicsState state)
		{
			if (!magnet.IsKinematic || !state.KinematicTransforms.TryGetValue(itemId, out var matrix)) {
				return float2.zero;
			}

			ApplyKinematicTransform(ref magnet, in matrix);
			return GetKinematicVelocity(itemId, in magnet, ref state);
		}

		internal static void ApplyKinematicTransform(ref MagnetState magnet, in float4x4 matrix)
		{
			magnet.Position = matrix.c3.xy;
			magnet.Height = matrix.c3.z;
		}

		internal static void ReleaseGrabbedBalls(int itemId, ref MagnetState magnet, ref PhysicsState state, bool suppressRegrab)
		{
			if (magnet.GrabbedBalls.Value != 0UL) {
				for (var bitIndex = 0; bitIndex < 64; bitIndex++) {
					if (!magnet.GrabbedBalls.IsSet(bitIndex)) {
						continue;
					}
					magnet.GrabbedBalls.SetBits(bitIndex, false);
					if (suppressRegrab) {
						magnet.ReleasedBalls.SetBits(bitIndex, true);
					}
					if (state.InsideOfs.TryGetBallIdAtBitIndex(bitIndex, out var ballId)) {
						state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallReleased, itemId, ballId, true));
					}
				}
			}

			if (!suppressRegrab) {
				magnet.ReleasedBalls = default;
			}
		}

		internal static void EjectGrabbedBalls(int itemId, ref MagnetState magnet, ref PhysicsState state, float speed, float angleDeg)
		{
			for (var bitIndex = 0; bitIndex < 64; bitIndex++) {
				if (!magnet.GrabbedBalls.IsSet(bitIndex)) {
					continue;
				}
				if (state.InsideOfs.TryGetBallIdAtBitIndex(bitIndex, out var ballId)) {
					if (state.Balls.ContainsKey(ballId)) {
						ref var ball = ref state.Balls.GetValueByRef(ballId);
						ApplyPlanarEject(ref ball, speed, angleDeg);
					}
					state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallReleased, itemId, ballId, true));
				}
				magnet.GrabbedBalls.SetBits(bitIndex, false);
				magnet.ReleasedBalls.SetBits(bitIndex, true);
			}
		}

		internal static void ApplyVpxCompatibleForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime)
			=> ApplyVpxCompatibleForce(ref ball, in magnet, physicsDiffTime, math.pow(magnet.PlanarDamping, physicsDiffTime));

		internal static void ApplyVpxCompatibleForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float damping)
		{
			var delta = ball.Position.xy - magnet.Position;
			var distance = math.length(delta);
			if (distance <= MinDistance || magnet.Radius <= MinDistance) {
				return;
			}

			// cvpmMagnet.AttractBall: ratio = dist / (1.5*Size), then the damping wraps
			// both the old velocity and the impulse: (vel - dir*force) * 0.985
			var ratio = distance / (1.5f * magnet.Radius);
			var force = magnet.Strength * math.exp(-0.2f / ratio) / (ratio * ratio * 56f) * 1.5f;
			var direction = delta / distance;

			var velocity = (ball.Velocity.xy - direction * force * physicsDiffTime) * damping;
			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
		}

		internal static void ApplyPhysicalForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime)
			=> ApplyPhysicalForce(ref ball, in magnet, physicsDiffTime, float2.zero);

		internal static void ApplyPhysicalForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float2 magnetVelocity)
		{
			var delta = ball.Position.xy - magnet.Position;
			var distanceSq = math.lengthsq(delta);
			if (distanceSq <= MinDistanceSq) {
				return;
			}

			var distance = math.sqrt(distanceSq);
			var effectiveDistance = math.max(distance, PhysicalCoreRadius(in magnet));
			var force = magnet.Strength / (effectiveDistance * effectiveDistance);
			var direction = delta / distance;
			var velocity = ball.Velocity.xy - direction * force * physicsDiffTime;

			var damping = math.saturate(math.abs(force) * PhysicalVelocityDamping * physicsDiffTime);
			velocity = magnetVelocity + (velocity - magnetVelocity) * (1f - damping);

			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
		}

		internal static void ApplyVpxCompatibleGrab(ref BallState ball, in MagnetState magnet)
			=> ApplyVpxCompatibleGrab(ref ball, in magnet, float2.zero);

		internal static void ApplyVpxCompatibleGrab(ref BallState ball, in MagnetState magnet, float2 magnetVelocity)
		{
			ball.Position = new float3(magnet.Position.x, magnet.Position.y, ball.Position.z);
			ball.EventPosition = new float3(magnet.Position.x, magnet.Position.y, ball.EventPosition.z);
			ball.Velocity = new float3(magnetVelocity.x, magnetVelocity.y, ball.Velocity.z);
			ball.OldVelocity = new float3(magnetVelocity.x, magnetVelocity.y, ball.OldVelocity.z);
			ball.AngularMomentum = float3.zero;
		}

		internal static void ApplyPhysicalHold(ref BallState ball, in MagnetState magnet, float physicsDiffTime)
			=> ApplyPhysicalHold(ref ball, in magnet, physicsDiffTime, float2.zero);

		internal static void ApplyPhysicalHold(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float2 magnetVelocity)
		{
			var offset = ball.Position.xy - magnet.Position;
			var velocity = ball.Velocity.xy;
			var relativeVelocity = velocity - magnetVelocity;
			var holdStrength = math.max(math.abs(magnet.Strength), PhysicalMinimumHoldStrength);
			var holdRadius = math.max(magnet.GrabRadius, PhysicalMinimumCoreRadius);
			var stiffness = holdStrength / holdRadius;
			var damping = 2f * math.sqrt(stiffness);
			var impulse = (-offset * stiffness - relativeVelocity * damping) * physicsDiffTime;
			var maxImpulse = holdStrength * physicsDiffTime;
			var impulseLenSq = math.lengthsq(impulse);
			var maxImpulseSq = maxImpulse * maxImpulse;

			if (impulseLenSq > maxImpulseSq && impulseLenSq > MinDistanceSq) {
				impulse *= maxImpulse / math.sqrt(impulseLenSq);
			}

			velocity += impulse;
			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
			ball.AngularMomentum *= 1f - math.saturate(physicsDiffTime * 0.5f);
		}

		internal static void ApplyPlanarEject(ref BallState ball, float speed, float angleDeg)
		{
			var angleRad = math.radians(angleDeg);
			var velocity = new float2(
				math.sin(angleRad) * speed,
				-math.cos(angleRad) * speed
			);
			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
			ball.OldVelocity = new float3(velocity.x, velocity.y, ball.OldVelocity.z);
			ball.AngularMomentum = float3.zero;
		}

		internal static bool IsBallInRange(in BallState ball, in MagnetState magnet)
		{
			if (magnet.Radius <= 0f) {
				return false;
			}
			if (magnet.HeightRange > 0f && (ball.Position.z < magnet.Height || ball.Position.z > magnet.Height + magnet.HeightRange)) {
				return false;
			}
			return math.lengthsq(ball.Position.xy - magnet.Position) <= magnet.Radius * magnet.Radius;
		}

		private static bool UpdateGrab(int itemId, ref MagnetState magnet, ref PhysicsState state, ref BallState ball, float physicsDiffTime, float2 magnetVelocity)
		{
			// plain attraction magnets never grab; skip the bookkeeping entirely
			if (magnet.GrabRadius <= 0f && magnet.GrabbedBalls.Value == 0UL && magnet.ReleasedBalls.Value == 0UL) {
				return false;
			}

			var bitIndex = state.InsideOfs.GetOrCreateBitIndex(ball.Id);
			var isGrabbed = magnet.GrabbedBalls.IsSet(bitIndex);
			var isInGrabRange = magnet.GrabRadius > 0f &&
			                    magnet.Strength > 0f &&
			                    math.lengthsq(ball.Position.xy - magnet.Position) <= magnet.GrabRadius * magnet.GrabRadius;

			if (!isInGrabRange) {
				magnet.ReleasedBalls.SetBits(bitIndex, false);
				if (isGrabbed) {
					ReleaseGrabbedBall(itemId, ref magnet, bitIndex, ball.Id, ref state, false);
				}
				return false;
			}

			if (magnet.ReleasedBalls.IsSet(bitIndex)) {
				return false;
			}

			if (!isGrabbed) {
				magnet.GrabbedBalls.SetBits(bitIndex, true);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallGrabbed, itemId, ball.Id, true));
			}
			switch (magnet.Profile) {
				case MagnetForceProfile.Physical:
					ApplyPhysicalHold(ref ball, in magnet, physicsDiffTime, magnetVelocity);
					break;
				default:
					ApplyVpxCompatibleGrab(ref ball, in magnet, magnetVelocity);
					break;
			}
			return true;
		}

		private static float PhysicalCoreRadius(in MagnetState magnet)
		{
			return math.max(PhysicalMinimumCoreRadius, magnet.Radius * PhysicalCoreRadiusRatio);
		}

		private static float2 GetKinematicVelocity(int itemId, in MagnetState magnet, ref PhysicsState state)
		{
			if (!state.KinematicVelocities.TryGetValue(itemId, out var velocity)) {
				return float2.zero;
			}

			var linear = velocity.LinearVelocity;
			var angular = velocity.AngularVelocity;
			if (math.lengthsq(velocity.StepVelocity) > math.lengthsq(linear)
			    || math.lengthsq(velocity.StepAngularVelocity) > math.lengthsq(angular)) {
				linear = velocity.StepVelocity;
				angular = velocity.StepAngularVelocity;
			}
			if (math.lengthsq(linear) < 1e-8f && math.lengthsq(angular) < 1e-8f) {
				return float2.zero;
			}

			var position = new float3(magnet.Position.x, magnet.Position.y, magnet.Height);
			return (linear + math.cross(angular, position - velocity.Pivot)).xy;
		}

		private static void ReleaseGrabbedBall(int itemId, ref MagnetState magnet, ref PhysicsState state, int ballId)
		{
			if (!state.InsideOfs.TryGetBitIndex(ballId, out var bitIndex)) {
				return;
			}
			ReleaseGrabbedBall(itemId, ref magnet, bitIndex, ballId, ref state, false);
		}

		private static void ReleaseGrabbedBall(int itemId, ref MagnetState magnet, int bitIndex, int ballId, ref PhysicsState state, bool suppressRegrab)
		{
			if (!magnet.GrabbedBalls.IsSet(bitIndex)) {
				return;
			}
			magnet.GrabbedBalls.SetBits(bitIndex, false);
			if (suppressRegrab) {
				magnet.ReleasedBalls.SetBits(bitIndex, true);
			}
			state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallReleased, itemId, ballId, true));
		}

		private static void ClearReleasedBall(ref MagnetState magnet, ref PhysicsState state, int ballId)
		{
			if (state.InsideOfs.TryGetBitIndex(ballId, out var bitIndex)) {
				magnet.ReleasedBalls.SetBits(bitIndex, false);
			}
		}

		private static void ReleaseMembership(int itemId, ref PhysicsState state)
		{
			var ballIds = state.InsideOfs.GetIdsOfBallsInsideItem(itemId);
			for (var i = 0; i < ballIds.Length; i++) {
				var ballId = ballIds[i];
				state.InsideOfs.SetOutsideOf(itemId, ballId);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallExited, itemId, ballId, true));
			}
		}

		private static void UpdateMembership(int itemId, int ballId, bool isInside, ref PhysicsState state)
		{
			var wasInside = state.InsideOfs.IsInsideOf(itemId, ballId);
			if (isInside == wasInside) {
				return;
			}

			if (isInside) {
				state.InsideOfs.SetInsideOf(itemId, ballId);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallEntered, itemId, ballId, true));
			} else {
				state.InsideOfs.SetOutsideOf(itemId, ballId);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallExited, itemId, ballId, true));
			}
		}
	}
}
