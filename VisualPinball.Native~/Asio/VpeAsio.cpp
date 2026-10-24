// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: GPL-3.0-or-later
// This library links the Steinberg ASIO SDK under its GPLv3 option, so it is kept apart from the
// MIT-licensed VpePcm graph and only calls VpePcmRender through the already loaded VpePcm module.

// ASIO output for the central PCM graph. One dedicated thread owns the driver (COM, load, init, start,
// stop and exit all happen there, with a message pump, as drivers expect), and the driver's buffer
// callback renders the graph straight into the driver's buffers. ASIO's C callbacks carry no user
// data, and the SDK host layer keeps one driver per process, so there is at most one stream.

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <objbase.h>
#include <algorithm>
#include <array>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <string>
#include <thread>
#include <vector>
#include "asiosys.h"
#include "asio.h"
#include "asiodrivers.h"

#define VPE_ASIO_API extern "C" __declspec(dllexport)

extern AsioDrivers* asioDrivers;
bool loadAsioDriver(char* name);

namespace {
using RenderFn = int (*)(void* context, float* output, int frames, int channels, int rate, int paused);
// Same layout as VpePcmOutputMetrics so the managed side reads both alike.
enum Metric { State, Callbacks, PeriodMs, BufferMs, QueuedMs, MarginMs, Glitches, Reopens, LastError, Rate, FirstUnderrunS, LastUnderrunS, MaxWakeGapMs, MetricCount };

struct Stream {
	void* context = nullptr;
	RenderFn render = nullptr;
	std::string driver;
	int rate = 0;
	long requestedFrames = 0, frames = 0;
	std::atomic<int> paused{0}, opened{0}, reset{0}, panel{0};
	bool panelOnly = false; // keeps the driver loaded only to show its control panel
	std::array<std::atomic<float>, MetricCount> metrics{};
	ASIOBufferInfo buffers[3]{};
	ASIOChannelInfo channels[3]{};
	int bufferCount = 0;
	std::vector<float> scratch, capture;
	HANDLE stop = nullptr, ready = nullptr;
	std::thread thread;
	std::string error;
	LARGE_INTEGER frequency{}, lastSwitch{};
	// Developer diagnostics: VPE_ASIO_CAPTURE="<input channel>;<file>" records one input with QPC stamps.
	long captureChannel = -1;
	FILE* captureFile = nullptr;
	long long samples = 0;

