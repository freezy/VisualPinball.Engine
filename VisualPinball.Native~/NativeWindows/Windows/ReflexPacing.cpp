// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: MIT

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include "ReflexPacing.h"
#include <Windows.h>
#include <d3d12.h>
#include <wrl/client.h>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <mutex>

namespace vpnw::reflex
{
	namespace
	{
		// Public NVAPI ABI from NVIDIA/nvapi (nvapi.h and nvapi_interface.h).
		// Resolve the driver copy on demand so the plugin also loads without an NVIDIA driver.
		struct SleepModeParams
		{
			std::uint32_t version;
			std::uint8_t lowLatencyMode;
			std::uint8_t lowLatencyBoost;
			std::uint32_t minimumIntervalUs;
			std::uint8_t useMarkersToOptimize;
			std::uint8_t reserved[31];
		};
		static_assert(sizeof(SleepModeParams) == 44 && offsetof(SleepModeParams, minimumIntervalUs) == 8);

		constexpr unsigned InitializeId = 0x0150e828;
		constexpr unsigned SetSleepModeId = 0xac1ca9e0;
		constexpr unsigned SleepId = 0x852cd1d2;
		constexpr int Ok = 0;
		constexpr int WaitingForDevice = 1;
		constexpr int DriverUnavailable = 2;

		using QueryInterface = void* (__cdecl*)(unsigned);
		using Initialize = int (__cdecl*)();
		using SetSleepMode = int (__cdecl*)(IUnknown*, SleepModeParams*);
		using SleepFrame = int (__cdecl*)(IUnknown*);

		std::mutex Mutex;
		Microsoft::WRL::ComPtr<ID3D12Device> Device;
		HMODULE Module = nullptr;
		SetSleepMode SetMode = nullptr;
		SleepFrame SleepApi = nullptr;
		bool ModeSet = false;
		int Result = WaitingForDevice;

		bool LoadDriver()
		{
			if (Module) return true;
			auto module = LoadLibraryExW(L"nvapi64.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
			if (!module) return false;

			const auto address = GetProcAddress(module, "nvapi_QueryInterface");
			QueryInterface query = nullptr;
			static_assert(sizeof(query) == sizeof(address));
			std::memcpy(&query, &address, sizeof(query));
			if (query) {
				const auto initialize = reinterpret_cast<Initialize>(query(InitializeId));
				const auto setMode = reinterpret_cast<SetSleepMode>(query(SetSleepModeId));
				const auto sleep = reinterpret_cast<SleepFrame>(query(SleepId));
				if (initialize && setMode && sleep && initialize() == Ok) {
					// Unity can keep using the device after unloading this plugin. Keep NVAPI
					// loaded for the process; unloading here can crash later DX12 calls.
					Module = module;
					SetMode = setMode;
					SleepApi = sleep;
					return true;
				}
			}
			FreeLibrary(module);
			return false;
		}

		int SetModeEnabled(bool enabled)
		{
			SleepModeParams parameters{};
			parameters.version = sizeof(parameters) | (1u << 16);
			parameters.lowLatencyMode = enabled ? 1 : 0;
			// Unity owns Present. Use driver pacing without passing approximate frame markers.
			return SetMode(Device.Get(), &parameters);
		}

		int StopLocked()
		{
			Result = WaitingForDevice;
			if (!ModeSet) return Ok;
			const auto status = SetModeEnabled(false);
			if (status == Ok) ModeSet = false;
			return status;
		}
	}

	void SetDevice(ID3D12Device* device)
	{
		std::lock_guard<std::mutex> lock(Mutex);
		if (Device.Get() == device) return;
		StopLocked();
		ModeSet = false;
		Device = device;
	}

	int Sleep()
	{
		std::lock_guard<std::mutex> lock(Mutex);
		if (!Device) return WaitingForDevice;
		if (Result == WaitingForDevice) {
			Result = LoadDriver() ? SetModeEnabled(true) : DriverUnavailable;
			if (Result == Ok) ModeSet = true;
		}
		if (Result != Ok) return Result;
		const auto status = SleepApi(Device.Get());
		if (status != Ok) {
			StopLocked();
			Result = status;
		}
		return Result;
	}

	int Stop()
	{
		std::lock_guard<std::mutex> lock(Mutex);
		return StopLocked();
	}

}
