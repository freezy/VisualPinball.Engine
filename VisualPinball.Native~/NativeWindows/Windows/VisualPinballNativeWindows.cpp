// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#define VPNW_BUILD
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include "VisualPinballNativeWindows.h"
#include <Windows.h>
#include <windowsx.h>
#include <d3d11.h>
#include <d3d11_1.h>
#include <d3dcompiler.h>
#include <dxgi1_2.h>
#include <dxgi1_4.h>
#include <dcomp.h>
#include <wrl/client.h>
#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D11.h"
#include "D3D12CopyBridge.h"
#include <array>
#include <atomic>
#include <deque>
#include <functional>
#include <future>
#include <limits>
#include <memory>
#include <mutex>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

using Microsoft::WRL::ComPtr;
extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace vpnw
{
	constexpr wchar_t WindowClass[] = L"VisualPinball.NativeWindow";
	constexpr wchar_t WindowTitle[] = L"Visual Pinball Window";
	constexpr wchar_t PlacementProperty[] = L"VisualPinball.NativeWindow.Placement";
	std::mutex ErrorMutex;
	std::string LastError;
	IUnityInterfaces* UnityInterfaces = nullptr;
	IUnityGraphics* UnityGraphics = nullptr;
	std::atomic<int> Renderer{-1};
	D3D12CopyBridge CopyBridge;
	int DrawEventId = 0, PresentEventId = 0;

	void RecordError(const std::string& message)
	{
		std::lock_guard<std::mutex> lock(ErrorMutex);
		LastError = message;
		OutputDebugStringA(("VisualPinballNativeWindows: " + message + "\n").c_str());
	}

	void Check(HRESULT result, const char* operation)
	{
		if (FAILED(result)) {
			std::ostringstream text;
			text << operation << " failed (HRESULT 0x" << std::hex << static_cast<std::uint32_t>(result) << ").";
			throw std::runtime_error(text.str());
		}
	}

	void CheckWindow(bool success, const char* operation)
	{
		if (!success)
			throw std::runtime_error(std::string(operation) + " failed (Win32 " + std::to_string(GetLastError()) + ").");
	}

	template<class Function> int Guard(Function operation)
	{
		try { return operation(); }
		catch (const std::exception& error) { RecordError(error.what()); }
		catch (...) { RecordError("Unexpected native exception."); }
		return 0;
	}

	void ValidateRectangle(int x, int y, int width, int height)
	{
		if (width < 1 || height < 1 || width > D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION || height > D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION)
			throw std::invalid_argument("Window dimensions must be between 1 and 16384 physical pixels.");
		if (static_cast<std::int64_t>(x) + width > INT_MAX || static_cast<std::int64_t>(y) + height > INT_MAX)
			throw std::invalid_argument("Desktop rectangle exceeds signed Win32 coordinate range.");
	}

	constexpr UINT WindowTasksMessage = WM_APP + 1;
	void DrainWindowTasks(void* thread);
	LRESULT CALLBACK WindowProcedure(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
	{
		if (message == WM_NCCREATE) SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(reinterpret_cast<CREATESTRUCTW*>(lParam)->lpCreateParams));
		if (message == WindowTasksMessage) {
			if (auto thread = reinterpret_cast<void*>(GetWindowLongPtrW(window, GWLP_USERDATA))) DrainWindowTasks(thread);
			return 0;
		}
		if (GetPropW(window, PlacementProperty)) {
			if (message == WM_NCCALCSIZE) return 0;
			if (message == WM_NCHITTEST) {
				RECT bounds{}; GetWindowRect(window, &bounds);
				const int x = GET_X_LPARAM(lParam), y = GET_Y_LPARAM(lParam);
				const bool left = x < bounds.left + 10, right = x >= bounds.right - 10;
				const bool top = y < bounds.top + 10, bottom = y >= bounds.bottom - 10;
				if (top) return left ? HTTOPLEFT : right ? HTTOPRIGHT : HTTOP;
				if (bottom) return left ? HTBOTTOMLEFT : right ? HTBOTTOMRIGHT : HTBOTTOM;
				return left ? HTLEFT : right ? HTRIGHT : HTCAPTION;
			}
			if (message == WM_GETMINMAXINFO) {
				auto* limits = reinterpret_cast<MINMAXINFO*>(lParam);
				limits->ptMinTrackSize = { 64, 32 }; return 0;
			}
		}
		switch (message) {
			case WM_PAINT: ValidateRect(window, nullptr); return 0;
			case WM_ERASEBKGND: return 1;
			case WM_MOUSEACTIVATE: return MA_NOACTIVATE;
			case WM_CLOSE: ShowWindow(window, SW_HIDE); return 0;
			// The application owns physical-pixel rectangles. Do not substitute Windows' suggested logical size.
			case WM_DPICHANGED: return 0;
			default: return DefWindowProcW(window, message, wParam, lParam);
		}
	}

	// HWND creation, positioning and destruction stay on one message-pumping thread.
	// This thread's DPI context changes; the Unity process-wide DPI policy does not.
	class WindowThread
	{
		HANDLE _wake = nullptr;
		std::thread _thread;
		std::atomic<bool> _stop{false};
		DWORD _threadId = 0;
		HWND _dispatcher = nullptr;
		std::mutex _mutex;
		std::deque<std::function<void()>> _tasks;
	public:
		WindowThread()
		{
			_wake = CreateEventW(nullptr, FALSE, FALSE, nullptr);
			CheckWindow(_wake != nullptr, "CreateEvent");
			std::promise<void> started;
			auto future = started.get_future();
			_thread = std::thread([this, ready = std::move(started)]() mutable {
				_threadId = GetCurrentThreadId();
				try {
					CheckWindow(SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) != nullptr, "SetThreadDpiAwarenessContext");
					WNDCLASSEXW description{};
					description.cbSize = sizeof(description);
					description.lpfnWndProc = WindowProcedure;
					description.hInstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
					description.hCursor = LoadCursorW(nullptr, IDC_ARROW);
					description.lpszClassName = WindowClass;
					CheckWindow(RegisterClassExW(&description) != 0, "RegisterClassEx");
					_dispatcher = CreateWindowExW(0, WindowClass, L"", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, description.hInstance, this);
					CheckWindow(_dispatcher != nullptr, "Create window-task dispatcher");
					ready.set_value();
				}
				catch (...) { ready.set_exception(std::current_exception()); return; }
				while (!_stop.load()) {
					MsgWaitForMultipleObjects(1, &_wake, FALSE, INFINITE, QS_ALLINPUT);
					DrainTasks();
					MSG message{};
					while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
						TranslateMessage(&message);
						DispatchMessageW(&message);
					}
				}
				DestroyWindow(_dispatcher);
				UnregisterClassW(WindowClass, reinterpret_cast<HINSTANCE>(&__ImageBase));
			});
			try { future.get(); }
			catch (...) { _thread.join(); CloseHandle(_wake); throw; }
		}
		~WindowThread()
		{
			_stop.store(true);
			SetEvent(_wake);
			_thread.join();
			CloseHandle(_wake);
		}
		template<class Function> auto Invoke(Function operation) -> decltype(operation())
		{
			if (GetCurrentThreadId() == _threadId) return operation();
			using Result = decltype(operation());
			auto task = std::make_shared<std::packaged_task<Result()>>(std::move(operation));
			auto future = task->get_future();
			{ std::lock_guard<std::mutex> lock(_mutex); _tasks.emplace_back([task] { (*task)(); }); }
			SetEvent(_wake);
			// Windows' nested move/resize loop dispatches window messages, but does not
			// wait on our wake event. A message-only HWND keeps queued calls responsive there.
			PostMessageW(_dispatcher, WindowTasksMessage, 0, 0);
			return future.get();
		}
		void DrainTasks()
		{
			std::deque<std::function<void()>> tasks;
			{ std::lock_guard<std::mutex> lock(_mutex); tasks.swap(_tasks); }
			for (auto& task : tasks) task();
		}
	};
	void DrainWindowTasks(void* thread) { static_cast<WindowThread*>(thread)->DrainTasks(); }

	struct Surface
	{
		std::mutex mutex;
		std::atomic<bool> alive{true};
		std::atomic<HWND> window{nullptr};
		ComPtr<ID3D11Texture2D> source;
		std::shared_ptr<D3D12CopySource> source12;
		ComPtr<ID3D11Texture2D> resolved;
		ComPtr<ID3D11ShaderResourceView> sourceView;
		bool copyStoredValues = false;
		ComPtr<IDXGISwapChain1> swapChain;
		ComPtr<ID3D11RenderTargetView> targetView;
		std::array<ComPtr<ID3D11RenderTargetView>, 2> targetViews12;
		std::array<ComPtr<ID3D11Resource>, 2> targetResources12;
		ID3D11Resource* acquiredTarget12 = nullptr;
		UINT64 lastFence12 = 0;
		ComPtr<IDCompositionDevice> composition;
		ComPtr<IDCompositionTarget> compositionTarget;
		ComPtr<IDCompositionVisual> visual;
		UINT width = 0, height = 0;
		bool flipY = false;
		// Configuration cache is owned exclusively by the HWND message thread.
		bool configured = false, requestedVisible = false, requestedTopmost = false;
		int requestedX = 0, requestedY = 0, requestedWidth = 0, requestedHeight = 0;
		std::uint64_t presented = 0, skipped = 0;
		HRESULT lastResult = S_OK;

		void ReleasePresentation()
		{
			if (compositionTarget) compositionTarget->SetRoot(nullptr);
			if (visual) visual->SetContent(nullptr);
			if (composition) composition->Commit();
			visual.Reset(); compositionTarget.Reset(); composition.Reset();
			targetView.Reset();
			for (auto& view : targetViews12) view.Reset();
			for (auto& resource : targetResources12) resource.Reset();
			swapChain.Reset();
			width = height = 0;
		}
	};

	std::mutex RegistryMutex;
	std::unordered_map<int, std::shared_ptr<Surface>> Surfaces;
	std::uint64_t NextId = 1;
	std::mutex RetiredSurfaceMutex;
	std::vector<std::shared_ptr<Surface>> RetiredSurfaces;
	std::mutex WindowServiceMutex;
	std::shared_ptr<WindowThread> WindowService;

	std::shared_ptr<WindowThread> GetWindowThread()
	{
		std::lock_guard<std::mutex> lock(WindowServiceMutex);
		if (!WindowService) WindowService = std::make_shared<WindowThread>();
		return WindowService;
	}

	std::shared_ptr<Surface> Find(int id)
	{
		std::lock_guard<std::mutex> lock(RegistryMutex);
		auto item = Surfaces.find(id);
		return item == Surfaces.end() ? nullptr : item->second;
	}

	std::shared_ptr<Surface> Require(int id)
	{
		auto surface = Find(id);
		if (!surface) throw std::invalid_argument("Unknown or destroyed native window ID.");
		return surface;
	}

	std::mutex DeviceMutex;
	ComPtr<ID3D11Device> Device;
	ComPtr<ID3D11DeviceContext> Context;
	ComPtr<ID3D11DeviceContext1> Context1;
	ComPtr<ID3DDeviceContextState> PluginContextState;
	ComPtr<ID3D11VertexShader> VertexShader;
	ComPtr<ID3D11PixelShader> PixelShader;
	ComPtr<ID3D11SamplerState> Sampler;
	ComPtr<ID3D11RasterizerState> Rasterizer;
	ComPtr<ID3D11BlendState> Blend;
	ComPtr<ID3D11DepthStencilState> Depth;
	ComPtr<ID3D11Buffer> Parameters;

	constexpr char ShaderSource[] = R"(
