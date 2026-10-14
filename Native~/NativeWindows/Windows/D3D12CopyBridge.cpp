// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#include "D3D12CopyBridge.h"
#include <sstream>
#include <algorithm>
#include <stdexcept>

using Microsoft::WRL::ComPtr;

namespace vpnw
{
	namespace
	{
		void Check12(HRESULT result, const char* operation)
		{
			if (FAILED(result)) {
				std::ostringstream message;
				message << operation << " failed (HRESULT 0x" << std::hex << static_cast<UINT>(result) << ").";
				throw std::runtime_error(message.str());
			}
		}
		DXGI_FORMAT TypedFormat(DXGI_FORMAT format)
		{
			switch (format) {
				case DXGI_FORMAT_R8G8B8A8_TYPELESS: case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return DXGI_FORMAT_R8G8B8A8_UNORM;
				case DXGI_FORMAT_B8G8R8A8_TYPELESS: case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB: return DXGI_FORMAT_B8G8R8A8_UNORM;
				case DXGI_FORMAT_R16G16B16A16_TYPELESS: return DXGI_FORMAT_R16G16B16A16_FLOAT;
				case DXGI_FORMAT_R32G32B32A32_TYPELESS: return DXGI_FORMAT_R32G32B32A32_FLOAT;
				default: return format;
			}
		}
		D3D12_RESOURCE_BARRIER Barrier(ID3D12Resource* resource, D3D12_RESOURCE_STATES before, D3D12_RESOURCE_STATES after)
		{
			D3D12_RESOURCE_BARRIER barrier{};
			barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
			barrier.Transition.pResource = resource;
			barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
			barrier.Transition.StateBefore = before;
			barrier.Transition.StateAfter = after;
			return barrier;
		}
	}

	void D3D12CopyBridge::InitializeDevice()
	{
		if (_device) return;
		if (!_unity || !_unity->GetDevice()) throw std::runtime_error("Unity D3D12 v7 device is unavailable.");
		_device = _unity->GetDevice();
	}

	void D3D12CopyBridge::InitializePresentation(ID3D11Device** device, ID3D11DeviceContext** context)
	{
		InitializeDevice();
		ID3D12CommandQueue* queue = _unity->GetCommandQueue();
		if (!queue) throw std::runtime_error("D3D12 presentation must execute in the configured submission-thread event.");
		IUnknown* queues[] = {queue};
		const D3D_FEATURE_LEVEL levels[] = {D3D_FEATURE_LEVEL_11_0};
		Check12(D3D11On12CreateDevice(_device.Get(), D3D11_CREATE_DEVICE_BGRA_SUPPORT,
			levels, 1, queues, 1, 0, device, context, nullptr), "Create same-queue D3D11On12 presenter");
		Check12((*device)->QueryInterface(IID_PPV_ARGS(&_on12)), "Query D3D11On12 interface");
	}

	std::shared_ptr<D3D12CopySource> D3D12CopyBridge::Source(void* nativeTexture)
	{
		if (!nativeTexture) return nullptr;
		auto result = std::make_shared<D3D12CopySource>();
		Check12(static_cast<IUnknown*>(nativeTexture)->QueryInterface(IID_PPV_ARGS(&result->source)), "Acquire Unity D3D12 texture");
		const auto description = result->source->GetDesc();
		if (description.Dimension != D3D12_RESOURCE_DIMENSION_TEXTURE2D || description.DepthOrArraySize != 1 ||
			description.Width > 16384 || description.Height > 16384)
			throw std::invalid_argument("D3D12 output requires a single 2D texture no larger than16384 pixels.");
		result->width = static_cast<UINT>(description.Width); result->height = description.Height;
		result->format = description.Format;
		return result;
	}

