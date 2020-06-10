using Unity.Entities;

namespace VisualPinball.Unity.VPT.Plunger
{
	public struct PlungerMovementData : IComponentData
	{
		/// <summary>
		/// Current rod speed, in table distance units per second(?)
		/// </summary>
		public float Speed;
		public bool RetractMotion;

		/// <summary>
		/// Forward travel limit.  When we're about to collide with a ball,
		/// we'll temporarily set this so the collision location.  We set
		/// this in HitTest(), and use it (and reset it) in the next call
		/// to UpdateDisplacements().  This is expressed in absolute
		/// position coordinates, so the default value (which allows full
		/// forward travel) is m_frameEnd.
		///
		/// The purpose of this limit is to fix buggy behavior that can
		/// happen when the ball speed after a collision is slower than the
		/// plunger speed before the collision.  (In past versions, this
		/// scenario wasn't really possible, because the plunger code just
		/// gave the ball the same velocity as the plunger at the time of
		/// collision.  But this was a bit limiting; the physical process
		/// we're modeling is really a transfer of momentum, not velocity.
		/// With the addition of the Momentum Transfer property and the new
		/// accounting for the relative mass of the ball, the ball can now
		/// come out of the collision with a slower speed than the plunger
		/// had going in.)
		///
		/// The bug that this can trigger is that the fast-moving plunger
		/// can overtake and shoot past the slow-moving ball.  Left to their
		/// own devices, the two objects would just keep moving at their
		/// programmed velocities.  If the plunger velocity is faster than
		/// the ball after the collision, the plunger can shoot past the
		/// ball as though it's not there.  The best way I've found to fix
		/// this is to add this extra limit on the travel distance.  This
		/// lets us figure where the ball is at the point of collision and
		/// explicitly prevent the plunger from going past that until the
		/// next displacement update, when the ball will have been moved
		/// as well.
		/// </summary>
		public float TravelLimit;

		/// <summary>
		/// Current rod position, in table distance units.  This represents
		/// the location of the tip of the plunger.
		/// </summary>
		public float Position;

		/// <summary>
		/// Fire event bounce position.  When we reach this position,
		/// we'll reverse course, simulating the bounce off the barrel
		/// spring (or, if already in the bounce, the next reversal).
		/// </summary>
		public float FireBounce;

		/// <summary>
		/// Reverse impulse.  This models the little bit of force applied
		/// to the plunger when a moving ball hits it.  Most of our
		/// collisions involve a moving plunger hitting a stationary ball,
		/// so we transfer momentum from the plunger to the ball.  But
		/// this works both ways; if a stationary plunger is hit by a
		/// moving ball, it gets a little bump.  This isn't a huge effect
		/// but it's a nice bit of added realism.
		/// </summary>
		public float ReverseImpulse;
	}
}
