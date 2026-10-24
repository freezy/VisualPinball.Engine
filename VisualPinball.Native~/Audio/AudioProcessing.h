// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#pragma once
#include <memory>

namespace vpe {
// Version 1 wire format, shared with AudioProcessingSettings.cs. Seven buses + stereo output.
constexpr int ProcessingHeader = 14, ProcessingRoute = 41, ProcessingRoutes = 8;
constexpr int ProcessingValues = ProcessingHeader + ProcessingRoute * ProcessingRoutes;
constexpr int ProcessingMeters = 16;

class AudioProcessing {
public:
	explicit AudioProcessing(int rate);
	~AudioProcessing();
	// One control producer and one render consumer. Preparation/validation is off the audio thread.
	bool Set(const float* values, int count);
	void Process(float* buses, int stride, float* output, int frames, bool paused, bool preview);
	void Meters(float* values) const;
	float EqResponse(int route, float hz) const; // Control thread; response of the last accepted snapshot.
private:
	struct Impl;
	std::unique_ptr<Impl> impl;
};
}
