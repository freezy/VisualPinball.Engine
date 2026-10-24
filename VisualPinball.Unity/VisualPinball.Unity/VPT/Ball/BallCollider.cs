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
		private const float StaticContactPenetrationTolerance = 0.5f;

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

		public static void HandleStaticContact(ref BallState ball, in CollisionEventData collEvent, float friction, float dTime, in float3 gravity, in float3 colliderVelocity)
		{
			var frictionAcceleration = gravity + ball.ExternalAcceleration;
			var frictionVelocity = ball.Velocity;
			var frictionAngularMomentum = ball.AngularMomentum;
			HandleStaticContact(ref ball, in collEvent, friction, dTime, in gravity, in colliderVelocity,
				in frictionAcceleration, in frictionVelocity, in frictionAngularMomentum);
		}

		internal static void HandleStaticContact(ref BallState ball, in CollisionEventData collEvent, float friction,
			float dTime, in float3 gravity, in float3 colliderVelocity, in float3 frictionAcceleration,
			in float3 frictionVelocity, in float3 frictionAngularMomentum)
		{
			HandleStaticContact(ref ball, in collEvent, friction, dTime, in gravity, in colliderVelocity,
				in frictionAcceleration, in frictionVelocity, in frictionAngularMomentum,
				IsSupportContact(in collEvent.HitNormal), float3.zero);
		}

		/// <summary>
		/// Whether a contact normal, in playfield space, belongs to a support (floor,
		/// ramp) rather than a wall.
		/// </summary>
		internal static bool IsSupportContact(in float3 playfieldHitNormal)
			=> math.normalizesafe(playfieldHitNormal).z > 0.5f;

		/// <param name="isSupportContact">Whether the contact supports the ball from below,
		/// see <see cref="IsSupportContact"/>. The hit normal may be in a collider's local
		/// frame here, so the caller decides from the playfield-space normal.</param>
		/// <param name="priorContactVelocity">What the contacts resolved before this one in the
		/// same pass did to the ball's velocity: all of it for ball/ball contacts, the friction
		/// part for static ones (see <see cref="ContactBufferElement.PriorContactVelocity"/>).</param>
		internal static void HandleStaticContact(ref BallState ball, in CollisionEventData collEvent, float friction,
			float dTime, in float3 gravity, in float3 colliderVelocity, in float3 frictionAcceleration,
			in float3 frictionVelocity, in float3 frictionAngularMomentum, bool isSupportContact,
			in float3 priorContactVelocity)
		{
			// this should be zero, but only up to +/- PhysicsConstants.ContactVel
			// (relative to the surface, which may be moving if the collider is kinematic)
			var normVel = math.dot(ball.Velocity - colliderVelocity, collEvent.HitNormal);

			// If some collision has changed the ball's velocity, we may not have to do anything.
			if (normVel <= PhysicsConstants.ContactVel) {
				// Impacts correct penetration in Collide3DWall, but sustained contacts
				// arrive through this path instead. A moving support can leave a ball
				// slightly inside the next static mesh; without position recovery that
				// error accumulates until a one-sided triangle rejects the ball as being
				// behind it. Keep kinematic contacts unchanged so an intentionally moving
				// support remains authoritative while it carries the ball.
				// Never recover along gravity: a collider embedded in the ball from above
				// (a cover or wire lower than the ball is tall) would otherwise shove the
				// ball through the floor, up to DispLimit per tick, and the floor's own
				// recovery would fight it, leaving the ball visibly sunk while it rolls.
				if (collEvent.ColliderId >= 0 && !collEvent.IsKinematic &&
				    collEvent.HitDistance < -StaticContactPenetrationTolerance &&
				    math.dot(collEvent.HitNormal, gravity) <= 0.2f * math.length(gravity)) {
					var correction = math.min(-PhysicsConstants.DispGain * collEvent.HitDistance,
						PhysicsConstants.DispLimit);
					ball.Position += collEvent.HitNormal * correction;
				}

				// Balance every continuous load already integrated this tick. Without the
				// additional acceleration, a magnet can press a resting ball through a wall
				// because the contact solver only compensates gravity.
				var acceleration = gravity + ball.ExternalAcceleration;
				var dot = math.dot(acceleration, collEvent.HitNormal);

				// note: for kinematic colliders, HitOrgNormalVelocity is already relative to the
				// surface — the narrow phase hit-tests in the collider's reference frame

				// Also cancel what the contacts resolved before this one did to the ball: stopping
				// the spin of a ball pressed against a wall or another ball, their friction pushes it
				// up, and left alone the ball climbs a bit every step until it leaves its support and
				// drops back (the jitter of balls resting in a row). Otherwise this works from the
				// hit test velocity: the normal impulses of the other static contacts stay out (two
				// wires of a wire ramp), and so do collisions earlier in this cycle (an impact stays
				// a collision).
				var priorNormalVelocity = math.dot(priorContactVelocity, collEvent.HitNormal);
				var hitNormVel = collEvent.HitOrgNormalVelocity + priorNormalVelocity;

				// The velocity change that kills the original normal velocity and counteracts the
				// external forces for this step, whatever the ball's mass (VPX scaled the load by the
				// mass without dividing it back out, which threw balls heavier than 1 off their
				// support). It never pulls the ball towards the surface, except to take back a lift
				// of the earlier contacts: that can exceed one step of load on a short step, since
				// friction removes the slip at once, whatever the step length.
				var normalVelocityChange = math.max(-math.max(priorNormalVelocity, 0f), -(dot * dTime + hitNormVel));

				// Add just enough to kill original normal velocity and counteract the external forces.
				ball.Velocity += collEvent.HitNormal * normalVelocityChange;

				// the support this contact gives, the friction budget
				var normalForce = math.max(0f, normalVelocityChange);

				ApplyFriction(ref ball, collEvent.HitNormal, dTime, friction, normalForce, isSupportContact,
					in frictionAcceleration, in colliderVelocity, in colliderVelocity, 0f, in frictionVelocity,
					in frictionAngularMomentum);
			}
		}

		/// <summary>
		/// Ball/ball contact: <paramref name="ball"/> touches <paramref name="otherBall"/>, which
		/// reports its own contact with this ball if it is free (each ball of a pair resolves its
		/// own record).
		/// </summary>
		/// <param name="collEvent">The contact, its normal pointing from the other ball to this one.</param>
		/// <remarks>
		/// Unlike static contacts, the friction works from the live motion of both balls, as in
		/// Visual Pinball: the other ball's motion already holds the normal response of its own
		/// earlier ball contacts, and comparing it to a snapshot of this ball would read those
		/// as slip (a packed group of balls would get sideways friction and spin). Ball/ball
		/// contacts are resolved before any static one, so the live motion holds nothing else.
		/// </remarks>
		internal static void HandleBallContact(ref BallState ball, in BallState otherBall, in CollisionEventData collEvent,
			float dTime, in float3 gravity, in float3 frictionAcceleration)
		{
			var normal = collEvent.HitNormal;

			// The approach is relative to the supporting ball, which may itself be moving (a stack
			// pressed on a wall, a ball resting on a rolling ball): using the ball's own normal
			// velocity here either lets it creep into its support or wrongly skips the impulse.
			var approach = math.dot(otherBall.Velocity - ball.Velocity, normal);

			// If some collision has changed the ball's velocity, we may not have to do anything.
			if (approach <= -PhysicsConstants.ContactVel) {
				return;
			}

			// Resting on another ball: kill the residual normal velocity only (vel.n -> 0).
			// Matching the supporting ball's velocity instead lets each neighbour's contact
			// rewrite this velocity in a single pass, and the chain never converges. Only a
			// contact-scale approach is cancelled (plus the step of the load a resting stack
			// carries): a faster one comes from a collision earlier in this cycle, a ball hit
			// into a resting pair, and must reach the other ball through their collision
			// instead of being absorbed here (Newton's cradle). Nor is more than the pair's own
			// approach (plus the load) cancelled: two balls moving together along the normal
			// don't close in on each other, and VPX braked the trailing one against the other.
			var acceleration = gravity + ball.ExternalAcceleration;
			var load = math.max(-math.dot(acceleration, normal), 0f) * PhysicsConstants.PhysFactor;
			var maxCancel = math.min(PhysicsConstants.ContactVel, math.max(approach, 0f)) + load;
			var normalForce = math.max(0f, math.min(-math.dot(ball.Velocity, normal), maxCancel));
			ball.Velocity += normal * normalForce;

			// Contacts resolve after displacement, so a slowly pressed ball still creeps into its
			// support a little each step; left alone the overlap sinks past the contact band,
			// turns into a restitution collision plus a displacement-correction pop, and the cycle
			// repeats. Drain the overlap with a small capped nudge, quiet enough to stay a contact.
			if (collEvent.HitDistance < 0f) {
				ball.Position += normal * math.min(-0.5f * collEvent.HitDistance, 0.02f);
			}

			// Friction acts on the slip relative to the other ball's surface, and the other ball
			// takes its share of the impulse (its effective inverse mass along the tangent is
			// 1/m + r^2/I for a sphere, none if it is frozen), so the two records of a pair don't
			// overshoot: for equal balls 75% of the slip is removed per step, the rest later.
			var otherContactPoint = otherBall.Radius * normal;
			var otherSurfaceVelocity = BallState.SurfaceVelocity(in otherBall, in otherContactPoint);
			var otherInvMassTangent = otherBall.IsFrozen
				? 0f
				: otherBall.InvMass + otherBall.Radius * otherBall.Radius / otherBall.Inertia;
			var velocity = ball.Velocity;
			var angularMomentum = ball.AngularMomentum;
			ApplyFriction(ref ball, in normal, dTime, PhysicsConstants.BallBallFriction, normalForce,
				IsSupportContact(in normal), in frictionAcceleration, in otherSurfaceVelocity, float3.zero,
				otherInvMassTangent, in velocity, in angularMomentum);
		}

		/// <param name="slipReferenceVelocity">Velocity of the other surface at the contact point
		/// (a moving collider, the other ball), the slip is relative to it.</param>
		/// <param name="normalReferenceVelocity">Velocity the residual normal velocity of the
		/// static friction gate is measured against.</param>
		/// <param name="otherInvMassTangent">Effective inverse mass of the other body along the
		/// tangent, zero for static colliders.</param>
		private static void ApplyFriction(ref BallState ball, in float3 hitNormal, float dTime, float frictionCoeff,
			float normalImpulse, bool isSupportContact, in float3 acceleration, in float3 slipReferenceVelocity,
			in float3 normalReferenceVelocity, float otherInvMassTangent, in float3 frictionVelocity,
			in float3 frictionAngularMomentum)
		{
			// surface contact point relative to center of mass
			var surfP = -ball.Radius * hitNormal;

			// velocity of the ball's surface relative to the (possibly moving) other surface:
			// once the ball rides along with a moving surface, slip goes to zero and static
			// friction keeps it locked to the surface
			var frictionState = ball;
			frictionState.Velocity = frictionVelocity;
			frictionState.AngularMomentum = frictionAngularMomentum;
			var surfVel = BallState.SurfaceVelocity(in frictionState, in surfP) - slipReferenceVelocity;

			// calc the tangential slip velocity
			var slip = surfVel - hitNormal * math.dot(surfVel, hitNormal);

			// Coulomb cone: the friction impulse is bounded by mu times the normal impulse this
			// contact applied in this step (normalImpulse is the velocity change of the normal
			// solve). VPX bounded it by the gravity component pressing the ball on the surface,
			// which is next to nothing on walls and nothing at all on the glass.
			var maxImpulse = math.max(frictionCoeff, 0f) * ball.Mass * normalImpulse;

			var slipSpeed = math.length(slip);
			float3 slipDir;
			float numer;
			float numerScale; // dTime if numer is an acceleration (static case), 1 if it is a velocity (dynamic case)

			// The normal solve has already added this tick's support velocity. Keep that
			// live value for the static/dynamic branch gate; otherwise a skidding ball
			// whose pre-contact normal velocity was zero never receives kinetic friction.
			// After the normal solve the residual is about -(a.n) dTime, so on its own the
			// gate tests the orientation of the normal and picks the static branch on every
			// wall, even with real slip. Like VPX, only supports get the quench.
			var normVel = math.dot(ball.Velocity - normalReferenceVelocity, hitNormal);
			if (normVel <= 0.025 && isSupportContact || slipSpeed < PhysicsConstants.Precision) {
				// check for <=0.025 originated from ball<->rubber collisions pushing the ball upwards, but this is still not enough, some could even use <=0.2
				// slip speed zero - static friction case

				var surfAcc = BallState.SurfaceAcceleration(in frictionState, in surfP, in acceleration);
				// calc the tangential slip acceleration
				var slipAcc = surfAcc - hitNormal * math.dot(surfAcc, hitNormal);

				// neither slip velocity nor slip acceleration? nothing to do here
				if (math.lengthsq(slipAcc) < 1e-6) {
					return;
				}

				slipDir = math.normalize(slipAcc);
				numer = -math.dot(slipDir, surfAcc);
				numerScale = dTime;
				otherInvMassTangent = 0f; // this ball's own slip acceleration: the other ball holds it like a static surface

			} else {
				// nonzero slip speed - dynamic friction case
				slipDir = slip / slipSpeed;
				numer = -math.dot(slipDir, surfVel);
				numerScale = 1f;
			}

			var cp = math.cross(surfP, slipDir);
			var denom = 1.0f / ball.Mass + math.dot(slipDir, math.cross(cp / ball.Inertia, surfP)) + otherInvMassTangent;

			// Dynamic case: the full slip-removing impulse, Coulomb bound clamped. VPX requested
			// dTime times it, which made sliding friction depend on the step length.
			var friction = math.clamp(numerScale * numer / denom, -maxImpulse, maxImpulse);

			if (!float.IsNaN(friction) && !float.IsInfinity(friction)) {
				ball.ApplySurfaceImpulse(friction * cp, friction * slipDir);
			}
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
			var isContact = false;
			if (bnd <= PhysicsConstants.PhysTouch) {
				// in contact?
				if (bnd < otherBall.Radius * -2.0f) {
					return -1.0f;                                              // embedded too deep?
				}

				hitTime = 0; // already touching or overlapping: immediate response

				// A slow pair inside the touch layer is a steady contact. Keep it a contact for a
				// deeper overlap too: a resting stack always hovers a bit inside the touch layer,
				// and crossing it turned the pair into a restitution collision plus a
				// displacement-correction pop, the slow sink-then-pop jitter of resting stacks.
				isContact = math.abs(bnv) <= PhysicsConstants.ContactVel && bnd > -0.5f;

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

			var hitPos = otherBall.Position + hitTime * otherBall.Velocity; // new ball position

			// calc unit normal of collision, between the two centers at hit time
			var hitNormal = hitPos - (ball.Position + hitTime * ball.Velocity);
			if (math.abs(hitNormal.x) <= Constants.FloatMin && math.abs(hitNormal.y) <= Constants.FloatMin &&
			    math.abs(hitNormal.z) <= Constants.FloatMin) {
				return -1.0f;
			}

			collEvent.HitNormal = math.normalize(hitNormal);
			collEvent.HitDistance = bnd;                                            // actual contact distance

			collEvent.IsContact = isContact;
			if (isContact) {
				collEvent.HitOrgNormalVelocity = bnv;
			}

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
			var impactSpeed = -dot; // approach speed before the embedded kick below

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

			// For resting-scale impacts (below the script-event impact speed) on shallow overlap,
			// skip the displacement correction: it pops the pair far enough apart that the
			// following ball re-approaches as a new collision forever (the slow jitter loop of
			// resting stacks). Persistent contacts handle the small residual overlap instead;
			// deep embeds and real impacts keep it.
			if (impactSpeed > 0.25f || otherCollEvent.HitDistance < -0.5f) {
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
			}

			var myInvMass = ball.IsFrozen ? 0.0f : ball.InvMass; // frozen ball has infinite mass

			// Low speed impacts stay inelastic: a restitution bounce leaves the pair receding just
			// above the contact velocity window, so balls resting in a stack would be kicked apart,
			// re-approach above the contact velocity and bounce again forever. -0.25 is the impact
			// scale under which a collision is not worth a script event either.
			var restitution = dot < -0.25f ? 0.8f : 0.0f;
			var impulse = -(1.0f + restitution) * dot / (myInvMass + otherBall.InvMass);

			if (!ball.IsFrozen) {
				ball.Velocity -= impulse * myInvMass * vNormal;
			}

			otherBall.Velocity += impulse * otherBall.InvMass * vNormal;

			return true;
		}
	}
}