cbuffer Parameters : register(b0) { float flipY; float3 padding; };
struct Vertex { float4 position : SV_Position; float2 uv : TEXCOORD0; };
Vertex VertexMain(uint id : SV_VertexID) {
	Vertex result;
	result.uv = float2((id << 1) & 2, id & 2);
	result.position = float4(result.uv * float2(2, -2) + float2(-1, 1), 0, 1);
	return result;
}
Texture2D scene : register(t0);
SamplerState sceneSampler : register(s0);
float4 PixelMain(Vertex input) : SV_Target {
	float2 uv = input.uv;
	if (flipY != 0) uv.y = 1 - uv.y;
	return scene.Sample(sceneSampler, uv);
})";

	void CreatePipeline()
	{
		ComPtr<ID3DBlob> vertex, pixel, errors;
		Check(D3DCompile(ShaderSource, sizeof(ShaderSource) - 1, "VisualPinballNativeWindow", nullptr, nullptr,
			"VertexMain", "vs_5_0", D3DCOMPILE_OPTIMIZATION_LEVEL3, 0, &vertex, &errors), "Compile native vertex shader");
		Check(D3DCompile(ShaderSource, sizeof(ShaderSource) - 1, "VisualPinballNativeWindow", nullptr, nullptr,
			"PixelMain", "ps_5_0", D3DCOMPILE_OPTIMIZATION_LEVEL3, 0, &pixel, &errors), "Compile native pixel shader");
		Check(Device->CreateVertexShader(vertex->GetBufferPointer(), vertex->GetBufferSize(), nullptr, &VertexShader), "CreateVertexShader");
		Check(Device->CreatePixelShader(pixel->GetBufferPointer(), pixel->GetBufferSize(), nullptr, &PixelShader), "CreatePixelShader");
		D3D11_SAMPLER_DESC sampler{};
		sampler.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
		sampler.AddressU = sampler.AddressV = sampler.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
		sampler.MaxLOD = D3D11_FLOAT32_MAX;
		Check(Device->CreateSamplerState(&sampler, &Sampler), "CreateSamplerState");
		D3D11_RASTERIZER_DESC rasterizer{};
		rasterizer.FillMode = D3D11_FILL_SOLID; rasterizer.CullMode = D3D11_CULL_NONE; rasterizer.DepthClipEnable = TRUE;
		Check(Device->CreateRasterizerState(&rasterizer, &Rasterizer), "CreateRasterizerState");
		D3D11_BLEND_DESC blend{};
		blend.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
		blend.RenderTarget[0].SrcBlend = blend.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
		blend.RenderTarget[0].DestBlend = blend.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
		blend.RenderTarget[0].BlendOp = blend.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
		Check(Device->CreateBlendState(&blend, &Blend), "CreateBlendState");
		D3D11_DEPTH_STENCIL_DESC depth{};
		depth.DepthFunc = D3D11_COMPARISON_ALWAYS;
		depth.StencilReadMask = depth.StencilWriteMask = 255;
		depth.FrontFace.StencilFailOp = depth.FrontFace.StencilDepthFailOp = depth.FrontFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
		depth.FrontFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
		depth.BackFace = depth.FrontFace;
		Check(Device->CreateDepthStencilState(&depth, &Depth), "CreateDepthStencilState");
		D3D11_BUFFER_DESC buffer{};
		buffer.ByteWidth = 16; buffer.Usage = D3D11_USAGE_DEFAULT; buffer.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
		Check(Device->CreateBuffer(&buffer, nullptr, &Parameters), "CreateBuffer");
	}

	// D3D11.1 swaps the entire context state, including UAVs and shader resources which
	// resource hazards could implicitly unbind. This avoids partial-state restoration bugs.
	class SavedContext
	{
		ID3D11DeviceContext1* _context;
		ComPtr<ID3DDeviceContextState> _previous;
	public:
		explicit SavedContext(ID3D11DeviceContext1* context) : _context(context)
		{
			_context->SwapDeviceContextState(PluginContextState.Get(), &_previous);
		}
		~SavedContext() { _context->SwapDeviceContextState(_previous.Get(), nullptr); }
	};

	DXGI_FORMAT SamplingFormat(DXGI_FORMAT format)
	{
		// Present the stored SDR RGB values without an implicit sRGB decoding step.
		switch (format) {
			case DXGI_FORMAT_R8G8B8A8_TYPELESS: case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return DXGI_FORMAT_R8G8B8A8_UNORM;
			case DXGI_FORMAT_B8G8R8A8_TYPELESS: case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB: return DXGI_FORMAT_B8G8R8A8_UNORM;
			case DXGI_FORMAT_R16G16B16A16_TYPELESS: return DXGI_FORMAT_R16G16B16A16_FLOAT;
			case DXGI_FORMAT_R32G32B32A32_TYPELESS: return DXGI_FORMAT_R32G32B32A32_FLOAT;
			default: return format;
		}
	}

	void EnsureSourceView(Surface& surface)
	{
		if (surface.sourceView) return;
		D3D11_TEXTURE2D_DESC description{};
		surface.source->GetDesc(&description);
		if (description.ArraySize != 1) throw std::invalid_argument("Native output requires a single 2D texture, not an array or XR surface.");
		ID3D11Texture2D* texture = surface.source.Get();
		const bool typedSrgb = description.Format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB || description.Format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
		if (description.SampleDesc.Count > 1 || typedSrgb) {
			auto resolved = description;
			resolved.SampleDesc.Count = 1; resolved.SampleDesc.Quality = 0;
			resolved.MipLevels = 1;
			resolved.Usage = D3D11_USAGE_DEFAULT; resolved.CPUAccessFlags = 0;
			resolved.BindFlags = D3D11_BIND_SHADER_RESOURCE; resolved.MiscFlags = 0;
			resolved.Format = SamplingFormat(description.Format);
			Check(Device->CreateTexture2D(&resolved, nullptr, &surface.resolved), "Create typed presentation source texture");
			surface.copyStoredValues = description.SampleDesc.Count == 1;
			texture = surface.resolved.Get();
		}
		D3D11_SHADER_RESOURCE_VIEW_DESC view{};
		view.Format = SamplingFormat(description.Format);
		view.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
		view.Texture2D.MipLevels = 1;
		Check(Device->CreateShaderResourceView(texture, &view, &surface.sourceView), "Create source texture view");
	}

	bool EnsurePresentation(Surface& surface, UINT width, UINT height)
	{
		if (!surface.swapChain) {
			ComPtr<IDXGIDevice> dxgiDevice; Check(Device.As(&dxgiDevice), "Query DXGI device");
			ComPtr<IDXGIFactory2> factory; Check(CreateDXGIFactory1(IID_PPV_ARGS(&factory)), "Create DXGI factory");
			DXGI_SWAP_CHAIN_DESC1 description{};
			description.Width = width; description.Height = height;
			description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
			description.SampleDesc.Count = 1; description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
			description.BufferCount = 2; description.Scaling = DXGI_SCALING_STRETCH;
			description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
			description.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;
			Check(factory->CreateSwapChainForComposition(Renderer.load() == kUnityGfxRendererD3D12 ? CopyBridge.Queue() : Device.Get(), &description, nullptr, &surface.swapChain), "Create composition swapchain");
			Check(DCompositionCreateDevice(dxgiDevice.Get(), IID_PPV_ARGS(&surface.composition)), "Create DirectComposition device");
			Check(surface.composition->CreateTargetForHwnd(surface.window.load(), TRUE, &surface.compositionTarget), "Create composition window target");
			Check(surface.composition->CreateVisual(&surface.visual), "Create composition visual");
			Check(surface.visual->SetContent(surface.swapChain.Get()), "Attach composition swapchain");
			Check(surface.compositionTarget->SetRoot(surface.visual.Get()), "Attach composition root");
			Check(surface.composition->Commit(), "Commit window composition");
		}
		else if (surface.width != width || surface.height != height) {
			if (Renderer.load() == kUnityGfxRendererD3D12 && !CopyBridge.Completed(surface.lastFence12)) return false;
			surface.targetView.Reset();
			for (auto& view : surface.targetViews12) view.Reset();
			for (auto& resource : surface.targetResources12) resource.Reset();
			Check(surface.swapChain->ResizeBuffers(2, width, height, DXGI_FORMAT_B8G8R8A8_UNORM, 0), "Resize composition swapchain");
		}
		if (Renderer.load() == kUnityGfxRendererD3D12) {
			for (UINT index = 0; index < 2; index++) if (!surface.targetViews12[index]) {
				ComPtr<ID3D12Resource> buffer;
				Check(surface.swapChain->GetBuffer(index, IID_PPV_ARGS(&buffer)), "Get D3D12 composition backbuffer");
				surface.targetResources12[index] = CopyBridge.WrapTarget(buffer.Get());
				Check(Device->CreateRenderTargetView(surface.targetResources12[index].Get(), nullptr,
					&surface.targetViews12[index]), "Create wrapped composition render target");
			}
			ComPtr<IDXGISwapChain3> swap3; Check(surface.swapChain.As(&swap3), "Query composition buffer index");
			UINT index = swap3->GetCurrentBackBufferIndex();
			surface.targetView = surface.targetViews12[index];
			surface.acquiredTarget12 = surface.targetResources12[index].Get();
			CopyBridge.AcquireTarget(surface.acquiredTarget12);
		}
		else if (!surface.targetView) {
			ComPtr<ID3D11Texture2D> buffer;
			Check(surface.swapChain->GetBuffer(0, IID_PPV_ARGS(&buffer)), "Get composition backbuffer");
			Check(Device->CreateRenderTargetView(buffer.Get(), nullptr, &surface.targetView), "Create composition render target");
		}
		surface.width = width; surface.height = height;
		return true;
	}

	void RenderSurface(const std::shared_ptr<Surface>& surface, bool present = true)
	{
		std::lock_guard<std::mutex> lock(surface->mutex);
		HWND window = surface->window.load();
		if (!surface->alive.load() || !window || (!surface->source && !surface->source12) || !IsWindowVisible(window)) return;
		RECT client{};
		if (!GetClientRect(window, &client) || client.right < 1 || client.bottom < 1) return;
		bool acquiredSource = false;
		try {
			if (surface->source12) {
				auto wrapped = CopyBridge.Acquire(*surface->source12);
				if (!wrapped) return;
				acquiredSource = true;
				ComPtr<ID3D11Texture2D> texture; Check(wrapped->QueryInterface(IID_PPV_ARGS(&texture)), "Query wrapped source texture");
				if (surface->source.Get() != texture.Get()) {
					surface->source = texture; surface->sourceView.Reset(); surface->resolved.Reset();
				}
			}
			EnsureSourceView(*surface);
			if (!EnsurePresentation(*surface, static_cast<UINT>(client.right), static_cast<UINT>(client.bottom))) {
				surface->skipped++;
				if (acquiredSource) CopyBridge.Release(*surface->source12);
				return;
			}
			if (surface->resolved) {
				D3D11_TEXTURE2D_DESC description{}; surface->resolved->GetDesc(&description);
				if (surface->copyStoredValues) Context->CopySubresourceRegion(surface->resolved.Get(), 0, 0, 0, 0, surface->source.Get(), 0, nullptr);
				else Context->ResolveSubresource(surface->resolved.Get(), 0, surface->source.Get(), 0, description.Format);
			}
			ID3D11RenderTargetView* target = surface->targetView.Get();
			Context->OMSetRenderTargets(1, &target, nullptr);
			Context->OMSetBlendState(Blend.Get(), nullptr, 0xffffffff);
			Context->OMSetDepthStencilState(Depth.Get(), 0);
			Context->RSSetState(Rasterizer.Get());
			D3D11_VIEWPORT viewport{0, 0, static_cast<float>(surface->width), static_cast<float>(surface->height), 0, 1};
			Context->RSSetViewports(1, &viewport);
			Context->IASetInputLayout(nullptr); Context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
			Context->VSSetShader(VertexShader.Get(), nullptr, 0); Context->PSSetShader(PixelShader.Get(), nullptr, 0);
			Context->GSSetShader(nullptr, nullptr, 0); Context->HSSetShader(nullptr, nullptr, 0); Context->DSSetShader(nullptr, nullptr, 0);
			Context->PSSetShaderResources(0, 1, surface->sourceView.GetAddressOf());
			Context->PSSetSamplers(0, 1, Sampler.GetAddressOf());
			const float parameters[4] = {surface->flipY ? 1.f : 0.f, 0, 0, 0};
			Context->UpdateSubresource(Parameters.Get(), 0, nullptr, parameters, 0, 0);
			Context->PSSetConstantBuffers(0, 1, Parameters.GetAddressOf());
			Context->Draw(3, 0);
			ID3D11RenderTargetView* noTarget = nullptr;
			Context->OMSetRenderTargets(1, &noTarget, nullptr);
			ID3D11ShaderResourceView* noSource = nullptr;
			Context->PSSetShaderResources(0, 1, &noSource);
			if (surface->acquiredTarget12) {
				CopyBridge.ReleaseTarget(surface->acquiredTarget12); surface->acquiredTarget12 = nullptr;
			}
			if (acquiredSource) { CopyBridge.Release(*surface->source12); acquiredSource = false; }
			if (Renderer.load() == kUnityGfxRendererD3D12) {
				Context->Flush(); // Same queue, after Unity's configured flush/submission event.
				surface->lastFence12 = CopyBridge.FrameFenceValue();
			}
			// A fixture can inspect the rendered buffer before a flip discards its contents.
			if (!present) return;
			// Never stall Unity's render thread waiting for a desktop display's refresh rate.
			surface->lastResult = surface->swapChain->Present(0, DXGI_PRESENT_DO_NOT_WAIT);
			if (surface->lastResult == DXGI_ERROR_WAS_STILL_DRAWING || surface->lastResult == DXGI_STATUS_OCCLUDED) surface->skipped++;
			else { Check(surface->lastResult, "Present composition swapchain"); surface->presented++; }
		}
		catch (const std::exception& error) {
			if (surface->acquiredTarget12) {
				CopyBridge.ReleaseTarget(surface->acquiredTarget12); surface->acquiredTarget12 = nullptr;
			}
			if (acquiredSource) CopyBridge.Release(*surface->source12);
			surface->skipped++;
			if (SUCCEEDED(surface->lastResult)) surface->lastResult = E_FAIL;
			surface->ReleasePresentation();
			RecordError(error.what());
		}
	}

	void ReleaseDevice()
	{
		std::lock_guard<std::mutex> registry(RegistryMutex);
		for (auto& entry : Surfaces) {
			std::lock_guard<std::mutex> surface(entry.second->mutex);
			entry.second->ReleasePresentation();
			entry.second->sourceView.Reset(); entry.second->resolved.Reset(); entry.second->source.Reset(); entry.second->source12.reset();
		}
		{ std::lock_guard<std::mutex> retired(RetiredSurfaceMutex); RetiredSurfaces.clear(); }
		CopyBridge.Shutdown();
		Parameters.Reset(); Depth.Reset(); Blend.Reset(); Rasterizer.Reset(); Sampler.Reset();
		PixelShader.Reset(); VertexShader.Reset(); PluginContextState.Reset(); Context1.Reset(); Context.Reset(); Device.Reset();
	}

	void InitializeDevice()
	{
		if (Device || !UnityInterfaces) return;
		if (Renderer.load() == kUnityGfxRendererD3D12) { CopyBridge.InitializeDevice(); return; }
		if (Renderer.load() != kUnityGfxRendererD3D11) return;
		auto graphics = UnityInterfaces->Get<IUnityGraphicsD3D11>();
		if (!graphics || !graphics->GetDevice()) throw std::runtime_error("Unity's Direct3D11 device is not ready.");
		Device = graphics->GetDevice();
		Device->GetImmediateContext(&Context);
		Check(Context.As(&Context1), "Query D3D11.1 immediate context");
		ComPtr<ID3D11Device1> device1; Check(Device.As(&device1), "Query D3D11.1 device");
		D3D_FEATURE_LEVEL level = Device->GetFeatureLevel();
		if (level < D3D_FEATURE_LEVEL_11_0) throw std::runtime_error("Native output requires Direct3D feature level11_0 or newer.");
		UINT flags = (Device->GetCreationFlags() & D3D11_CREATE_DEVICE_SINGLETHREADED) != 0
			? D3D11_1_CREATE_DEVICE_CONTEXT_STATE_SINGLETHREADED : 0;
		Check(device1->CreateDeviceContextState(flags, &level, 1, D3D11_SDK_VERSION, __uuidof(ID3D11Device), nullptr, &PluginContextState), "Create isolated presentation context state");
		CreatePipeline();
	}

	void UNITY_INTERFACE_API GraphicsEvent(UnityGfxDeviceEventType event)
	{
		std::lock_guard<std::mutex> lock(DeviceMutex);
		try {
			if (event == kUnityGfxDeviceEventShutdown || event == kUnityGfxDeviceEventBeforeReset) ReleaseDevice();
			else if (event == kUnityGfxDeviceEventInitialize || event == kUnityGfxDeviceEventAfterReset) {
				Renderer.store(UnityGraphics ? UnityGraphics->GetRenderer() : -1);
				InitializeDevice();
			}
		}
		catch (const std::exception& error) { ReleaseDevice(); RecordError(error.what()); }
	}

	void UNITY_INTERFACE_API RenderEvent(int id)
	{
		std::lock_guard<std::mutex> lock(DeviceMutex);
		try {
			InitializeDevice();
			thread_local std::vector<std::shared_ptr<Surface>> surfaces;
			surfaces.clear();
			{ std::lock_guard<std::mutex> registry(RegistryMutex); for (auto& entry : Surfaces) surfaces.push_back(entry.second); }
			if (Renderer.load() == kUnityGfxRendererD3D12) {
				CopyBridge.Collect();
				{ std::lock_guard<std::mutex> retired(RetiredSurfaceMutex);
					for (auto item = RetiredSurfaces.begin(); item != RetiredSurfaces.end();) {
						if (CopyBridge.Completed((*item)->lastFence12)) { (*item)->ReleasePresentation(); item = RetiredSurfaces.erase(item); }
						else ++item;
					}
				}
				if (id == DrawEventId) {
					for (auto& surface : surfaces) {
						std::lock_guard<std::mutex> state(surface->mutex);
						if (surface->alive.load() && surface->source12 && IsWindowVisible(surface->window.load()))
							if (!CopyBridge.RecordCopy(*surface->source12)) surface->skipped++;
					}
				}
				else if (id == PresentEventId) {
					if (!Device) { CopyBridge.InitializePresentation(&Device, &Context); CreatePipeline(); }
					for (auto& surface : surfaces) RenderSurface(surface);
					Context->Flush();
				}
			}
			else if (Device && id != PresentEventId) {
				SavedContext saved(Context1.Get());
				if (id == DrawEventId || id == 0) for (auto& surface : surfaces) RenderSurface(surface);
				else if (auto surface = Find(id)) RenderSurface(surface);
			}
			else if (!Device) RecordError("Native output requires a supported Windows Direct3D11 or Direct3D12 backend.");
			surfaces.clear();
		}
		catch (const std::exception& error) { RecordError(error.what()); }
		catch (...) { RecordError("Unexpected native rendering exception."); }
	}

}