	void Set(Metric metric, float value) { metrics[metric].store(value, std::memory_order_relaxed); }
	void Add(Metric metric) { metrics[metric].store(metrics[metric].load(std::memory_order_relaxed) + 1, std::memory_order_relaxed); }
};
Stream* g = nullptr; // the playing stream, which the buffer callbacks use
Stream* gPanel = nullptr; // a driver loaded only for its control panel (no table playing)

float Read(const void* buffer, ASIOSampleType type, long i)
{
	switch (type) {
		case ASIOSTInt16LSB: return static_cast<const short*>(buffer)[i] / 32768.f;
		case ASIOSTInt24LSB: { auto b = static_cast<const unsigned char*>(buffer) + 3 * i; return static_cast<int>((b[0] << 8) | (b[1] << 16) | (b[2] << 24)) / 2147483648.f; }
		case ASIOSTInt32LSB: return static_cast<float>(static_cast<const int*>(buffer)[i] / 2147483648.0);
		case ASIOSTFloat32LSB: return static_cast<const float*>(buffer)[i];
		default: return 0;
	}
}
void Write(void* buffer, ASIOSampleType type, long i, float value)
{
	value = std::clamp(value, -1.f, 1.f);
	switch (type) {
		case ASIOSTInt16LSB: static_cast<short*>(buffer)[i] = static_cast<short>(value * 32767); break;
		case ASIOSTInt24LSB: { const int v = static_cast<int>(value * 8388607); auto b = static_cast<unsigned char*>(buffer) + 3 * i; b[0] = static_cast<unsigned char>(v); b[1] = static_cast<unsigned char>(v >> 8); b[2] = static_cast<unsigned char>(v >> 16); break; }
		case ASIOSTInt32LSB: static_cast<int*>(buffer)[i] = static_cast<int>(value * 2147483647.0); break;
		case ASIOSTFloat32LSB: static_cast<float*>(buffer)[i] = value; break;
		default: break;
	}
}
bool Supported(ASIOSampleType type) { return type == ASIOSTInt16LSB || type == ASIOSTInt24LSB || type == ASIOSTInt32LSB || type == ASIOSTFloat32LSB; }
int Bytes(ASIOSampleType type) { return type == ASIOSTInt16LSB ? 2 : type == ASIOSTInt24LSB ? 3 : 4; }

void Switch(long index)
{
	auto* s = g;
	if (!s) return;
	LARGE_INTEGER now; QueryPerformanceCounter(&now);
	if (s->lastSwitch.QuadPart) s->metrics[MaxWakeGapMs].store(std::max(s->metrics[MaxWakeGapMs].load(std::memory_order_relaxed),
		float(1000.0 * (now.QuadPart - s->lastSwitch.QuadPart) / s->frequency.QuadPart)), std::memory_order_relaxed);
	s->lastSwitch = now;
	const long frames = s->frames;
	s->render(s->context, s->scratch.data(), static_cast<int>(frames), 2, s->rate, s->paused.load(std::memory_order_relaxed));
	for (int c = 0; c < s->bufferCount; ++c) {
		const auto& info = s->buffers[c]; const auto type = s->channels[c].type;
		if (info.isInput) {
			if (!s->captureFile) continue;
			for (long i = 0; i < frames; ++i) s->capture[i] = Read(info.buffers[index], type, i);
			const long long header[2] = { now.QuadPart, s->samples }; const int count = static_cast<int>(frames);
			std::fwrite(header, sizeof header, 1, s->captureFile); std::fwrite(&count, 4, 1, s->captureFile); std::fwrite(s->capture.data(), 4, frames, s->captureFile);
			continue;
		}
		for (long i = 0; i < frames; ++i) Write(info.buffers[index], type, i, s->scratch[static_cast<size_t>(i) * 2 + info.channelNum]);
	}
	s->samples += frames;
	s->Add(Callbacks);
	ASIOOutputReady();
}
void BufferSwitch(long index, ASIOBool) { Switch(index); }
ASIOTime* BufferSwitchTimeInfo(ASIOTime* time, long index, ASIOBool) { Switch(index); return time; }
void RateChanged(ASIOSampleRate) { if (g) g->reset.store(1); }
long Message(long selector, long value, void*, double*)
{
	switch (selector) {
		case kAsioSelectorSupported:
			return value == kAsioEngineVersion || value == kAsioResetRequest || value == kAsioResyncRequest || value == kAsioLatenciesChanged || value == kAsioSupportsTimeInfo || value == kAsioOverload;
		case kAsioEngineVersion: return 2;
		case kAsioResetRequest: if (g) g->reset.store(1); return 1; // e.g. the buffer size changed in the driver's panel
		case kAsioResyncRequest: if (g) g->Add(Glitches); return 1;
		case kAsioOverload: if (g) g->Add(Glitches); return 1;
		case kAsioLatenciesChanged: if (g) g->reset.store(1); return 1;
		default: return 0;
	}
}

// Creates and starts buffers on the owner thread. Driver must be initialized.
bool Start(Stream& s)
{
	long minimum = 0, maximum = 0, preferred = 0, granularity = 0;
	if (ASIOGetBufferSize(&minimum, &maximum, &preferred, &granularity) != ASE_OK) { s.error = "ASIOGetBufferSize failed"; return false; }
	long frames = s.requestedFrames > 0 ? std::clamp(s.requestedFrames, minimum, maximum) : preferred;
	if (granularity > 0 && s.requestedFrames > 0) frames = minimum + (frames - minimum) / granularity * granularity;
	if (granularity == -1 && s.requestedFrames > 0) { long p = minimum; while (p < frames && p * 2 <= maximum) p *= 2; frames = p; }
	long inputs = 0, outputs = 0; ASIOGetChannels(&inputs, &outputs);
	if (outputs < 2) { s.error = "the driver has fewer than two outputs"; return false; }
	s.bufferCount = 0;
	for (long c = 0; c < 2; ++c) { s.buffers[s.bufferCount] = ASIOBufferInfo{}; s.buffers[s.bufferCount].isInput = ASIOFalse; s.buffers[s.bufferCount].channelNum = c; ++s.bufferCount; }
	if (s.captureChannel >= 0 && s.captureChannel < inputs) { s.buffers[s.bufferCount] = ASIOBufferInfo{}; s.buffers[s.bufferCount].isInput = ASIOTrue; s.buffers[s.bufferCount].channelNum = s.captureChannel; ++s.bufferCount; }
	static ASIOCallbacks callbacks{ BufferSwitch, RateChanged, Message, BufferSwitchTimeInfo };
	s.frames = frames;
	s.scratch.assign(static_cast<size_t>(frames) * 2, 0.f); s.capture.assign(static_cast<size_t>(frames), 0.f);
	if (ASIOCreateBuffers(s.buffers, s.bufferCount, frames, &callbacks) != ASE_OK) { s.error = "ASIOCreateBuffers failed"; return false; }
	for (int c = 0; c < s.bufferCount; ++c) {
		s.channels[c] = ASIOChannelInfo{}; s.channels[c].channel = s.buffers[c].channelNum; s.channels[c].isInput = s.buffers[c].isInput;
		ASIOGetChannelInfo(&s.channels[c]);
		if (!Supported(s.channels[c].type)) { ASIODisposeBuffers(); s.error = "unsupported ASIO sample type"; return false; }
		for (int half = 0; half < 2; ++half) std::memset(s.buffers[c].buffers[half], 0, static_cast<size_t>(Bytes(s.channels[c].type)) * frames);
	}
	long inputLatency = 0, outputLatency = 0; ASIOGetLatencies(&inputLatency, &outputLatency);
	s.Set(PeriodMs, 1000.f * frames / s.rate); s.Set(BufferMs, 1000.f * frames / s.rate); s.Set(QueuedMs, 1000.f * outputLatency / s.rate);
	s.lastSwitch.QuadPart = 0;
	if (ASIOStart() != ASE_OK) { ASIODisposeBuffers(); s.error = "ASIOStart failed"; return false; }
	s.Set(State, 1);
	return true;
}

// Loads and initializes the driver, and for playback puts the device at the graph's rate. Owner thread only.
bool Open(Stream& s, bool& initialized)
{
	std::vector<char> name(s.driver.begin(), s.driver.end()); name.push_back(0);
	if (!loadAsioDriver(name.data())) { s.error = "cannot load the ASIO driver"; return false; }
	ASIODriverInfo info{}; info.asioVersion = 2; info.sysRef = GetDesktopWindow();
	if (ASIOInit(&info) != ASE_OK) { s.error = std::string("ASIOInit failed: ") + info.errorMessage; return false; }
	initialized = true;
	if (s.panelOnly) return true;
	// The graph's DSP is prepared for Unity's rate, so the device has to run at it.
	ASIOSampleRate current = 0; ASIOGetSampleRate(&current);
	if (static_cast<int>(current) != s.rate && (ASIOCanSampleRate(s.rate) != ASE_OK || ASIOSetSampleRate(s.rate) != ASE_OK)) {
		s.error = "the device cannot run at the output rate"; return false;
	}
	return true;
}
void Shut(Stream& s, bool& initialized, bool& started)
{
	if (started) { ASIOStop(); ASIODisposeBuffers(); started = false; }
	if (initialized) { ASIOExit(); initialized = false; }
	if (asioDrivers) asioDrivers->removeCurrentDriver();
	s.Set(State, 2);
}

void Run(Stream& s)
{
	CoInitialize(nullptr); // drivers are apartment-threaded COM objects
	QueryPerformanceFrequency(&s.frequency);
	if (!asioDrivers) asioDrivers = new AsioDrivers();
	bool initialized = false, started = false;
	bool ok = Open(s, initialized);
	if (ok && !s.panelOnly) ok = started = Start(s);
	s.opened.store(ok ? 1 : -1, std::memory_order_release);
	SetEvent(s.ready);
	ULONGLONG retryAt = 0;
	while (ok) {
		const DWORD signaled = MsgWaitForMultipleObjects(1, &s.stop, FALSE, 100, QS_ALLINPUT);
		if (signaled == WAIT_OBJECT_0) break;
		MSG msg; while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) { TranslateMessage(&msg); DispatchMessageW(&msg); }
		// The driver's own settings window (device choice, buffer size). Called on the owner thread, as drivers expect.
		if (initialized && s.panel.exchange(0)) ASIOControlPanel();
		if (s.panelOnly) continue;
		if (s.reset.exchange(0)) {
			// The driver asked for a reset (new device, buffer size or latency) or changed its rate. The SDK
			// contract is a full reinitialization: exit, reload, init, then the graph's rate and new buffers.
			Shut(s, initialized, started); s.Add(Reopens);
			retryAt = GetTickCount64();
		}
		if (!started && retryAt && GetTickCount64() >= retryAt) {
			if (Open(s, initialized) && (started = Start(s))) retryAt = 0;
			else { Shut(s, initialized, started); s.Set(LastError, 1); retryAt = GetTickCount64() + 1000; }
		}
	}
	Shut(s, initialized, started);
	CoUninitialize();
	s.Set(State, 0);
}

