using Unity.Entities;

namespace VisualPinball.Unity
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

		/// <summary>
		/// Firing mode timer.  When this is non-zero, we're in a Fire
		/// event.
		///
		/// A Fire event is initiated in one of two ways:
		///
		///  1. The keyboard/script interface calls the Fire method
		///  2. The mechanical plunger moves forware rapidly
		///
		/// In either case, we calculate the firing speed based on how
		/// far the plunger is pulled back.  Since the plunger is basically
		/// a spring, pulling it back and letting it go converts the
		/// potential energy in the spring to kinetic energy in the
		/// plunger rod; the bottom line is that the final speed of
		/// the plunger is proportional to the spring displacement (how
		/// far back the plunger was pulled).  So we calculate the speed
		/// at the start of the release and allow the rod to move freely
		/// at this speed until it strikes the ball.
		///
		/// Durina a Fire event, the simulated plunger is completely
		/// disconnected from the mechanical plunger and moves under its
		/// own power.  In principle, if we have a mechanical plunger,
		/// we *should* be able to track the actual physical motion of
		/// the real plunger in real time and just have the software
		/// plunger do exactly the same thing.  But this doesn't work
		/// in practice because real plungers move much too quickly
		/// for our simulation and USB input to keep up with.  Our
		/// nominal simulation time base is 10ms, and the USB input
		/// updates at 10-30ms cycles.  (USB isn't synchronized with
		/// our physics cycle, either, so even if the USB updates were
		/// 10ms or faster, we still wouldn't get USB updates on every
		/// physics cycle just because the timing wouldn't always align.)
		/// In 20ms, a real physical plunger can shoot all the way
		/// forward, bounce part way back, and move forward again.  The
		/// result is aliasing.
		///
		/// To deal with this, we use heuristics to try to guess when the
		/// physical plunger has been released.  When we detect that it
		/// has, we simply disconnect the simulated plunger from the
		/// physical plunger and let the simulated version move freely
		/// under its own spring forces.  We ignore inputs from the analog
		/// plunger during this interval.  The real plunger can be expected
		/// to come to rest after a full release in about 200ms, so we
		/// only leave this mode in effect for a limited time, at which
		/// point we start tracking the real plunger position again.
		/// </summary>
		public int FireTimer;

		/// <summary>
		/// Firing speed.  When a Fire event is initiated, we calculate
		/// the speed and store it here.  UpdateVelocities() applies this
		/// as long as we're in fire mode.
		/// </summary>
		public float FireSpeed;

		/// <summary>
		/// Stroke Events are armed.  We use this for a hysteresis system
		/// for the End-of-stroke and Beginning-of-stroke events.  Any time
		/// plunger is away from the extremes of its range of motion, we
		/// set this flat to true.  This arms the events for the next time
		/// we approach one of the extremes.  If we're close to one of the
		/// ends, if this flag is true, we'll fire the corresponding event
		/// and clear this flag.  This lets us fire the events when we're
		/// *close* to one of the ends without having to actually reach the
		/// exact end, and ensures that we don't fire the event repeatedly
		/// if we stop at one of the ends for a while.
		/// </summary>
		public bool StrokeEventsArmed;
	}
}
