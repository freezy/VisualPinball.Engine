// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

namespace VisualPinball.Unity
{
	internal enum CuePhase
	{
		Entering,
		Running,
		Exiting,
	}

	internal sealed class CueInstance
	{
		public readonly CueHandle Handle;
		public readonly DmdCueAsset Cue;
		public readonly DmdParams Params;
		public CueInstanceState State = new CueInstanceState();
		public readonly CueDiagnostics Diagnostics = new CueDiagnostics();
		public readonly long Sequence;
		public int ElapsedFrames;
		public CuePhase Phase;
		// Set when another cue owns the transition across this cue's exit window. It remains
		// set across Resume preemption because replaying the full exit would exceed the cue's
		// already-advanced DurationFrames lifetime.
		public bool ExitSuppressed;
		public int PublishedDiagnostics;

		public CueInstance(CueHandle handle, DmdCueAsset cue, DmdParams parameters, long sequence)
		{
			Handle = handle;
			Cue = cue;
			Params = parameters;
			Sequence = sequence;
			Phase = CuePhase.Running;
			ExitSuppressed = false;
		}

		public void Restart()
		{
			ElapsedFrames = 0;
			Phase = CuePhase.Running;
			ExitSuppressed = false;
			State = new CueInstanceState();
			Diagnostics.Clear();
			PublishedDiagnostics = 0;
		}
	}
}
