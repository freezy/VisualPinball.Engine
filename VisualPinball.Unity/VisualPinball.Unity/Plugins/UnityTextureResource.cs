// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>A borrowed Unity texture. Its producer owns it; consumers may sample it until Changed or resource removal.</summary>
	public interface IUnityTextureResource : IPluginSurface
	{
		Texture Texture { get; }
		event Action<Texture> Changed;
	}
}
