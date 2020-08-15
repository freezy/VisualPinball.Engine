using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct PlungerVelocityData : IComponentData
	{
		/// <summary>
		/// Recent history of mechanical plunger readings.  We keep the
		/// last few distinct readings so that we can make a better guess
		/// at the true starting point of a release motion when we detect
		/// that the analog plunger is moving rapidly forward.  We
		/// usually detect a release motion by seeing a rapid forward
		/// position change between two consecutive USB samples.  However,
		/// the real plunger moves so quickly that the first of these
		/// two samples is usually already somewhat forward of the point
		/// where the release actually started.  The history lets us go
		/// back to the position where the plunger was hovering before
		/// being released.  In most cases, the user moves the plunger
		/// slowly enough for our USB samples to keep up and give us an
		/// accurate position reading; it's only on release that it starts
		/// moving too fast.
		/// </summary>
		public float Mech0;
		public float Mech1;
		public float Mech2;

		/// <summary>
		/// Auto Fire mode timer.  When we're acting as an Auto Plunger,
		/// we'll initiate a synthetic Fire event, which consists of a
		/// KeyDown(Return) message to the script, followed a short
		/// time later by a corresponding KeyUp(Return) message.  This
		/// lets the player use the natural pull-and-release gesture with
		/// a mechanical plunger to trigger the Launch Ball event on a
		/// table that has a pushbutton launcher instead of a regular
		/// plunger.  This timer handles the interval between the KeyDown
		/// and KeyUp events.
		/// </summary>
		public int AutoFireTimer;

		/// <summary>
		/// Pull force.  This models the force being applied by the player
		/// when pulling back the plunger via the keyboard interface.  When
		/// this is non-zero, we ignore the mechanical plunger position and
		/// instead move under this force.
		/// </summary>
		public float PullForce;

		public bool AddRetractMotion;
		public int RetractWaitLoop;
		public float InitialSpeed;

		public float MechStrength;
	}
}