// Starts the owner thread and waits until the driver is up (or failed).
bool Launch(Stream* s)
{
	s->stop = CreateEventW(nullptr, TRUE, FALSE, nullptr);
	s->ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
	try { s->thread = std::thread([s] { Run(*s); }); } catch (...) {}
	if (s->thread.joinable()) WaitForSingleObject(s->ready, 10000);
	return s->opened.load(std::memory_order_acquire) == 1;
}
void Close(Stream* s)
{
	if (!s) return;
	if (s->stop) SetEvent(s->stop);
	if (s->thread.joinable()) s->thread.join();
	if (s->captureFile) std::fclose(s->captureFile);
	if (s->stop) CloseHandle(s->stop);
	if (s->ready) CloseHandle(s->ready);
	delete s;
}
void ClosePanel() { Close(gPanel); gPanel = nullptr; }
}

VPE_ASIO_API int VpeAsioDriverCount()
{
	CoInitialize(nullptr);
	AsioDrivers drivers; char* names[64]; static char storage[64][64]; for (int i = 0; i < 64; ++i) names[i] = storage[i];
	const long count = drivers.getDriverNames(names, 64);
	CoUninitialize();
	return static_cast<int>(count);
}

VPE_ASIO_API int VpeAsioDriverName(int index, char* name, int length)
{
	if (!name || length <= 0) return 0;
	CoInitialize(nullptr);
	AsioDrivers drivers; char* names[64]; static char storage[64][64]; for (int i = 0; i < 64; ++i) names[i] = storage[i];
	const long count = drivers.getDriverNames(names, 64);
	CoUninitialize();
	if (index < 0 || index >= count) return 0;
	std::snprintf(name, static_cast<size_t>(length), "%s", names[index]);
	return 1;
}

