// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
namespace VisualPinball.Unity
{
	/// <summary>How the central PCM graph reaches the audio device on Windows.</summary>
	public enum PcmOutputMode
	{
		/// <summary>Shared WASAPI stream, mixed with other apps by Windows.</summary>
		Shared,
		/// <summary>Exclusive WASAPI stream: skips the Windows mixer, but no other app can play on the device.</summary>
		Exclusive,
		/// <summary>An ASIO driver, such as the device vendor's or ASIO4ALL.</summary>
		Asio,
	}
}
