// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT

// Low-latency device output for the central PCM graph. On Windows, an event-driven WASAPI shared-mode
// stream renders the graph on its own MMCSS thread and keeps only one engine period plus a small margin
// queued, instead of going through Unity's mixer ring and FMOD's own WASAPI buffer. Other platforms
// report no output, so the managed side keeps Unity's carrier.

#include "PcmRenderer.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <vector>

namespace {
enum Metric { State, Callbacks, PeriodMs, BufferMs, QueuedMs, MarginMs, Glitches, Reopens, LastError, Rate, FirstUnderrunS, LastUnderrunS, MaxWakeGapMs, MetricCount };
}

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <avrt.h>
#include <ksmedia.h>
#include <string>
#include <thread>

namespace {
template <typename T> void Release(T*& value) { if (value) { value->Release(); value = nullptr; } }

struct Stream {
	IAudioClient* client = nullptr;
	IAudioRenderClient* render = nullptr;
	std::wstring device;
	UINT32 bufferFrames = 0, periodFrames = 0;
	void Close() {
		if (client) client->Stop();
		Release(render); Release(client); device.clear(); bufferFrames = periodFrames = 0;
	}
};

struct Output {
	void* context = nullptr;
	int rate = 0;
	std::atomic<int> paused{0};
	std::atomic<int> opened{0}; // 1 when the first device open succeeded, -1 when it failed
	std::array<std::atomic<float>, MetricCount> metrics{};
	HANDLE stop = nullptr, ready = nullptr;
	std::thread thread;
	UINT32 margin = 0; // render thread only: frames kept queued beyond one engine period
	LARGE_INTEGER frequency{}, started{}, lastWake{};
	double Seconds(const LARGE_INTEGER& from, const LARGE_INTEGER& to) const { return double(to.QuadPart - from.QuadPart) / frequency.QuadPart; }
	int warmup = 0; // render thread only: callbacks left before underruns count (the engine drains the prefill on start)
	int settle = 0; // render thread only: callbacks left before underruns widen the margin (new streams underrun while the engine settles)

	void Set(Metric metric, float value) { metrics[metric].store(value, std::memory_order_relaxed); }
	void Add(Metric metric) { metrics[metric].store(metrics[metric].load(std::memory_order_relaxed) + 1, std::memory_order_relaxed); }
	float Ms(UINT32 frames) const { return 1000.f * frames / rate; }

	void Fill(Stream& stream, UINT32 frames)
	{
		BYTE* data = nullptr;
		if (frames == 0 || FAILED(stream.render->GetBuffer(frames, &data))) return;
		VpePcmRender(context, reinterpret_cast<float*>(data), static_cast<int>(frames), 2, rate, paused.load(std::memory_order_relaxed));
		stream.render->ReleaseBuffer(frames, 0);
		Add(Callbacks);
	}

