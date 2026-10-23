// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Opt-in stereo graph. Not table data: the player owns its policy and lifetime.</summary>
	[DisallowMultipleComponent, DefaultExecutionOrder(-2000)]
	[AddComponentMenu("Pinball/Sound/Central Stereo Renderer")]
	public sealed class PcmAudioRenderer : MonoBehaviour
	{
		private static readonly HashSet<PcmAudioRenderer> Active = new();
		private static readonly float[] GlobalLevels = { 1, 1, 1, 1, 1, 1, 1 };
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
		private readonly Dictionary<AudioClip, int> _clips = new();
		private readonly HashSet<string> _diagnostics = new();
		private readonly float[] _busLevels = { 1, 1, 1, 1, 1, 1, 1 };
		private readonly float[] _meters = new float[12];
		private NativePcmSession _session;
		private PcmRomInput _rom;
		private AudioSource _output;
		private PcmAudioOutput _callback;
		private AudioClip _silence;
		private PlayfieldComponent _playfield;
		private int _rate, _paused;
		private long _clipSamples;
		private bool _applicationPaused, _romAttempted;
		public bool IsReady => _session != null && _session.Context != IntPtr.Zero && _session.RenderedBlocks > 0;
		public string Status { get; private set; } = "Reference playback";
		public string RomStatus { get; private set; } = "Waiting for the renderer";
		public int BypassedVoices { get; private set; }
		public static void SetGlobalBusGain(string bus, float gain)
		{
			int index = SoundBuses.Index(bus);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(bus));
			if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0 || gain > 4) throw new ArgumentOutOfRangeException(nameof(gain));
			GlobalLevels[index] = gain; foreach (var renderer in Active) renderer.SetBusGain(bus, gain);
		}
		internal NativePcmSession Session => _session;
		internal bool Paused => Volatile.Read(ref _paused) != 0;
		public float BusPeak(string bus) { int index = SoundBuses.Index(bus); return index >= 0 ? _meters[index] : 0; }
		public void SetBusGain(string bus, float gain)
		{
			int index = SoundBuses.Index(bus);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(bus));
			if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0 || gain > 4) throw new ArgumentOutOfRangeException(nameof(gain));
			_busLevels[index] = gain;
			if (_session != null && _session.Context != IntPtr.Zero) NativePcmSession.VpePcmSetBusGain(_session.Context, index, gain);
		}
		private void OnEnable()
		{
			if (!Application.isPlaying) return;
			try {
				if (AudioSettings.GetConfiguration().speakerMode != AudioSpeakerMode.Stereo) throw new InvalidOperationException("Central playback currently requires Stereo; discrete output is not yet enabled.");
				_session = new NativePcmSession(); _rate = AudioSettings.outputSampleRate;
				Array.Copy(GlobalLevels, _busLevels, GlobalLevels.Length); Active.Add(this);
				_playfield = GetComponentInChildren<PlayfieldComponent>(true);
				var outputObject = new GameObject("VPE central PCM output"); outputObject.transform.SetParent(transform, false);
				_callback = outputObject.AddComponent<PcmAudioOutput>(); _callback.Session = _session; _callback.SampleRate = _rate;
				_output = outputObject.AddComponent<AudioSource>();
				_silence = AudioClip.Create("VPE PCM carrier", 32, 2, _rate, false);
				_output.clip = _silence; _output.loop = true; _output.playOnAwake = false; _output.spatialBlend = 0;
				_output.spatialize = false; _output.dopplerLevel = 0; _output.priority = 0; _output.ignoreListenerPause = true;
				_output.bypassReverbZones = true; _output.Play();
				for (int i = 0; i < _busLevels.Length; ++i) NativePcmSession.VpePcmSetBusGain(_session.Context, i, _busLevels[i]);
				AudioSettings.OnAudioConfigurationChanged += ConfigurationChanged;
				Status = "Central stereo PCM (dry)";
			} catch (Exception error) {
				Status = "Reference fallback: " + error.Message; Diagnose(Status); Shutdown();
			}
		}
		private void LateUpdate()
		{
			if (!IsReady) return;
			bool paused = AudioListener.pause || _applicationPaused;
			Volatile.Write(ref _paused, paused ? 1 : 0); _rom?.SetPaused(paused);
			Volatile.Write(ref _callback.Paused, paused ? 1 : 0);
			if (!_output.isPlaying && !paused) _output.Play(); // AudioSettings.Reset can stop the carrier.
			if (!_romAttempted && _session.RenderedBlocks > 0) {
				_romAttempted = true;
				var player = GetComponentInChildren<Player>(true);
				var provider = player ? player.GetComponent<IGamelogicAudioResourceProvider>() : null;
				if (provider == null) RomStatus = "No ROM audio resource";
				else try {
					_rom = new PcmRomInput(_session, provider.AudioSource);
					RomStatus = "Central PCM; integration owns gain";
				} catch (Exception error) { RomStatus = "External/integration playback: " + error.Message; Diagnose(RomStatus); }
			}
			if (_rom != null) {
				try { if (_rom.Fault != null) throw new InvalidOperationException(_rom.Fault); _rom.PublishGain(); }
				catch (Exception error) { RomStatus = error.Message; _rom.Dispose(); _rom = null; Diagnose(RomStatus); }
			}
			SoundPlayback.Synchronize(this);
			NativePcmSession.VpePcmMetrics(_session.Context, _meters, _meters.Length);
		}
		internal bool TryClip(AudioSource source, out int clip)
		{
			clip = -1;
			if (!IsReady || !source.clip) return false;
			if (source.outputAudioMixerGroup) { BypassedVoices++; Diagnose("An authored mixer group remains on Reference playback and bypasses central buses."); return false; }
			if (_clips.TryGetValue(source.clip, out clip)) return clip >= 0;
			var asset = source.clip;
			if (asset.loadState != AudioDataLoadState.Loaded) {
				BypassedVoices++; Diagnose("A clip still loading uses Reference playback; later plays will retry the central route.");
				return false;
			}
			long count = (long)asset.samples * asset.channels;
			if ((asset.channels == 1 || asset.channels == 2) && asset.loadState == AudioDataLoadState.Loaded && count <= 8 * 1024 * 1024 && _clipSamples + count <= 64 * 1024 * 1024) {
				try {
					var samples = new float[(int)count];
					if (asset.GetData(samples, 0)) clip = NativePcmSession.VpePcmRegisterClip(_session.Context, samples, asset.samples, asset.channels, asset.frequency);
					if (clip >= 0) _clipSamples += count;
				} catch (Exception error) { Diagnose("Clip preparation failed: " + error.Message); }
			}
			_clips.Add(asset, clip);
			if (clip < 0) { BypassedVoices++; Diagnose("An unreadable, oversized or unsupported clip remains on Reference playback."); }
			return clip >= 0;
		}
		internal float Pan(AudioSource source, int bus)
		{
			if ((bus == 0 || bus == 1) && _playfield && source.spatialBlend > 0) {
				var position = source.transform.position.TranslateToVpx(_playfield.transform);
				return Mathf.Lerp(source.panStereo, Mathf.Clamp((position.x-_playfield.Left) / Mathf.Max(1, _playfield.Right-_playfield.Left) * 2-1, -1, 1), source.spatialBlend);
			}
			return source.panStereo;
		}
		internal void Diagnose(string message) { if (_diagnostics.Add(message)) Logger.Warn("Audio routing: {Message:l}", message); }
		private void ConfigurationChanged(bool changed)
		{
			Volatile.Write(ref _rate, AudioSettings.outputSampleRate);
			if (_callback) Volatile.Write(ref _callback.SampleRate, _rate);
			if (IsReady) NativePcmSession.VpePcmFlushStream(_session.Context);
			if (AudioSettings.GetConfiguration().speakerMode != AudioSpeakerMode.Stereo) {
				Status = "Reference fallback after output layout changed"; Diagnose(Status);
				SoundPlayback.ResumeReference(this); Shutdown();
			}
		}
		private void OnApplicationPause(bool paused)
		{
			_applicationPaused = paused;
			bool outputPaused = paused || AudioListener.pause;
			Volatile.Write(ref _paused, outputPaused ? 1 : 0);
			if (_callback) Volatile.Write(ref _callback.Paused, outputPaused ? 1 : 0);
			_rom?.SetPaused(outputPaused);
		}
		private void OnDisable() => Shutdown();
		public void StopRouting() => Shutdown();
		private void Shutdown()
		{
			Active.Remove(this);
			AudioSettings.OnAudioConfigurationChanged -= ConfigurationChanged;
			_rom?.Dispose(); _rom = null; _romAttempted = false;
			SoundPlayback.StopOwner(this);
			if (_output) { _output.Stop(); Destroy(_output.gameObject); _output = null; _callback = null; }
			_session?.Dispose(); _session = null; _clips.Clear(); _clipSamples = 0;
			if (_silence) { Destroy(_silence); _silence = null; }
		}
	}
}
