using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public struct PlungerCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private LineCollider _lineSegBase;
		private LineZCollider _jointBase0;
		private LineZCollider _jointBase1;

		public ColliderType Type => ColliderType.Plunger;

		public static void Create(BlobBuilder builder, PlungerHit src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<PlungerCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		private void Init(PlungerHit src)
		{
			_header.Type = ColliderType.Plunger;
			_header.ItemType = src.ObjType;
			_header.Entity = new Entity {Index = src.ItemIndex, Version = src.ItemVersion};
			_header.Id = src.Id;
			_header.Material = new PhysicsMaterialData {
				Elasticity = src.Elasticity,
				ElasticityFalloff = src.ElasticityFalloff,
				Friction = src.Friction,
				Scatter = src.Scatter,
			};

			_lineSegBase = LineCollider.Create(src.LineSegBase);
			_jointBase0 = LineZCollider.Create(src.JointBase[0]);
			_jointBase1 = LineZCollider.Create(src.JointBase[1]);
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			ref PlungerMovementData movementData, in PlungerColliderData colliderData, in PlungerStaticData staticData, in BallData ball, float dTime)
		{
			var hitTime = dTime; //start time
			var isHit = false;

			// If we got here, then the ball is close enough to the plunger
			// to where we should animate the button's light.

			// todo Save the time so we can tell the button when to turn on/off.
			//g_pplayer->m_LastPlungerHit = g_pplayer->m_time_msec;

			// We are close enable the plunger light.
			var newCollEvent = new CollisionEventData();

			// Check for hits on the non-moving parts, like the side of back
			// of the plunger.  These are just like hitting a wall.
			// Check all and find the nearest collision.
			var newTime = _lineSegBase.HitTest(ref newCollEvent, ref insideOfs, in ball, dTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime);

			newTime = LineCollider.HitTest(ref newCollEvent, ref insideOfs, in colliderData.LineSegSide0, in ball, hitTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime);

			newTime = _jointBase0.HitTest(ref newCollEvent, in ball, hitTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime);

			newTime = LineCollider.HitTest(ref newCollEvent, ref insideOfs, in colliderData.LineSegSide1, in ball, hitTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime);

			newTime = _jointBase1.HitTest(ref newCollEvent, in ball, hitTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime);

			// Now check for hits on the business end, which might be moving.
			//
			// Our line segments are static, but they're meant to model a moving
			// object (the tip of the plunger).  We need to include the motion of
			// the tip to know if there's going to be a collision within the
			// interval we're covering, since it's not going to stay in the same
			// place throughout the interval.  Use a little physics trick: do the
			// calculation in an inertial frame where the tip is stationary.  To
			// do this, just adjust the ball speed to what it looks like in the
			// tip's rest frame.
			var ballTmp = ball;
			ballTmp.Velocity.y -= movementData.Speed;

			// Figure the impulse from hitting the moving end.
			// Calculate this as the product of the plunger speed and the
			// momentum transfer factor, which essentially models the plunger's
			// mass in abstract units.  In practical terms, this lets table
			// authors fine-tune the plunger's strength in terms of the amount
			// of energy it transfers when striking a ball.  Note that table
			// authors can also adjust the strength via the release speed,
			// but that's also used for the visual animation, so it's not
			// always possible to get the right combination of visuals and
			// physics purely by adjusting the speed.  The momentum transfer
			// factor provides a way to tweak the physics without affecting
			// the visuals.
			//
			// Further adjust the transfered momentum by the ball's mass
			// (which is likewise in abstract units).  Divide by the ball's
			// mass, since a heavier ball will have less velocity transfered
			// for a given amount of momentum (p=mv -> v=p/m).
			//
			// Note that both the plunger momentum transfer factor and the
			// ball's mass are expressed in relative units, where 1.0f is
			// the baseline and default.  Older tables that were designed
			// before these properties existed won't be affected since we'll
			// multiply the legacy calculation by 1.0/1.0 == 1.0.  (Set an
			// arbitrary lower bound to prevent division by zero and/or crazy
			// physics.)
			var ballMass = ball.Mass > 0.05f ? ball.Mass : 0.05f;
			var xferRatio = staticData.MomentumXfer / ballMass;
			var deltaY = movementData.Speed * xferRatio;

			// check the moving bits
			newTime = LineCollider.HitTest(ref newCollEvent, ref insideOfs, in colliderData.LineSegEnd, in ballTmp, hitTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime, deltaY);

			newTime = LineZCollider.HitTest(ref newCollEvent, in colliderData.JointEnd0, in ballTmp, hitTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime, deltaY);

			newTime = LineZCollider.HitTest(ref newCollEvent, in colliderData.JointEnd1, in ballTmp, hitTime);
			UpdateCollision(ref collEvent, ref newCollEvent, ref isHit, ref hitTime, in newTime, deltaY);

			// check only if the plunger is not in a controlled retract motion
			// and check for a hit
			if (isHit && !movementData.RetractMotion) {
				// We hit the ball.  Set a travel limit to freeze the plunger at
				// its current position for the next displacement update.  This
				// is necessary in case we have a relatively heavy ball with a
				// relatively light plunger, in which case the ball won't speed
				// up to the plunger's current speed.  Freezing the plunger here
				// prevents the plunger from overtaking the ball.  This serves
				// two purposes, one physically meaningful and the other a bit of
				// a hack for the physics loop.  The physical situation is that we
				// have a slow-moving ball blocking a fast-moving plunger; this
				// momentary travel limit effectively models the blockage.  The
				// hack is that the physics loop can't handle a situation where
				// a moving object is in continuous contact with the ball.  The
				// physics loop is written so that time only advances as far as
				// the next collision.  This means that the loop will get stuck
				// if two objects remain in continuous contact, because the time
				// to the next collision will be exactly 0.0 as long as the contact
				// continues.  We *have* to break the contact for time to progress
				// in the loop.  This has never been a problem for other objects
				// because other collisions always impart enough momentum to send
				// the colliding objects on their separate ways.  With a low
				// momentum transfer ratio in the plunger, though, we can find
				// ourselves pushing the ball along, with the spring keeping the
				// plunger pressed against the ball the whole way.  The plunger
				// freeze here deals with this by breaking contact for just long
				// enough to let the ball move a little bit, so that there's a
				// non-zero time to the next collision with the plunger.  We'll
				// then catch up again and push it along a little further.
				if (movementData.TravelLimit < movementData.Position) {
					movementData.TravelLimit = movementData.Position;
				}

				// If the distance is negative, it means the objects are
				// overlapping.  Make certain that we give the ball enough
				// of an impulse to get it not to overlap.
				if (collEvent.HitDistance <= 0.0f
				    && collEvent.HitVelocity.y == deltaY
				    && math.abs(deltaY) < math.abs(collEvent.HitDistance)) {
					collEvent.HitVelocity.y = -math.abs(collEvent.HitDistance);
				}

				// return the collision time delta
				return hitTime;
			}

			// no collision
			return -1.0f;
		}

		private static void UpdateCollision(ref CollisionEventData collEvent,
			ref CollisionEventData newCollEvent, ref bool isHit, ref float hitTime, in float newTime, in float velY = 0f)
		{
			if (newTime >= 0.0f && newTime <= hitTime) {
				isHit = true;
				hitTime = newTime;
				collEvent = newCollEvent;
				collEvent.HitVelocity.x = 0.0f;
				collEvent.HitVelocity.y = velY;
			}
		}

		#endregion

		#region Collision

		public static void Collide(ref BallData ball, ref CollisionEventData collEvent,
			ref PlungerMovementData movementData, in PlungerStaticData staticData, ref Random random)
		{
			var dot = (ball.Velocity.x - collEvent.HitVelocity.x) * collEvent.HitNormal.x
			          + (ball.Velocity.y - collEvent.HitVelocity.y) * collEvent.HitNormal.y;

			// nearly receding ... make sure of conditions
			if (dot >= -PhysicsConstants.LowNormVel) {

				// otherwise if clearly approaching .. process the collision
				if (dot > PhysicsConstants.LowNormVel) {
					// is this velocity clearly receding (i.e must > a minimum)
					return;
				}

				if (collEvent.HitDistance < -PhysicsConstants.Embedded) {
					// has ball become embedded???, give it a kick
					dot = -PhysicsConstants.EmbedShot;

				} else {
					return;
				}
			}
			//g_pplayer->m_pactiveballBC = pball; // todo Ball control most recently collided with plunger

			// correct displacements, mostly from low velocity blidness, an alternative to true acceleration processing
			var hDist = -PhysicsConstants.DispGain * collEvent.HitDistance; // distance found in hit detection
			if (hDist > 1.0e-4f) {
				// magnitude of jump
				if (hDist > PhysicsConstants.DispLimit) {
					// crossing ramps, delta noise
					hDist = PhysicsConstants.DispLimit;
				}

				// push along norm, back to free area (use the norm, but is not correct)
				ball.Position += hDist * collEvent.HitNormal;
			}

			// figure the basic impulse
			var impulse = dot * -1.45f / (1.0f + 1.0f / Plunger.PlungerMass);

			// We hit the ball, so attenuate any plunger bounce we have queued up
			// for a Fire event.  Real plungers bounce quite a bit when fired without
			// hitting anything, but bounce much less when they hit something, since
			// most of the momentum gets transferred out of the plunger and to the ball.
			movementData.FireBounce *= 0.6f;

			// Check for a downward collision with the tip.  This is the moving
			// part of the plunger, so it has some special handling.
			if (collEvent.HitVelocity.y != 0.0f) {
				// The tip hit the ball (or vice versa).
				//
				// Figure the reverse impulse to the plunger.  If the ball was moving
				// and the plunger wasn't, a little of the ball's momentum should
				// transfer to the plunger.  (Ideally this would just fall out of the
				// momentum calculations organically, the way it works in real life,
				// but our physics are pretty fake here.  So we add a bit to the
				// fakeness here to make it at least look a little more realistic.)
				//
				// Figure the reverse impulse as the dot product times the ball's
				// y velocity, multiplied by the ratio between the ball's collision
				// mass and the plunger's nominal mass.  In practice this is *almost*
				// satisfyingly realistic, but the bump seems a little too big.  So
				// apply a fudge factor to make it look more real.  The fudge factor
				// isn't entirely unreasonable physically - you could look at it as
				// accounting for the spring tension and friction.
				const float reverseImpulseFudgeFactor = .22f;
				movementData.ReverseImpulse = ball.Velocity.y * impulse
					* (ball.Mass / Plunger.PlungerMass)
					* reverseImpulseFudgeFactor;
			}

			// update the ball speed for the impulse
			ball.Velocity += impulse * collEvent.HitNormal;
			ball.Velocity *= 0.999f; //friction all axiz     //!! TODO: fix this

			var scatterVel =
				staticData.ScatterVelocity *
				0.2f; // todo g_pplayer->m_ptable->m_globalDifficulty;// apply dificulty weighting

			// skip if low velocity
			if (scatterVel > 0.0f && math.abs(ball.Velocity.y) > scatterVel) {
				var scatter = random.NextFloat(-1f, 1f);
				// shape quadratic distribution and scale
				scatter *= (1.0f - scatter * scatter) * 2.59808f * scatterVel;
				ball.Velocity.y += scatter;
			}
		}

		#endregion
	}
}
