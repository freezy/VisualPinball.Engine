// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#include "X11Windows.h"
#include <X11/Xatom.h>
#include <X11/Xresource.h>
#include <X11/Xutil.h>
#include <X11/extensions/Xrender.h>
#include <cmath>
#include <cstdlib>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <unordered_set>

namespace
{
	std::mutex displayMutex;
	Display* display = nullptr;
	std::mutex initializationMutex;
	std::unordered_map<Window, Colormap> colormaps;
	std::unordered_set<Window> placementWindows;
	XErrorHandler previousError = nullptr;
	int dpi = 0;
	int Error(Display* connection, XErrorEvent* event)
	{
		if (connection != display) return previousError ? previousError(connection, event) : 0;
		char message[256]{}; XGetErrorText(connection, event->error_code, message, sizeof(message));
		vpnw::SetError(std::string("X11: ") + message); return 0;
	}
	Atom Property(const char* name) { return XInternAtom(vpnw::XDisplay(), name, False); }
	void State(Window window, int action, Atom first, Atom second = None)
	{
		XEvent event{}; event.xclient.type = ClientMessage; event.xclient.window = window;
		event.xclient.message_type = Property("_NET_WM_STATE"); event.xclient.format = 32;
		event.xclient.data.l[0] = action; event.xclient.data.l[1] = static_cast<long>(first);
		event.xclient.data.l[2] = static_cast<long>(second); event.xclient.data.l[3] = 1;
		XSendEvent(vpnw::XDisplay(), DefaultRootWindow(vpnw::XDisplay()), False, SubstructureRedirectMask | SubstructureNotifyMask, &event);
	}
	// Desktop scale as published by the session (Xft.dpi in RESOURCE_MANAGER); X11 itself has no per-window DPI.
	int DesktopDpi(Display* connection)
	{
		if (dpi) return dpi;
		dpi = 96;
		if (const char* resources = XResourceManagerString(connection))
		{
			XrmInitialize();
			if (XrmDatabase database = XrmGetStringDatabase(resources))
			{
				char* type = nullptr; XrmValue value{};
				if (XrmGetResource(database, "Xft.dpi", "Xft.Dpi", &type, &value) && value.addr)
				{
					const double parsed = std::atof(value.addr);
					if (parsed > 0 && parsed <= 960) dpi = static_cast<int>(std::lround(parsed));
				}
				XrmDestroyDatabase(database);
			}
		}
		return dpi;
	}
}

