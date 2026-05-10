// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public struct VpeLightsPayloadV1
	{
		public int Version;
		public List<LightSourcePackable> Lights;
	}
}
