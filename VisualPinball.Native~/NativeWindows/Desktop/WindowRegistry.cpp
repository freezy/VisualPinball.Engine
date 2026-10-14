// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#include "WindowBackend.h"
#include <algorithm>
#include <atomic>
#include <cstring>
#include <exception>
#include <limits>
#include <mutex>
#include <unordered_map>
#include <vector>

namespace
{
	std::mutex registryMutex, errorMutex;
	std::unordered_map<int, std::shared_ptr<vpnw::WindowBackend>> windows;
	std::vector<std::shared_ptr<vpnw::WindowBackend>> retired;
	std::string lastError;
	int nextId = 1;
	IUnityInterfaces* unity = nullptr;
	IUnityGraphics* graphics = nullptr;
	std::atomic<int> renderer{ kUnityGfxRendererNull };

	std::shared_ptr<vpnw::WindowBackend> Find(int id)
	{
		std::lock_guard<std::mutex> lock(registryMutex);
		auto found = windows.find(id);
		return found == windows.end() ? nullptr : found->second;
	}
	void UNITY_INTERFACE_API OnDevice(UnityGfxDeviceEventType event)
	{
		try
		{
			if (event == kUnityGfxDeviceEventInitialize)
			{
				renderer = graphics->GetRenderer();
				vpnw::InitializeGraphics(unity, static_cast<UnityGfxRenderer>(renderer.load()));
			}
			else if (event == kUnityGfxDeviceEventShutdown)
			{
				VPNW_DestroyAll();
				vpnw::ShutdownGraphics();
				std::lock_guard<std::mutex> lock(registryMutex);
				retired.clear(); renderer = kUnityGfxRendererNull;
			}
		}
		catch (const std::exception& error) { vpnw::SetError(error.what()); }
	}
	void UNITY_INTERFACE_API OnRender(int event)
	{
		try
		{
			std::vector<std::shared_ptr<vpnw::WindowBackend>> active, pending;
			{
				std::lock_guard<std::mutex> lock(registryMutex);
				for (auto& pair : windows) active.push_back(pair.second);
				pending = retired;
			}
			for (auto& window : active)
				if (event == vpnw::DrawEvent) window->Draw(); else if (event == vpnw::PresentEvent) window->Present();
			if (event == vpnw::PresentEvent) for (auto& window : pending)
				if (window->Collect())
				{
					std::lock_guard<std::mutex> lock(registryMutex);
					retired.erase(std::remove(retired.begin(), retired.end(), window), retired.end());
				}
		}
		catch (const std::exception& error) { vpnw::SetError(error.what()); }
	}
	template<class Function> int Attempt(Function function)
	{
		try { return function() ? 1 : 0; }
		catch (const std::exception& error) { vpnw::SetError(error.what()); return 0; }
	}
}

int vpnw::DrawEvent = 0;
int vpnw::PresentEvent = 1;

void vpnw::SetError(const std::string& message)
{
	std::lock_guard<std::mutex> lock(errorMutex); lastError = message;
}
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* interfaces)
{
	unity = interfaces; graphics = unity->Get<IUnityGraphics>();
	if (!graphics) { vpnw::SetError("Unity graphics interface is unavailable."); return; }
	vpnw::DrawEvent = graphics->ReserveEventIDRange(2);
	vpnw::PresentEvent = vpnw::DrawEvent + 1;
	vpnw::PreloadGraphics(unity);
	graphics->RegisterDeviceEventCallback(OnDevice); OnDevice(kUnityGfxDeviceEventInitialize);
}
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
	OnDevice(kUnityGfxDeviceEventShutdown);
	if (graphics) graphics->UnregisterDeviceEventCallback(OnDevice);
	if (unity) vpnw::UnloadGraphics(unity);
	unity = nullptr; graphics = nullptr;
}

int VPNW_CreateWindow(int x, int y, int width, int height)
{
	int id = 0;
	Attempt([&] {
		if (width <= 0 || height <= 0 || width > 32768 || height > 32768) return false;
		auto window = vpnw::CreateWindowBackend(x, y, width, height);
		if (!window) return false;
		std::lock_guard<std::mutex> lock(registryMutex);
		if (nextId == std::numeric_limits<int>::max()) return false;
		id = nextId++; windows.emplace(id, window); return true;
	});
	return id;
}
int VPNW_ConfigureWindow(int id, int x, int y, int width, int height, int visible, int topmost)
{
	return Attempt([&] { auto window = Find(id); return window && width > 0 && height > 0 && width <= 32768 && height <= 32768 && window->Configure(x, y, width, height, visible != 0, topmost != 0); });
}
int VPNW_SetZOrder(int id, int order)
{
	return Attempt([&] { auto window = Find(id); return window && order >= 0 && order <= 3 && window->SetOrder(order); });
}
int VPNW_SetPlacementMode(int id, int enabled)
{
	return Attempt([&] { auto window = Find(id); return window && window->SetPlacementMode(enabled != 0); });
}
int VPNW_SetTexture(int id, void* texture, int flipY)
{
	return Attempt([&] { auto window = Find(id); return window && window->SetTexture(texture, flipY != 0); });
}
int VPNW_DestroyWindow(int id)
{
	return Attempt([&] {
		std::shared_ptr<vpnw::WindowBackend> window;
		{
			std::lock_guard<std::mutex> lock(registryMutex);
			auto found = windows.find(id); if (found == windows.end()) return true;
			window = found->second; windows.erase(found); retired.push_back(window);
		}
		window->Retire(); return true;
	});
}
void VPNW_DestroyAll()
{
	std::vector<int> ids;
	{ std::lock_guard<std::mutex> lock(registryMutex); for (auto& pair : windows) ids.push_back(pair.first); }
	for (int id : ids) VPNW_DestroyWindow(id);
}
void VPNW_Pump() { vpnw::PumpPlatform(); }
void* VPNW_GetRenderEventFunc() { return reinterpret_cast<void*>(OnRender); }
int VPNW_GetGraphicsRenderer() { return renderer.load(); }
int VPNW_GetDrawEventId() { return vpnw::DrawEvent; }
int VPNW_GetPresentEventId() { return vpnw::PresentEvent; }
int VPNW_GetPendingResourceCount() { std::lock_guard<std::mutex> lock(registryMutex); return static_cast<int>(retired.size()); }
int VPNW_GetDiagnostics(int id, VPNW_Diagnostics* output)
{
	return Attempt([&] { auto window = Find(id); if (!window || !output || output->structBytes != sizeof(VPNW_Diagnostics)) return false; *output = window->Diagnostics(); return true; });
}
int VPNW_GetLastError(char* output, int capacity)
{
	std::lock_guard<std::mutex> lock(errorMutex);
	if (output && capacity > 0) { int count = std::min(capacity - 1, static_cast<int>(lastError.size())); std::memcpy(output, lastError.data(), count); output[count] = 0; }
	return static_cast<int>(lastError.size()) + 1;
}
std::uint32_t VPNW_GetCapabilities() { return vpnw::PlatformCapabilities(); }