using namespace vpnw;

VPNW_API int VPNW_CALL VPNW_CreateWindow(int x, int y, int width, int height)
{
	return Guard([=] {
		ValidateRectangle(x, y, width, height);
		if (Renderer.load() != kUnityGfxRendererD3D11 && (Renderer.load() != kUnityGfxRendererD3D12 || !CopyBridge.Supported()))
			throw std::runtime_error("Native windows require Unity Direct3D11 or Direct3D12 with the v7 native graphics interface.");
		auto surface = std::make_shared<Surface>();
		int id;
		{ std::lock_guard<std::mutex> lock(RegistryMutex);
			if (NextId > INT_MAX) throw std::runtime_error("Native window ID range exhausted.");
			id = static_cast<int>(NextId++);
		}
		GetWindowThread()->Invoke([=] {
			HWND window = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP,
				WindowClass, WindowTitle, WS_POPUP, x, y, width, height, nullptr, nullptr,
				reinterpret_cast<HINSTANCE>(&__ImageBase), nullptr);
			CheckWindow(window != nullptr, "Create borderless output window");
			surface->window.store(window);
		});
		{ std::lock_guard<std::mutex> lock(RegistryMutex); Surfaces.emplace(id, surface); }
		return id;
	});
}

VPNW_API int VPNW_CALL VPNW_ConfigureWindow(int id, int x, int y, int width, int height, int visible, int topmost)
{
	return Guard([=] {
		ValidateRectangle(x, y, width, height);
		auto surface = Require(id);
		GetWindowThread()->Invoke([=] {
			if (!surface->alive.load()) throw std::runtime_error("Window was destroyed before configuration.");
			HWND window = surface->window.load();
			bool changedRectangle = !surface->configured || surface->requestedX != x || surface->requestedY != y ||
				surface->requestedWidth != width || surface->requestedHeight != height;
			bool changedVisibility = !surface->configured || surface->requestedVisible != (visible != 0);
			bool changedTopmost = !surface->configured || surface->requestedTopmost != (topmost != 0);
			if (changedVisibility) ShowWindow(window, visible ? SW_SHOWNOACTIVATE : SW_HIDE);
			// Apply z-order after showing: Windows need not mark a hidden window topmost.
			if (changedRectangle || changedTopmost || (changedVisibility && visible)) {
				UINT flags = SWP_NOACTIVATE;
				if (!changedRectangle) flags |= SWP_NOMOVE | SWP_NOSIZE;
				CheckWindow(SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, x, y, width, height, flags) != FALSE, "Set physical desktop rectangle");
			}
			surface->configured = true; surface->requestedX = x; surface->requestedY = y;
			surface->requestedWidth = width; surface->requestedHeight = height;
			surface->requestedVisible = visible != 0; surface->requestedTopmost = topmost != 0;
		});
		return 1;
	});
}

