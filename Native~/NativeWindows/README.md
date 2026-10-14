# Native windows

`VisualPinballNativeWindows` presents a Unity texture in independent borderless desktop windows, entirely on the GPU. Player plugins such as PinUP and the B2S backglass use it to put a premultiplied RGBA RenderTexture on a second or third monitor, with transparent areas showing the desktop behind. Production code never reads texture pixels back to the CPU.

| Platform | Library | Unity graphics | Presentation |
| --- | --- | --- | --- |
| Windows x64 | `VisualPinballNativeWindows.dll` | Direct3D11, Direct3D12 (v7 interface) | DirectComposition, premultiplied-alpha flip-model swapchain |
| macOS (arm64 + x86_64) | `libVisualPinballNativeWindows.dylib` | Metal | Borderless `NSWindow` with a `CAMetalLayer` |
| Linux x64 | `libVisualPinballNativeWindows.so` | Vulkan | X11/XWayland window with a 32-bit ARGB visual and a Vulkan swapchain |

All platforms export the same C API (`VisualPinballNativeWindows.h`, prefix `VPNW_`), so the managed side is a single P/Invoke class: `NativeWindowSurface` in the `VisualPinball.Unity` assembly.

## Layout

| Path | Content |
| --- | --- |
| `VisualPinballNativeWindows.h` | Public C API shared by all platforms. |
| `Windows/VisualPinballNativeWindows.cpp` | Dedicated Win32 message thread, registry IDs, DirectComposition windows, GPU scaling and flip, Direct3D11 state isolation. |
| `Windows/D3D12CopyBridge.cpp` | Unity resource-state declarations, private GPU snapshots, same-queue D3D11On12 presentation and frame-fence retirement. |
| `Windows/NativeWindowTests.cpp` | Standalone real-device fixture with a minimal Unity graphics-interface host. |
| `Desktop/WindowRegistry.cpp` | Shared macOS/Linux registry, Unity plugin entry points and render events. |
| `Desktop/Mac/MetalWindows.mm` | Metal presentation; `MacCoordinates.h` and `DesktopLayout.h` map physical pixels to Cocoa points on mixed-DPI desktops. |
| `Desktop/Linux/VulkanWindows.cpp` | Vulkan swapchain presentation; `X11Windows.cpp` creates, places, orders and pumps the X11 windows. |
| `Desktop/DesktopTests.cpp` | Standalone fixture for macOS and Linux. |

## Design

Unity calls `UnityPluginLoad`, where the plugin reserves two event IDs: a draw event and a present event. Both operate on all registered windows, so a host submits one pair per frame after its GPU work, not one pair per window. Window handles are registry IDs that are never reused while the plugin is loaded, so queued render events can outlive a destroyed window safely.

On Windows the D3D11 path swaps the entire immediate-context state through `ID3D11DeviceContext1` and restores Unity's state afterwards. Typeless resources use typed views; typed sRGB resources get a cached compatible UNORM GPU copy, which avoids an illegal view and preserves the stored SDR values. The D3D12 path submits a snapshot copy through `IUnityGraphicsD3D12v7.ExecuteCommandList`, so Unity owns the source's barriers and ordering. The present event runs on Unity's submission thread after worker flushes and draws the snapshot into DirectComposition swapchain buffers through D3D11On12 on Unity's own queue. Retired snapshots and destroyed presentation resources stay alive until their frame fence completes; `PendingResourceCount` reports them, and the host keeps issuing event pairs until it is zero.

The HWNDs live on a plugin-owned message thread with per-monitor DPI awareness; Unity's process DPI mode is not changed. On macOS, window operations are dispatched to the main thread. On Linux, X11 events (placement drags) are processed by `VPNW_Pump`, which the host calls once per frame on the main thread. The Vulkan backend intercepts Unity's instance creation to add the Xlib surface extensions and keeps XWayland presentation when Unity itself runs on Wayland.

Present never waits for the display: Windows uses `DXGI_PRESENT_DO_NOT_WAIT`, Metal skips a frame when two are in flight, and Vulkan acquires with a zero timeout and prefers immediate mode. Skipped frames are counted in the diagnostics.

## C API

| Function | Purpose |
| --- | --- |
| `VPNW_CreateWindow(x, y, width, height)` | Creates a hidden borderless window; returns its ID or 0. |
| `VPNW_ConfigureWindow(id, x, y, width, height, visible, topmost)` | Sets rectangle, visibility and topmost state. |
| `VPNW_SetZOrder(id, order)` | 0 back, 1 front, 2 topmost, 3 non-topmost, without moving or activating. |
| `VPNW_SetPlacementMode(id, enabled)` | Lets the user drag the body and resize at the edges. |
| `VPNW_SetTexture(id, texture, flipY)` | Assigns Unity's native texture pointer; null detaches. |
| `VPNW_DestroyWindow(id)` / `VPNW_DestroyAll()` | Idempotent destruction; GPU resources retire through later events. |
| `VPNW_Pump()` | Processes platform window events (Linux); no-op on Windows and macOS. |
| `VPNW_GetRenderEventFunc()`, `VPNW_GetDrawEventId()`, `VPNW_GetPresentEventId()` | Unity render callback and its two event IDs. |
| `VPNW_GetPendingResourceCount()` | Retired GPU objects waiting for later events. |
| `VPNW_GetGraphicsRenderer()` | Unity's `UnityGfxRenderer` as seen by the plugin. |
| `VPNW_GetDiagnostics(id, diagnostics)` | Geometry, DPI, source, frame counters and last result (80-byte struct). |
| `VPNW_GetLastError(buffer, capacity)` | Last error as UTF-8; returns the required size. |
| `VPNW_GetCapabilities()` | Bits: 1 windows, 2 placement, 4 topmost, 8 transparency. |

