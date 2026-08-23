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

using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal static class BallCollider
	{
		private const float HardScatter = 0.0f;

		public static void Collide3DWall(ref BallState ball, in PhysicsMaterialData material, in CollisionEventData collEvent, in float3 hitNormal, ref PhysicsState state)
		{
			// surface velocity of the collider at the contact point (zero unless kinematic and moving)
			var colliderVelocity = state.GetKinematicSurfaceVelocity(in collEvent, ball.Position - ball.Radius * hitNormal);

			// speed normal to wall, relative to the (possibly moving) surface
			var dot = math.dot(ball.Velocity - colliderVelocity, hitNormal);

			if (dot >= -PhysicsConstants.LowNormVel) {
				// nearly receding ... make sure of conditions
				if (dot > PhysicsConstants.LowNormVel) {
					// otherwise if clearly approaching .. process the collision
					return; // is this velocity clearly receding (i.E must > a minimum)
				}

				if (collEvent.HitDistance < -PhysicsConstants.Embedded) {
					dot = -PhysicsConstants.EmbedShot; // has ball become embedded???, give it a kick

				} else {
					return;
				}
			}

			// correct displacements, mostly from low velocity, alternative to acceleration processing
			var hDist = -PhysicsConstants.DispGain * collEvent.HitDistance; // limit delta noise crossing ramps,
			if (hDist > 1.0e-4) {
				// when hit detection checked it what was the displacement
				if (hDist > PhysicsConstants.DispLimit) {
					hDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				// push along norm, back to free area
				ball.Position += hitNormal * hDist;
				// use the norm, but this is not correct, reverse time is correct
			}

			// magnitude of the impulse which is just sufficient to keep the ball from
			// penetrating the wall (needed for friction computations)
			var reactionImpulse = ball.Mass * math.abs(dot);
			float elasticity = 0;
			if (material.UseElasticityOverVelocity) {
				// nFozzy used the xy velocity, but using the dot velocity seems more "physical".
				//var velocity = math.sqrt(ball.Velocity.x * ball.Velocity.x + ball.Velocity.y * ball.Velocity.y + ball.Velocity.z * ball.Velocity.z);
				var velocity = math.abs(dot);
				var colliders = collEvent.IsKinematic ? state.KinematicColliders : state.Colliders;
				var itemId = colliders.GetItemId(collEvent.ColliderId);
				var lut = state.ElasticityOverVelocityLUTs[itemId];
				elasticity = lut.InterpolateLUT(0, 63f, velocity);
			} else {
				elasticity = Math.ElasticityWithFalloff(material.Elasticity, material.ElasticityFalloff, dot);
			}

			dot *= -(1.0f + elasticity);
			ball.Velocity += hitNormal * dot;                                  // apply collision impulse (along normal, so no torque)

			// compute friction impulse
			var surfP = -ball.Radius * hitNormal;                        // surface contact point relative to center of mass
			var surfVel = BallState.SurfaceVelocity(in ball, in surfP) - colliderVelocity;           // velocity at impact point, relative to the surface
			var tangent = surfVel - hitNormal * math.dot(surfVel, hitNormal);  // calc the tangential velocity

			var tangentSpSq = math.lengthsq(tangent);
			if (tangentSpSq > 1e-6) {
				tangent /= math.sqrt(tangentSpSq);                        // normalize to get tangent direction
				var vt = math.dot(surfVel, tangent);                      // get speed in tangential direction

				// compute friction impulse
				var cross = math.cross(surfP, tangent); // todo check this does the same as Vertex3D.CrossProduct
				var kt = 1f / ball.Mass + math.dot(tangent, math.cross(cross / ball.Inertia, surfP));

				// Cupiiis Friction over Velocity LUT-Code
				// get friction based on normal Velocity  
				float friction;
				if (material.UseFrictionOverVelocity) {
					var normalVelocity = math.dot(ball.Velocity - colliderVelocity, hitNormal);
					var colliders = collEvent.IsKinematic ? state.KinematicColliders : state.Colliders;
					var itemId = colliders.GetItemId(collEvent.ColliderId);
					var lut = state.FrictionOverVelocityLUTs[itemId];
					friction = lut.InterpolateLUT(0, 127f, normalVelocity);

				} else {
					friction = material.Friction;
				}
				// End of Cupiiis Friction over Velocity LUT-Code

				// friction impulse can't be greater than coefficient of friction times collision impulse (Coulomb friction cone)
				//var maxFric = material.Friction * reactionImpulse;   // changed by cupiii because of cupiii's friction over Vel code
				var maxFric = friction * reactionImpulse;

				var jt = math.clamp(-vt / kt, -maxFric, maxFric);

				if (!float.IsNaN(jt) && !float.IsInfinity(jt)) {
					ball.ApplySurfaceImpulse(jt * cross, jt * tangent);
				}
			}

			var scatterAngle = material.ScatterAngleRad;
			if (scatterAngle < 0.0) {
				scatterAngle = HardScatter;
			} // if < 0 use global value

			// todo don't hardcode
			scatterAngle *= 0.2f; //_tableData.GlobalDifficulty; // apply difficulty weighting

			if (dot > 1.0 && scatterAngle > 1.0e-5) {
				// no scatter at low velocity
				var scatter = state.Env.Random.NextFloat(-1f, 1f);                            // -1.0f..1.0f
				scatter *= (1.0f - scatter * scatter) * 2.59808f * scatterAngle;         // shape quadratic distribution and scale
				var radSin = math.sin(scatter);                               // Green's transform matrix... rotate angle delta
				var radCos = math.cos(scatter);                               // rotational transform from current position to position at time t
				var vxt = ball.Velocity.x;
				var vyt = ball.Velocity.y;
				ball.Velocity.x = vxt * radCos - vyt * radSin;                           // rotate to random scatter angle
				ball.Velocity.y = vyt * radCos + vxt * radSin;
			}
		}

		/// <returns>
		/// The steady support impulse for this contact. Rolling resistance is
		/// applied after all of the ball's contacts have been solved and aggregated.
		/// </returns>
		public static float HandleStaticContact(ref BallState ball, in CollisionEventData collEvent,
			in PhysicsMaterialData material, float dTime, in float3 gravity, in float3 colliderVelocity)
		{
			if (!collEvent.IsContact) {
				return 0f;
			}

			// this should be zero, but only up to +/- PhysicsConstants.ContactVel
			// (relative to the surface, which may be moving if the collider is kinematic)
			var relativeNormalVelocity = math.dot(ball.Velocity - colliderVelocity, collEvent.HitNormal);

			// Another collision can make a previously detected contact separate before it is
			// solved. In that case neither penetration correction nor friction is needed.
			var isClearlySeparating = relativeNormalVelocity > PhysicsConstants.ContactVel;
			if (isClearlySeparating) {
				return 0f;
			}

			var supportImpulse = SolveNormalContact(ref ball, in collEvent, dTime, in gravity,
				in colliderVelocity, relativeNormalVelocity);
			ApplyCoulombContactImpulse(ref ball, collEvent.HitNormal, dTime, material.Friction, supportImpulse,
				gravity, colliderVelocity);
			return supportImpulse;
		}

		private static float SolveNormalContact(ref BallState ball, in CollisionEventData collEvent,
			float dTime, in float3 gravity, in float3 colliderVelocity, float relativeNormalVelocity)
		{
			// Gravity is integrated once at the start of the physics frame, before contact
			// detection. The stored velocity therefore already contains that acceleration;
			// adding gravity again to the correction would apply the support impulse twice.
			// Use the most approaching of the stored and current relative velocities so a
			// later collision cannot leave the ball moving into the surface.
			var approachVelocity = math.min(collEvent.HitOrgNormalVelocity, relativeNormalVelocity);
			var correctionImpulse = ball.Mass * math.max(0f, -approachVelocity);
			ball.Velocity += correctionImpulse * ball.InvMass * collEvent.HitNormal;

			// The steady support load is independent of the transient penetration
			// correction. Constant-velocity kinematic surfaces have the same support load
			// as static surfaces because no surface acceleration is currently available.
			var relativeNormalAcceleration = math.dot(gravity, collEvent.HitNormal);
			return ball.Mass * math.max(0f, -relativeNormalAcceleration) * math.max(0f, dTime);
		}

		private static void ApplyCoulombContactImpulse(ref BallState ball, in float3 hitNormal,
			float dTime, float frictionCoeff, float supportImpulse, in float3 gravity, in float3 colliderVelocity)
		{
			// surface contact point relative to center of mass
			var surfP = -ball.Radius * hitNormal;

			// velocity of the ball's surface relative to the (possibly moving) collider surface:
			// once the ball rides along with a moving surface, slip goes to zero and static
			// friction keeps it locked to the surface
			var surfVel = BallState.SurfaceVelocity(in ball, in surfP) - colliderVelocity;

			// calc the tangential slip velocity
			var slip = surfVel - hitNormal * math.dot(surfVel, hitNormal);

			var maxFrictionImpulse = math.max(0f, frictionCoeff) * supportImpulse;
			if (maxFrictionImpulse <= 0f) {
				return;
			}

			var slipSpeed = math.length(slip);
			float3 slipDir;
			float numer;

			if (slipSpeed < PhysicsConstants.Precision) {
				// External acceleration is integrated before contact detection. Exact no-slip
				// therefore means an earlier contact substep already supplied the needed
				// impulse; applying the acceleration correction again would make the result
				// depend on how the frame was partitioned.
				if (slipSpeed < 1e-6f) {
					return;
				}
				// near-zero slip - static friction case

				var surfAcc = BallState.SurfaceAcceleration(in ball, in surfP, in gravity);
				// calc the tangential slip acceleration
				var slipAcc = surfAcc - hitNormal * math.dot(surfAcc, hitNormal);

				// neither slip velocity nor slip acceleration? nothing to do here
				if (math.lengthsq(slipAcc) < 1e-6) {
					return;
				}

				slipDir = math.normalize(slipAcc);
				// Convert the force needed to cancel tangential acceleration into an
				// impulse over this contact substep.
				numer = -math.dot(slipDir, surfAcc) * dTime;

			} else {
				// nonzero slip speed - dynamic friction case
				slipDir = slip / slipSpeed;
				numer = -math.dot(slipDir, surfVel);
			}

			var cp = math.cross(surfP, slipDir);
			var denom = ball.InvMass + math.dot(slipDir, math.cross(cp / ball.Inertia, surfP));
			if (denom <= 0f || float.IsNaN(denom) || float.IsInfinity(denom)) {
				return;
			}
			var frictionImpulse = math.clamp(numer / denom, -maxFrictionImpulse, maxFrictionImpulse);

			if (!float.IsNaN(frictionImpulse) && !float.IsInfinity(frictionImpulse)) {
				ball.ApplySurfaceImpulse(frictionImpulse * cp, frictionImpulse * slipDir);
			}
		}

		internal static void ApplyRollingResistance(ref BallState ball, in RollingContactData contact)
		{
			if (!TryGetRollingState(in ball, in contact, out var tangentDirection,
				    out var rollingAxis, out var centerSpeed, out var angularRollingSpeed,
				    out var effectiveMass)) {
				return;
			}

			var deltaSpeed = contact.RollingImpulseLimit / effectiveMass;
			if (!math.isfinite(deltaSpeed) || deltaSpeed <= 0f) {
				return;
			}
			deltaSpeed = math.min(deltaSpeed, math.min(centerSpeed, angularRollingSpeed));

			// Rolling resistance is a coupled linear/angular impulse pair. It is
			// not a single impulse applied at the surface contact point.
			var deltaMomentum = -ball.Mass * deltaSpeed * tangentDirection;
			var deltaAngularMomentum = -(ball.Inertia / ball.Radius) * deltaSpeed * rollingAxis;
			ball.Velocity += deltaMomentum * ball.InvMass;
			ball.AngularMomentum += deltaAngularMomentum;
		}

		internal static bool IsRollingContact(in BallState ball, in RollingContactData contact)
		{
			return TryGetRollingState(in ball, in contact, out _, out _, out _, out _, out _);
		}

		private static bool TryGetRollingState(in BallState ball, in RollingContactData contact,
			out float3 tangentDirection, out float3 rollingAxis, out float centerSpeed,
			out float angularRollingSpeed, out float effectiveMass)
		{
			tangentDirection = default;
			rollingAxis = default;
			centerSpeed = 0f;
			angularRollingSpeed = 0f;
			effectiveMass = 0f;

			if (!contact.IsValid || !math.isfinite(ball.Mass) || !math.isfinite(ball.Radius)
			    || !math.isfinite(ball.Inertia) || ball.Mass <= 0f || ball.Radius <= 0f || ball.Inertia <= 0f) {
				return false;
			}

			var normalLengthSq = math.lengthsq(contact.ContactNormal);
			if (!math.isfinite(normalLengthSq) || normalLengthSq <= 0f) {
				return false;
			}
			var contactNormal = contact.ContactNormal * math.rsqrt(normalLengthSq);
			var contactPoint = -ball.Radius * contactNormal;
			var surfaceVelocity = BallState.SurfaceVelocity(in ball, in contactPoint) - contact.ColliderVelocity;
			var tangentialSurfaceVelocity = surfaceVelocity
				- contactNormal * math.dot(surfaceVelocity, contactNormal);
			var slipSpeedSq = math.lengthsq(tangentialSurfaceVelocity);
			if (!math.isfinite(slipSpeedSq)
			    || slipSpeedSq > PhysicsConstants.Precision * PhysicsConstants.Precision) {
				return false;
			}

			var centerVelocity = ball.Velocity - contact.ColliderVelocity;
			var tangentialCenterVelocity = centerVelocity
				- contactNormal * math.dot(centerVelocity, contactNormal);
			centerSpeed = math.length(tangentialCenterVelocity);
			if (!math.isfinite(centerSpeed) || centerSpeed <= 1e-6f) {
				return false;
			}
			tangentDirection = tangentialCenterVelocity / centerSpeed;
			rollingAxis = math.normalize(math.cross(contactNormal, tangentDirection));

			var angularVelocity = ball.AngularMomentum / ball.Inertia;
			var tangentialAngularVelocity = angularVelocity
				- contactNormal * math.dot(angularVelocity, contactNormal);
			angularRollingSpeed = math.dot(tangentialAngularVelocity, rollingAxis) * ball.Radius;
			if (!math.isfinite(angularRollingSpeed) || angularRollingSpeed <= 0f
			    || math.abs(angularRollingSpeed - centerSpeed) > PhysicsConstants.Precision) {
				return false;
			}

			effectiveMass = ball.Mass + ball.Inertia / (ball.Radius * ball.Radius);
			if (!math.isfinite(effectiveMass) || effectiveMass <= 0f) {
				return false;
			}
			return true;
		}

		public static float HitTest(ref CollisionEventData collEvent, ref BallState otherBall, in BallState ball, float dTime)
		{
			var d = ball.Position - otherBall.Position;                    // delta position
			var dv = ball.Velocity - otherBall.Velocity;                            // delta velocity

			var bcddSq = math.lengthsq(d);                                         // square of ball center's delta distance
			var bcdd = math.sqrt(bcddSq);                                     // length of delta

			// if (bcdd < 1.0e-8) {
			// 	// two balls center-over-center embedded
			// 	d.z = -1.0f;                                                   // patch up
			// 	otherBall.Position.z -= d.z;                                       // lift up
			//
			// 	bcdd = 1.0f;                                                   // patch up
			// 	bcddSq = 1.0f;                                                 // patch up
			// 	dv.z = 0.1f;                                                   // small speed difference
			// 	otherBall.Velocity.z -= dv.z;
			// }

			var b = math.dot(dv, d);                                                 // inner product
			var bnv = b / bcdd;                                                // normal speed of balls toward each other

			if (bnv > PhysicsConstants.LowNormVel) {
				// dot of delta velocity and delta displacement, positive if receding no collision
				return -1.0f;
			}

			var totalRadius = otherBall.Radius + ball.Radius;
			var bnd = bcdd - totalRadius;                                      // distance between ball surfaces

			float hitTime;
			//#ifdef BALL_CONTACTS
			//var isContact = false;
			if (bnd <= PhysicsConstants.PhysTouch) {
				// in contact?
				if (bnd < otherBall.Radius * -2.0f) {
					return -1.0f;                                              // embedded too deep?
				}

				if (math.abs(bnv) > PhysicsConstants.ContactVel               // >fast velocity, return zero time
				    || bnd <= -PhysicsConstants.PhysTouch) {
					// zero time for rigid fast bodies
					hitTime = 0; // slow moving but embedded

				} else {
					hitTime = bnd / -bnv;
				}

				//#ifdef BALL_CONTACTS
				// if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
				// 	isContact = true;
				// }

			} else {
				var a = math.lengthsq(dv);                                         // square of differential velocity
				if (a < 1.0e-8) {
					// ball moving really slow, then wait for contact
					return -1.0f;
				}

				var solved = Math.SolveQuadraticEq(a, 2.0f * b, bcddSq - totalRadius * totalRadius,
					out var time1, out var time2);
				if (!solved) {
					return -1.0f;
				}

				hitTime = time1 * time2 < 0
					? math.max(time1, time2)
					: math.min(time1, time2);                                 // find smallest nonnegative solution
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// .. was some time previous || beyond the next physics tick
				return -1.0f;
			}

			var hitPos = otherBall.Position + hitTime * dv; // new ball position

			// calc unit normal of collision
			var hitNormal = hitPos - ball.Position;
			if (math.abs(hitNormal.x) <= Constants.FloatMin && math.abs(hitNormal.y) <= Constants.FloatMin &&
			    math.abs(hitNormal.z) <= Constants.FloatMin) {
				return -1.0f;
			}

			collEvent.HitNormal = math.normalize(hitNormal);
			collEvent.HitDistance = bnd;                                            // actual contact distance

			//#ifdef BALL_CONTACTS
			// collEvent.IsContact = isContact;
			// if (isContact) {
			// 	collEvent.HitOrgNormalVelocity = bnv;
			// }

			return hitTime;
		}

		public static bool Collide(ref BallState ball, ref BallState otherBall,
			in CollisionEventData ballCollEvent, in CollisionEventData otherCollEvent,
			bool swapBallCollisionHandling)
		{
			// make sure we process each ball/ball collision only once
			// (but if we are frozen, there won't be a second collision event, so deal with it now!)
			if ((swapBallCollisionHandling && otherBall.Id >= ball.Id ||
			     !swapBallCollisionHandling && otherBall.Id <= ball.Id) && !ball.IsFrozen) {
				return false;
			}

			// target ball to object ball delta velocity
			var vRel = otherBall.Velocity - ball.Velocity;
			var vNormal = otherCollEvent.HitNormal;
			var dot = math.dot(vRel, vNormal);

			// correct displacements, mostly from low velocity, alternative to true acceleration processing
			if (dot >= -PhysicsConstants.LowNormVel) {

				// nearly receding ... make sure of conditions
				if (dot > PhysicsConstants.LowNormVel) {

					// otherwise if clearly approaching .. process the collision
					return false; // is this velocity clearly receding (i.E must > a minimum)
				}

				//#ifdef PhysicsConstants.Embedded
				if (otherCollEvent.HitDistance < -PhysicsConstants.Embedded) {
					dot = -PhysicsConstants.EmbedShot; // has ball become embedded???, give it a kick

				} else {
					return false;
				}

				//#endif
			}

			// todo script
			// send ball/ball collision event to script function
			// if (dot < -0.25f) {   // only collisions with at least some small true impact velocity (no contacts)
			//      g_pplayer->m_ptable->InvokeBallBallCollisionCallback(this, pball, -dot);
			// }

			var eDist = -PhysicsConstants.DispGain * otherCollEvent.HitDistance;
			if (eDist > 1.0e-4) {
				if (eDist > PhysicsConstants.DispLimit) {
					eDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				if (!ball.IsFrozen) {
					// if the hit ball is not frozen
					eDist *= 0.5f;
				}
				otherBall.Position += eDist * vNormal; // push along norm, back to free area
				// use the norm, but is not correct, but cheaply handled
			}

			eDist = -PhysicsConstants.DispGain * ballCollEvent.HitDistance; // noisy value .... needs investigation
			if (!ball.IsFrozen && eDist > 1.0e-4) {
				if (eDist > PhysicsConstants.DispLimit) {
					eDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				eDist *= 0.5f;
				ball.Position -= eDist * vNormal; // pull along norm, back to free area
			}

			var myInvMass = ball.IsFrozen ? 0.0f : ball.InvMass; // frozen ball has infinite mass
			var impulse = -(float)(1.0 + 0.8) * dot / (myInvMass + otherBall.InvMass); // resitution = 0.8

			if (!ball.IsFrozen) {
				ball.Velocity -= impulse * myInvMass * vNormal;
			}

			otherBall.Velocity += impulse * otherBall.InvMass * vNormal;

			return true;
		}
	}
}
