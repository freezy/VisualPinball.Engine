// ReSharper disable CommentTypo
// ReSharper disable CompareOfFloatsByEqualityOperator

using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperHit : HitObject
	{
		private readonly FlipperMover _mover;
		private readonly FlipperData _data;
		private readonly FlipperState _state;
		private readonly TableData _tableData;
		private readonly EventProxy _events;
		private uint _lastHitTime;                                             // m_last_hittime

		public HitCircle HitCircleBase => _mover.HitCircleBase;

		public FlipperHit(FlipperData data, FlipperState state, EventProxy events, Table.Table table) : base(ItemType.Flipper)
		{
			data.UpdatePhysicsSettings(table);
			_events = events;
			_mover = new FlipperMover(data, state, events, table);
			_data = data;
			_state = state;
			_tableData = table.Data;
			UpdatePhysicsFromFlipper();
		}

		public override void SetIndex(int index, int version)
		{
			base.SetIndex(index, version);
			HitCircleBase.SetIndex(index, version);
		}

		public override void CalcHitBBox()
		{
			// Allow roundoff
			HitBBox = new Rect3D(
				_mover.HitCircleBase.Center.X - _mover.FlipperRadius - _mover.EndRadius - 0.1f,
				_mover.HitCircleBase.Center.X + _mover.FlipperRadius + _mover.EndRadius + 0.1f,
				_mover.HitCircleBase.Center.Y - _mover.FlipperRadius - _mover.EndRadius - 0.1f,
				_mover.HitCircleBase.Center.Y + _mover.FlipperRadius + _mover.EndRadius + 0.1f,
				_mover.HitCircleBase.HitBBox.ZLow,
				_mover.HitCircleBase.HitBBox.ZHigh
			);
		}

		public override float HitTest(Ball.Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			if (!_data.IsEnabled) {
				return -1.0f;
			}

			var lastFace = _mover.LastHitFace;

			// for effective computing, adding a last face hit value to speed calculations
			// a ball can only hit one face never two
			// also if a ball hits a face then it can not hit either radius
			// so only check these if a face is not hit
			// endRadius is more likely than baseRadius ... so check it first

			var hitTime = HitTestFlipperFace(ball, dTime, coll, lastFace); // first face
			if (hitTime >= 0) {
				return hitTime;
			}

			hitTime = HitTestFlipperFace(ball, dTime, coll, !lastFace); //second face
			if (hitTime >= 0) {
				_mover.LastHitFace = !lastFace; // change this face to check first // HACK
				return hitTime;
			}

			hitTime = HitTestFlipperEnd(ball, dTime, coll); // end radius
			if (hitTime >= 0) {
				return hitTime;
			}

			hitTime = _mover.HitCircleBase.HitTest(ball, dTime, coll, physics);
			if (hitTime >= 0) {
				// Tangent velocity of contact point (rotate Normal right)
				// rad units*d/t (Radians*diameter/time)
				coll.HitVel.Set(0, 0);
				coll.HitMomentBit = true;
				return hitTime;
			}

			return -1.0f; // no hits
		}

		public override void Contact(CollisionEvent coll, float dTime, PlayerPhysics physics)
		{
			var ball = coll.Ball;
			var normal = coll.HitNormal;

			//#ifdef PhysicsConstants.EMBEDDED
			if (coll.HitDistance < -PhysicsConstants.Embedded) {
				// magic to avoid balls being pushed by each other through resting flippers!
				ball.Hit.Vel.Add(normal.Clone().MultiplyScalar(0.1f));
			}
			//#endif

			var vRel = new Vertex3D();
			var rB = new Vertex3D();
			var rF = new Vertex3D();
			GetRelativeVelocity(normal, ball, vRel, rB, rF);

			// this should be zero, but only up to +/- C_CONTACTVEL
			var normVel = vRel.Dot(normal);

			// If some collision has changed the ball's velocity, we may not have to do anything.
			if (normVel <= PhysicsConstants.ContactVel) {
				// compute accelerations of point on ball and flipper
				var aB = ball.Hit.SurfaceAcceleration(rB, physics);
				var aF = _mover.SurfaceAcceleration(rF);
				var aRel = aB.Clone().Sub(aF);

				// time derivative of the normal vector
				var normalDeriv = Vertex3D.CrossZ(_mover.AngleSpeed, normal);

				// relative acceleration in the normal direction
				var normAcc = aRel.Dot(normal) + 2.0f * normalDeriv.Dot(vRel);

				if (normAcc >= 0) {
					return; // objects accelerating away from each other, nothing to do
				}

				// hypothetical accelerations arising from a unit contact force in normal direction
				var aBc = normal.Clone().MultiplyScalar(ball.Hit.InvMass);
				var pv2 = normal.Clone().MultiplyScalar(-1);
				var cross = Vertex3D.CrossProduct(rF, pv2);
				var pv1 = cross.Clone().DivideScalar(_mover.Inertia);
				var aFc = Vertex3D.CrossProduct(pv1, rF);
				var contactForceAcc = normal.Dot(aBc.Clone().Sub(aFc));

				// find j >= 0 such that normAcc + j * contactForceAcc >= 0  (bodies should not accelerate towards each other)
				var j = -normAcc / contactForceAcc;

				// kill any existing normal velocity
				ball.Hit.Vel.Add(normal.Clone().MultiplyScalar(j * dTime * ball.Hit.InvMass - coll.HitOrgNormalVelocity));
				_mover.ApplyImpulse(cross.Clone().MultiplyScalar(j * dTime));

				// apply friction

				// first check for slippage
				var slip = vRel.Clone().Sub(normal.Clone().MultiplyScalar(normVel)); // calc the tangential slip velocity
				var maxFriction = j * Friction;
				var slipSpeed = slip.Length();
				Vertex3D slipDir;
				Vertex3D crossF;
				float numer;
				float denomF;
				Vertex3D pv13;

				if (slipSpeed < PhysicsConstants.Precision) {
					// slip speed zero - static friction case
					var slipAcc = aRel.Clone().Sub(normal.Clone().MultiplyScalar(aRel.Dot(normal))); // calc the tangential slip acceleration

					// neither slip velocity nor slip acceleration? nothing to do here
					if (slipAcc.LengthSq() < 1e-6) {
						return;
					}

					slipDir = slipAcc.Normalize();
					numer = -slipDir.Dot(aRel);
					crossF = Vertex3D.CrossProduct(rF, slipDir);
					pv13 = crossF.Clone().DivideScalar(-_mover.Inertia);
					denomF = slipDir.Dot(Vertex3D.CrossProduct(pv13, rF));

				} else {
					// nonzero slip speed - dynamic friction case
					slipDir = slip.Clone().DivideScalar(slipSpeed);

					numer = -slipDir.Dot(vRel);
					crossF = Vertex3D.CrossProduct(rF, slipDir);
					pv13 = crossF.Clone().DivideScalar(_mover.Inertia);
					denomF = slipDir.Dot(Vertex3D.CrossProduct(pv13, rF));
				}

				var crossB = Vertex3D.CrossProduct(rB, slipDir);
				var pv12 = crossB.Clone().DivideScalar(ball.Hit.Inertia);
				var denomB = ball.Hit.InvMass + slipDir.Dot(Vertex3D.CrossProduct(pv12, rB));
				var friction = Functions.Clamp(numer / (denomB + denomF), -maxFriction, maxFriction);

				ball.Hit.ApplySurfaceImpulse(
					crossB.Clone().MultiplyScalar(dTime * friction),
					slipDir.Clone().MultiplyScalar(dTime * friction)
				);
				_mover.ApplyImpulse(crossF.Clone().MultiplyScalar(-dTime * friction));
			}
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			var ball = coll.Ball;
			var normal = coll.HitNormal;
			var vRel = new Vertex3D();
			var rB = new Vertex3D();
			var rF = new Vertex3D();
			GetRelativeVelocity(normal, ball, vRel, rB, rF);

			var bnv = normal.Dot(vRel); // relative normal velocity

			if (bnv >= -PhysicsConstants.LowNormVel) {
				// nearly receding ... make sure of conditions
				if (bnv > PhysicsConstants.LowNormVel) {
					// otherwise if clearly approaching .. process the collision
					// is this velocity clearly receding (i.E must > a minimum)
					return;
				}

				//#ifdef PhysicsConstants.EMBEDDED
				if (coll.HitDistance < -PhysicsConstants.Embedded) {
					bnv = -PhysicsConstants.EmbedShot; // has ball become embedded???, give it a kick

				} else {
					return;
				}

				//#endif
			}

			//#ifdef PhysicsConstants.DISP_GAIN
			// correct displacements, mostly from low velocity blindness, an alternative to true acceleration processing
			var hitDist = -PhysicsConstants.DispGain * coll.HitDistance; // distance found in hit detection
			if (hitDist > 1.0e-4) {
				if (hitDist > PhysicsConstants.DispLimit) {
					hitDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				// push along norm, back to free area; use the norm, but is not correct
				ball.State.Pos.Add(coll.HitNormal.Clone().MultiplyScalar(hitDist));
			}
			//#endif

			// angular response to impulse in normal direction
			var angResp = Vertex3D.CrossProduct(rF, normal);

			/*
			 * Check if flipper is in contact with its stopper and the collision impulse
			 * would push it beyond the stopper. In that case, don"t allow any transfer
			 * of kinetic energy from ball to flipper. This avoids overly dead bounces
			 * in that case.
			 */
			var angImp = -angResp.Z; // minus because impulse will apply in -normal direction
			var flipperResponseScaling = 1.0f;
			if (_mover.IsInContact && _mover.ContactTorque * angImp >= 0f) {
				// if impulse pushes against stopper, allow no loss of kinetic energy to flipper
				// (still allow flipper recoil, but a diminished amount)
				angResp.SetZero();
				flipperResponseScaling = 0.5f;
			}

			/*
			 * Rubber has a coefficient of restitution which decreases with the impact velocity.
			 * We use a heuristic model which decreases the COR according to a falloff parameter:
			 * 0 = no falloff, 1 = half the COR at 1 m/s (18.53 speed units)
			 */
			var epsilon = Functions.ElasticityWithFalloff(Elasticity, ElasticityFalloff, bnv);

			var pv1 = angResp.Clone().DivideScalar(_mover.Inertia);
			var impulse = -(1.0f + epsilon) * bnv / (ball.Hit.InvMass + normal.Dot(Vertex3D.CrossProduct(pv1, rF)));
			var flipperImp = normal.Clone().MultiplyScalar(-(impulse * flipperResponseScaling));

			var rotI = Vertex3D.CrossProduct(rF, flipperImp);
			if (_mover.IsInContact) {
				if (rotI.Z * _mover.ContactTorque < 0) {
					// pushing against the solenoid?

					// Get a bound on the time the flipper needs to return to static conditions.
					// If it"s too short, we treat the flipper as static during the whole collision.
					var recoilTime = -rotI.Z / _mover.ContactTorque; // time flipper needs to eliminate this impulse, in 10ms

					// Check ball normal velocity after collision. If the ball rebounded
					// off the flipper, we need to make sure it does so with full
					// reflection, i.E., treat the flipper as static, otherwise
					// we get overly dead bounces.
					var bnvAfter = bnv + impulse * ball.Hit.InvMass;

					if (recoilTime <= 0.5 || bnvAfter > 0) {
						// treat flipper as static for this impact
						impulse = -(1.0f + epsilon) * bnv * ball.Data.Mass;
						flipperImp.SetZero();
						rotI.SetZero();
					}
				}
			}

			ball.Hit.Vel.Add(normal.Clone().MultiplyScalar(impulse * ball.Hit.InvMass)); // new velocity for ball after impact
			_mover.ApplyImpulse(rotI);

			// apply friction
			var tangent = vRel.Clone().Sub(normal.Clone().MultiplyScalar(vRel.Dot(normal))); // calc the tangential velocity

			var tangentSpSq = tangent.LengthSq();
			if (tangentSpSq > 1e-6) {
				tangent.DivideScalar(MathF.Sqrt(tangentSpSq)); // normalize to get tangent direction
				var vt = vRel.Dot(tangent); // get speed in tangential direction

				// compute friction impulse
				var crossB = Vertex3D.CrossProduct(rB, tangent);
				var pv12 = crossB.Clone().DivideScalar(ball.Hit.Inertia);
				var kt = ball.Hit.InvMass + tangent.Dot(Vertex3D.CrossProduct(pv12, rB));

				var crossF = Vertex3D.CrossProduct(rF, tangent);
				var pv13 = crossF.Clone().DivideScalar(_mover.Inertia);
				kt += tangent.Dot(Vertex3D.CrossProduct(pv13, rF)); // flipper only has angular response

				// friction impulse can't be greater than coefficient of friction times collision impulse (Coulomb friction cone)
				var maxFriction = Friction * impulse;
				var jt = Functions.Clamp(-vt / kt, -maxFriction, maxFriction);

				ball.Hit.ApplySurfaceImpulse(
					crossB.Clone().MultiplyScalar(jt),
					tangent.Clone().MultiplyScalar(jt)
				);
				_mover.ApplyImpulse(crossF.Clone().MultiplyScalar(-jt));
			}

			if (bnv < -0.25 && physics.TimeMsec - _lastHitTime > 250) {
				// limit rate to 250 milliseconds per event
				var flipperHit =
					coll.HitMomentBit ? -1.0 : -bnv; // move event processing to end of collision handler...
				if (flipperHit < 0) {
					_events.FireGroupEvent(EventId.HitEventsHit); // simple hit event

				} else {
					// collision velocity (normal to face)
					_events.FireVoidEventParam(EventId.FlipperEventsCollide, flipperHit);
				}
			}

			_lastHitTime = physics.TimeMsec; // keep resetting until idle for 250 milliseconds
		}

		public FlipperMover GetMoverObject()
		{
			return _mover;
		}

		public void UpdatePhysicsFromFlipper()
		{
			ElasticityFalloff = _data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideElasticityFalloff
				: _data.ElasticityFalloff;
			Elasticity = _data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideElasticity
				: _data.Elasticity;
			SetFriction(_data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideFriction
				: _data.Friction);
			Scatter = MathF.DegToRad( _data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideScatterAngle
				: _data.Scatter);
		}

		public float HitTestFlipperFace(Ball.Ball ball, float dTime, CollisionEvent coll, bool face1)
		{
			var angleCur = _state.Angle;
			var angleSpeed = _mover.AngleSpeed; // rotation rate

			var flipperBase = _mover.HitCircleBase.Center;
			var feRadius = _mover.EndRadius;

			var angleMin = MathF.Min(_mover.AngleStart, _mover.AngleEnd);
			var angleMax = MathF.Max(_mover.AngleStart, _mover.AngleEnd);

			var ballRadius = ball.Data.Radius;
			var ballVx = ball.Hit.Vel.X;
			var ballVy = ball.Hit.Vel.Y;

			// flipper positions at zero degrees rotation
			var ffnx = _mover.ZeroAngNorm.X; // flipper face normal vector //Face2
			if (face1) {
				// negative for face1 (left face)
				ffnx = -ffnx;
			}

			var ffny = _mover.ZeroAngNorm.Y; // norm y component same for either face
			var vp = new Vertex2D( // face segment V1 point
				_mover.HitCircleBase.Radius * ffnx, // face endpoint of line segment on base radius
				_mover.HitCircleBase.Radius * ffny
			);

			var faceNormal = new Vertex2D(); // flipper face normal

			float bffnd = 0; // ball flipper face normal distance (negative for normal side)
			float ballVtx = 0; // new ball position at time t in flipper face coordinate
			float ballVty = 0;
			float contactAng = 0;

			// Modified False Position control
			float t = 0;
			float t0 = 0;
			float t1 = 0;
			float d0 = 0;
			float d1 = 0;
			float dp = 0;

			// start first interval ++++++++++++++++++++++++++
			int k;
			for (k = 1; k <= PhysicsConstants.Internations; ++k) {
				// determine flipper rotation direction, limits and parking
				contactAng = angleCur + angleSpeed * t; // angle at time t

				if (contactAng >= angleMax) {
					// stop here
					contactAng = angleMax;
				}
				else if (contactAng <= angleMin) {
					// stop here
					contactAng = angleMin;
				}

				var radSin = MathF.Sin(contactAng); // Green"s transform matrix... rotate angle delta
				var radCos = MathF.Cos(contactAng); // rotational transform from current position to position at time t

				faceNormal.X = ffnx * radCos - ffny * radSin; // rotate to time t, norm and face offset point
				faceNormal.Y = ffny * radCos + ffnx * radSin;

				var vt = new Vertex2D(
					vp.X * radCos - vp.Y * radSin + flipperBase.X, // rotate and translate to world position
					vp.Y * radCos + vp.X * radSin + flipperBase.Y
				);

				ballVtx = ball.State.Pos.X + ballVx * t - vt.X; // new ball position relative to rotated line segment endpoint
				ballVty = ball.State.Pos.Y + ballVy * t - vt.Y;

				bffnd = ballVtx * faceNormal.X + ballVty * faceNormal.Y - ballRadius; // normal distance to segment

				if (MathF.Abs(bffnd) <= PhysicsConstants.Precision) {
					break;
				}

				// loop control, boundary checks, next estimate, etc.
				if (k == 1) {
					// end of pass one ... set full interval pass, t = dtime

					// test for already inside flipper plane, either embedded or beyond the face endpoints
					if (bffnd < -(ball.Data.Radius + feRadius)) {
						return -1.0f; // wrong side of face, or too deeply embedded
					}

					if (bffnd <= PhysicsConstants.PhysTouch) {
						break; // inside the clearance limits, go check face endpoints
					}

					t0 = t1 = dTime;
					d0 = 0;
					d1 = bffnd; // set for second pass, so t=dtime

				} else if (k == 2) {
					// end pass two, check if zero crossing on initial interval, exit
					if (dp * bffnd > 0.0) {
						return -1.0f; // no solution ... no obvious zero crossing
					}

					t0 = 0;
					t1 = dTime;
					d0 = dp;
					d1 = bffnd; // testing MFP estimates

				} else {
					// (k >= 3)                           // MFP root search +++++++++++++++++++++++++++++++++++++++++
					if (bffnd * d0 <= 0.0) {
						// zero crossing
						t1 = t;
						d1 = bffnd;
						if (dp * bffnd > 0.0) {
							d0 *= 0.5f;
						}

					} else {
						// move right limits
						t0 = t;
						d0 = bffnd;
						if (dp * bffnd > 0.0) {
							d1 *= 0.5f;
						}
					} // move left limits
				}

				t = t0 - d0 * (t1 - t0) / (d1 - d0); // next estimate
				dp = bffnd; // remember
			}

			// +++ End time iteration loop found time t soultion ++++++
			if (float.IsNaN(t) || float.IsInfinity(t)
			                   || t < 0
			                   || t > dTime // time is outside this frame ... no collision
			                   || k > PhysicsConstants.Internations && MathF.Abs(bffnd) > ball.Data.Radius * 0.25) {
				// last ditch effort to accept a near solution

				return -1.0f; // no solution
			}

			// here ball and flipper face are in contact... past the endpoints, also, don"t forget embedded and near solution
			var faceTangent = new Vertex2D(); // flipper face tangent
			if (face1) {
				// left face?
				faceTangent.X = -faceNormal.Y;
				faceTangent.Y = faceNormal.X;
			}
			else {
				// rotate to form Tangent vector
				faceTangent.X = faceNormal.Y;
				faceTangent.Y = -faceNormal.X;
			}

			var bfftd = ballVtx * faceTangent.X + ballVty * faceTangent.Y; // ball to flipper face tangent distance

			var len = _mover.FlipperRadius * _mover.ZeroAngNorm.X; // face segment length ... e.G. same on either face
			if (bfftd < -PhysicsConstants.ToleranceEndPoints || bfftd > len + PhysicsConstants.ToleranceEndPoints) {
				return -1.0f; // not in range of touching
			}

			var hitZ = ball.State.Pos.Z + ball.Hit.Vel.Z * t; // check for a hole, relative to ball rolling point at hittime

			// check limits of object"s height and depth
			if (hitZ + ballRadius * 0.5 < HitBBox.ZLow || hitZ - ballRadius * 0.5 > HitBBox.ZHigh) {
				return -1.0f;
			}

			// ok we have a confirmed contact, calc the stats, remember there are "near" solution, so all
			// parameters need to be calculated from the actual configuration, i.E contact radius must be calc"ed

			// hit normal is same as line segment normal
			coll.HitNormal.Set(faceNormal.X, faceNormal.Y, 0);

			var dist = new Vertex2D( // calculate moment from flipper base center
				ball.State.Pos.X + ballVx * t - ballRadius * faceNormal.X -
				_mover.HitCircleBase.Center.X, // center of ball + projected radius to contact point
				ball.State.Pos.Y + ballVy * t - ballRadius * faceNormal.Y -
				_mover.HitCircleBase.Center.Y // all at time t
			);

			var distance = MathF.Sqrt(dist.X * dist.X + dist.Y * dist.Y); // distance from base center to contact point

			var invDist = 1.0f / distance;
			coll.HitVel.Set(-dist.Y * invDist,
				dist.X * invDist); // Unit Tangent velocity of contact point(rotate Normal clockwise)
			//coll.Hitvelocity.Z = 0.0f; // used as normal velocity so far, only if isContact is set, see below

			if (contactAng >= angleMax && angleSpeed > 0 || contactAng <= angleMin && angleSpeed < 0) {
				// hit limits ???
				angleSpeed = 0.0f; // rotation stopped
			}

			coll.HitMomentBit = distance == 0;

			var dv = new Vertex2D( // delta velocity ball to face
				ballVx - coll.HitVel.X * angleSpeed * distance,
				ballVy - coll.HitVel.Y * angleSpeed * distance
			);

			var bnv = dv.X * coll.HitNormal.X + dv.Y * coll.HitNormal.Y; // dot Normal to delta v

			if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel && bffnd <= PhysicsConstants.PhysTouch) {
				coll.IsContact = true;
				coll.HitOrgNormalVelocity = bnv;

			} else if (bnv > PhysicsConstants.LowNormVel) {
				return -1.0f; // not hit ... ball is receding from endradius already, must have been embedded
			}

			coll.HitDistance = bffnd; // normal ...Actual contact distance ...
			//coll.M_hitRigid = true;                               // collision type

			return t;
		}

		private void GetRelativeVelocity(Vertex3D normal, Ball.Ball ball, Vertex3D vRel, Vertex3D rB, Vertex3D rF)
		{
			rB.Set(normal.Clone().MultiplyScalar(-ball.Data.Radius));
			var hitPos = ball.State.Pos.Clone().Add(rB);

			var cF = new Vertex3D(
				_mover.HitCircleBase.Center.X,
				_mover.HitCircleBase.Center.Y,
				ball.State.Pos.Z // make sure collision happens in same z plane where ball is
			);

			rF.Set(hitPos.Clone().Sub(cF)); // displacement relative to flipper center
			var vB = ball.Hit.SurfaceVelocity(rB);
			var vF = _mover.SurfaceVelocity(rF);
			vRel.Set(vB.Clone().Sub(vF));
		}

		private float HitTestFlipperEnd(Ball.Ball ball, float dTime, CollisionEvent coll)
		{
			var angleCur = _state.Angle;
			var angleSpeed = _mover.AngleSpeed; // rotation rate

			var flipperBase = _mover.HitCircleBase.Center;

			var angleMin = MathF.Min(_mover.AngleStart, _mover.AngleEnd);
			var angleMax = MathF.Max(_mover.AngleStart, _mover.AngleEnd);

			var ballRadius = ball.Data.Radius;
			var feRadius = _mover.EndRadius;

			var ballEndRadius = feRadius + ballRadius; // magnititude of (ball - flipperEnd)

			var ballX = ball.State.Pos.X;
			var ballY = ball.State.Pos.Y;

			var ballVx = ball.Hit.Vel.X;
			var ballVy = ball.Hit.Vel.Y;

			var vp = new Vertex2D(
				0.0f, // m_flipperradius * sin(0);
				-_mover.FlipperRadius // m_flipperradius * (-cos(0));
			);

			float ballVtx = 0;
			float ballVty = 0; // new ball position at time t in flipper face coordinate
			float contactAng = 0;
			float bFend = 0;
			float cbceDist = 0;
			float t0 = 0;
			float t1 = 0;
			float d0 = 0;
			float d1 = 0;
			float dp = 0;

			// start first interval ++++++++++++++++++++++++++
			float t = 0;
			int k;
			for (k = 1; k <= PhysicsConstants.Internations; ++k) {
				// determine flipper rotation direction, limits and parking
				contactAng = angleCur + angleSpeed * t; // angle at time t

				if (contactAng >= angleMax) {
					contactAng = angleMax; // stop here

				} else if (contactAng <= angleMin) {
					contactAng = angleMin; // stop here
				}

				var radSin = MathF.Sin(contactAng); // Green"s transform matrix... rotate angle delta
				var radCos = MathF.Cos(contactAng); // rotational transform from zero position to position at time t

				// rotate angle delta unit vector, rotates system according to flipper face angle
				var vt = new Vertex2D(
					vp.X * radCos - vp.Y * radSin + flipperBase.X, // rotate and translate to world position
					vp.Y * radCos + vp.X * radSin + flipperBase.Y
				);

				ballVtx = ballX + ballVx * t - vt.X; // new ball position relative to flipper end radius
				ballVty = ballY + ballVy * t - vt.Y;

				// center ball to center end radius distance
				cbceDist = MathF.Sqrt(ballVtx * ballVtx + ballVty * ballVty);

				// ball face-to-radius surface distance
				bFend = cbceDist - ballEndRadius;

				if (MathF.Abs(bFend) <= PhysicsConstants.Precision) {
					break;
				}

				if (k == 1) {
					// end of pass one ... set full interval pass, t = dtime
					// test for extreme conditions
					if (bFend < -(ball.Data.Radius + feRadius)) {
						// too deeply embedded, ambiguous position
						return -1.0f;
					}

					if (bFend <= PhysicsConstants.PhysTouch) {
						// inside the clearance limits
						break;
					}

					// set for second pass, force t=dtime
					t0 = t1 = dTime;
					d0 = 0;
					d1 = bFend;

				} else if (k == 2) {
					// end pass two, check if zero crossing on initial interval, exit if none
					if (dp * bFend > 0.0) {
						// no solution ... no obvious zero crossing
						return -1.0f;
					}

					t0 = 0;
					t1 = dTime;
					d0 = dp;
					d1 = bFend; // set initial boundaries

				} else {
					// (k >= 3) // MFP root search
					if (bFend * d0 <= 0.0) {
						// zero crossing
						t1 = t;
						d1 = bFend;
						if (dp * bFend > 0) {
							d0 *= 0.5f;
						}

					} else {
						t0 = t;
						d0 = bFend;
						if (dp * bFend > 0) {
							d1 *= 0.5f;
						}
					} // move left interval limit
				}

				t = t0 - d0 * (t1 - t0) / (d1 - d0); // estimate next t
				dp = bFend; // remember
			}

			//+++ End time interaction loop found time t solution ++++++

			// time is outside this frame ... no collision
			if (float.IsNaN(t) || float.IsInfinity(t) || t < 0 || t > dTime ||
			    k > PhysicsConstants.Internations && MathF.Abs(bFend) > ball.Data.Radius * 0.25) {
				// last ditch effort to accept a solution
				return -1.0f; // no solution
			}

			// here ball and flipper end are in contact .. well in most cases, near and embedded solutions need calculations
			var hitZ = ball.State.Pos.Z + ball.Hit.Vel.Z * t; // check for a hole, relative to ball rolling point at hittime

			// check limits of object"s height and depth
			if (hitZ + ballRadius * 0.5 < HitBBox.ZLow || hitZ - ballRadius * 0.5 > HitBBox.ZHigh) {
				return -1.0f;
			}

			// ok we have a confirmed contact, calc the stats, remember there are "near" solution, so all
			// parameters need to be calculated from the actual configuration, i.E. contact radius must be calc"ed
			var invCbceDist = 1.0f / cbceDist;
			coll.HitNormal.Set(
				ballVtx * invCbceDist, // normal vector from flipper end to ball
				ballVty * invCbceDist,
				0.0f
			);

			// vector from base to flipperEnd plus the projected End radius
			var dist = new Vertex2D(
				ball.State.Pos.X + ballVx * t - ballRadius * coll.HitNormal.X - _mover.HitCircleBase.Center.X,
				ball.State.Pos.Y + ballVy * t - ballRadius * coll.HitNormal.Y - _mover.HitCircleBase.Center.Y
			);

			// distance from base center to contact point
			var distance = MathF.Sqrt(dist.X * dist.X + dist.Y * dist.Y);

			// hit limits ???
			if (contactAng >= angleMax && angleSpeed > 0 || contactAng <= angleMin && angleSpeed < 0) {
				angleSpeed = 0; // rotation stopped
			}

			// Unit Tangent vector velocity of contact point(rotate normal right)
			var invDistance = 1.0f / distance;
			coll.HitVel.Set(-dist.Y * invDistance, dist.X * invDistance);
			coll.HitMomentBit = distance == 0;

			// recheck using actual contact angle of velocity direction
			var dv = new Vertex2D(
				ballVx - coll.HitVel.X * angleSpeed * distance, // delta velocity ball to face
				ballVy - coll.HitVel.Y * angleSpeed * distance
			);

			var bnv = dv.X * coll.HitNormal.X + dv.Y * coll.HitNormal.Y; // dot Normal to delta v

			if (bnv >= 0) {
				// not hit ... ball is receding from face already, must have been embedded or shallow angled
				return -1.0f;
			}

			if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel && bFend <= PhysicsConstants.PhysTouch) {
				coll.IsContact = true;
				coll.HitOrgNormalVelocity = bnv;
			}

			coll.HitDistance = bFend; // actual contact distance ..

			return t;
		}

		public float GetHitTime()
		{
			return _mover.GetHitTime();
		}
	}
}
