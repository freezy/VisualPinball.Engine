// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

// Standalone integration checks against the same production source, with a small Unity-interface fixture.
// Owns temporary output windows and GPU devices only; it does not alter display settings.
#include "VisualPinballNativeWindows.cpp"
#include "D3D12CopyBridge.cpp"
#include <iostream>

using namespace vpnw;

namespace Test
{
	UnityGfxRenderer renderer = kUnityGfxRendererD3D11;
	ComPtr<ID3D11Device> device11;
	ComPtr<ID3D12Device> device12;
	ComPtr<ID3D12CommandQueue> queue12;
	ComPtr<ID3D12Fence> fence12;
	UINT64 nextFence = 1;
	std::vector<ComPtr<ID3D12CommandAllocator>> transitionAllocators;
	std::vector<ComPtr<ID3D12GraphicsCommandList>> transitionLists;
	std::unordered_map<ID3D12Resource*, D3D12_RESOURCE_STATES> states;

	UnityGfxRenderer UNITY_INTERFACE_API GetRenderer() { return renderer; }
	void UNITY_INTERFACE_API Register(IUnityGraphicsDeviceEventCallback) {}
	void UNITY_INTERFACE_API Unregister(IUnityGraphicsDeviceEventCallback) {}
	int UNITY_INTERFACE_API Reserve(int) { return 10000; }
	ID3D11Device* UNITY_INTERFACE_API Device11() { return device11.Get(); }
	ID3D12Device* UNITY_INTERFACE_API Device12() { return device12.Get(); }
	ID3D12CommandQueue* UNITY_INTERFACE_API Queue12() { return queue12.Get(); }
	ID3D12Fence* UNITY_INTERFACE_API Fence12() { return fence12.Get(); }
	UINT64 UNITY_INTERFACE_API NextFence() { return nextFence; }
	void UNITY_INTERFACE_API Configure(int, const UnityD3D12PluginEventConfig*) {}
	UINT64 UNITY_INTERFACE_API Execute(ID3D12GraphicsCommandList* commands, int count, UnityGraphicsD3D12ResourceState* requested)
	{
		ComPtr<ID3D12CommandAllocator> allocator;
		ComPtr<ID3D12GraphicsCommandList> preparation;
		Check12(device12->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator)), "Create fixture state allocator");
		Check12(device12->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, allocator.Get(), nullptr, IID_PPV_ARGS(&preparation)), "Create fixture state command list");
		for (int index = 0; index < count; index++) {
			auto& state = requested[index];
			if (states[state.resource] != state.expected) {
				auto barrier = Barrier(state.resource, states[state.resource], state.expected);
				preparation->ResourceBarrier(1, &barrier);
			}
			states[state.resource] = state.current;
		}
		Check12(preparation->Close(), "Close fixture preparation");
		ID3D12CommandList* lists[] = {preparation.Get(), commands};
		queue12->ExecuteCommandLists(2, lists);
		transitionAllocators.push_back(allocator); transitionLists.push_back(preparation);
		return nextFence;
	}
	IUnityGraphics graphics{};
	IUnityGraphicsD3D11 graphics11{};
	IUnityGraphicsD3D12v7 graphics12{};
	IUnityInterface* UNITY_INTERFACE_API Interface(UnityInterfaceGUID guid)
	{
		auto matches = [guid](UnityInterfaceGUID candidate) { return guid.m_GUIDHigh == candidate.m_GUIDHigh && guid.m_GUIDLow == candidate.m_GUIDLow; };
		if (matches(GetUnityInterfaceGUID<IUnityGraphics>())) return &graphics;
		if (renderer == kUnityGfxRendererD3D11 && matches(GetUnityInterfaceGUID<IUnityGraphicsD3D11>())) return &graphics11;
		if (renderer == kUnityGfxRendererD3D12 && matches(GetUnityInterfaceGUID<IUnityGraphicsD3D12v7>())) return &graphics12;
		return nullptr;
	}
	void Assert(bool condition, const char* description)
	{
		if (!condition) throw std::runtime_error(std::string(description) + ": " + LastError);
	}
	void Wait12()
	{
		Check12(queue12->Signal(fence12.Get(), nextFence), "Signal fixture frame fence");
		HANDLE completed = CreateEventW(nullptr, FALSE, FALSE, nullptr);
		Check12(fence12->SetEventOnCompletion(nextFence++, completed), "Wait for fixture GPU work");
		Assert(WaitForSingleObject(completed, 10000) == WAIT_OBJECT_0, "GPU fixture completion");
		CloseHandle(completed);
		transitionAllocators.clear(); transitionLists.clear();
	}
	ComPtr<ID3D11Texture2D> Texture11(DXGI_FORMAT format = DXGI_FORMAT_B8G8R8A8_UNORM, bool corners = false)
	{
		const std::uint32_t pixels[4] = {corners ? 0xffff0000u : 0x80008000u, corners ? 0xff00ff00u : 0x80008000u, corners ? 0xff0000ffu : 0x80008000u, corners ? 0xffffff00u : 0x80008000u};
		D3D11_TEXTURE2D_DESC description{};
		description.Width = description.Height = 2; description.ArraySize = description.MipLevels = 1;
		description.Format = format; description.SampleDesc.Count = 1;
		description.Usage = D3D11_USAGE_IMMUTABLE; description.BindFlags = D3D11_BIND_SHADER_RESOURCE;
		D3D11_SUBRESOURCE_DATA data{pixels, 8, 0};
		ComPtr<ID3D11Texture2D> texture;
		Check(device11->CreateTexture2D(&description, &data, &texture), "Create fixture RGBA texture");
		return texture;
	}
	ComPtr<ID3D12Resource> Texture12(DXGI_FORMAT format = DXGI_FORMAT_B8G8R8A8_UNORM, bool corners = false)
	{
		D3D12_RESOURCE_DESC description{};
		description.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D; description.Width = description.Height = 2;
		description.DepthOrArraySize = description.MipLevels = 1;
		description.Format = format; description.SampleDesc.Count = 1;
		D3D12_HEAP_PROPERTIES heap{}; heap.Type = D3D12_HEAP_TYPE_DEFAULT;
		ComPtr<ID3D12Resource> texture;
		Check12(device12->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &description,
			D3D12_RESOURCE_STATE_COPY_DEST, nullptr, IID_PPV_ARGS(&texture)), "Create D3D12 fixture texture");
		D3D12_PLACED_SUBRESOURCE_FOOTPRINT footprint{}; UINT64 bytes;
		device12->GetCopyableFootprints(&description, 0, 1, 0, &footprint, nullptr, nullptr, &bytes);
		D3D12_RESOURCE_DESC uploadDescription{};
		uploadDescription.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER; uploadDescription.Width = bytes;
		uploadDescription.Height = uploadDescription.DepthOrArraySize = uploadDescription.MipLevels = 1;
		uploadDescription.SampleDesc.Count = 1; uploadDescription.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
		heap.Type = D3D12_HEAP_TYPE_UPLOAD;
		ComPtr<ID3D12Resource> upload;
		Check12(device12->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &uploadDescription,
			D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(&upload)), "Create fixture upload");
		void* pointer; Check12(upload->Map(0, nullptr, &pointer), "Map fixture upload");
		const std::uint32_t pixels[4] = {0xffff0000u, 0xff00ff00u, 0xff0000ffu, 0xffffff00u};
		for (UINT y = 0; y < 2; y++) for (UINT x = 0; x < 2; x++)
			*reinterpret_cast<std::uint32_t*>(static_cast<char*>(pointer) + y * footprint.Footprint.RowPitch + x * 4) = corners ? pixels[y * 2 + x] : 0x80008000;
		upload->Unmap(0, nullptr);
		ComPtr<ID3D12CommandAllocator> allocator; ComPtr<ID3D12GraphicsCommandList> commands;
		Check12(device12->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator)), "Create fixture upload allocator");
		Check12(device12->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, allocator.Get(), nullptr, IID_PPV_ARGS(&commands)), "Create fixture upload commands");
		D3D12_TEXTURE_COPY_LOCATION output{}, input{};
		output.pResource = texture.Get(); input.pResource = upload.Get(); input.Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT;
		input.PlacedFootprint = footprint;
		commands->CopyTextureRegion(&output, 0, 0, 0, &input, nullptr);
		Check12(commands->Close(), "Close fixture upload");
		ID3D12CommandList* list = commands.Get(); queue12->ExecuteCommandLists(1, &list);
		states[texture.Get()] = D3D12_RESOURCE_STATE_COPY_DEST;
		Wait12();
		return texture;
	}
	void Frame()
	{
		RenderEvent(VPNW_GetDrawEventId());
		RenderEvent(VPNW_GetPresentEventId());
		if (renderer == kUnityGfxRendererD3D12) Wait12();
	}
	void NestedWindowWork(HWND ownWindow, int id)
	{
		std::promise<void> entered;
		auto started = entered.get_future();
		std::atomic<bool> finish{false};
		auto nested = std::async(std::launch::async, [&] {
			GetWindowThread()->Invoke([&] {
				entered.set_value();
				while (!finish.load()) {
					MSG message{};
					if (GetMessageW(&message, nullptr, 0, 0) > 0) DispatchMessageW(&message);
				}
			});
		});
		started.get();
		auto queued = std::async(std::launch::async, [=] { return VPNW_SetZOrder(id, 1); });
		const bool responsive = queued.wait_for(std::chrono::seconds(2)) == std::future_status::ready;
		finish.store(true); PostMessageW(ownWindow, WM_NULL, 0, 0);
		nested.get();
		Assert(responsive && queued.get() != 0, "Window work completes inside a nested modal message loop");
	}
	void CheckPresentedPixels(int id, bool corners = false, bool flipped = false)
	{
		// One-off fixture readback proves GPU shader/color/alpha output. Production never reads pixels.
		auto surface = Require(id);
		RenderSurface(surface, false);
		if (renderer == kUnityGfxRendererD3D12) Wait12();
		ComPtr<ID3D11Resource> rendered;
		surface->targetView->GetResource(&rendered);
		ComPtr<ID3D11Texture2D> texture;
		Check(rendered.As(&texture), "Read fixture backbuffer type");
		D3D11_TEXTURE2D_DESC description{}; texture->GetDesc(&description);
		description.Usage = D3D11_USAGE_STAGING; description.BindFlags = 0;
		description.CPUAccessFlags = D3D11_CPU_ACCESS_READ; description.MiscFlags = 0;
		ComPtr<ID3D11Texture2D> staging;
		Check(Device->CreateTexture2D(&description, nullptr, &staging), "Create test-only pixel readback");
		if (renderer == kUnityGfxRendererD3D12) CopyBridge.AcquireTarget(rendered.Get());
		Context->CopyResource(staging.Get(), rendered.Get());
		if (renderer == kUnityGfxRendererD3D12) {
			CopyBridge.ReleaseTarget(rendered.Get()); Context->Flush(); Wait12();
		}
		D3D11_MAPPED_SUBRESOURCE mapped{};
		Check(Context->Map(staging.Get(), 0, D3D11_MAP_READ, 0, &mapped), "Map test-only pixel readback");
		const auto pixel = *reinterpret_cast<const std::uint32_t*>(static_cast<const char*>(mapped.pData) +
			(description.Height / 2) * mapped.RowPitch + (description.Width / 2) * 4);
		bool correct = pixel == 0x80008000;
		if (corners) {
			const std::uint32_t expected[4] = {0xffff0000u, 0xff00ff00u, 0xff0000ffu, 0xffffff00u};
			correct = true;
			for (UINT y = 0; y < 2; y++) for (UINT x = 0; x < 2; x++) {
				const auto observed = *reinterpret_cast<const std::uint32_t*>(static_cast<const char*>(mapped.pData) +
					(y ? description.Height - 8 : 8) * mapped.RowPitch + (x ? description.Width - 8 : 8) * 4);
				correct &= observed == expected[(flipped ? 1 - y : y) * 2 + x];
			}
		}
		Context->Unmap(staging.Get(), 0);
		Assert(correct, corners ? "Native output preserves four-corner orientation and explicit vertical flip" : "GPU output retains exact stored SDR green128/alpha128 premultiplied bytes");
	}
	void Roundtrip(IUnityInterfaces& interfaces, IUnknown* texture)
	{
		UnityPluginLoad(&interfaces);
		const int id = VPNW_CreateWindow(-160, -96, 320, 240);
		Assert(id != 0, "Create borderless window");
		Assert(VPNW_ConfigureWindow(id, -160, -96, 320, 240, 0, 1) != 0, "Configure signed desktop rectangle");
		VPNW_Diagnostics diagnostics{}; diagnostics.structBytes = sizeof(diagnostics);
		Assert(VPNW_GetDiagnostics(id, &diagnostics) != 0, "Read hidden window diagnostics");
		Assert(diagnostics.left == -160 && diagnostics.top == -96 && diagnostics.windowWidth == 320 && diagnostics.windowHeight == 240,
			"Exact physical signed window rectangle");
		std::cout << "hidden dpi=" << diagnostics.dpi << " visible=" << diagnostics.visible << " topmost=" << diagnostics.topmost << "\n";
		Assert(diagnostics.dpi >= 96 && diagnostics.visible == 0, "DPI/visibility/z-order");
		Assert(VPNW_SetPlacementMode(id, 1) != 0, "Enable native placement interaction");
		const HWND ownWindow = Require(id)->window.load();
		GetWindowThread()->Invoke([&] {
			RECT bounds{}; GetWindowRect(ownWindow, &bounds);
			Assert(SendMessageW(ownWindow, WM_NCHITTEST, 0, MAKELPARAM((bounds.left + bounds.right) / 2, (bounds.top + bounds.bottom) / 2)) == HTCAPTION, "Drag the placement window body");
			Assert(SendMessageW(ownWindow, WM_NCHITTEST, 0, MAKELPARAM(bounds.left + 2, bounds.top + 2)) == HTTOPLEFT, "Resize the placement window corner");
		});
		Assert(VPNW_SetPlacementMode(id, 0) != 0, "Disable placement during normal playback");
		Assert(VPNW_SetTexture(id, texture, 0) != 0, "Retain GPU source texture");
		Assert(VPNW_ConfigureWindow(id, 20, 20, 64, 64, 1, 0) != 0, "Show temporary own output window");
		Assert(VPNW_SetPlacementMode(id, 1) && VPNW_SetPlacementMode(id, 0) && IsWindowVisible(ownWindow), "Placement toggles preserve visible window style");
		NestedWindowWork(ownWindow, id);
		Assert(VPNW_SetZOrder(id, 2) != 0 && VPNW_GetDiagnostics(id, &diagnostics) != 0 && diagnostics.topmost == 1, "Topmost visible output");
		Assert(VPNW_ConfigureWindow(id, 20, 20, 64, 64, 1, 0) != 0 && VPNW_GetDiagnostics(id, &diagnostics) != 0 && diagnostics.topmost == 0,
			"Configure clears a topmost state set through z-order");
		Assert(VPNW_SetZOrder(id, 2) != 0 && VPNW_GetDiagnostics(id, &diagnostics) != 0 && diagnostics.topmost == 1, "Topmost visible output again");
		Assert(VPNW_SetZOrder(id, 3) != 0 && VPNW_GetDiagnostics(id, &diagnostics) != 0 && diagnostics.topmost == 0, "Clear topmost visible output");
		Assert(VPNW_SetZOrder(id, 0) != 0 && VPNW_SetZOrder(id, 1) != 0, "Send back and bring front");
		for (int attempt = 0; attempt < 20; attempt++) {
			Frame();
			Assert(VPNW_GetDiagnostics(id, &diagnostics) != 0, "Read nonblocking presentation attempt");
			if (diagnostics.presentedFrames > 0) break;
			Sleep(10); // A busy DWM may reject an initial DO_NOT_WAIT present; bounded fixture retry only.
		}
		Assert(VPNW_GetDiagnostics(id, &diagnostics) != 0, "Read presented output diagnostics");
		std::cout << "presentation renderer=" << diagnostics.graphicsRenderer << " ready=" << diagnostics.deviceReady <<
			" visible=" << diagnostics.visible << " source=" << diagnostics.sourceWidth << 'x' << diagnostics.sourceHeight <<
			" presents=" << diagnostics.presentedFrames << " skips=" << diagnostics.skippedFrames << " hr=" << diagnostics.lastResult << "\n";
		Assert((diagnostics.presentedFrames > 0 || diagnostics.lastResult == DXGI_STATUS_OCCLUDED) &&
			diagnostics.sourceWidth == 2 && diagnostics.sourceHeight == 2, "GPU texture presented or desktop explicitly occluded");
		Assert(diagnostics.premultipliedAlpha == 1 && diagnostics.lastResult >= 0, "DirectComposition premultiplied-alpha output");
		CheckPresentedPixels(id);
		Assert(VPNW_ConfigureWindow(id, 20, 20, 96, 48, 1, 0) != 0, "Resize own output");
		Frame(); Frame();
		Assert(VPNW_GetDiagnostics(id, &diagnostics) != 0 && diagnostics.windowWidth == 96 && diagnostics.windowHeight == 48, "Resize presentation path");
		const auto before = diagnostics.presentedFrames;
		Assert(VPNW_SetTexture(id, nullptr, 0) != 0, "Detach source");
		Frame();
		Assert(VPNW_GetDiagnostics(id, &diagnostics) != 0 && diagnostics.presentedFrames == before, "No texture means no presentation");
		Assert(VPNW_DestroyWindow(id) != 0 && VPNW_DestroyWindow(id) != 0, "Idempotent destruction");
		Frame(); // Queued events can outlive the surface; no recycled ID/pointer is dereferenced.
		if (renderer == kUnityGfxRendererD3D12) {
			const int pending = VPNW_CreateWindow(20, 20, 32, 32);
			Assert(pending != 0 && VPNW_SetTexture(pending, texture, 0) != 0 &&
				VPNW_ConfigureWindow(pending, 20, 20, 32, 32, 1, 0) != 0, "Create pending-frame destruction fixture");
			RenderEvent(VPNW_GetDrawEventId()); RenderEvent(VPNW_GetPresentEventId());
			Assert(VPNW_SetTexture(pending, nullptr, 0) != 0 && VPNW_DestroyWindow(pending) != 0, "Detach and destroy before GPU frame fence");
			Assert(!RetiredSurfaces.empty(), "Detached presentation resources retained until fence");
			Assert(VPNW_GetPendingResourceCount() >= 2, "Pending source and presentation resource diagnostics");
			Wait12(); Frame();
			Assert(RetiredSurfaces.empty(), "Completed presentation resources collected");
			Assert(VPNW_GetPendingResourceCount() == 0, "Cleanup events drain pending resource diagnostics");
		}
		UnityPluginUnload();
		std::cout << "Passed native backend " << static_cast<int>(renderer) << " roundtrip/pixel output; presents=" << before << ".\n";
	}
	void Orientation(IUnityInterfaces& interfaces, IUnknown* texture)
	{
		UnityPluginLoad(&interfaces);
		const int id = VPNW_CreateWindow(20, 20, 64, 64);
		Assert(id != 0 && VPNW_ConfigureWindow(id, 20, 20, 64, 64, 1, 0), "Create orientation fixture");
		for (int flip : {0, 1}) {
			Assert(VPNW_SetTexture(id, texture, flip) != 0, "Set corner texture");
			Frame(); CheckPresentedPixels(id, true, flip != 0);
		}
		VPNW_DestroyWindow(id); Frame(); UnityPluginUnload();
		std::cout << "Passed four-corner orientation for backend " << static_cast<int>(renderer) << ".\n";
	}
}

