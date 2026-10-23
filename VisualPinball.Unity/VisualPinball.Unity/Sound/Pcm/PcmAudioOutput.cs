// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Threading;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Dedicated carrier object: an audio filter must never share ordinary table voice sources.</summary>
	[AddComponentMenu("")]
	public sealed class PcmAudioOutput : MonoBehaviour
	{
		internal NativePcmSession Session;
		internal int SampleRate, Paused;
		private void OnAudioFilterRead(float[] data, int channels)
		{
			var session = Session;
			if (session == null) Array.Clear(data, 0, data.Length);
			else session.Render(data, channels, Volatile.Read(ref SampleRate), Volatile.Read(ref Paused) != 0);
		}
	}
}
