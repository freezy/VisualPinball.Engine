using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class KickerCollider
	{
		/// <summary>
		/// Legacy mode adds about 300 iterations to the physics loop,
		/// resulting in stutter. Disabling this until we find another
		/// solution.
		/// </summary>
		public const bool ForceLegacyMode = true;

		public static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, ref KickerCollisionData collData,
			in KickerStaticData staticData, in ColliderMeshData meshData, in CollisionEventData collEvent,
			in Entity collEntity, in Entity ballEntity, bool newBall)
		{
			// a previous ball already in kicker?
			if (collData.HasBall) {
				return;
			}

			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			var legacyMode = ForceLegacyMode || staticData.LegacyMode;
			var hitNormal = collEvent.HitNormal;
			var hitBit = collEvent.HitFlag;

			// check if kicker in ball's volume set
			var isBallInside = BallData.IsInsideOf(in insideOfs, collEntity);

			// New or (Hit && !Vol || UnHit && Vol)
			if (newBall || hitBit == isBallInside) {

				if (legacyMode || newBall) {
					ball.Position += PhysicsConstants.StaticTime * ball.Velocity; // move ball slightly forward
				}

				// entering Kickers volume
				if (!isBallInside) {
					var grabHeight = (staticData.ZLow + ball.Radius) * staticData.HitAccuracy;

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
					}

					if (hitEvent) {

						ball.IsFrozen = !staticData.FallThrough;
						if (ball.IsFrozen) {
							BallData.SetInsideOf(ref insideOfs, collEntity); // add kicker to ball's volume set
							collData.HasBall = true;
							collData.LastCapturedBallEntity = ballEntity;
						}

						// Don't fire the hit event if the ball was just created
						// Fire the event before changing ball attributes, so scripters can get a useful ball state
						if (!newBall) {
							events.Enqueue(new EventData(EventId.HitEventsHit, collEntity, true));
						}

						if (ball.IsFrozen || staticData.FallThrough) { // script may have unfrozen the ball

							// if ball falls through hole, we fake the collision algo by changing the ball height
							// in HitTestBasicRadius() the z-position of the ball is checked if it is >= to the hit cylinder
							// if we don't change the height of the ball we get a lot of hit events while the ball is falling!!

							// Only mess with variables if ball was not kicked during event
							ball.Velocity = float3.zero;
							ball.AngularMomentum = float3.zero;
							ball.Position = new float3(staticData.Center.x, staticData.Center.y, ball.Position.z);
							if (staticData.FallThrough) {
								ball.Position.z = staticData.ZLow - ball.Radius - 5.0f;

							} else {
								ball.Position.z = staticData.ZLow + ball.Radius;
							}

						} else {
							collData.HasBall = false; // make sure
						}
					}


				} else { // exiting kickers volume
					// remove kicker to ball's volume set
					BallData.SetOutsideOf(ref insideOfs, collEntity);
					events.Enqueue(new EventData(EventId.HitEventsUnhit, collEntity, true));
				}
			}
		}

		private static void DoChangeBallVelocity(ref BallData ball, in float3 hitNormal, in ColliderMeshData meshData)
		{
			var minDistSqr = float.MaxValue;
			var idx = 0u;
			ref var hitMesh = ref meshData.Value.Value.Vertices;
			ref var hitMeshNormals = ref meshData.Value.Value.Normals;
			for (var t = 0; t < hitMesh.Length; t++) {
				// find the right normal by calculating the distance from current ball position to vertex of the kicker mesh
				ref var vertex = ref hitMesh[t].Vertex;
				var lengthSqr = math.lengthsq(ball.Position - vertex);
				if (lengthSqr < minDistSqr) {
					minDistSqr = lengthSqr;
					idx = (uint) t;
				}
			}

			if (idx != ~0u) {

				// we have the nearest vertex now use the normal and damp it so it doesn't speed up the ball velocity too much
				ref var hitNorm = ref hitMeshNormals[(int)idx].Vertex;
				var dot = -math.dot(ball.Velocity, hitNorm);
				var reactionImpulse = ball.Mass * math.abs(dot);

				var surfP = -ball.Radius * hitNormal;                                    // surface contact point relative to center of mass
				var surfVel = BallData.SurfaceVelocity(in ball, surfP);                  // velocity at impact point
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
