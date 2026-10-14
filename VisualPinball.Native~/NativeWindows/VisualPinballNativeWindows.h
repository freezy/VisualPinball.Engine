// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

// Public C API of VisualPinballNativeWindows. Identical on Windows (D3D11/D3D12), macOS (Metal) and Linux (Vulkan/X11).
#pragma once
#include <cstdint>

#if defined(_WIN32)
#define VPNW_CALL __cdecl
#if defined(VPNW_BUILD)
#define VPNW_API extern "C" __declspec(dllexport)
#else
#define VPNW_API extern "C" __declspec(dllimport)
#endif
#else
#define VPNW_CALL
#define VPNW_API extern "C" __attribute__((visibility("default")))
#endif

// All coordinates and sizes are physical desktop pixels, including negative monitor origins.
// Handles are registry IDs, never HWNDs or pointers. IDs are never reused during a plugin load.
struct VPNW_Diagnostics
{
	std::uint32_t structBytes;
	std::int32_t graphicsRenderer;
	std::int32_t deviceReady;
	std::int32_t alive;
	std::int32_t visible;
	std::int32_t topmost;
	std::int32_t left;
	std::int32_t top;
	std::int32_t windowWidth;
	std::int32_t windowHeight;
	std::uint32_t dpi;
	std::uint32_t sourceWidth;
	std::uint32_t sourceHeight;
	std::uint32_t sourceFormat;
	std::uint64_t presentedFrames;
	std::uint64_t skippedFrames;
	// Windows: last presentation HRESULT. macOS/Linux: 0 on success, negative after a presentation failure.
	std::int32_t lastResult;
	std::int32_t premultipliedAlpha;
};
static_assert(sizeof(VPNW_Diagnostics) == 80, "Managed diagnostics ABI must remain stable.");

// Bits returned by VPNW_GetCapabilities.
enum VPNW_Capability : std::uint32_t
{
	VPNW_CapabilityWindows = 1, // Borderless GPU output windows can be created.
	VPNW_CapabilityPlacement = 2, // VPNW_SetPlacementMode is functional.
	VPNW_CapabilityTopmost = 4, // Topmost/non-topmost requests are honoured.
	VPNW_CapabilityTransparency = 8 // Premultiplied alpha reaches the desktop compositor.
};

// Creates an initially hidden borderless window. Returns 0 on failure (see VPNW_GetLastError).
VPNW_API int VPNW_CALL VPNW_CreateWindow(int x, int y, int width, int height);
// Identical repeated calls do not reposition or reshow the window.
VPNW_API int VPNW_CALL VPNW_ConfigureWindow(int id, int x, int y, int width, int height, int visible, int topmost);
// order: 0 back, 1 front, 2 topmost, 3 non-topmost. Geometry and activation remain unchanged.
VPNW_API int VPNW_CALL VPNW_SetZOrder(int id, int order);
// Settings-only interaction: drag the body and resize at the edges. Playback windows remain fixed.
VPNW_API int VPNW_CALL VPNW_SetPlacementMode(int id, int enabled);
// Texture is Unity's native texture pointer (ID3D11Texture2D, ID3D12Resource, MTLTexture or VkImage*),
// with premultiplied RGBA in the desired SDR encoding. nullptr detaches the source.
VPNW_API int VPNW_CALL VPNW_SetTexture(int id, void* texture, int flipY);
// Idempotent. GPU resources are retired and collected by later render events.
VPNW_API int VPNW_CALL VPNW_DestroyWindow(int id);
VPNW_API void VPNW_CALL VPNW_DestroyAll();
// Processes platform window events (X11 placement drags). No-op on Windows and macOS. Call from the main thread.
VPNW_API void VPNW_CALL VPNW_Pump();
VPNW_API void* VPNW_CALL VPNW_GetRenderEventFunc();
VPNW_API int VPNW_CALL VPNW_GetGraphicsRenderer();
// Issue the draw event and then the present event once per frame; both operate on all registered windows.
VPNW_API int VPNW_CALL VPNW_GetDrawEventId();
VPNW_API int VPNW_CALL VPNW_GetPresentEventId();
// Pending retired source/presentation objects. Issue later event pairs until this reaches zero.
VPNW_API int VPNW_CALL VPNW_GetPendingResourceCount();
// diagnostics->structBytes must be sizeof(VPNW_Diagnostics).
VPNW_API int VPNW_CALL VPNW_GetDiagnostics(int id, VPNW_Diagnostics* diagnostics);
// UTF-8, null terminated when capacity > 0. Return value is the required byte count, including the terminator.
VPNW_API int VPNW_CALL VPNW_GetLastError(char* destination, int capacity);
// VPNW_Capability bits for the current platform and session.
VPNW_API std::uint32_t VPNW_CALL VPNW_GetCapabilities();