VPNW_API int VPNW_CALL VPNW_SetPlacementMode(int id, int enabled)
{
	return Guard([=] {
		auto surface = Require(id);
		GetWindowThread()->Invoke([=] {
			HWND window = surface->window.load();
			if (enabled) CheckWindow(SetPropW(window, PlacementProperty, reinterpret_cast<HANDLE>(1)) != FALSE, "Enable placement interaction");
			else RemovePropW(window, PlacementProperty);
			const auto style = GetWindowLongPtrW(window, GWL_STYLE);
			SetWindowLongPtrW(window, GWL_STYLE, enabled ? style | WS_THICKFRAME : style & ~WS_THICKFRAME);
			CheckWindow(SetWindowPos(window, nullptr, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER) != FALSE, "Update placement frame");
		});
		return 1;
	});
}

VPNW_API int VPNW_CALL VPNW_SetZOrder(int id, int order)
{
	return Guard([=] {
		if (order < 0 || order > 3) throw std::invalid_argument("Z-order must be Back0, Front1, Topmost2 or NonTopmost3.");
		auto surface = Require(id);
		GetWindowThread()->Invoke([=] {
			if (!surface->alive.load()) throw std::runtime_error("Window was destroyed before z-order update.");
			const HWND positions[] = {HWND_BOTTOM, HWND_TOP, HWND_TOPMOST, HWND_NOTOPMOST};
			CheckWindow(SetWindowPos(surface->window.load(), positions[order], 0, 0, 0, 0,
				SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE) != FALSE, "Update window z-order");
			// Keep the configuration cache truthful, so a later Configure can restore the other topmost state.
			if (order >= 2) surface->requestedTopmost = order == 2;
		});
		return 1;
	});
}

