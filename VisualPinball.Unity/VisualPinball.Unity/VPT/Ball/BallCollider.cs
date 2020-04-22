using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Ball
{
	public static class BallCollider
	{
		private const float HardScatter = 0.0f;

		public static void Collide3DWall(ref BallData ball, in PhysicsMaterialData material, in CollisionEventData coll, in float3 hitNormal)
		{
			// speed normal to wall
			var dot = math.dot(ball.Velocity, hitNormal);

			if (dot >= -PhysicsConstants.LowNormVel) {
				// nearly receding ... make sure of conditions
				if (dot > PhysicsConstants.LowNormVel) {
					// otherwise if clearly approaching .. process the collision
					return; // is this velocity clearly receding (i.E must > a minimum)
				}

				if (coll.HitDistance < -PhysicsConstants.Embedded) {
					dot = -PhysicsConstants.EmbedShot; // has ball become embedded???, give it a kick

				} else {
					return;
				}
			}

			// correct displacements, mostly from low velocity, alternative to acceleration processing
			var hDist = -PhysicsConstants.DispGain * coll.HitDistance; // limit delta noise crossing ramps,
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
			var reactionImpulse = ball.Mass * MathF.Abs(dot);

			var elasticity = Functions.ElasticityWithFalloff(material.Elasticity, material.ElasticityFalloff, dot);
			dot *= -(1.0f + elasticity);
			ball.Velocity += hitNormal * dot;                                  // apply collision impulse (along normal, so no torque)

			// compute friction impulse
			var surfP = -ball.Radius * hitNormal;                        // surface contact point relative to center of mass
			var surfVel = BallData.SurfaceVelocity(in ball, in surfP);                              // velocity at impact point
			var tangent = surfVel - hitNormal * math.dot(surfVel, hitNormal);  // calc the tangential velocity

			var tangentSpSq = math.lengthsq(tangent);
			if (tangentSpSq > 1e-6) {
				tangent /= math.sqrt(tangentSpSq);                        // normalize to get tangent direction
				var vt = math.dot(surfVel, tangent);                      // get speed in tangential direction

				// compute friction impulse
				var cross = math.cross(surfP, tangent); // todo check this does the same as Vertex3D.CrossProduct
				var kt = 1f / ball.Mass + math.dot(tangent, math.cross(cross / ball.Inertia, surfP));

				// friction impulse can't be greater than coefficient of friction times collision impulse (Coulomb friction cone)
				var maxFric = material.Friction * reactionImpulse;
				var jt = math.clamp(-vt / kt, -maxFric, maxFric);

				if (!float.IsNaN(jt) && !float.IsInfinity(jt)) {
					ball.ApplySurfaceImpulse(jt * cross, jt * tangent);
				}
			}

			var scatterAngle = material.Scatter;
			if (scatterAngle < 0.0) {
				scatterAngle = HardScatter;
			} // if < 0 use global value

			// todo don't hardcode
			scatterAngle *= 0.2f; //_tableData.GlobalDifficulty; // apply difficulty weighting

			if (dot > 1.0 && scatterAngle > 1.0e-5) {
				// no scatter at low velocity
				var scatter = MathF.Random() * 2 - 1;                                    // -1.0f..1.0f
				scatter *= (1.0f - scatter * scatter) * 2.59808f * scatterAngle;         // shape quadratic distribution and scale
				var radSin = MathF.Sin(scatter);                               // Green's transform matrix... rotate angle delta
				var radCos = MathF.Cos(scatter);                               // rotational transform from current position to position at time t
				var vxt = ball.Velocity.x;
				var vyt = ball.Velocity.y;
				ball.Velocity.x = vxt * radCos - vyt * radSin;                           // rotate to random scatter angle
				ball.Velocity.y = vyt * radCos + vxt * radSin;
			}
		}

		public static void HandleStaticContact(ref BallData ball, in CollisionEventData coll, float friction, float dTime, in float3 gravity)
		{
			// this should be zero, but only up to +/- PhysicsConstants.ContactVel
			var normVel = math.dot(ball.Velocity, coll.HitNormal);

			// If some collision has changed the ball's velocity, we may not have to do anything.
			if (normVel <= PhysicsConstants.ContactVel) {

				// external forces (only gravity for now)
				var fe = gravity * ball.Mass;
				var dot = math.dot(fe, coll.HitNormal);

				// normal force is always nonnegative
				var normalForce = math.max(0.0f, -(dot * dTime + coll.HitOrgNormalVelocity));

				// Add just enough to kill original normal velocity and counteract the external forces.
				ball.Velocity += coll.HitNormal * normalForce;

				ApplyFriction(ref ball, coll.HitNormal, dTime, friction, gravity);
			}
		}

		public static void ApplyFriction(ref BallData ball, in float3 hitNormal, float dTime, float frictionCoeff, in float3 gravity)
		{
			// surface contact point relative to center of mass
			var surfP = -ball.Radius * hitNormal;
			var surfVel = BallData.SurfaceVelocity(in ball, in surfP);

			// calc the tangential slip velocity
			var slip = surfVel - hitNormal * math.dot(surfVel, hitNormal);

			var maxFriction = frictionCoeff * ball.Mass * -math.dot(gravity, hitNormal);

			var slipSpeed = math.length(slip);
			float3 slipDir;
			float numer;

			var normVel = math.dot(ball.Velocity, hitNormal);
			if (normVel <= 0.025 || slipSpeed < PhysicsConstants.Precision) {
				// check for <=0.025 originated from ball<->rubber collisions pushing the ball upwards, but this is still not enough, some could even use <=0.2
				// slip speed zero - static friction case

				var surfAcc = BallData.SurfaceAcceleration(in ball, in surfP, in gravity);
				// calc the tangential slip acceleration
				var slipAcc = surfAcc - hitNormal * math.dot(surfAcc, hitNormal);

				// neither slip velocity nor slip acceleration? nothing to do here
				if (math.lengthsq(slipAcc) < 1e-6) {
					return;
				}

				slipDir = math.normalize(slipAcc);
				numer = -math.dot(slipDir, surfAcc);

			} else {
				// nonzero slip speed - dynamic friction case
				slipDir = slip / slipSpeed;
				numer = -math.dot(slipDir, surfVel);
			}

			var cp = math.cross(surfP, slipDir);
			var denom = 1.0f / ball.Mass + math.dot(slipDir, math.cross(cp / ball.Inertia, surfP));
			var friction = math.clamp(numer / denom, -maxFriction, maxFriction);

			if (!float.IsNaN(friction) && !float.IsInfinity(friction)) {
				ball.ApplySurfaceImpulse(dTime * friction * cp, dTime * friction * slipDir);
			}
		}
	}
}