	HRESULT Open(IMMDeviceEnumerator* enumerator, HANDLE wake, Stream& stream)
	{
		IMMDevice* device = nullptr;
		HRESULT hr = enumerator->GetDefaultAudioEndpoint(eRender, eConsole, &device);
		if (SUCCEEDED(hr)) {
			LPWSTR id = nullptr;
			if (SUCCEEDED(device->GetId(&id))) { stream.device = id; CoTaskMemFree(id); }
			hr = device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(&stream.client));
			device->Release();
		}
		// Float stereo at the graph's rate; Windows converts to the engine's mix format when they differ.
		WAVEFORMATEXTENSIBLE format{};
		format.Format.wFormatTag = WAVE_FORMAT_EXTENSIBLE;
		format.Format.nChannels = 2;
		format.Format.nSamplesPerSec = static_cast<DWORD>(rate);
		format.Format.wBitsPerSample = 32;
		format.Format.nBlockAlign = 8;
		format.Format.nAvgBytesPerSec = static_cast<DWORD>(rate) * 8;
		format.Format.cbSize = sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX);
		format.Samples.wValidBitsPerSample = 32;
		format.dwChannelMask = SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT;
		format.SubFormat = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
		REFERENCE_TIME period = 0, minimum = 0;
		if (SUCCEEDED(hr)) hr = stream.client->GetDevicePeriod(&period, &minimum);
		if (SUCCEEDED(hr)) hr = stream.client->Initialize(AUDCLNT_SHAREMODE_SHARED,
			AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY | AUDCLNT_STREAMFLAGS_NOPERSIST,
			0, 0, &format.Format, nullptr);
		if (SUCCEEDED(hr)) hr = stream.client->SetEventHandle(wake);
		if (SUCCEEDED(hr)) hr = stream.client->GetBufferSize(&stream.bufferFrames);
		if (SUCCEEDED(hr)) hr = stream.client->GetService(__uuidof(IAudioRenderClient), reinterpret_cast<void**>(&stream.render));
		if (SUCCEEDED(hr)) {
			stream.periodFrames = static_cast<UINT32>((period * rate + 9999999) / 10000000);
			// 3 ms beyond the period absorbs wake-up jitter; underruns widen it (see Run).
			margin = std::min(static_cast<UINT32>(rate * 3 / 1000), stream.bufferFrames > stream.periodFrames ? stream.bufferFrames - stream.periodFrames : 0);
			Fill(stream, std::min(stream.bufferFrames, stream.periodFrames + margin));
			warmup = 4; settle = stream.periodFrames ? static_cast<int>(2 * rate / stream.periodFrames) : 0; lastWake.QuadPart = 0;
			hr = stream.client->Start();
		}
		if (FAILED(hr)) { stream.Close(); Set(LastError, static_cast<float>(static_cast<unsigned>(hr))); return hr; }
		Set(PeriodMs, Ms(stream.periodFrames)); Set(BufferMs, Ms(stream.bufferFrames)); Set(MarginMs, Ms(margin));
		return hr;
	}

	static bool DefaultChanged(IMMDeviceEnumerator* enumerator, const std::wstring& current)
	{
		IMMDevice* device = nullptr;
		if (FAILED(enumerator->GetDefaultAudioEndpoint(eRender, eConsole, &device))) return true;
		LPWSTR id = nullptr; bool changed = true;
		if (SUCCEEDED(device->GetId(&id))) { changed = current != id; CoTaskMemFree(id); }
		device->Release();
		return changed;
	}

	void Run()
	{
		QueryPerformanceFrequency(&frequency); QueryPerformanceCounter(&started);
		const HRESULT com = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
		DWORD task = 0;
		HANDLE mmcss = AvSetMmThreadCharacteristicsW(L"Pro Audio", &task);
		HANDLE wake = CreateEventW(nullptr, FALSE, FALSE, nullptr);
		IMMDeviceEnumerator* enumerator = nullptr;
		HRESULT hr = wake ? CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&enumerator)) : E_FAIL;
		Stream stream;
		if (SUCCEEDED(hr)) hr = Open(enumerator, wake, stream);
		opened.store(SUCCEEDED(hr) ? 1 : -1, std::memory_order_release);
		SetEvent(ready);
		std::vector<float> scratch(static_cast<size_t>(rate / 100) * 2);
		ULONGLONG nextDeviceCheck = GetTickCount64() + 1000, nextRetry = 0;
		bool running = SUCCEEDED(hr);
		while (running) {
			const ULONGLONG now = GetTickCount64();
			if (!stream.client) {
				if (now >= nextRetry) {
					if (SUCCEEDED(Open(enumerator, wake, stream))) Add(Reopens);
					else nextRetry = now + 500;
				}
				if (!stream.client) {
					// No device: keep the graph's clock running so one-shots finish and the ROM stream drains.
					Set(State, 2);
					if (WaitForSingleObject(stop, 10) == WAIT_OBJECT_0) break;
					VpePcmRender(context, scratch.data(), rate / 100, 2, rate, paused.load(std::memory_order_relaxed));
					continue;
				}
			}
			Set(State, 1);
			HANDLE handles[] = { stop, wake };
			const DWORD signaled = WaitForMultipleObjects(2, handles, FALSE, 200);
			if (signaled == WAIT_OBJECT_0) break;
			UINT32 padding = 0;
			hr = signaled == WAIT_TIMEOUT ? AUDCLNT_E_DEVICE_INVALIDATED : stream.client->GetCurrentPadding(&padding);
			if (SUCCEEDED(hr)) {
				LARGE_INTEGER wokeAt; QueryPerformanceCounter(&wokeAt);
				if (lastWake.QuadPart && warmup == 0) metrics[MaxWakeGapMs].store(std::max(metrics[MaxWakeGapMs].load(std::memory_order_relaxed), float(1000 * Seconds(lastWake, wokeAt))), std::memory_order_relaxed);
				lastWake = wokeAt;
				if (settle > 0) --settle;
				if (warmup > 0) --warmup;
				else if (padding == 0) {
					// The engine drained everything we queued: count it and, once the stream settled, keep a little more queued from now on.
					Add(Glitches);
					if (metrics[FirstUnderrunS].load(std::memory_order_relaxed) == 0) Set(FirstUnderrunS, float(Seconds(started, wokeAt)));
					Set(LastUnderrunS, float(Seconds(started, wokeAt)));
					if (settle == 0) {
						const UINT32 room = stream.bufferFrames > stream.periodFrames ? stream.bufferFrames - stream.periodFrames : 0;
						margin = std::min(margin + static_cast<UINT32>(rate / 1000), room);
						Set(MarginMs, Ms(margin));
					}
				}
				const UINT32 target = std::min(stream.bufferFrames, stream.periodFrames + margin);
				const UINT32 frames = target > padding ? target - padding : 0;
				Fill(stream, frames);
				Set(QueuedMs, Ms(padding + frames));
			}
			if (FAILED(hr) || (now >= nextDeviceCheck && DefaultChanged(enumerator, stream.device))) {
				// Device lost or the user picked another default output: follow it like Unity does.
				if (FAILED(hr)) Set(LastError, static_cast<float>(static_cast<unsigned>(hr)));
				stream.Close(); nextRetry = 0;
			}
			if (now >= nextDeviceCheck) nextDeviceCheck = now + 1000;
		}
		stream.Close();
		Release(enumerator);
		if (wake) CloseHandle(wake);
		if (mmcss) AvRevertMmThreadCharacteristics(mmcss);
		if (SUCCEEDED(com)) CoUninitialize();
		Set(State, 0);
	}
};
}