VPNW_API int VPNW_CALL VPNW_SetTexture(int id, void* texture, int flipY)
{
	return Guard([=] {
		auto surface = Require(id);
		std::lock_guard<std::mutex> device(DeviceMutex);
		std::lock_guard<std::mutex> lock(surface->mutex);
		if (!surface->alive.load()) throw std::runtime_error("Window was destroyed before texture assignment.");
		if (Renderer.load() == kUnityGfxRendererD3D12) {
			if (!surface->source12 || surface->source12->source.Get() != texture) {
				auto replacement = CopyBridge.Source(texture);
				CopyBridge.Retire(std::move(surface->source12)); surface->source12 = std::move(replacement);
				surface->source.Reset(); surface->sourceView.Reset(); surface->resolved.Reset();
			}
		}
		else {
			ComPtr<ID3D11Texture2D> source;
			if (texture) Check(static_cast<IUnknown*>(texture)->QueryInterface(IID_PPV_ARGS(&source)), "Acquire Unity texture");
			if (source && Device) {
				ComPtr<ID3D11Device> owner; source->GetDevice(&owner);
				if (owner.Get() != Device.Get()) throw std::invalid_argument("Texture belongs to a different Direct3D11 device.");
			}
			if (surface->source.Get() != source.Get()) {
				surface->source = source; surface->sourceView.Reset(); surface->resolved.Reset();
			}
		}
		surface->flipY = flipY != 0;
		return 1;
	});
}

