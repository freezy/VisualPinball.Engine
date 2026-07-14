// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

namespace VisualPinball.Unity
{
	public interface IDmdFrameSink
	{
		void RequestDisplays(RequestedDisplays displays);
		void UpdateFrame(DisplayFrameData frame);
		void Clear(string displayId);
	}
}
