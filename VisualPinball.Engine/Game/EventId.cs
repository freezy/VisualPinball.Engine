namespace VisualPinball.Engine.Game
{
	public enum EventId
	{
		// Table
		GameEventsKeyDown = 1000, // DISPID_GameEvents_KeyDown
		GameEventsKeyUp = 1001, // DISPID_GameEvents_KeyUp
		GameEventsInit = 1002, // DISPID_GameEvents_Init
		GameEventsMusicDone = 1003, // DISPID_GameEvents_MusicDone
		GameEventsExit = 1004, // DISPID_GameEvents_Exit
		GameEventsPaused = 1005, // DISPID_GameEvents_Paused
		GameEventsUnPaused = 1006, // DISPID_GameEvents_UnPaused

		// Surface
		SurfaceEventsSlingshot = 1101, // DISPID_SurfaceEvents_Slingshot

		// Flipper
		FlipperEventsCollide = 1200, // DISPID_FlipperEvents_Collide

		// Timer
		TimerEventsTimer = 1300, // DISPID_TimerEvents_Timer

		// Spinner
		SpinnerEventsSpin = 1301, // DISPID_SpinnerEvents_Spin

		// HitTarget
		TargetEventsDropped = 1302, // DISPID_TargetEvents_Dropped
		TargetEventsRaised = 1303,  // DISPID_TargetEvents_Raised

		// Light Sequencer
		LightSeqEventsPlayDone = 1320,

		// Plunger
		// PluFrames = 464,
		// Width = 465,
		// ZAdjust = 466,
		//
		// RodDiam = 467,
		// RingDiam = 468,
		// RingThickness = 469,
		// SpringDiam = 470,
		// TipShape = 471,
		// SpringGauge = 472,
		// SpringLoops = 473,
		// RingGap = 474,
		// SpringEndLoops = 475,

		// Generic
		HitEventsHit = 1400, // DISPID_HitEvents_Hit
		HitEventsUnhit = 1401, // DISPID_HitEvents_Unhit
		LimitEventsEos = 1402, // DISPID_LimitEvents_EOS
		LimitEventsBos = 1403, // DISPID_LimitEvents_BOS
	}
}