VPNW_API int VPNW_CALL VPNW_DestroyWindow(int id)
{
	return Guard([=] {
		std::shared_ptr<Surface> surface;
		{ std::lock_guard<std::mutex> lock(RegistryMutex);
			auto item = Surfaces.find(id);
			if (item == Surfaces.end()) return 1; // Idempotent cleanup, including after Unity device shutdown.
			surface = item->second; Surfaces.erase(item);
		}
		HWND window;
		{ std::lock_guard<std::mutex> lock(surface->mutex);
			surface->alive.store(false); window = surface->window.exchange(nullptr);
			if (Renderer.load() == kUnityGfxRendererD3D12) {
				if (surface->source12) {
					surface->lastFence12 = (std::max)(surface->lastFence12, surface->source12->lastFence);
					CopyBridge.Retire(std::move(surface->source12));
				}
				// Detaching the source does not imply that submitted backbuffer work has completed.
				if (surface->compositionTarget) surface->compositionTarget->SetRoot(nullptr);
				if (surface->composition) surface->composition->Commit();
				std::lock_guard<std::mutex> retired(RetiredSurfaceMutex); RetiredSurfaces.push_back(surface);
			}
			else surface->ReleasePresentation();
			surface->sourceView.Reset(); surface->resolved.Reset(); surface->source.Reset();
		}
		GetWindowThread()->Invoke([=] { if (window) CheckWindow(DestroyWindow(window) != FALSE, "Destroy output window"); });
		return 1;
	});
}

