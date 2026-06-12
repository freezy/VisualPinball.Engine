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
	/// <summary>
	/// Light-source payload (table/meta/lights.json), the authored source of truth for the
	/// table's lights. Lights are referenced by stable node id. Readers must check
	/// <see cref="Version"/> before interpreting the payload.
	/// </summary>
	public struct VpeLightsPayload
	{
		public int Version;
		public List<LightSourcePackable> Lights;
	}
}
