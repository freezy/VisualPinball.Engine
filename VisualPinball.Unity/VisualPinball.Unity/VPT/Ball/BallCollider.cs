using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Ball
{
	public static class BallCollider
	{
		private const float HardScatter = 0.0f;

		public static void Collide3DWall(ref BallData ball, ref PhysicsMaterialData material, ref CollisionEventData coll, ref float3 hitNormal)
		{
			// speed normal to wall
			var dot = math.dot(ball.Velocity, hitNormal);
			var inertia = 2.0f / 5.0f * ball.Radius * ball.Radius * ball.Mass;

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
			var surfVel = ball.SurfaceVelocity(surfP);                              // velocity at impact point
			var tangent = surfVel - hitNormal * math.dot(surfVel, hitNormal);  // calc the tangential velocity

			var tangentSpSq = math.lengthsq(tangent);
			if (tangentSpSq > 1e-6) {
				tangent /= math.sqrt(tangentSpSq);                        // normalize to get tangent direction
				var vt = math.dot(surfVel, tangent);                      // get speed in tangential direction

				// compute friction impulse
				var cross = math.cross(surfP, tangent); // todo check this does the same as Vertex3D.CrossProduct
				var kt = 1f / ball.Mass + math.dot(tangent, math.cross(cross / inertia, surfP));

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
	}
}