VPNW_API void VPNW_CALL VPNW_DestroyAll()
{
	std::vector<int> ids;
	{ std::lock_guard<std::mutex> lock(RegistryMutex); for (auto& entry : Surfaces) ids.push_back(entry.first); }
	for (int id : ids) VPNW_DestroyWindow(id);
}

// The HWNDs live on the plugin's own message thread; Unity's main thread has nothing to pump.
VPNW_API void VPNW_CALL VPNW_Pump() {}
VPNW_API std::uint32_t VPNW_CALL VPNW_GetCapabilities()
{
	return VPNW_CapabilityWindows | VPNW_CapabilityPlacement | VPNW_CapabilityTopmost | VPNW_CapabilityTransparency;
}
VPNW_API void* VPNW_CALL VPNW_GetRenderEventFunc() { return reinterpret_cast<void*>(RenderEvent); }
VPNW_API int VPNW_CALL VPNW_GetGraphicsRenderer() { return Renderer.load(); }
VPNW_API int VPNW_CALL VPNW_GetDrawEventId() { return DrawEventId; }
VPNW_API int VPNW_CALL VPNW_GetPresentEventId() { return PresentEventId; }

VPNW_API int VPNW_CALL VPNW_GetPendingResourceCount()
{
	std::lock_guard<std::mutex> device(DeviceMutex);
	const int sources = CopyBridge.PendingResourceCount();
	std::lock_guard<std::mutex> retired(RetiredSurfaceMutex);
	return sources + static_cast<int>(RetiredSurfaces.size());
}

