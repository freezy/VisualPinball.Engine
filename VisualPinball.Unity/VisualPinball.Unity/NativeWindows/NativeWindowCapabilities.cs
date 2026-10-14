// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Window features reported by the native library, matching the native <c>VPNW_Capability</c> bits.
	/// </summary>
	[Flags]
	public enum NativeWindowCapabilities : uint
	{
		None = 0,
		/// <summary>Borderless GPU output windows can be created.</summary>
		Windows = 1,
		/// <summary>Placement mode (mouse dragging and edge resizing) is functional.</summary>
		Placement = 2,
		/// <summary>Topmost and non-topmost requests are honoured.</summary>
		Topmost = 4,
		/// <summary>Premultiplied alpha reaches the desktop compositor.</summary>
		Transparency = 8,
	}
}
