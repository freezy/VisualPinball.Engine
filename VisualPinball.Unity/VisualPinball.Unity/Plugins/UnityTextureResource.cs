// SPDX-License-Identifier: MIT
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