Display* vpnw::XDisplay()
{
	std::lock_guard<std::mutex> lock(initializationMutex);
	if (!display)
	{
		XInitThreads(); display = XOpenDisplay(nullptr);
		if (!display) throw std::runtime_error("An X11/XWayland DISPLAY is required for native desktop windows. Wayland toplevel placement is compositor controlled.");
		previousError = XSetErrorHandler(Error);
	}
	return display;
}
std::mutex& vpnw::XMutex() { return displayMutex; }
Window vpnw::XCreateBorderless(int x, int y, int width, int height)
{
	std::lock_guard<std::mutex> lock(displayMutex); Display* connection = XDisplay();
	XVisualInfo query{}; query.screen = DefaultScreen(connection); query.depth = 32; query.c_class = TrueColor;
	int count = 0; XVisualInfo* visuals = XGetVisualInfo(connection, VisualScreenMask | VisualDepthMask | VisualClassMask, &query, &count);
	Visual* visual = nullptr;
	for (int i = 0; i < count; i++) { auto* format = XRenderFindVisualFormat(connection, visuals[i].visual); if (format && format->direct.alphaMask) { visual = visuals[i].visual; break; } }
	if (visuals) XFree(visuals);
	if (!visual) throw std::runtime_error("The X11 display has no 32-bit ARGB visual for transparent windows.");
	XSetWindowAttributes attributes{}; attributes.colormap = XCreateColormap(connection, DefaultRootWindow(connection), visual, AllocNone);
	attributes.border_pixel = 0; attributes.background_pixel = 0; attributes.event_mask = StructureNotifyMask | ExposureMask | ButtonPressMask;
	Window window = XCreateWindow(connection, DefaultRootWindow(connection), x, y, static_cast<unsigned int>(width), static_cast<unsigned int>(height), 0, 32, InputOutput, visual,
		CWColormap | CWBorderPixel | CWBackPixel | CWEventMask, &attributes);
	colormaps.emplace(window, attributes.colormap);
	unsigned long motif[] = { 2, 0, 0, 0, 0 };
	XChangeProperty(connection, window, Property("_MOTIF_WM_HINTS"), Property("_MOTIF_WM_HINTS"), 32, PropModeReplace, reinterpret_cast<unsigned char*>(motif), 5);
	XStoreName(connection, window, "Visual Pinball Window");
	XClassHint classHint{}; char className[] = "VisualPinball.NativeWindow"; classHint.res_name = className; classHint.res_class = className;
	XSetClassHint(connection, window, &classHint);
	XWMHints hints{}; hints.flags = InputHint; hints.input = False; XSetWMHints(connection, window, &hints);
	Atom states[] = { Property("_NET_WM_STATE_SKIP_TASKBAR"), Property("_NET_WM_STATE_SKIP_PAGER") };
	XChangeProperty(connection, window, Property("_NET_WM_STATE"), XA_ATOM, 32, PropModeReplace, reinterpret_cast<unsigned char*>(states), 2);
	XFlush(connection); return window;
}
void vpnw::XDestroyBorderless(Window window)
{
	std::lock_guard<std::mutex> lock(displayMutex);
	XDestroyWindow(XDisplay(), window);
	placementWindows.erase(window);
	auto found = colormaps.find(window);
	if (found != colormaps.end()) { XFreeColormap(XDisplay(), found->second); colormaps.erase(found); }
	XFlush(XDisplay());
}
bool vpnw::XConfigure(Window window, int x, int y, int width, int height, bool visible, bool topmost)
{
	std::lock_guard<std::mutex> lock(displayMutex);
	XMoveResizeWindow(XDisplay(), window, x, y, static_cast<unsigned int>(width), static_cast<unsigned int>(height));
	State(window, topmost ? 1 : 0, Property("_NET_WM_STATE_ABOVE"));
	if (visible) XMapWindow(XDisplay(), window); else XUnmapWindow(XDisplay(), window);
	XFlush(XDisplay()); return true;
}
bool vpnw::XOrder(Window window, int order)
{
	std::lock_guard<std::mutex> lock(displayMutex);
	if (order == 0) XLowerWindow(XDisplay(), window); else if (order == 1) XRaiseWindow(XDisplay(), window);
	else State(window, order == 2 ? 1 : 0, Property("_NET_WM_STATE_ABOVE"));
	XFlush(XDisplay()); return true;
}
bool vpnw::XSetPlacementMode(Window window, bool enabled)
{
	std::lock_guard<std::mutex> lock(displayMutex);
	if (enabled) placementWindows.insert(window); else placementWindows.erase(window);
	return true;
}
bool vpnw::XDiagnostics(Window window, VPNW_Diagnostics& output)
{
	std::lock_guard<std::mutex> lock(displayMutex); XWindowAttributes attributes{};
	if (!XGetWindowAttributes(XDisplay(), window, &attributes)) return false;
	Window child = None; XTranslateCoordinates(XDisplay(), window, DefaultRootWindow(XDisplay()), 0, 0, &output.left, &output.top, &child);
	output.windowWidth = attributes.width; output.windowHeight = attributes.height; output.visible = attributes.map_state == IsViewable ? 1 : 0;
	output.dpi = static_cast<std::uint32_t>(DesktopDpi(XDisplay())); return true;
}
void vpnw::PumpPlatform()
{
	std::lock_guard<std::mutex> lock(displayMutex);
	if (!display) return;
	while (XPending(display)) {
		XEvent event{}; XNextEvent(display, &event);
		if (event.type != ButtonPress || event.xbutton.button != Button1 || !placementWindows.count(event.xbutton.window)) continue;
		XWindowAttributes bounds{}; if (!XGetWindowAttributes(display, event.xbutton.window, &bounds)) continue;
		bool left = event.xbutton.x < 10, right = event.xbutton.x >= bounds.width - 10;
		bool top = event.xbutton.y < 10, bottom = event.xbutton.y >= bounds.height - 10;
		int direction = top ? (left ? 0 : right ? 2 : 1) : bottom ? (left ? 6 : right ? 4 : 5) : left ? 7 : right ? 3 : 8;
		XUngrabPointer(display, CurrentTime);
		XEvent request{}; request.xclient.type = ClientMessage; request.xclient.window = event.xbutton.window;
		request.xclient.message_type = Property("_NET_WM_MOVERESIZE"); request.xclient.format = 32;
		request.xclient.data.l[0] = event.xbutton.x_root; request.xclient.data.l[1] = event.xbutton.y_root;
		request.xclient.data.l[2] = direction; request.xclient.data.l[3] = Button1; request.xclient.data.l[4] = 1;
		XSendEvent(display, DefaultRootWindow(display), False, SubstructureRedirectMask | SubstructureNotifyMask, &request);
		XFlush(display);
	}
}
std::uint32_t vpnw::PlatformCapabilities()
{
	const char* setting = std::getenv("DISPLAY");
	return setting && *setting ? VPNW_CapabilityWindows | VPNW_CapabilityPlacement | VPNW_CapabilityTopmost | VPNW_CapabilityTransparency : 0u;
}
