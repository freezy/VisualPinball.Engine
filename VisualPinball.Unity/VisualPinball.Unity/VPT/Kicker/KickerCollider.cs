// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class KickerCollider
	{
		/// <summary>
		/// Legacy mode adds about 300 iterations to the physics loop,
		/// resulting in stutter. Disabling this until we find another
		/// solution.
		/// </summary>
		public const bool ForceLegacyMode = false;

		public static void Collide(float3 position, ref BallState ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref InsideOfs insideOfs, ref KickerCollisionState collState, in KickerStaticState staticState,
			in ColliderMeshData meshData, in CollisionEventData collEvent, in int itemId, bool newBall)
		{
			// a previous ball already in kicker?
			if (collState.HasBall) {
				return;
			}

			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			var legacyMode = ForceLegacyMode || staticState.LegacyMode;
			var hitNormal = collEvent.HitNormal;
			var hitBit = collEvent.HitFlag;

			// check if kicker in ball's volume set
			var isBallInside = insideOfs.IsInsideOf(itemId, ball.Id);

			// if "New or (Hit && !Vol || UnHit && Vol)", continue.
			if (!newBall && hitBit != isBallInside) {
				return;
			}
			if (legacyMode || newBall) {
				ball.Position += PhysicsConstants.StaticTime * ball.Velocity; // move ball slightly forward
			}

			// entering Kickers volume
			if (!isBallInside) {
				var grabHeight = (staticState.ZLow + ball.Radius) * staticState.HitAccuracy;

				// early out here if the ball is slow and we are near the kicker center
				var hitEvent = ball.Position.z < grabHeight || legacyMode || newBall;

				if (!hitEvent) {

					DoChangeBallVelocity(ref ball, in hitNormal, in meshData);

					// this is an ugly hack to prevent the ball stopping rapidly at the kicker bevel
					// something with the friction calculation is wrong in the physics engine
					// so we monitor the ball velocity if it drop under a length value of 0.2
					// if so we take the last "good" velocity to help the ball moving over the critical spot at the kicker bevel
					// this hack seems to work only if the kicker is on the playfield, a kicker attached to a wall has still problems
					// because the friction calculation for a wall is also different
					if (math.lengthsq(ball.Velocity) < (float) (0.2 * 0.2)) {
						ball.Velocity = ball.OldVelocity;
					}

					ball.OldVelocity = ball.Velocity;

				} else {

					ball.IsFrozen = !staticState.FallThrough;
					if (ball.IsFrozen) {
						insideOfs.SetInsideOf(itemId, ball.Id); // add kicker to ball's volume set
						collState.BallId = ball.Id;
						collState.LastCapturedBallId = ball.Id;
					}

					// Fire the event before changing ball attributes, so scripters can get a useful ball state
					if (!newBall) {
						events.Enqueue(new EventData(EventId.HitEventsHit, itemId, ball.Id, true));
					}

					if (ball.IsFrozen || staticState.FallThrough) { // script may have unfrozen the ball

						// if ball falls through hole, we fake the collision algo by changing the ball height
						// in HitTestBasicRadius() the z-position of the ball is checked if it is >= to the hit cylinder
						// if we don't change the height of the ball we get a lot of hit events while the ball is falling!!

						// Only mess with variables if ball was not kicked during event
						ball.Velocity = float3.zero;
						ball.AngularMomentum = float3.zero;
						var posZ = !staticState.FallIn
							? position.z + ball.Radius * 2
							: staticState.FallThrough
								? position.z - ball.Radius - 5.0f
								: position.z + ball.Radius;
						ball.Position = new float3(position.x, position.y, posZ);

					} else {
						collState.BallId = 0; // make sure
					}
				}


			} else { // exiting kickers volume
				// remove kicker to ball's volume set
				insideOfs.SetOutsideOf(itemId, ball.Id);
				events.Enqueue(new EventData(EventId.HitEventsUnhit, itemId, ball.Id, true));
			}
		}

		private static void DoChangeBallVelocity(ref BallState ball, in float3 hitNormal, in ColliderMeshData meshData)
		{
			var minDistSqr = Constants.FloatMax;
			var idx = 0u;
			var hitMesh = meshData.Vertices;
			var hitMeshNormals = meshData.Normals;
			for (var t = 0; t < hitMesh.Length; t++) {
				// find the right normal by calculating the distance from current ball position to vertex of the kicker mesh
				ref var vertex = ref hitMesh.GetAsRef(t);
				var lengthSqr = math.lengthsq(ball.Position - vertex);
				if (lengthSqr < minDistSqr) {
					minDistSqr = lengthSqr;
					idx = (uint) t;
				}
			}

			if (idx != ~0u) {

				// we have the nearest vertex now use the normal and damp it so it doesn't speed up the ball velocity too much
				ref var hitNorm = ref hitMeshNormals.GetAsRef((int)idx);
				var dot = -math.dot(ball.Velocity, hitNorm);
				var reactionImpulse = ball.Mass * math.abs(dot);

				var surfP = -ball.Radius * hitNormal;                                    // surface contact point relative to center of mass
				var surfVel = BallState.SurfaceVelocity(in ball, surfP);                  // velocity at impact point
				var tangent = surfVel - math.dot(surfVel, hitNormal) * hitNorm;    // calc the tangential velocity

				// apply collision impulse (along normal, so no torque)
				ball.Velocity += dot * hitNorm;

				const float friction = 0.3f;
				var tangentSpSq = math.lengthsq(tangent);

				if (tangentSpSq > 1e-6f) {

					// normalize to get tangent direction
					tangent /= math.sqrt(tangentSpSq);

					// get speed in tangential direction
					var vt = math.dot(surfVel, tangent);

					// compute friction impulse
					var cross = math.cross(surfP, tangent);
					var kt = 1.0f / ball.Mass + math.dot(tangent, math.cross(cross / ball.Inertia, surfP));

					// friction impulse can't be greater than coefficient of friction times collision impulse (Coulomb friction cone)
					var maxFriction = friction * reactionImpulse;
					var jt = math.clamp(-vt / kt, -maxFriction, maxFriction);

					ball.ApplySurfaceImpulse(jt * cross, jt * tangent);
				}
			}
		}
	}
}
