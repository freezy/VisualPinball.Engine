// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

namespace VisualPinball.Unity
{
	/// <summary>
	/// What limits the frame rate.
	/// </summary>
	public enum FrameBound
	{
		/// <summary>
		/// Not enough data, or the data is ambiguous.
		/// </summary>
		Unknown,

		/// <summary>
		/// Vsync or the frame rate limit holds the frame rate back; there's headroom.
		/// </summary>
		Capped,

		/// <summary>
		/// The GPU takes longest.
		/// </summary>
		Gpu,

		/// <summary>
		/// The main thread takes longest.
		/// </summary>
		CpuMain,

		/// <summary>
		/// The render thread takes longest.
		/// </summary>
		CpuRender,
	}

	/// <summary>
	/// The median timings of one measuring window, in milliseconds.
	/// </summary>
	public struct FrameWindowTimings
	{
		/// <summary>
		/// Mean time between frames.
		/// </summary>
		public float FrameMs;

		/// <summary>
		/// Main thread time without waiting for present.
		/// </summary>
		public float CpuMainMs;

		public float CpuRenderMs;
		public float GpuMs;

		/// <summary>
		/// Time the main thread waited for the previous frame to be presented.
		/// </summary>
		public float PresentWaitMs;

		/// <summary>
		/// How many frame timings with CPU and GPU data the medians come from.
		/// </summary>
		public int Samples;
	}

	/// <summary>
	/// Decides what limits the frame rate from a window's median timings.
	/// </summary>
	public static class FrameBoundClassifier
	{
		/// <summary>
		/// Fewer timed frames than this in a window and the result is <see cref="FrameBound.Unknown"/>.
		/// </summary>
		public const int MinSamples = 10;

		/// <param name="window">The window's median timings.</param>
		/// <param name="capIntervalMs">The frame interval vsync or the frame rate limit
		/// imposes, or 0 if neither is active.</param>
		public static FrameBound Classify(in FrameWindowTimings window, float capIntervalMs)
		{
			if (window.Samples < MinSamples || window.FrameMs <= 0f) {
				return FrameBound.Unknown;
			}
			if (window.GpuMs <= 0f) {
				// no GPU timing on this API or driver
				return FrameBound.Unknown;
			}

			if (capIntervalMs > 0f && window.FrameMs <= capIntervalMs * 1.05f) {
				// Running at the cap. If the main thread waits a good part of the frame for the
				// previous one to be presented, there's headroom.
				if (window.PresentWaitMs >= 0.3f * window.FrameMs) {
					return FrameBound.Capped;
				}
				// Some D3D12 drivers report no present wait at all, even while capped: can't
				// tell capped from bound then.
				if (window.PresentWaitMs <= 0f) {
					return FrameBound.Unknown;
				}
			}

			var cpu = System.Math.Max(window.CpuMainMs, window.CpuRenderMs);
			if (window.GpuMs >= 1.10f * cpu) {
				return FrameBound.Gpu;
			}
			return window.CpuMainMs >= window.CpuRenderMs ? FrameBound.CpuMain : FrameBound.CpuRender;
		}

		/// <summary>
		/// Lower case, for logs and report tags.
		/// </summary>
		public static string Name(FrameBound bound)
		{
			switch (bound) {
				case FrameBound.Capped: return "capped";
				case FrameBound.Gpu: return "gpu";
				case FrameBound.CpuMain: return "cpu-main";
				case FrameBound.CpuRender: return "cpu-render";
				default: return "unknown";
			}
		}
	}
}