void* VpePcmOutputCreate(void* context, int rate, int paused)
{
	if (!context || rate < 8000 || rate > 192000) return nullptr;
	auto* output = new (std::nothrow) Output();
	if (!output) return nullptr;
	output->context = context; output->rate = rate; output->paused.store(paused ? 1 : 0); // before the prefill renders
	for (auto& metric : output->metrics) metric.store(0);
	output->Set(Rate, static_cast<float>(rate));
	output->stop = CreateEventW(nullptr, TRUE, FALSE, nullptr);
	output->ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
	if (output->stop && output->ready) {
		try { output->thread = std::thread([output] { output->Run(); }); } catch (...) {}
	}
	if (output->thread.joinable()) WaitForSingleObject(output->ready, 5000);
	if (output->opened.load(std::memory_order_acquire) != 1) { VpePcmOutputDestroy(output); return nullptr; }
	return output;
}

void VpePcmOutputDestroy(void* handle)
{
	auto* output = static_cast<Output*>(handle);
	if (!output) return;
	if (output->stop) SetEvent(output->stop);
	if (output->thread.joinable()) output->thread.join();
	if (output->stop) CloseHandle(output->stop);
	if (output->ready) CloseHandle(output->ready);
	delete output;
}

void VpePcmOutputSetPaused(void* handle, int paused)
{
	if (handle) static_cast<Output*>(handle)->paused.store(paused ? 1 : 0, std::memory_order_relaxed);
}

int VpePcmOutputMetrics(void* handle, float* values, int count)
{
	if (!handle || !values || count != MetricCount) return 0;
	for (int i = 0; i < count; ++i) values[i] = static_cast<Output*>(handle)->metrics[i].load(std::memory_order_relaxed);
	return 1;
}

#else

void* VpePcmOutputCreate(void*, int, int) { return nullptr; }
void VpePcmOutputDestroy(void*) {}
void VpePcmOutputSetPaused(void*, int) {}
int VpePcmOutputMetrics(void*, float*, int) { return 0; }

#endif
