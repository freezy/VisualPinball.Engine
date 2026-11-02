// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Optional desktop representation of the same published surface. Generating it must not require a plugin-owned
	/// window. Withdraw the publication before releasing its texture. Read only on the owner thread while ready.
	/// DesktopTexture is re-read every render frame; the inherited Changed event describes the scene Texture only.
	/// </summary>
	public interface IUnityDesktopSurface : IUnityTextureResource, ICabinetSurface
	{
		Texture DesktopTexture { get; }
		/// <summary>Encoding of shader-sampled RGB values, after any texture hardware sRGB conversion.</summary>
		FrameColorSpace SampleColorSpace { get; }
		FrameAlpha DesktopAlpha { get; }
		bool DesktopFlipY { get; }
	}
}