int main()
{
	try {
		Test::graphics.GetRenderer = Test::GetRenderer; Test::graphics.RegisterDeviceEventCallback = Test::Register;
		Test::graphics.UnregisterDeviceEventCallback = Test::Unregister; Test::graphics.ReserveEventIDRange = Test::Reserve;
		Test::graphics11.GetDevice = Test::Device11;
		Test::graphics12.GetDevice = Test::Device12; Test::graphics12.GetCommandQueue = Test::Queue12;
		Test::graphics12.GetFrameFence = Test::Fence12; Test::graphics12.GetNextFrameFenceValue = Test::NextFence;
		Test::graphics12.ExecuteCommandList = Test::Execute; Test::graphics12.ConfigureEvent = Test::Configure;
		IUnityInterfaces interfaces{}; interfaces.GetInterface = Test::Interface;
		const std::uint32_t capabilities = VPNW_CapabilityWindows | VPNW_CapabilityPlacement | VPNW_CapabilityTopmost | VPNW_CapabilityTransparency;
		Test::Assert(VPNW_GetCapabilities() == capabilities, "Windows reports window, placement, topmost and transparency capabilities");
		VPNW_Pump(); // No-op on Windows; must be safe to call before any window exists.
		Check(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
			nullptr, 0, D3D11_SDK_VERSION, &Test::device11, nullptr, nullptr), "Create D3D11 test device");
		auto texture11 = Test::Texture11();
		Test::Roundtrip(interfaces, texture11.Get());
		auto corners11 = Test::Texture11(DXGI_FORMAT_B8G8R8A8_UNORM, true);
		Test::Orientation(interfaces, corners11.Get());
		for (auto format : {DXGI_FORMAT_R8G8B8A8_TYPELESS, DXGI_FORMAT_B8G8R8A8_TYPELESS, DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, DXGI_FORMAT_B8G8R8A8_UNORM_SRGB}) {
			std::cout << "D3D11 source format=" << static_cast<int>(format) << "\n";
			auto typedFixture = Test::Texture11(format);
			Test::Roundtrip(interfaces, typedFixture.Get());
		}
		Test::renderer = kUnityGfxRendererD3D12;
		Check12(D3D12CreateDevice(nullptr, D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&Test::device12)), "Create D3D12 test device");
		D3D12_COMMAND_QUEUE_DESC queue{}; queue.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;
		Check12(Test::device12->CreateCommandQueue(&queue, IID_PPV_ARGS(&Test::queue12)), "Create D3D12 test queue");
		Check12(Test::device12->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&Test::fence12)), "Create D3D12 test fence");
		auto texture12 = Test::Texture12();
		Test::Roundtrip(interfaces, texture12.Get());
		auto corners12 = Test::Texture12(DXGI_FORMAT_B8G8R8A8_UNORM, true);
		Test::Orientation(interfaces, corners12.Get());
		for (auto format : {DXGI_FORMAT_R8G8B8A8_TYPELESS, DXGI_FORMAT_B8G8R8A8_TYPELESS, DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, DXGI_FORMAT_B8G8R8A8_UNORM_SRGB}) {
			std::cout << "D3D12 source format=" << static_cast<int>(format) << "\n";
			auto typedFixture = Test::Texture12(format);
			Test::Roundtrip(interfaces, typedFixture.Get());
		}
		return 0;
	}
	catch (const std::exception& error) {
		std::cerr << error.what() << "\n";
		VPNW_DestroyAll();
		UnityPluginUnload();
		return 1;
	}
}
