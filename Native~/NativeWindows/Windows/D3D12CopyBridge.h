// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#pragma once
#include <Windows.h>
#include <d3d12.h>
#include <d3d11on12.h>
#include <wrl/client.h>
#include "IUnityGraphicsD3D12.h"
#include <array>
#include <memory>
#include <mutex>
#include <vector>

namespace vpnw
{
	// A source and its private GPU snapshot. Copies are submitted through Unity's state tracker.
	// The snapshot is then read by D3D11On12 on the same queue after Unity flushes its workers.
	struct D3D12CopySource
	{
		Microsoft::WRL::ComPtr<ID3D12Resource> source;
		Microsoft::WRL::ComPtr<ID3D12Resource> snapshot;
		Microsoft::WRL::ComPtr<ID3D11Resource> wrapped;
		struct CommandSlot
		{
			Microsoft::WRL::ComPtr<ID3D12CommandAllocator> allocator;
			Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList> commands;
			UINT64 fence = 0;
		};
		std::array<CommandSlot, 3> slots;
		UINT64 lastFence = 0;
		UINT width = 0, height = 0;
		DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
		bool copied = false;
		UnityGraphicsD3D12ResourceState unityState{};
	};

	class D3D12CopyBridge
	{
		IUnityGraphicsD3D12v7* _unity = nullptr;
		Microsoft::WRL::ComPtr<ID3D12Device> _device;
		Microsoft::WRL::ComPtr<ID3D11On12Device> _on12;
		std::mutex _retiredMutex;
		std::vector<std::shared_ptr<D3D12CopySource>> _retired;
		void Prepare(D3D12CopySource& source);
	public:
		void SetUnity(IUnityGraphicsD3D12v7* unity) { _unity = unity; }
		bool Supported() const { return _unity != nullptr; }
		bool DeviceReady() const { return _device != nullptr; }
		void InitializeDevice(); // Render thread: no direct queue access.
		void InitializePresentation(ID3D11Device** device, ID3D11DeviceContext** context); // Submission thread only.
		std::shared_ptr<D3D12CopySource> Source(void* nativeTexture);
		bool RecordCopy(D3D12CopySource& source);
		ID3D11Resource* Acquire(D3D12CopySource& source);
		void Release(D3D12CopySource& source);
		IUnknown* Queue() { return _unity->GetCommandQueue(); } // Submission-thread event only.
		Microsoft::WRL::ComPtr<ID3D11Resource> WrapTarget(ID3D12Resource* resource);
		void AcquireTarget(ID3D11Resource* resource);
		void ReleaseTarget(ID3D11Resource* resource);
		UINT64 FrameFenceValue() { return _unity->GetNextFrameFenceValue(); }
		bool Completed(UINT64 value) { return _unity->GetFrameFence()->GetCompletedValue() >= value; }
		void Retire(std::shared_ptr<D3D12CopySource> source);
		int PendingResourceCount();
		void Collect(); // Rendering/submission thread only.
		void Shutdown();
	};
}
