// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: MIT

#pragma once

struct ID3D12Device;

namespace vpnw::reflex
{
	void SetDevice(ID3D12Device* device);
	int Sleep();
	int Stop();
}