	void D3D12CopyBridge::Prepare(D3D12CopySource& source)
	{
		if (source.snapshot) return;
		ComPtr<ID3D12Device> owner;
		Check12(source.source->GetDevice(IID_PPV_ARGS(&owner)), "Read Unity texture device");
		if (owner.Get() != _device.Get()) throw std::invalid_argument("Texture belongs to a different D3D12 device.");
		auto description = source.source->GetDesc();
		description.SampleDesc.Count = 1; description.SampleDesc.Quality = 0;
		description.MipLevels = 1; description.Flags = D3D12_RESOURCE_FLAG_NONE;
		// Compatible format-family copies retain the stored bytes. A typed UNORM snapshot
		// avoids illegal UNORM views on typed sRGB resources, including Unity ARGB32 targets.
		description.Format = TypedFormat(description.Format);
		D3D12_HEAP_PROPERTIES heap{};
		heap.Type = D3D12_HEAP_TYPE_DEFAULT; heap.CreationNodeMask = 1; heap.VisibleNodeMask = 1;
		Check12(_device->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &description,
			D3D12_RESOURCE_STATE_COMMON, nullptr, IID_PPV_ARGS(&source.snapshot)), "Create private GPU window snapshot");
		std::array<D3D12CopySource::CommandSlot, 3> slots;
		for (auto& slot : slots) {
			Check12(_device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&slot.allocator)), "Create GPU copy allocator");
			Check12(_device->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, slot.allocator.Get(), nullptr, IID_PPV_ARGS(&slot.commands)), "Create GPU copy command list");
			Check12(slot.commands->Close(), "Close initial copy command list");
		}
		source.slots = std::move(slots);
		source.unityState.resource = source.source.Get();
		source.unityState.expected = source.unityState.current = source.source->GetDesc().SampleDesc.Count > 1
			? D3D12_RESOURCE_STATE_RESOLVE_SOURCE : D3D12_RESOURCE_STATE_COPY_SOURCE;
	}

	bool D3D12CopyBridge::RecordCopy(D3D12CopySource& source)
	{
		InitializeDevice();
		try { Prepare(source); } catch (...) { source.snapshot.Reset(); throw; }
		const UINT64 completed = _unity->GetFrameFence()->GetCompletedValue();
		D3D12CopySource::CommandSlot* available = nullptr;
		for (auto& slot : source.slots) if (slot.fence <= completed) { available = &slot; break; }
		if (!available) return false; // Never block Unity waiting for a busy copy allocator.
		Check12(available->allocator->Reset(), "Reset GPU copy allocator");
		Check12(available->commands->Reset(available->allocator.Get(), nullptr), "Reset GPU copy command list");
		const bool multisampled = source.source->GetDesc().SampleDesc.Count > 1;
		const auto destinationState = multisampled ? D3D12_RESOURCE_STATE_RESOLVE_DEST : D3D12_RESOURCE_STATE_COPY_DEST;
		auto before = Barrier(source.snapshot.Get(), D3D12_RESOURCE_STATE_COMMON, destinationState);
		available->commands->ResourceBarrier(1, &before);
		if (multisampled) {
			available->commands->ResolveSubresource(source.snapshot.Get(), 0, source.source.Get(), 0, TypedFormat(source.format));
		}
		else {
			D3D12_TEXTURE_COPY_LOCATION destination{}, input{};
			destination.pResource = source.snapshot.Get(); destination.Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
			input.pResource = source.source.Get(); input.Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
			available->commands->CopyTextureRegion(&destination, 0, 0, 0, &input, nullptr);
		}
		auto after = Barrier(source.snapshot.Get(), destinationState, D3D12_RESOURCE_STATE_COMMON);
		available->commands->ResourceBarrier(1, &after);
		Check12(available->commands->Close(), "Close GPU copy command list");
		// Source state declaration is immutable after preparation; queued Unity workers may still read it.
		// Unity orders this after preceding compositor work and inserts/tracks the source barriers.
		available->fence = _unity->ExecuteCommandList(available->commands.Get(), 1, &source.unityState);
		source.lastFence = (std::max)(source.lastFence, available->fence);
		source.copied = true;
		return true;
	}

	ID3D11Resource* D3D12CopyBridge::Acquire(D3D12CopySource& source)
	{
		if (!_on12 || !source.copied) return nullptr;
		source.lastFence = (std::max)(source.lastFence, FrameFenceValue());
		if (!source.wrapped) {
			D3D11_RESOURCE_FLAGS flags{}; flags.BindFlags = D3D11_BIND_SHADER_RESOURCE;
			Check12(_on12->CreateWrappedResource(source.snapshot.Get(), &flags,
				D3D12_RESOURCE_STATE_COMMON, D3D12_RESOURCE_STATE_COMMON, IID_PPV_ARGS(&source.wrapped)), "Wrap private GPU snapshot");
		}
		ID3D11Resource* resource = source.wrapped.Get();
		_on12->AcquireWrappedResources(&resource, 1);
		return resource;
	}

	void D3D12CopyBridge::Release(D3D12CopySource& source)
	{
		if (!_on12 || !source.wrapped) return;
		ID3D11Resource* resource = source.wrapped.Get();
		_on12->ReleaseWrappedResources(&resource, 1);
	}

	void D3D12CopyBridge::Retire(std::shared_ptr<D3D12CopySource> source)
	{
		if (!source) return;
		std::lock_guard<std::mutex> lock(_retiredMutex);
		_retired.push_back(std::move(source));
	}

	void D3D12CopyBridge::Collect()
	{
		if (!_unity) return;
		const UINT64 completed = _unity->GetFrameFence()->GetCompletedValue();
		std::lock_guard<std::mutex> lock(_retiredMutex);
		for (auto item = _retired.begin(); item != _retired.end();) {
			if ((*item)->lastFence <= completed) item = _retired.erase(item);
			else ++item;
		}
	}

	int D3D12CopyBridge::PendingResourceCount()
	{
		std::lock_guard<std::mutex> lock(_retiredMutex);
		return static_cast<int>(_retired.size());
	}

	void D3D12CopyBridge::Shutdown()
	{
		std::lock_guard<std::mutex> lock(_retiredMutex);
		_retired.clear(); _on12.Reset(); _device.Reset();
	}

	ComPtr<ID3D11Resource> D3D12CopyBridge::WrapTarget(ID3D12Resource* resource)
	{
		ComPtr<ID3D11Resource> result;
		D3D11_RESOURCE_FLAGS flags{}; flags.BindFlags = D3D11_BIND_RENDER_TARGET;
		Check12(_on12->CreateWrappedResource(resource, &flags, D3D12_RESOURCE_STATE_COMMON,
			D3D12_RESOURCE_STATE_COMMON, IID_PPV_ARGS(&result)), "Wrap composition backbuffer");
		return result;
	}
	void D3D12CopyBridge::AcquireTarget(ID3D11Resource* resource) { _on12->AcquireWrappedResources(&resource, 1); }
	void D3D12CopyBridge::ReleaseTarget(ID3D11Resource* resource) { _on12->ReleaseWrappedResources(&resource, 1); }
}
