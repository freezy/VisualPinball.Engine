// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System.Runtime.InteropServices;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Snapshot of a native window's state, matching the native <c>VPNW_Diagnostics</c> layout (80 bytes).
	/// </summary>
	/// <remarks>Coordinates and sizes are physical desktop pixels. Boolean fields are 0 or 1.</remarks>
	[StructLayout(LayoutKind.Sequential)]
	public struct NativeWindowDiagnostics
	{
		public uint StructBytes;
		/// <summary>UnityGfxRenderer value seen by the native plugin.</summary>
		public int GraphicsRenderer;
		public int DeviceReady;
		public int Alive;
		public int Visible;
		public int Topmost;
		public int Left;
		public int Top;
		public int WindowWidth;
		public int WindowHeight;
		public uint Dpi;
		public uint SourceWidth;
		public uint SourceHeight;
		/// <summary>Platform texture format: DXGI_FORMAT, MTLPixelFormat or VkFormat.</summary>
		public uint SourceFormat;
		public ulong PresentedFrames;
		public ulong SkippedFrames;
		/// <summary>Windows: last presentation HRESULT. macOS/Linux: 0, or negative after a presentation failure.</summary>
		public int LastResult;
		public int PremultipliedAlpha;
	}
}