VPE_ASIO_API void* VpeAsioOutputCreate(void* context, const char* driver, int rate, int bufferFrames, int paused, char* error, int errorLength)
{
	auto fail = [&](const char* message) -> void* { if (error && errorLength > 0) std::snprintf(error, static_cast<size_t>(errorLength), "%s", message); return nullptr; };
	if (g) return fail("an ASIO stream is already open");
	ClosePanel(); // one driver per process: a settings-only session gives way to playback
	if (!context || !driver || !*driver || rate < 8000 || rate > 192000) return fail("invalid arguments");
	HMODULE pcm = GetModuleHandleW(L"VpePcm.dll");
	auto render = pcm ? reinterpret_cast<RenderFn>(reinterpret_cast<void*>(GetProcAddress(pcm, "VpePcmRender"))) : nullptr;
	if (!render) return fail("VpePcm is not loaded");
	auto* s = new Stream();
	s->context = context; s->render = render; s->driver = driver; s->rate = rate; s->requestedFrames = bufferFrames;
	s->paused.store(paused ? 1 : 0);
	for (auto& metric : s->metrics) metric.store(0);
	s->Set(Rate, static_cast<float>(rate));
	char capture[600] = {};
	if (GetEnvironmentVariableA("VPE_ASIO_CAPTURE", capture, sizeof capture) > 0) {
		if (const char* separator = std::strchr(capture, ';')) {
			s->captureChannel = std::atol(capture);
			if (fopen_s(&s->captureFile, separator + 1, "wb") == 0 && s->captureFile) {
				LARGE_INTEGER frequency; QueryPerformanceFrequency(&frequency);
				const long long header[3] = { frequency.QuadPart, rate, 0 };
				std::fwrite(header, sizeof header, 1, s->captureFile);
			} else s->captureChannel = -1;
		}
	}
	g = s; // the callbacks find the stream here
	if (!Launch(s)) {
		std::string message = s->error.empty() ? "the ASIO driver did not start" : s->error;
		Close(s); g = nullptr;
		return fail(message.c_str());
	}
	return s;
}

VPE_ASIO_API void VpeAsioOutputDestroy(void* handle)
{
	auto* s = static_cast<Stream*>(handle);
	if (!s || s != g) return;
	Close(s); // joins the owner thread, which stops the driver before the graph can go away
	g = nullptr;
}

VPE_ASIO_API void VpeAsioOutputSetPaused(void* handle, int paused)
{
	if (handle) static_cast<Stream*>(handle)->paused.store(paused ? 1 : 0, std::memory_order_relaxed);
}

VPE_ASIO_API int VpeAsioOutputMetrics(void* handle, float* values, int count)
{
	if (!handle || !values || count != MetricCount) return 0;
	for (int i = 0; i < count; ++i) values[i] = static_cast<Stream*>(handle)->metrics[i].load(std::memory_order_relaxed);
	return 1;
}

// Opens the driver's own settings window. Uses the playing stream when it runs this driver; otherwise loads
// the driver just for its panel until VpeAsioCloseControlPanel (or a stream starts). Main thread only.
VPE_ASIO_API int VpeAsioShowControlPanel(const char* driver)
{
	if (!driver || !*driver) return 0;
	if (g) {
		if (g->driver != driver) return 0;
		g->panel.store(1); return 1;
	}
	if (gPanel && gPanel->driver != driver) ClosePanel();
	if (!gPanel) {
		auto* s = new Stream(); s->driver = driver; s->panelOnly = true;
		if (!Launch(s)) { Close(s); return 0; }
		gPanel = s;
	}
	gPanel->panel.store(1);
	return 1;
}

VPE_ASIO_API void VpeAsioCloseControlPanel() { ClosePanel(); }
