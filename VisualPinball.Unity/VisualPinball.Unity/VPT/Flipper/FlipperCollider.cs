using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity
{
	public struct FlipperCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private CircleCollider _hitCircleBase;
		private float _zLow;
		private float _zHigh;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, FlipperHit src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<FlipperCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		private void Init(FlipperHit src)
		{
			_header.Init(ColliderType.Flipper, src);
			_hitCircleBase = CircleCollider.Create(src.HitCircleBase);
			_zLow = src.HitBBox.ZLow;
			_zHigh = src.HitBBox.ZHigh;
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			ref FlipperHitData hitData,
			in FlipperMovementData movementData, in FlipperStaticData matData, in BallData ball, float dTime)
		{
			// todo
			// if (!_data.IsEnabled) {
			// 	return -1.0f;
			// }

			var lastFace = hitData.LastHitFace;

			// for effective computing, adding a last face hit value to speed calculations
			// a ball can only hit one face never two
			// also if a ball hits a face then it can not hit either radius
			// so only check these if a face is not hit
			// endRadius is more likely than baseRadius ... so check it first

			// first face
			var hitTime = HitTestFlipperFace(ref collEvent, ref hitData, movementData, matData, ball, dTime, lastFace);
			if (hitTime >= 0) {
				return hitTime;
			}

			// second face
			hitTime = HitTestFlipperFace(ref collEvent, ref hitData, movementData, matData, ball, dTime, !lastFace);
			if (hitTime >= 0) {
				hitData.LastHitFace = !lastFace; // change this face to check first // HACK
				return hitTime;
			}

			// end radius
			hitTime = HitTestFlipperEnd(ref collEvent, ref hitData, movementData, matData, ball, dTime);
			if (hitTime >= 0) {
				return hitTime;
			}

			hitTime = _hitCircleBase.HitTest(ref collEvent, ref insideOfs, in ball, dTime);
			if (hitTime >= 0) {
				hitData.HitVelocity.x = 0;  // Tangent velocity of contact point (rotate Normal right)
				hitData.HitVelocity.y = 0;  // rad units*d/t (Radians*diameter/time)
				hitData.HitMomentBit = true;
				return hitTime;
			}

			return -1;
		}

		private float HitTestFlipperFace(ref CollisionEventData collEvent, ref FlipperHitData hitData,
			in FlipperMovementData movementData, in FlipperStaticData matData, in BallData ball,
			float dTime, bool face1)
		{
			var angleCur = movementData.Angle;
			var angleSpeed = movementData.AngleSpeed; // rotation rate

			var flipperBase = _hitCircleBase.Center;
			var feRadius = matData.EndRadius;

			var angleMin = math.min(matData.AngleStart, matData.AngleEnd);
			var angleMax = math.max(matData.AngleStart, matData.AngleEnd);

			var ballRadius = ball.Radius;
			var ballVx = ball.Velocity.x;
			var ballVy = ball.Velocity.y;

			// flipper positions at zero degrees rotation
			var ffnx = hitData.ZeroAngNorm.x; // flipper face normal vector //Face2
			if (face1) {
				// negative for face1 (left face)
				ffnx = -ffnx;
			}

			var ffny = hitData.ZeroAngNorm.y; // norm y component same for either face

			// face segment V1 point
			var vp = new float2(
				_hitCircleBase.Radius * ffnx,    // face endpoint of line segment on base radius
				_hitCircleBase.Radius * ffny
			);

			// flipper face normal
			var faceNormal = new float2();

			float bffnd = 0;     // ball flipper face normal distance (negative for normal side)
			float ballVtx = 0;   // new ball position at time t in flipper face coordinate
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

				if (contactAng >= angleMax) {           // stop here
					contactAng = angleMax;

				} else if (contactAng <= angleMin) {    // stop here
					contactAng = angleMin;
				}

				var radSin = math.sin(contactAng); // Green's transform matrix... rotate angle delta
				var radCos = math.cos(contactAng); // rotational transform from current position to position at time t

				faceNormal.x = ffnx * radCos - ffny * radSin; // rotate to time t, norm and face offset point
				faceNormal.y = ffny * radCos + ffnx * radSin;

				// rotate and translate to world position
				var vt = new float2(
					vp.x * radCos - vp.y * radSin + flipperBase.x,
					vp.y * radCos + vp.x * radSin + flipperBase.y
				);

				// new ball position relative to rotated line segment endpoint
				ballVtx = ball.Position.x + ballVx * t - vt.x;
				ballVty = ball.Position.y + ballVy * t - vt.y;

				bffnd = ballVtx * faceNormal.x+ ballVty * faceNormal.y - ballRadius; // normal distance to segment

				if (math.abs(bffnd) <= PhysicsConstants.Precision) {
					break;
				}

				// loop control, boundary checks, next estimate, etc.

				if (k == 1) { // end of pass one ... set full interval pass, t = dtime

					// test for already inside flipper plane, either embedded or beyond the face endpoints
					if (bffnd < -(ball.Radius + feRadius)) {
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
					// (k >= 3) MFP root search +++++++++++++++++++++++++++++++++++++++++
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
			                   || k > PhysicsConstants.Internations && math.abs(bffnd) > ball.Radius * 0.25) {
				// last ditch effort to accept a near solution

				return -1.0f; // no solution
			}

			// here ball and flipper face are in contact... past the endpoints, also, don"t forget embedded and near solution
			var faceTangent = new float2(); // flipper face tangent
			if (face1) {
				// left face?
				faceTangent.x = -faceNormal.y;
				faceTangent.y = faceNormal.x;

			} else {
				// rotate to form Tangent vector
				faceTangent.x = faceNormal.y;
				faceTangent.y = -faceNormal.x;
			}

			var bfftd = ballVtx * faceTangent.x + ballVty * faceTangent.y; // ball to flipper face tangent distance

			var len = matData.FlipperRadius * hitData.ZeroAngNorm.x; // face segment length ... e.G. same on either face
			if (bfftd < -PhysicsConstants.ToleranceEndPoints || bfftd > len + PhysicsConstants.ToleranceEndPoints) {
				return -1.0f; // not in range of touching
			}

			var hitZ = ball.Position.z + ball.Velocity.z * t; // check for a hole, relative to ball rolling point at hittime

			// check limits of object's height and depth
			if (hitZ + ballRadius * 0.5 < _zLow || hitZ - ballRadius * 0.5 > _zHigh) {
				return -1.0f;
			}

			// ok we have a confirmed contact, calc the stats, remember there are "near" solution, so all
			// parameters need to be calculated from the actual configuration, i.E contact radius must be calc"ed

			// hit normal is same as line segment normal
			collEvent.HitNormal.x = faceNormal.x;
			collEvent.HitNormal.y = faceNormal.y;
			collEvent.HitNormal.z = 0;

			var dist = new float2( // calculate moment from flipper base center
				ball.Position.x + ballVx * t - ballRadius * faceNormal.x - _hitCircleBase.Center.x, // center of ball + projected radius to contact point
				ball.Position.y + ballVy * t - ballRadius * faceNormal.y - _hitCircleBase.Center.y // all at time t
			);

			var distance = math.sqrt(dist.x * dist.x + dist.y * dist.y); // distance from base center to contact point
			var invDist = 1.0f / distance;

			hitData.HitVelocity.x = -dist.y * invDist;
			hitData.HitVelocity.y = dist.x * invDist; // Unit Tangent velocity of contact point(rotate Normal clockwise)
			//coll.Hitvelocity.Z = 0.0f; // used as normal velocity so far, only if isContact is set, see below

			if (contactAng >= angleMax && angleSpeed > 0 || contactAng <= angleMin && angleSpeed < 0) {
				// hit limits ???
				angleSpeed = 0.0f; // rotation stopped
			}

			hitData.HitMomentBit = distance == 0;

			var dv = new float2( // delta velocity ball to face
				ballVx - hitData.HitVelocity.x * angleSpeed * distance,
				ballVy - hitData.HitVelocity.y * angleSpeed * distance
			);

			var bnv = dv.x * collEvent.HitNormal.x + dv.y * collEvent.HitNormal.y; // dot Normal to delta v

			if (math.abs(bnv) <= PhysicsConstants.ContactVel && bffnd <= PhysicsConstants.PhysTouch) {
				collEvent.IsContact = true;
				collEvent.HitOrgNormalVelocity = bnv;

			} else if (bnv > PhysicsConstants.LowNormVel) {
				return -1.0f; // not hit ... ball is receding from endradius already, must have been embedded
			}

			collEvent.HitDistance = bffnd; // normal ...Actual contact distance ...
			//coll.M_hitRigid = true;                               // collision type

			return t;
		}

		private float HitTestFlipperEnd(ref CollisionEventData collEvent, ref FlipperHitData hitData,
			in FlipperMovementData movementData, in FlipperStaticData matData, in BallData ball, float dTime)
		{
			var angleCur = movementData.Angle;
			var angleSpeed = movementData.AngleSpeed; // rotation rate

			var flipperBase = _hitCircleBase.Center;

			var angleMin = math.min(matData.AngleStart, matData.AngleEnd);
			var angleMax = math.max(matData.AngleStart, matData.AngleEnd);

			var ballRadius = ball.Radius;
			var feRadius = matData.EndRadius;

			var ballEndRadius = feRadius + ballRadius; // magnititude of (ball - flipperEnd)

			var ballX = ball.Position.x;
			var ballY = ball.Position.y;

			var ballVx = ball.Velocity.x;
			var ballVy = ball.Velocity.y;

			var vp = new float2(
				0.0f,                   // m_flipperradius * sin(0);
				-matData.FlipperRadius  // m_flipperradius * (-cos(0));
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
					contactAng = angleMax;              // stop here

				} else if (contactAng <= angleMin) {
					contactAng = angleMin;              // stop here
				}

				var radSin = math.sin(contactAng); // Green's transform matrix... rotate angle delta
				var radCos = math.cos(contactAng); // rotational transform from zero position to position at time t

				// rotate angle delta unit vector, rotates system according to flipper face angle
				var vt = new float2(
					vp.x * radCos - vp.y * radSin + flipperBase.x, // rotate and translate to world position
					vp.y * radCos + vp.x * radSin + flipperBase.y
				);

				ballVtx = ballX + ballVx * t - vt.x; // new ball position relative to flipper end radius
				ballVty = ballY + ballVy * t - vt.y;

				// center ball to center end radius distance
				cbceDist = math.sqrt(ballVtx * ballVtx + ballVty * ballVty);

				// ball face-to-radius surface distance
				bFend = cbceDist - ballEndRadius;

				if (math.abs(bFend) <= PhysicsConstants.Precision) {
					break;
				}

				if (k == 1) {
					// end of pass one ... set full interval pass, t = dtime
					// test for extreme conditions
					if (bFend < -(ball.Radius + feRadius)) {
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
			    k > PhysicsConstants.Internations && math.abs(bFend) > ball.Radius * 0.25) {
				// last ditch effort to accept a solution
				return -1.0f; // no solution
			}

			// here ball and flipper end are in contact .. well in most cases, near and embedded solutions need calculations
			var hitZ = ball.Position.z + ball.Velocity.z * t; // check for a hole, relative to ball rolling point at hittime

			// check limits of object"s height and depth
			if (hitZ + ballRadius * 0.5 < _zLow || hitZ - ballRadius * 0.5 > _zHigh) {
				return -1.0f;
			}

			// ok we have a confirmed contact, calc the stats, remember there are "near" solution, so all
			// parameters need to be calculated from the actual configuration, i.E. contact radius must be calc"ed
			var invCbceDist = 1.0f / cbceDist;
			collEvent.HitNormal.x = ballVtx * invCbceDist; // normal vector from flipper end to ball
			collEvent.HitNormal.y = ballVty * invCbceDist;
			collEvent.HitNormal.z = 0.0f;

			// vector from base to flipperEnd plus the projected End radius
			var dist = new float2(
				ball.Position.x + ballVx * t - ballRadius * collEvent.HitNormal.x - _hitCircleBase.Center.x,
				ball.Position.y + ballVy * t - ballRadius * collEvent.HitNormal.y - _hitCircleBase.Center.y
			);

			// distance from base center to contact point
			var distance = math.sqrt(dist.x * dist.x + dist.y * dist.y);

			// hit limits ???
			if (contactAng >= angleMax && angleSpeed > 0 || contactAng <= angleMin && angleSpeed < 0) {
				angleSpeed = 0; // rotation stopped
			}

			// Unit Tangent vector velocity of contact point(rotate normal right)
			var invDistance = 1.0f / distance;
			hitData.HitVelocity.x = -dist.y * invDistance;
			hitData.HitVelocity.y = dist.x * invDistance;
			hitData.HitMomentBit = distance == 0;

			// recheck using actual contact angle of velocity direction
			var dv = new float2(
				ballVx - hitData.HitVelocity.x * angleSpeed * distance, // delta velocity ball to face
				ballVy - hitData.HitVelocity.y * angleSpeed * distance
			);

			var bnv = dv.x * collEvent.HitNormal.x + dv.y * collEvent.HitNormal.y; // dot Normal to delta v

			if (bnv >= 0) {
				// not hit ... ball is receding from face already, must have been embedded or shallow angled
				return -1.0f;
			}

			if (math.abs(bnv) <= PhysicsConstants.ContactVel && bFend <= PhysicsConstants.PhysTouch) {
				collEvent.IsContact = true;
				collEvent.HitOrgNormalVelocity = bnv;
			}

			collEvent.HitDistance = bFend; // actual contact distance ..

			return t;
		}

		#endregion

		#region Contact

		public void Contact(ref BallData ball, ref CollisionEventData collEvent, ref FlipperMovementData movementData,
			in FlipperStaticData matData, in FlipperVelocityData velData, float dTime, in float3 gravity)
		{
			var normal = collEvent.HitNormal;

			if (collEvent.HitDistance < -PhysicsConstants.Embedded) {
				// magic to avoid balls being pushed by each other through resting flippers!
				ball.Velocity += 0.1f * normal;
			}

			GetRelativeVelocity(normal, ball, movementData, out var vRel, out var rB, out var rF);

			// this should be zero, but only up to +/- C_CONTACTVEL
			var normVel = math.dot(vRel, normal);

			// If some collision has changed the ball's velocity, we may not have to do anything.
			if (normVel <= PhysicsConstants.ContactVel) {
				// compute accelerations of point on ball and flipper
				var aB = BallData.SurfaceAcceleration(in ball, in rB, in gravity);
				var aF = FlipperMovementData.SurfaceAcceleration(in movementData, in velData, in rF);
				var aRel = aB - aF;

				// time derivative of the normal vector
				var normalDeriv = Math.CrossZ(movementData.AngleSpeed, normal);

				// relative acceleration in the normal direction
				var normAcc = math.dot(aRel, normal) + 2.0f * math.dot(normalDeriv, vRel);

				if (normAcc >= 0) {
					return; // objects accelerating away from each other, nothing to do
				}

				// hypothetical accelerations arising from a unit contact force in normal direction
				var aBc = ball.InvMass * normal;
				var cross = math.cross(rF, -normal);
				var aFc = math.cross(cross / matData.Inertia, rF);
				var contactForceAcc = math.dot(normal, aBc - aFc);

				// find j >= 0 such that normAcc + j * contactForceAcc >= 0  (bodies should not accelerate towards each other)
				var j = -normAcc / contactForceAcc;

				// kill any existing normal velocity
				ball.Velocity += (j * dTime * ball.InvMass - collEvent.HitOrgNormalVelocity) * normal;
				movementData.ApplyImpulse(j * dTime * cross, matData.Inertia);

				// apply friction

				// first check for slippage
				var slip = vRel - normVel * normal; // calc the tangential slip velocity
				var maxFriction = j * _header.Material.Friction;
				var slipSpeed = math.length(slip);
				float3 slipDir;
				float3 crossF;
				float numer;
				float denomF;

				if (slipSpeed < PhysicsConstants.Precision) {
					// slip speed zero - static friction case
					var slipAcc = aRel - math.dot(aRel, normal) * normal; // calc the tangential slip acceleration

					// neither slip velocity nor slip acceleration? nothing to do here
					if (math.lengthsq(slipAcc) < 1e-6) {
						return;
					}

					slipDir = math.normalize(slipAcc);
					numer = math.dot(-slipDir, aRel);
					crossF = math.cross(rF, slipDir);
					denomF = math.dot(slipDir, math.cross(crossF / -matData.Inertia, rF));

				} else {
					// nonzero slip speed - dynamic friction case
					slipDir = slip / slipSpeed;

					numer = math.dot(-slipDir, vRel);
					crossF = math.cross(rF, slipDir);
					denomF = math.dot(slipDir, math.cross(crossF / matData.Inertia, rF));
				}

				var crossB = math.cross(rB, slipDir);
				var denomB = ball.InvMass + math.dot(slipDir, math.cross(crossB / ball.Inertia, rB));
				var friction = math.clamp(numer / (denomB + denomF), -maxFriction, maxFriction);

				ball.ApplySurfaceImpulse(dTime * friction * crossB, dTime * friction * slipDir);
				movementData.ApplyImpulse(-dTime * friction * crossF, matData.Inertia);
			}
		}

		private void GetRelativeVelocity(in float3 normal, in BallData ball, in FlipperMovementData movementData, out float3 vRel, out float3 rB, out float3 rF)
		{
			rB = -ball.Radius * normal;
			var hitPos = ball.Position + rB;

			var cF = new float3(
				_hitCircleBase.Center.x,
				_hitCircleBase.Center.y,
				ball.Position.z // make sure collision happens in same z plane where ball is
			);

			rF = hitPos - cF; // displacement relative to flipper center
			var vB = BallData.SurfaceVelocity(in ball, in rB);
			var vF = FlipperMovementData.SurfaceVelocity(in movementData, in rF);
			vRel = vB - vF;
		}

		#endregion

		#region Collision

		public void Collide(ref BallData ball, ref CollisionEventData collEvent, ref FlipperMovementData movementData,
			ref NativeQueue<EventData>.ParallelWriter events, in FlipperStaticData matData,
			in FlipperVelocityData velData, in FlipperHitData hitData, uint timeMsec)
		{
			var normal = collEvent.HitNormal;
			GetRelativeVelocity(normal, ball, movementData, out var vRel, out var rB, out var rF);

			var bnv = math.dot(normal, vRel); // relative normal velocity

			if (bnv >= -PhysicsConstants.LowNormVel) {
				// nearly receding ... make sure of conditions
				if (bnv > PhysicsConstants.LowNormVel) {
					// otherwise if clearly approaching .. process the collision
					// is this velocity clearly receding (i.E must > a minimum)
					return;
				}

				//#ifdef PhysicsConstants.EMBEDDED
				if (collEvent.HitDistance < -PhysicsConstants.Embedded) {
					bnv = -PhysicsConstants.EmbedShot; // has ball become embedded???, give it a kick

				} else {
					return;
				}

				//#endif
			}

			//#ifdef PhysicsConstants.DISP_GAIN
			// correct displacements, mostly from low velocity blindness, an alternative to true acceleration processing
			var hitDist = -PhysicsConstants.DispGain * collEvent.HitDistance; // distance found in hit detection
			if (hitDist > 1.0e-4) {
				if (hitDist > PhysicsConstants.DispLimit) {
					hitDist = PhysicsConstants.DispLimit; // crossing ramps, delta noise
				}

				// push along norm, back to free area; use the norm, but is not correct
				ball.Position += hitDist * collEvent.HitNormal;
			}
			//#endif

			// angular response to impulse in normal direction
			var angResp = math.cross(rF, normal);

			/*
			 * Check if flipper is in contact with its stopper and the collision impulse
			 * would push it beyond the stopper. In that case, don"t allow any transfer
			 * of kinetic energy from ball to flipper. This avoids overly dead bounces
			 * in that case.
			 */
			var angImp = -angResp.z; // minus because impulse will apply in -normal direction
			var flipperResponseScaling = 1.0f;
			if (velData.IsInContact && velData.ContactTorque * angImp >= 0f) {
				// if impulse pushes against stopper, allow no loss of kinetic energy to flipper
				// (still allow flipper recoil, but a diminished amount)
				angResp.x = 0;
				angResp.y = 0;
				angResp.z = 0;
				flipperResponseScaling = 0.5f;
			}

			/*
			 * Rubber has a coefficient of restitution which decreases with the impact velocity.
			 * We use a heuristic model which decreases the COR according to a falloff parameter:
			 * 0 = no falloff, 1 = half the COR at 1 m/s (18.53 speed units)
			 */
			var epsilon = Math.ElasticityWithFalloff(_header.Material.Elasticity, _header.Material.ElasticityFalloff, bnv);

			var pv1 = angResp / matData.Inertia;
			var impulse = -(1.0f + epsilon) * bnv / (ball.InvMass + math.dot(normal, math.cross(pv1, rF)));
			var flipperImp = -(impulse * flipperResponseScaling) * normal;

			var rotI = math.cross(rF, flipperImp);
			if (velData.IsInContact) {
				if (rotI.z * velData.ContactTorque < 0) {
					// pushing against the solenoid?

					// Get a bound on the time the flipper needs to return to static conditions.
					// If it"s too short, we treat the flipper as static during the whole collision.
					var recoilTime = -rotI.z / velData.ContactTorque; // time flipper needs to eliminate this impulse, in 10ms

					// Check ball normal velocity after collision. If the ball rebounded
					// off the flipper, we need to make sure it does so with full
					// reflection, i.E., treat the flipper as static, otherwise
					// we get overly dead bounces.
					var bnvAfter = bnv + impulse * ball.InvMass;

					if (recoilTime <= 0.5 || bnvAfter > 0) {
						// treat flipper as static for this impact
						impulse = -(1.0f + epsilon) * bnv * ball.Mass;
						flipperImp.x = 0;
						flipperImp.y = 0;
						flipperImp.z = 0;
						rotI.x = 0;
						rotI.y = 0;
						rotI.z = 0;
					}
				}
			}

			ball.Velocity += impulse * ball.InvMass * normal; // new velocity for ball after impact
			movementData.ApplyImpulse(rotI, matData.Inertia);

			// apply friction
			var tangent = vRel - math.dot(vRel, normal) * normal; // calc the tangential velocity

			var tangentSpSq = math.lengthsq(tangent);
			if (tangentSpSq > 1e-6) {
				tangent /= math.sqrt(tangentSpSq); // normalize to get tangent direction
				var vt = math.dot(vRel, tangent); // get speed in tangential direction

				// compute friction impulse
				var crossB = math.cross(rB, tangent);
				var pv12 = crossB / ball.Inertia;
				var kt = ball.InvMass + math.dot(tangent, math.cross(pv12, rB));

				var crossF = math.cross(rF, tangent);
				var pv13 = crossF / matData.Inertia;
				kt += math.dot(tangent, math.cross(pv13, rF)); // flipper only has angular response

				// friction impulse can't be greater than coefficient of friction times collision impulse (Coulomb friction cone)
				var maxFriction = _header.Material.Friction * impulse;
				var jt = math.clamp(-vt / kt, -maxFriction, maxFriction);

				ball.ApplySurfaceImpulse(
					jt * crossB,
					jt * tangent
				);
				movementData.ApplyImpulse(-jt * crossF, matData.Inertia);
			}

			// event
			if (bnv < -0.25f && timeMsec - movementData.LastHitTime > 250) {
				// limit rate to 250 milliseconds per event
				var flipperHit = hitData.HitMomentBit ? -1.0f : -bnv; // move event processing to end of collision handler...
				if (flipperHit < 0f) {
					// simple hit event
					events.Enqueue(new EventData(EventId.HitEventsHit, _header.Entity, true));

				} else {
					// collision velocity (normal to face)
					events.Enqueue(new EventData(EventId.FlipperEventsCollide, _header.Entity, flipperHit));
				}
			}
			movementData.LastHitTime = timeMsec; // keep resetting until idle for 250 milliseconds
		}

		#endregion
	}
}
