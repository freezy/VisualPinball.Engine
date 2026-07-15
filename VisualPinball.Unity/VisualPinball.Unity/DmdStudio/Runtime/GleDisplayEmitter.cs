// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Adapts a <see cref="DmdCuePlayer"/> to the display events exposed by a gamelogic engine.
	/// </summary>
	/// <remarks>
	/// The emitter deliberately holds no display state. Announcement timing, frame reuse, and
	/// disposal are owned by <see cref="DmdCuePlayer"/>; this type only forwards each sink call.
	/// </remarks>
	public sealed class GleDisplayEmitter : IDmdFrameSink
	{
		private readonly Action<RequestedDisplays> _raiseDisplaysRequested;
		private readonly Action<DisplayFrameData> _raiseDisplayUpdateFrame;
		private readonly Action<string> _raiseDisplayClear;

		public GleDisplayEmitter(
			Action<RequestedDisplays> raiseDisplaysRequested,
			Action<DisplayFrameData> raiseDisplayUpdateFrame,
			Action<string> raiseDisplayClear)
		{
			_raiseDisplaysRequested = raiseDisplaysRequested ??
				throw new ArgumentNullException(nameof(raiseDisplaysRequested));
			_raiseDisplayUpdateFrame = raiseDisplayUpdateFrame ??
				throw new ArgumentNullException(nameof(raiseDisplayUpdateFrame));
			_raiseDisplayClear = raiseDisplayClear ??
				throw new ArgumentNullException(nameof(raiseDisplayClear));
		}

		public void RequestDisplays(RequestedDisplays displays)
		{
			_raiseDisplaysRequested(displays);
		}

		public void UpdateFrame(DisplayFrameData frame)
		{
			_raiseDisplayUpdateFrame(frame);
		}

		public void Clear(string displayId)
		{
			_raiseDisplayClear(displayId);
		}
	}
}
