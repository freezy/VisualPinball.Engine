// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#pragma once
#include <algorithm>
#include <cmath>
#include <limits>
#include <vector>

namespace vpnw
{
	struct DesktopRect { double x, y, width, height; };
	struct Monitor { DesktopRect logical; double pixelWidth, pixelHeight; };
	// Anchor the primary display at (0,0), then preserve monitor adjacency in physical pixels.
	// Scaling every global origin by the primary DPI creates gaps/overlaps on mixed-DPI desktops.
	inline std::vector<DesktopRect> PhysicalLayout(const std::vector<Monitor>& monitors)
	{
		std::vector<DesktopRect> result(monitors.size()); if (monitors.empty()) return result;
		std::vector<bool> placed(monitors.size(), false); placed[0] = true;
		result[0] = { 0, 0, monitors[0].pixelWidth, monitors[0].pixelHeight };
		for (std::size_t count = 1; count < monitors.size(); count++)
		{
			std::size_t target = 0, anchor = 0; double nearest = std::numeric_limits<double>::infinity(), shared = -1;
			for (std::size_t i = 0; i < monitors.size(); i++) if (!placed[i])
				for (std::size_t j = 0; j < monitors.size(); j++) if (placed[j])
				{
					auto a = monitors[j].logical, b = monitors[i].logical;
					double dx = std::max({ a.x - b.x - b.width, b.x - a.x - a.width, 0.0 });
					double dy = std::max({ a.y - b.y - b.height, b.y - a.y - a.height, 0.0 });
					double distance = dx * dx + dy * dy;
					double overlap = std::max(0.0, std::min(a.x + a.width, b.x + b.width) - std::max(a.x, b.x)) +
						std::max(0.0, std::min(a.y + a.height, b.y + b.height) - std::max(a.y, b.y));
					if (distance < nearest || (distance == nearest && overlap > shared)) { nearest = distance; shared = overlap; target = i; anchor = j; }
				}
			auto parent = monitors[anchor].logical, child = monitors[target].logical, pixels = result[anchor];
			double sx = monitors[target].pixelWidth / child.width, sy = monitors[target].pixelHeight / child.height;
			DesktopRect rectangle{ pixels.x + (child.x - parent.x) * sx, pixels.y + (child.y - parent.y) * sy, monitors[target].pixelWidth, monitors[target].pixelHeight };
			if (child.x >= parent.x + parent.width) rectangle.x = pixels.x + pixels.width + (child.x - parent.x - parent.width) * sx;
			else if (child.x + child.width <= parent.x) rectangle.x = pixels.x - (parent.x - child.x - child.width) * sx - rectangle.width;
			else if (child.y >= parent.y + parent.height) rectangle.y = pixels.y + pixels.height + (child.y - parent.y - parent.height) * sy;
			else if (child.y + child.height <= parent.y) rectangle.y = pixels.y - (parent.y - child.y - child.height) * sy - rectangle.height;
			result[target] = rectangle; placed[target] = true;
		}
		return result;
	}
}
