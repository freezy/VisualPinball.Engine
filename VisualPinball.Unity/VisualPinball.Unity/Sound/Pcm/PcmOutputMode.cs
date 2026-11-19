// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	/// <summary>How the central PCM graph reaches the audio device on Windows.</summary>
	/// <summary>A playback device of the system, as the mixer can open it.</summary>
	public readonly struct PcmOutputDevice
	{
		/// <summary>The system's identifier of the device, which stays the same while it is installed.</summary>
		public readonly string Id;
		public readonly string Name;
		/// <summary>The channels of the device's own format: 2 for stereo, 6 for 5.1, 8 for 7.1.</summary>
		public readonly int Channels;
		/// <summary>Whether it is the system's default playback device.</summary>
		public readonly bool IsDefault;
		public PcmOutputDevice(string id, string name, int channels, bool isDefault) { Id = id; Name = name; Channels = channels; IsDefault = isDefault; }
	}

	/// <summary>
	/// Where the mixer's sound buses play. The main output renders the mix and has the least delay. Up to four more
	/// outputs take some of the buses: another stereo pair of the main device (a 7.1 card, an ASIO interface), or another
	/// device, which then plays a little later than the main one, since the two run on their own clocks.
	/// </summary>
	public sealed class PcmOutputRouting
	{
		public const int MaxExtraOutputs = 4;
		public sealed class Output
		{
			/// <summary>The device's identifier (see <see cref="PcmOutputDevice.Id"/>), or empty for the main output's device.</summary>
			public string Device = string.Empty;
			/// <summary>What to call the device in messages while it isn't connected.</summary>
			public string Name = string.Empty;
			/// <summary>The stereo pair of the device: 0 for channels 1/2, 1 for 3/4, and so on.</summary>
			public int Pair;
		}
		/// <summary>The main output's device, or empty for the system's default device. ASIO output uses its driver instead.</summary>
		public string MainDevice = string.Empty;
		/// <summary>What to call the main device in messages while it isn't connected.</summary>
		public string MainDeviceName = string.Empty;
		/// <summary>The main output's stereo pair on its device.</summary>
		public int MainPair;
		/// <summary>Outputs 1 to 4; an output no bus plays on is never opened.</summary>
		public readonly List<Output> Extra = new();
		/// <summary>Per bus (in the order of <see cref="SoundBuses.Ids"/>): 0 for the main output, 1.. for <see cref="Extra"/>.</summary>
		public readonly int[] BusOutput = new int[7];
		/// <summary>
		/// Per mechanical bus (transients, rolling): plays it on four speakers. Its entry in <see cref="BusOutput"/> is
		/// then the pair near the player and <see cref="BusBackOutput"/> the pair near the backbox, and each sound fades
		/// between them by where it is on the playfield. It needs two different outputs.
		/// </summary>
		public readonly bool[] BusSurround = new bool[2];
		public readonly int[] BusBackOutput = new int[2];
	}

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
