// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#pragma once
#include "WindowBackend.h"
#include <X11/Xlib.h>
#include <mutex>

namespace vpnw
{
	Display* XDisplay();
	std::mutex& XMutex();
	Window XCreateBorderless(int x, int y, int width, int height);
	void XDestroyBorderless(Window window);
	bool XConfigure(Window window, int x, int y, int width, int height, bool visible, bool topmost);
	bool XOrder(Window window, int order);
	bool XSetPlacementMode(Window window, bool enabled);
	bool XDiagnostics(Window window, VPNW_Diagnostics& output);
}
