namespace VisualPinball.Engine.Game
{
	public enum EventId
	{
		// Surface
		SurfaceEventsSlingshot = 1101, // DISPID_SurfaceEvents_Slingshot

		// Flipper
		FlipperEventsCollide = 1200, // DISPID_FlipperEvents_Collide

		// Spinner
		SpinnerEventsSpin = 1301, // DISPID_SpinnerEvents_Spin

		// HitTarget
		TargetEventsDropped = 1302, // DISPID_TargetEvents_Dropped
		TargetEventsRaised = 1303,  // DISPID_TargetEvents_Raised

		// Generic
		HitEventsHit = 1400, // DISPID_HitEvents_Hit
		HitEventsUnhit = 1401, // DISPID_HitEvents_Unhit
		LimitEventsEos = 1402, // DISPID_LimitEvents_EOS
		LimitEventsBos = 1403, // DISPID_LimitEvents_BOS
	}
}
