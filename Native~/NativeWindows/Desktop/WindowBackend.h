// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#pragma once
#include "VisualPinballNativeWindows.h"
#include "IUnityGraphics.h"
#include "IUnityInterface.h"
#include <memory>
#include <string>

namespace vpnw
{
	extern int DrawEvent;
	extern int PresentEvent;
	void SetError(const std::string& message);
	void PreloadGraphics(IUnityInterfaces* interfaces);
	void UnloadGraphics(IUnityInterfaces* interfaces);
	void InitializeGraphics(IUnityInterfaces* interfaces, UnityGfxRenderer renderer);
	void ShutdownGraphics();
	void PumpPlatform();
	// VPNW_Capability bits.
	std::uint32_t PlatformCapabilities();
	class WindowBackend
	{
	public:
		virtual ~WindowBackend() = default;
		virtual bool Configure(int x, int y, int width, int height, bool visible, bool topmost) = 0;
		virtual bool SetOrder(int order) = 0;
		virtual bool SetPlacementMode(bool enabled) = 0;
		virtual bool SetTexture(void* texture, bool flipY) = 0;
		virtual void Retire() = 0;
		virtual bool Collect() = 0;
		virtual void Draw() = 0;
		virtual void Present() = 0;
		virtual VPNW_Diagnostics Diagnostics() = 0;
	};
	std::shared_ptr<WindowBackend> CreateWindowBackend(int x, int y, int width, int height);
}