Functions returning `int` return nonzero on success. Coordinates and sizes are signed physical desktop pixels; monitors left of or above the primary monitor have negative origins.

## Using it from a plugin

Create a `NativeWindowSurface`, give it the native pointer of the RenderTexture you render into, configure it, and register it with the shared `NativeWindowHost`. The host is a single hidden `DontDestroyOnLoad` object shared by all plugins; it issues one draw/present event pair per frame in `LateUpdate`, pumps platform events, and keeps running until deferred GPU releases have drained.

```csharp
if (!NativeWindowSurface.IsSupported) {
	return; // Missing library or unsupported graphics backend; never throws.
}
var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
texture.Create();
var surface = new NativeWindowSurface(x, y, width, height); // hidden
surface.SetTexture(texture.GetNativeTexturePtr());
surface.Configure(x, y, width, height, visible: true, topmost: false);
var lease = NativeWindowHost.Register(surface);

// Shutdown: unregister, destroy the window, then release the texture.
lease.Dispose();
surface.Dispose();
texture.Release();
```

Render premultiplied RGBA in the SDR encoding you want on screen. `Configure` caches its last arguments, so calling it every frame is cheap. Resubmit `GetNativeTexturePtr()` whenever the RenderTexture is recreated or resized, and dispose the surface before releasing its texture. `SendToBack`, `BringToFront` and `SetTopmost` change stacking without moving or activating the window. `SetPlacementMode(true)` lets the user move and resize the window while a settings screen is open; read the result from `Diagnostics`. `Diagnostics` crosses into the window system, so query it for troubleshooting, not every frame. `NativeWindowSurface.DestroyAll()` is available for host shutdown or domain-reload cleanup.

## Build and test

### Windows

Run `Windows/Build.ps1` from PowerShell with the Visual Studio C++ x64 toolchain and a Windows SDK installed. `-UnityEditor` accepts the installed Editor directory containing `Data/PluginAPI` (default: 6000.5.0f1). The script compiles with C++17, `/W4 /WX`, writes `VisualPinball.Unity/Plugins/win-x64/VisualPinballNativeWindows.dll` and keeps intermediates in `Windows/obj`. Use `-OutputPath` to stage a build elsewhere when Unity has loaded and locked the DLL.

`./Build.ps1 -Test` builds and runs the fixture. It checks real D3D11 and D3D12 presentation, exact signed coordinates, DPI, hidden creation, placement hit-testing, z-order commands (including that `Configure` can undo a topmost state set through z-order), resize, source detach, idempotent destruction, nested modal message loops, pending D3D12 retirement before the frame fence, four-corner orientation with and without flip, and a test-only GPU readback of an exact premultiplied green128/alpha128 pixel for BGRA UNORM, RGBA/BGRA typeless and RGBA/BGRA sRGB sources.

### macOS and Linux

The `Desktop` folder is a CMake project (3.22+). Point `UNITY_PLUGIN_API` at the Unity Editor's `PluginAPI` directory. With `VPNW_DEPLOY` (default on), the library is copied to `VisualPinball.Unity/Plugins/osx` or `VisualPinball.Unity/Plugins/linux-x64`.

```sh
cmake -S Native~/NativeWindows/Desktop -B Native~/NativeWindows/Desktop/obj -DUNITY_PLUGIN_API="/Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/PluginAPI"
cmake --build Native~/NativeWindows/Desktop/obj --config Release
Native~/NativeWindows/Desktop/obj/NativeWindowTests
```

On macOS the default `CMAKE_OSX_ARCHITECTURES` is `arm64;x86_64`, producing a universal `libVisualPinballNativeWindows.dylib` (deployment target 14.0). Create its Unity `.meta` (Editor OSX and Standalone OSXUniversal enabled) when the binary is first added.

On Linux install `cmake g++ libvulkan-dev libx11-dev libxrender-dev`. The fixture needs an X11 display (`DISPLAY`) and a Vulkan driver; it creates a window at a negative origin, checks geometry, capabilities, presentation through Unity's event pair for UNORM and sRGB sources, placement, z-order, detach, idempotent destruction and deferred retirement. It then reads back the presented centre pixel through a separate X11 connection. Virtual X servers such as Xvfb or WSLg may not expose Vulkan-presented contents to `XGetImage`; set `VPNW_SKIP_PIXEL_CHECK=1` to skip only that readback there. The window reports DPI from the session's `Xft.dpi` resource, or 96.

## Limits

Windows requires Windows 10 or later and Direct3D feature level 11.0; Direct3D12 also requires Unity's v7 native graphics interface and D3D11On12. macOS requires Unity Metal; Linux requires Unity Vulkan and an X11 or XWayland display, since Wayland toplevel placement is compositor controlled. Texture arrays, XR surfaces and arbitrary MSAA sources are not covered. Output is SDR 8-bit BGRA without tone mapping or color management. A desktop compositor or another topmost application can still affect visible stacking.

## References

The plugin uses the installed Unity SDK interfaces and the [official Unity native rendering sample](https://github.com/Unity-Technologies/NativeRenderingPlugin). Microsoft documents [full D3D11 context-state swapping](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_1/nf-d3d11_1-id3d11devicecontext1-swapdevicecontextstate), [D3D11On12 wrapped resources](https://learn.microsoft.com/en-us/windows/win32/api/d3d11on12/nf-d3d11on12-id3d11on12device-createwrappedresource), and [DirectComposition swapchain creation](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgifactory2-createswapchainforcomposition).
