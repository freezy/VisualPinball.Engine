// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#pragma once
#import <Cocoa/Cocoa.h>
#import <ApplicationServices/ApplicationServices.h>
#include <algorithm>
#include <cmath>
#include "DesktopLayout.h"

namespace vpnw
{
	// Desktop coordinates use top-left origins and physical pixels; Cocoa uses bottom-left points.
	inline NSRect PhysicalScreen(NSScreen* screen)
	{
		NSArray<NSScreen*>* screens = NSScreen.screens; std::vector<Monitor> monitors;
		for (NSScreen* entry in screens)
		{
			CGDirectDisplayID display = [entry.deviceDescription[@"NSScreenNumber"] unsignedIntValue]; CGRect bounds = CGDisplayBounds(display);
			monitors.push_back({ { bounds.origin.x, bounds.origin.y, bounds.size.width, bounds.size.height }, static_cast<double>(CGDisplayPixelsWide(display)), static_cast<double>(CGDisplayPixelsHigh(display)) });
		}
		auto layout = PhysicalLayout(monitors); NSUInteger index = [screens indexOfObject:screen];
		if (index == NSNotFound) return NSZeroRect;
		auto rectangle = layout[index]; return NSMakeRect(rectangle.x, rectangle.y, rectangle.width, rectangle.height);
	}
	inline NSScreen* ScreenForPixels(int x, int y, int width, int height)
	{
		NSScreen* selected = NSScreen.screens.firstObject; double largest = -1;
		NSRect requested = NSMakeRect(x, y, width, height);
		for (NSScreen* screen in NSScreen.screens)
		{
			NSRect intersection = NSIntersectionRect(requested, PhysicalScreen(screen));
			double area = intersection.size.width * intersection.size.height;
			if (area > largest) { largest = area; selected = screen; }
		}
		return selected;
	}
	inline NSRect CocoaFrame(int x, int y, int width, int height)
	{
		NSScreen* screen = ScreenForPixels(x, y, width, height);
		if (!screen) return NSMakeRect(x, y, width, height);
		NSRect physical = PhysicalScreen(screen); NSRect logical = screen.frame;
		double sx = physical.size.width / logical.size.width, sy = physical.size.height / logical.size.height;
		return NSMakeRect(logical.origin.x + (x - physical.origin.x) / sx,
			NSMaxY(logical) - (y - physical.origin.y + height) / sy, width / sx, height / sy);
	}
	inline NSRect PixelFrame(NSWindow* window)
	{
		NSScreen* screen = window.screen ?: NSScreen.screens.firstObject;
		if (!screen) return window.frame;
		NSRect physical = PhysicalScreen(screen), frame = window.frame, logical = screen.frame;
		double sx = physical.size.width / logical.size.width, sy = physical.size.height / logical.size.height;
		return NSMakeRect(physical.origin.x + (frame.origin.x - logical.origin.x) * sx,
			physical.origin.y + (NSMaxY(logical) - NSMaxY(frame)) * sy, frame.size.width * sx, frame.size.height * sy);
	}
	inline void MainThread(dispatch_block_t action)
	{
		if (NSThread.isMainThread) action(); else dispatch_sync(dispatch_get_main_queue(), action);
	}
}