VPNW_API int VPNW_CALL VPNW_GetDiagnostics(int id, VPNW_Diagnostics* diagnostics)
{
	return Guard([=] {
		if (!diagnostics || diagnostics->structBytes != sizeof(VPNW_Diagnostics)) throw std::invalid_argument("Unexpected native diagnostics structure size.");
		auto surface = Require(id);
		std::lock_guard<std::mutex> device(DeviceMutex);
		std::lock_guard<std::mutex> lock(surface->mutex);
		HWND window = surface->window.load();
		VPNW_Diagnostics result{};
		result.structBytes = sizeof(result); result.graphicsRenderer = Renderer.load(); result.deviceReady = Device ? 1 : 0;
		result.alive = surface->alive.load() ? 1 : 0; result.visible = IsWindowVisible(window) ? 1 : 0;
		result.topmost = (GetWindowLongPtrW(window, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0 ? 1 : 0;
		// The HWND thread can be inside Windows' modal drag/resize loop. Read geometry
		// directly with a temporary thread DPI context so diagnostics never wait for that loop.
		const auto previousDpi = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
		RECT rectangle{};
		const bool readRectangle = GetWindowRect(window, &rectangle) != FALSE;
		if (previousDpi) SetThreadDpiAwarenessContext(previousDpi);
		CheckWindow(readRectangle, "Read physical output window rectangle");
		result.left = rectangle.left; result.top = rectangle.top;
		result.windowWidth = rectangle.right - rectangle.left; result.windowHeight = rectangle.bottom - rectangle.top;
		result.dpi = GetDpiForWindow(window);
		if (surface->source12) {
			result.sourceWidth = surface->source12->width; result.sourceHeight = surface->source12->height; result.sourceFormat = surface->source12->format;
		}
		else if (surface->source) {
			D3D11_TEXTURE2D_DESC description{}; surface->source->GetDesc(&description);
			result.sourceWidth = description.Width; result.sourceHeight = description.Height; result.sourceFormat = description.Format;
		}
		result.presentedFrames = surface->presented; result.skippedFrames = surface->skipped;
		result.lastResult = surface->lastResult; result.premultipliedAlpha = 1;
		*diagnostics = result;
		return 1;
	});
}

VPNW_API int VPNW_CALL VPNW_GetLastError(char* destination, int capacity)
{
	std::lock_guard<std::mutex> lock(ErrorMutex);
	if (destination && capacity > 0) strncpy_s(destination, static_cast<std::size_t>(capacity), LastError.c_str(), _TRUNCATE);
	return static_cast<int>(LastError.size() + 1);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* interfaces)
{
	UnityInterfaces = interfaces;
	UnityGraphics = interfaces ? interfaces->Get<IUnityGraphics>() : nullptr;
	if (UnityGraphics) {
		Renderer.store(UnityGraphics->GetRenderer());
		UnityGraphics->RegisterDeviceEventCallback(GraphicsEvent);
		DrawEventId = UnityGraphics->ReserveEventIDRange(2); PresentEventId = DrawEventId + 1;
		auto graphics12 = interfaces->Get<IUnityGraphicsD3D12v7>();
		CopyBridge.SetUnity(graphics12);
		if (graphics12 && Renderer.load() == kUnityGfxRendererD3D12) {
			UnityD3D12PluginEventConfig draw{};
			draw.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
			draw.flags = kUnityD3D12EventConfigFlag_SyncWorkerThreads;
			graphics12->ConfigureEvent(DrawEventId, &draw);
			UnityD3D12PluginEventConfig present{};
			present.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_Allow;
			present.flags = kUnityD3D12EventConfigFlag_FlushCommandBuffers | kUnityD3D12EventConfigFlag_SyncWorkerThreads;
			graphics12->ConfigureEvent(PresentEventId, &present);
		}
	}
	// Device access is deferred to the graphics event/render callback, both on Unity's rendering thread.
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
	if (UnityGraphics) UnityGraphics->UnregisterDeviceEventCallback(GraphicsEvent);
	VPNW_DestroyAll();
	{ std::lock_guard<std::mutex> device(DeviceMutex); ReleaseDevice(); }
	{ std::lock_guard<std::mutex> windows(WindowServiceMutex); WindowService.reset(); }
	UnityGraphics = nullptr; UnityInterfaces = nullptr; Renderer.store(-1);
}
