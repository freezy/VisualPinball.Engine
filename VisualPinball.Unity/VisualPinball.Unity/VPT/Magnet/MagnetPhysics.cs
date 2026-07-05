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
				ReleaseMembership(itemId, ref state);
				return;
			}

			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					if (ball.IsFrozen) {
						UpdateMembership(itemId, ball.Id, false, ref state);
						continue;
					}

					var affectsBall = IsBallInRange(in ball, in magnet);
					UpdateMembership(itemId, ball.Id, affectsBall, ref state);
					if (!affectsBall) {
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
