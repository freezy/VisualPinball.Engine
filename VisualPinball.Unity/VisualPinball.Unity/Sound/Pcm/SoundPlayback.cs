// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Existing source settings remain authored units; central voices never also play through Unity.</summary>
	public static class SoundPlayback
	{
		private sealed class Voice { public PcmAudioRenderer Owner; public ulong Id; public bool Paused; public int Bus; public bool Listener; }
		private static readonly Dictionary<AudioSource, Voice> Voices = new();
		private static readonly HashSet<AudioSource> ReferencePaused = new();
		private static readonly List<AudioSource> Removed = new();
		public static void Play(AudioSource source, SoundAsset asset, GameObject owner = null, string automaticBus = null)
		{
			Stop(source);
			if (!source || !source.isActiveAndEnabled) return;
			var renderer = Application.isPlaying ? (owner ? owner : source.gameObject).GetComponentInParent<PcmAudioRenderer>() : null;
			var bus = SoundBuses.Index(SoundBuses.Resolve(asset, automaticBus));
			if (renderer && renderer.IsReady && !string.IsNullOrEmpty(asset.BusId) && SoundBuses.Index(asset.BusId) < 0) renderer.Diagnose("Unknown bus '" + asset.BusId + "' retained; using the asset's compatible default.");
			Start(source, renderer, bus, false);
		}
		/// <summary>
		/// Plays a plugin's <see cref="AudioSource"/> (clip, volume, pitch, loop, 3D position) through the central mixer on
		/// <paramref name="bus"/>, or through Unity when the mixer is off. Use <see cref="Stop"/>, <see cref="IsPlaying"/>,
		/// <see cref="Pause"/> and <see cref="UnPause"/> instead of the source's own methods for it; volume, pitch and
		/// position changes on the source follow each frame. A source that ignores the listener's pause, such as a pause-menu
		/// sound, plays through Unity, since the mixer pauses all of its voices with the game.
		/// </summary>
		public static void Play(AudioSource source, string bus, GameObject owner = null)
		{
			Stop(source);
			if (!source || !source.isActiveAndEnabled) return;
			var index = SoundBuses.Index(bus);
			if (index < 0) index = SoundBuses.Index(SoundBuses.Synthetic);
			PcmAudioRenderer renderer = null;
			if (Application.isPlaying && !source.ignoreListenerPause) {
				renderer = (owner ? owner : source.gameObject).GetComponentInParent<PcmAudioRenderer>();
				if (!renderer) renderer = PcmAudioRenderer.Current; // plugin objects live outside the table's hierarchy
			}
			Start(source, renderer, index, true);
		}
		// listener: 3D plugin voices pan and attenuate relative to the listener, like Unity would play them
		private static void Start(AudioSource source, PcmAudioRenderer renderer, int bus, bool listener)
		{
			if (!renderer || !renderer.IsReady || !renderer.TryClip(source, out int clip, listener) || source.pitch < .01f) { source.Play(); return; }
			var id = NativePcmSession.VpePcmPlay(renderer.Session.Context, clip, bus, renderer.Gain(source, bus, listener), source.pitch, renderer.Pan(source, bus, listener), source.loop ? 1 : 0);
			if (id == 0) { renderer.Diagnose("Central voice capacity exhausted; using Reference playback."); source.Play(); return; }
			Voices[source] = new Voice { Owner = renderer, Id = id, Bus = bus, Listener = listener };
		}
		public static bool IsPlaying(AudioSource source) => source && (Voices.TryGetValue(source, out var voice) ? voice.Owner && voice.Owner.IsReady && !voice.Paused && NativePcmSession.VpePcmActive(voice.Owner.Session.Context, voice.Id) != 0 : source.isPlaying);
		public static bool IsPaused(AudioSource source) => source && (ReferencePaused.Contains(source) || Voices.TryGetValue(source, out var voice) && (voice.Paused || voice.Owner && voice.Owner.Paused));
		public static void Stop(AudioSource source)
		{
			if (ReferenceEquals(source, null)) return;
			ReferencePaused.Remove(source);
			if (Voices.TryGetValue(source, out var voice)) { if (voice.Owner && voice.Owner.IsReady) NativePcmSession.VpePcmStop(voice.Owner.Session.Context, voice.Id); Voices.Remove(source); }
			if (source) source.Stop();
		}
		public static void Pause(AudioSource source)
		{
			if (!source) return;
			if (Voices.TryGetValue(source, out var voice)) voice.Paused = true; else { ReferencePaused.Add(source); source.Pause(); }
		}
		public static void UnPause(AudioSource source)
		{
			if (!source) return;
			if (Voices.TryGetValue(source, out var voice)) voice.Paused = false; else { ReferencePaused.Remove(source); source.UnPause(); }
		}
		internal static void Synchronize(PcmAudioRenderer owner)
		{
			Removed.Clear();
			foreach (var pair in Voices) {
				var source = pair.Key; var voice = pair.Value; if (voice.Owner != owner) continue;
				if (!source || !source.isActiveAndEnabled) { NativePcmSession.VpePcmStop(owner.Session.Context, voice.Id); Removed.Add(source); continue; }
				NativePcmSession.VpePcmUpdate(owner.Session.Context, voice.Id, owner.Gain(source, voice.Bus, voice.Listener), Mathf.Clamp(source.pitch, .01f, 3), owner.Pan(source, voice.Bus, voice.Listener), voice.Paused ? 1 : 0);
			}
			foreach (var source in Removed) Voices.Remove(source); Removed.Clear();
		}
		internal static void StopOwner(PcmAudioRenderer owner)
		{
			Removed.Clear(); foreach (var pair in Voices) if (pair.Value.Owner == owner) Removed.Add(pair.Key);
			foreach (var source in Removed) Stop(source); Removed.Clear();
		}
		internal static void ResumeReference(PcmAudioRenderer owner)
		{
			Removed.Clear();
			foreach (var pair in Voices) {
				var source = pair.Key; var voice = pair.Value; if (voice.Owner != owner) continue;
				if (source && source.isActiveAndEnabled && NativePcmSession.VpePcmActive(owner.Session.Context, voice.Id) != 0) {
					float position = NativePcmSession.VpePcmPosition(owner.Session.Context, voice.Id);
					NativePcmSession.VpePcmStop(owner.Session.Context, voice.Id);
					source.Play(); source.time = Mathf.Min(position, Mathf.Max(0, source.clip.length - 1f/source.clip.frequency));
					if (voice.Paused) { ReferencePaused.Add(source); source.Pause(); }
				}
				Removed.Add(source);
			}
			foreach (var source in Removed) Voices.Remove(source); Removed.Clear();
		}
	}
}
