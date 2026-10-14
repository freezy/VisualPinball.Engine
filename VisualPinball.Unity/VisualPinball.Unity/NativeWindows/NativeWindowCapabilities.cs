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
	/// <remarks>
	/// Read the current set from <see cref="NativeWindowSurface.Capabilities"/>. The bits describe the platform and the
	/// desktop session, not the graphics backend; <see cref="NativeWindowSurface.IsSupported"/> combines
	/// <see cref="Windows"/> with the backend check and is what a plugin should test before creating a window. The other
	/// bits let a plugin explain degraded behaviour, for example that a compositor ignores topmost requests.
	/// <example>
	/// <code>
	/// var capabilities = NativeWindowSurface.Capabilities;
	/// if ((capabilities &amp; NativeWindowCapabilities.Transparency) == 0) {
	///     log("Transparent areas of the backglass will not show the desktop.");
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	[Flags]
	public enum NativeWindowCapabilities : uint
	{
		/// <summary>No feature is available, or the native library could not be loaded.</summary>
		None = 0,
		/// <summary>Borderless GPU output windows can be created.</summary>
		Windows = 1,
		/// <summary>
		/// Placement mode (mouse dragging and edge resizing, see <see cref="NativeWindowSurface.SetPlacementMode"/>) is
		/// functional.
		/// </summary>
		Placement = 2,
		/// <summary>Topmost and non-topmost requests are honoured.</summary>
		Topmost = 4,
		/// <summary>Premultiplied alpha reaches the desktop compositor, so transparent pixels show the desktop behind.</summary>
		Transparency = 8,
	}
}
