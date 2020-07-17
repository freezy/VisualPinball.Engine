// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable CommentTypo

using System;
using System.Collections.Generic;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Ball
{
	public class BallHit : HitObject
	{
		/// <summary>
		/// Collision information, may not be a actual hit if something else
		/// happens first
		/// </summary>
		public readonly CollisionEvent Coll;                                   // m_coll

		/// <summary>
		/// Extended (by m_vel + magic) squared radius, used in collision
		/// detection
		/// </summary>
		public float HitRadiusSqr;                                             // m_rcHitRadiusSqr

		/// <summary>
		/// Vector of triggers and kickers we are now inside
		/// </summary>
		public readonly List<EventProxy> VpVolObjs = new List<EventProxy>();   // m_vpVolObjs

		public readonly Vertex3D Vel;                                          // m_vel
		public readonly Vertex3D AngularMomentum = new Vertex3D();             // m_angularmomentum
		public readonly Vertex3D AngularVelocity = new Vertex3D();             // m_angularvelocity
		public float Inertia;                                                  // m_inertia
		public float InvMass;                                                  // m_invMass

		/// <summary>
		/// last hit event position (to filter hit 'equal' hit events)
		/// </summary>
		public readonly Vertex3D EventPos = new Vertex3D(-1, -1, -1);  // m_Event_Pos

		private readonly uint _id;
		private readonly BallData _data;
		private readonly BallState _state;
		private readonly BallMover _mover;
		private readonly TableData _tableData;

		/// <summary>
		/// Creates a new ball hit.
		/// </summary>
		/// <param name="ball">Reference to ball</param>
		/// <param name="data">data Static ball data</param>
		/// <param name="state">Dynamic ball state</param>
		/// <param name="initialVelocity">Initial velocity</param>
		/// <param name="tableData">Table data</param>
		public BallHit(Ball ball, BallData data, BallState state, Vertex3D initialVelocity, TableData tableData) : base(ItemType.Ball)
		{
			_id = data.Id;
			_data = data;
			_state = state;
			_tableData = tableData;
			_mover = new BallMover(state, this);

			// Only called by real balls, not temporary objects created for physics/rendering
			InvMass = 1.0f / data.Mass;
			Inertia = 2.0f / 5.0f * data.Radius * data.Radius * data.Mass;
			Vel = initialVelocity;
			Coll = new CollisionEvent(ball);

			_state.IsFrozen = false;

			if (initialVelocity != null) {
				CalcHitBBox();
			}
		}

		public BallMover GetMoverObject() => _mover;

		public bool IsRealBall() => VpVolObjs != null;

		public override void CalcHitBBox()
		{
			var vl = Vel.Length() + _data.Radius + 0.05f; //!! 0.05f = paranoia
			HitBBox.Left = _state.Pos.X - vl;
			HitBBox.Right = _state.Pos.X + vl;
			HitBBox.Top = _state.Pos.Y - vl;
			HitBBox.Bottom = _state.Pos.Y + vl;
			HitBBox.ZLow = _state.Pos.Z - vl;
			HitBBox.ZHigh = _state.Pos.Z + vl;
			HitRadiusSqr = vl * vl;
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			var d = _state.Pos.Clone().Sub(ball.State.Pos);                    // delta position
			var dv = Vel.Clone().Sub(ball.Hit.Vel);                            // delta velocity

			var bcddSq = d.LengthSq();                                         // square of ball center"s delta distance
			var bcdd = MathF.Sqrt(bcddSq);                                     // length of delta

			if (bcdd < 1.0e-8) {
				// two balls center-over-center embedded
				d.Z = -1.0f;                                                   // patch up
				ball.State.Pos.Z -= d.Z;                                       // lift up

				bcdd = 1.0f;                                                   // patch up
				bcddSq = 1.0f;                                                 // patch up
				dv.Z = 0.1f;                                                   // small speed difference
				ball.Hit.Vel.Z -= dv.Z;
			}

			var b = dv.Dot(d);                                                 // inner product
			var bnv = b / bcdd;                                                // normal speed of balls toward each other

			if (bnv > PhysicsConstants.LowNormVel) {
				// dot of delta velocity and delta displacement, positive if receding no collision
				return -1.0f;
			}

			var totalRadius = ball.Data.Radius + _data.Radius;
			var bnd = bcdd - totalRadius;                                      // distance between ball surfaces

			float hitTime;
			var isContact = false;
			if (bnd <= PhysicsConstants.PhysTouch) {
				// in contact?
				if (bnd < ball.Data.Radius * -2.0f) {
					return -1.0f;                                              // embedded too deep?
				}

				if (MathF.Abs(bnv) > PhysicsConstants.ContactVel               // >fast velocity, return zero time
				    || bnd <= -PhysicsConstants.PhysTouch) {
					// zero time for rigid fast bodies
					hitTime = 0; // slow moving but embedded

				} else {
					hitTime = bnd / -bnv;
				}

				if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;
				}

			} else {
				var a = dv.LengthSq();                                         // square of differential velocity
				if (a < 1.0e-8) {
					// ball moving really slow, then wait for contact
					return -1.0f;
				}

				var sol = Functions.SolveQuadraticEq(a, 2.0f * b, bcddSq - totalRadius * totalRadius);
				if (sol == null) {
					return -1.0f;
				}

				var (time1, time2) = sol;
				hitTime = time1 * time2 < 0
					? MathF.Max(time1, time2)
					: MathF.Min(time1, time2);                                 // find smallest nonnegative solution
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// .. was some time previous || beyond the next physics tick
				return -1.0f;
			}

			var hitPos = ball.State.Pos.Clone().Add(dv.MultiplyScalar(hitTime)); // new ball position

			// calc unit normal of collision
			var hitNormal = hitPos.Clone().Sub(_state.Pos);
			if (MathF.Abs(hitNormal.X) <= Constants.FloatMin && MathF.Abs(hitNormal.Y) <= Constants.FloatMin &&
			    MathF.Abs(hitNormal.Z) <= Constants.FloatMin) {
				return -1.0f;
			}

			coll.HitNormal.Set(hitNormal).Normalize();
			coll.HitDistance = bnd;                                            // actual contact distance
			coll.IsContact = isContact;
			if (isContact) {
				coll.HitOrgNormalVelocity = bnv;
			}

			return hitTime;
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			var ball = coll.Ball;

			// make sure we process each ball/ball collision only once
			// (but if we are frozen, there won"t be a second collision event, so deal with it now!)
			if ((physics.SwapBallCollisionHandling && ball.Id >= _id ||
			     !physics.SwapBallCollisionHandling && ball.Id <= _id) && !_state.IsFrozen) {
				return;
			}

			// target ball to object ball delta velocity
			var vRel = ball.Hit.Vel.Clone().Sub(Vel);
			var vNormal = coll.HitNormal;
			var dot = vRel.Dot(vNormal);

			// correct displacements, mostly from low velocity, alternative to true acceleration processing
			if (dot >= -PhysicsConstants.LowNormVel) {

				// nearly receding ... make sure of conditions
				if (dot > PhysicsConstants.LowNormVel) {

					// otherwise if clearly approaching .. process the collision
					return; // is this velocity clearly receding (i.E must > a minimum)
				}

				//#ifdef PhysicsConstants.Embedded
				if (coll.HitDistance < -PhysicsConstants.Embedded) {
					dot = -PhysicsConstants.EmbedShot; // has ball become embedded???, give it a kick

				} else {
					return;
				}

				//#endif
			}

			// fixme script
			// send ball/ball collision event to script function
			// if (dot < -0.25f) {   // only collisions with at least some small true impact velocity (no contacts)
			//      g_pplayer->m_ptable->InvokeBallBallCollisionCallback(this, pball, -dot);
			// }

			//#ifdef PhysicsConstants.DispGain
			var eDist = -PhysicsConstants.DispGain * coll.HitDistance;
			var normalDist = vNormal.Clone().MultiplyScalar(eDist);
			if (eDist > 1.0e-4) {
				if (eDist > PhysicsConstants.DispLimit) {
					eDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				if (!_state.IsFrozen) {
					// if the hit ball is not frozen
					eDist *= 0.5f;
				}

				ball.State.Pos.Add(normalDist); // push along norm, back to free area
				// use the norm, but is not correct, but cheaply handled
			}

			eDist = -PhysicsConstants.DispGain * Coll.HitDistance; // noisy value .... needs investigation
			if (!_state.IsFrozen && eDist > 1.0e-4) {
				if (eDist > PhysicsConstants.DispLimit) {
					eDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				eDist *= 0.5f;
				_state.Pos.Sub(normalDist); // pull along norm, back to free area
			}
			//#endif

			var myInvMass = _state.IsFrozen ? 0.0f : InvMass; // frozen ball has infinite mass
			var impulse = -(1.0f + 0.8f) * dot / (myInvMass + ball.Hit.InvMass); // resitution = 0.8

			if (!_state.IsFrozen) {
				Vel.Sub(vNormal.Clone().MultiplyScalar(impulse * myInvMass));
			}

			ball.Hit.Vel.Add(vNormal.Clone().MultiplyScalar(impulse * ball.Hit.InvMass));
		}

		public void Collide3DWall(Vertex3D hitNormal, float elasticity, float elasticityFalloff, float friction, float scatterAngle)
		{
			// speed normal to wall
			var dot = Vel.Dot(hitNormal);

			if (dot >= -PhysicsConstants.LowNormVel) {
				// nearly receding ... make sure of conditions
				if (dot > PhysicsConstants.LowNormVel) {
					// otherwise if clearly approaching .. process the collision
					return; // is this velocity clearly receding (i.E must > a minimum)
				}

				//#ifdef PhysicsConstants.Embedded
				if (Coll.HitDistance < -PhysicsConstants.Embedded) {
					dot = -PhysicsConstants.EmbedShot; // has ball become embedded???, give it a kick

				} else {
					return;
				}

				//#endif
			}

			//#ifdef PhysicsConstants.DispGain
			// correct displacements, mostly from low velocity, alternative to acceleration processing
			var hDist = -PhysicsConstants.DispGain * Coll.HitDistance; // limit delta noise crossing ramps,
			if (hDist > 1.0e-4) {
				// when hit detection checked it what was the displacement
				if (hDist > PhysicsConstants.DispLimit) {
					hDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				// push along norm, back to free area
				_state.Pos.Add(hitNormal.Clone().MultiplyScalar(hDist));
				// use the norm, but this is not correct, reverse time is correct
			}
			//#endif

			// magnitude of the impulse which is just sufficient to keep the ball from
			// penetrating the wall (needed for friction computations)
			var reactionImpulse = _data.Mass * MathF.Abs(dot);

			elasticity = Functions.ElasticityWithFalloff(elasticity, elasticityFalloff, dot);
			dot *= -(1.0f + elasticity);
			Vel.Add(hitNormal.Clone().MultiplyScalar(dot));                    // apply collision impulse (along normal, so no torque)

			// compute friction impulse
			var surfP = hitNormal.Clone().MultiplyScalar(-_data.Radius);       // surface contact point relative to center of mass
			var surfVel = SurfaceVelocity(surfP);                              // velocity at impact point
			var tangent = surfVel.Clone()                                      // calc the tangential velocity
				.Sub(hitNormal.Clone()
				.MultiplyScalar(surfVel.Dot(hitNormal)));

			var tangentSpSq = tangent.LengthSq();
			if (tangentSpSq > 1e-6) {
				tangent.DivideScalar(MathF.Sqrt(tangentSpSq));                 // normalize to get tangent direction
				var vt = surfVel.Dot(tangent);                            // get speed in tangential direction

				// compute friction impulse
				var cross = Vertex3D.CrossProduct(surfP, tangent);
				var crossInertia = cross.Clone().DivideScalar(Inertia);
				var kt = InvMass + tangent.Dot(Vertex3D.CrossProduct(crossInertia, surfP));

				// friction impulse can"t be greather than coefficient of friction times collision impulse (Coulomb friction cone)
				var maxFric = friction * reactionImpulse;
				var jt = Functions.Clamp(-vt / kt, -maxFric, maxFric);

				if (!float.IsNaN(jt) && !float.IsInfinity(jt)) {
					ApplySurfaceImpulse(
						cross.Clone().MultiplyScalar(jt),
						tangent.Clone().MultiplyScalar(jt)
					);
				}
			}

			if (scatterAngle < 0.0) {
				scatterAngle = HardScatter;
			} // if < 0 use global value

			scatterAngle *= _tableData.GlobalDifficulty; // apply difficulty weighting

			if (dot > 1.0 && scatterAngle > 1.0e-5) {
				// no scatter at low velocity
				var scatter = MathF.Random() * 2 - 1;                                    // -1.0f..1.0f
				scatter *= (1.0f - scatter * scatter) * 2.59808f * scatterAngle;         // shape quadratic distribution and scale
				var radSin = MathF.Sin(scatter);                               // Green's transform matrix... rotate angle delta
				var radCos = MathF.Cos(scatter);                               // rotational transform from current position to position at time t
				var vxt = Vel.X;
				var vyt = Vel.Y;
				Vel.X = vxt * radCos - vyt * radSin;                           // rotate to random scatter angle
				Vel.Y = vyt * radCos + vxt * radSin;
			}
		}

		public Vertex3D SurfaceVelocity(Vertex3D surfP)
		{
			// linear velocity plus tangential velocity due to rotation
			return Vel
				.Clone()
				.Add(Vertex3D.CrossProduct(AngularVelocity, surfP));
		}

		public void ApplySurfaceImpulse(Vertex3D rotI, Vertex3D impulse)
		{
			Vel.Add(impulse.Clone().MultiplyScalar(InvMass));
			AngularMomentum.Add(rotI);
			var angularMomentum = AngularMomentum.Clone();
			AngularVelocity.Set(angularMomentum.DivideScalar(Inertia));
		}

		public void HandleStaticContact(CollisionEvent coll, float friction, float dTime, PlayerPhysics physics)
		{
			var normVel = Vel.Dot(coll.HitNormal);                             // this should be zero, but only up to +/- PhysicsConstants.ContactVel

			// If some collision has changed the ball's velocity, we may not have to do anything.
			if (normVel <= PhysicsConstants.ContactVel) {

				// external forces (only gravity for now)
				var fe = physics.Gravity.Clone().MultiplyScalar(_data.Mass);
				var dot = fe.Dot(coll.HitNormal);

				// normal force is always nonnegative
				var normalForce = MathF.Max(0.0f, -(dot * dTime + coll.HitOrgNormalVelocity));

				// Add just enough to kill original normal velocity and counteract the external forces.
				Vel.Add(coll.HitNormal.Clone().MultiplyScalar(normalForce));

				// #ifdef C_EMBEDVELLIMIT
				if (coll.HitDistance <= PhysicsConstants.PhysTouch) {
					Vel.Add(coll.HitNormal.Clone()
						.MultiplyScalar(MathF.Max(MathF.Min(PhysicsConstants.EmbedVelLimit, -coll.HitDistance), PhysicsConstants.PhysTouch)));
				}
				// #endif

				ApplyFriction(coll.HitNormal, dTime, friction, physics);
			}
		}

		public void ApplyFriction(Vertex3D hitNormal, float dTime, float frictionCoeff, PlayerPhysics physics)
		{
			// surface contact point relative to center of mass
			var surfP = hitNormal.Clone().MultiplyScalar(-_data.Radius);
			var surfVel = SurfaceVelocity(surfP);

			// calc the tangential slip velocity
			var slip = surfVel.Clone().Sub(hitNormal.Clone().MultiplyScalar(surfVel.Dot(hitNormal)));

			var maxFriction = frictionCoeff * _data.Mass * -physics.Gravity.Dot(hitNormal);

			var slipSpeed = slip.Length();
			Vertex3D slipDir;
			float numer;

			//#ifdef C_BALL_SPIN_HACK
			var normVel = Vel.Dot(hitNormal);
			if (normVel <= 0.025 || slipSpeed < PhysicsConstants.Precision) {
				// check for <=0.025 originated from ball<->rubber collisions pushing the ball upwards, but this is still not enough, some could even use <=0.2
				// slip speed zero - static friction case

				var surfAcc = SurfaceAcceleration(surfP, physics);
				// calc the tangential slip acceleration
				var slipAcc = surfAcc.Clone()
					.Sub(hitNormal.Clone().MultiplyScalar(surfAcc.Dot(hitNormal)));

				// neither slip velocity nor slip acceleration? nothing to do here
				if (slipAcc.LengthSq() < 1e-6) {
					return;
				}

				slipDir = slipAcc.Clone().Normalize();
				numer = -slipDir.Dot(surfAcc);

			} else {
				// nonzero slip speed - dynamic friction case
				slipDir = slip.Clone().DivideScalar(slipSpeed);
				numer = -slipDir.Dot(surfVel);
			}

			var cp = Vertex3D.CrossProduct(surfP, slipDir);
			var p1 = cp.Clone().DivideScalar(Inertia);
			var denom = InvMass + slipDir.Dot(Vertex3D.CrossProduct(p1, surfP));
			var friction = Functions.Clamp(numer / denom, -maxFriction, maxFriction);

			if (!float.IsNaN(friction) && !float.IsInfinity(friction)) {
				ApplySurfaceImpulse(
					cp.Clone().MultiplyScalar(dTime * friction),
					slipDir.Clone().MultiplyScalar(dTime * friction)
				);
			}
		}

		public Vertex3D SurfaceAcceleration(Vertex3D surfP, PlayerPhysics physics)
		{
			// if we had any external torque, we would have to add "(deriv. of ang.Vel.) x surfP" here
			var p2 = Vertex3D.CrossProduct(AngularVelocity, surfP);
			var acceleration = physics.Gravity
				.Clone()
				.MultiplyScalar(InvMass)                                       // linear acceleration
				.Add(Vertex3D.CrossProduct(AngularVelocity, p2));              // centripetal acceleration
			return acceleration;
		}

		public void SetMass(float mass)
		{
			_data.Mass = mass;
			InvMass = 1.0f / mass;
			Inertia = 2.0f / 5.0f * _data.Radius * _data.Radius * _data.Mass;
		}

		public void SetRadius(float radius)
		{
			_data.Radius = radius;
			Inertia = 2.0f / 5.0f * _data.Radius * _data.Radius * _data.Mass;
			CalcHitBBox();
		}
	}
}
