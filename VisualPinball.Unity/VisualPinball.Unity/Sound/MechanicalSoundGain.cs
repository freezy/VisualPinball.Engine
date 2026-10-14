// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Applies a user gain while keeping mechanical requests and fades in their authored units.</summary>
	public static class MechanicalSoundGain
	{
		private static readonly Dictionary<AudioSource, float> Levels = new();
		private static readonly List<AudioSource> Destroyed = new();
		private static float _gain = 1;
		public static float Gain {
			get => _gain;
			set {
				if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
				_gain = Mathf.Clamp01(value);
				Destroyed.Clear();
				foreach (var pair in Levels) {
					if (pair.Key == null) Destroyed.Add(pair.Key);
					else pair.Key.volume = pair.Value * _gain;
				}
				foreach (var source in Destroyed) Levels.Remove(source);
				Destroyed.Clear();
			}
		}
		internal static void Register(AudioSource source)
		{
			Levels[source] = source.volume;
			source.volume *= _gain;
		}
		internal static void Unregister(AudioSource source)
		{
			// Unity's destroyed-object null check must not prevent removing its managed key.
			if (!ReferenceEquals(source, null)) Levels.Remove(source);
		}
		public static float AuthoredLevel(AudioSource source) => Levels.TryGetValue(source, out float level) ? level : source.volume;
		public static void SetAuthoredLevel(AudioSource source, float level)
		{
			level = Mathf.Clamp01(level);
			if (Levels.ContainsKey(source)) { Levels[source] = level; source.volume = level * _gain; }
			else source.volume = level;
		}
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Reset() { Levels.Clear(); Destroyed.Clear(); _gain = 1; }
	}
}
