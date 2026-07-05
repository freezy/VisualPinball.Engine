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

		[BurstCompile]
		internal static void Update(int itemId, ref MagnetState magnet, ref PhysicsState state, float physicsDiffTime)
		{
			if (!magnet.IsEnabled) {
				ReleaseGrabbedBalls(itemId, ref magnet, ref state, false);
				ReleaseMembership(itemId, ref state);
				return;
			}

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
					if (UpdateGrab(itemId, ref magnet, ref state, ref ball)) {
						continue;
					}

					switch (magnet.Profile) {
						case MagnetForceProfile.VpxCompatible:
						case MagnetForceProfile.Physical:
							ApplyVpxCompatibleForce(ref ball, in magnet, physicsDiffTime);
							break;
					}
				}
			}
		}

		internal static void ReleaseGrabbedBalls(int itemId, ref MagnetState magnet, ref PhysicsState state, bool suppressRegrab)
		{
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
		{
			var delta = ball.Position.xy - magnet.Position;
			var distance = math.length(delta);
			if (distance <= MinDistance || magnet.Radius <= MinDistance) {
				return;
			}

			var ratio = distance / magnet.Radius;
			var force = magnet.Strength * math.exp(-0.2f / ratio) / (ratio * ratio * 56f) * 1.5f;
			var damping = math.pow(magnet.PlanarDamping, physicsDiffTime);
			var direction = delta / distance;

			var velocity = ball.Velocity.xy * damping - direction * force * physicsDiffTime;
			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
		}

		internal static void ApplyVpxCompatibleGrab(ref BallState ball, in MagnetState magnet)
		{
			ball.Position = new float3(magnet.Position.x, magnet.Position.y, ball.Position.z);
			ball.EventPosition = new float3(magnet.Position.x, magnet.Position.y, ball.EventPosition.z);
			ball.Velocity = new float3(0f, 0f, ball.Velocity.z);
			ball.OldVelocity = new float3(0f, 0f, ball.OldVelocity.z);
			ball.AngularMomentum = float3.zero;
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

		private static bool UpdateGrab(int itemId, ref MagnetState magnet, ref PhysicsState state, ref BallState ball)
		{
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
			ApplyVpxCompatibleGrab(ref ball, in magnet);
			return true;
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
