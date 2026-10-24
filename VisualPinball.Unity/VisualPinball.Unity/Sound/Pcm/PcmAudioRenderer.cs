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
		private static AudioProcessingSettings GlobalProcessing = AudioProcessingSettings.Preset("reference");
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
		private readonly Dictionary<AudioClip, int> _clips = new();
		private readonly HashSet<string> _diagnostics = new();
		private readonly float[] _busLevels = { 1, 1, 1, 1, 1, 1, 1 };
		private readonly float[] _meters = new float[12];
		private readonly float[] _processingMeters = new float[16];
		private readonly float[] _outputMetrics = new float[NativePcmSession.OutputMetricCount];
		private float[] _pendingProcessing;
		private bool _prepareProcessing;
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
		public bool ProcessingSupported => _session?.ProcessingSupported == true;
		public bool ProcessingReady => IsReady && ProcessingSupported && !_prepareProcessing;
		public float OutputPeakLeft => _processingMeters[9];
		public float OutputPeakRight => _processingMeters[10];
		public float LimiterReductionDb => _processingMeters[8];
		public float ProcessingLatencyMs => _processingMeters[11];
		public float ReverbPeak => _processingMeters[12];
		public float RomUnderruns => _meters[7];
		public float RomOverruns => _meters[8];
		public float DroppedCommands => _meters[10];
		/// <summary>
		/// Whether new renderers play through the graph's own low-latency device stream (Windows WASAPI)
		/// instead of a Unity audio callback, which adds Unity's mixer ring and FMOD's device buffer.
		/// Unity's callback remains the fallback when no device stream opens.
		/// </summary>
		public static bool PreferDeviceOutput { get; set; } = true;
		/// <summary>How the device stream reaches the hardware (Windows). Falls back to Shared, then to Unity.</summary>
		public static PcmOutputMode OutputMode { get; set; } = PcmOutputMode.Shared;
		/// <summary>
		/// ASIO driver for <see cref="PcmOutputMode.Asio"/> (needs the optional VpeAsio library). ASIO skips the
		/// Windows audio engine and the device's Windows driver buffers entirely.
		/// </summary>
		public static string AsioDriver { get; set; } = string.Empty;
		/// <summary>Buffer size in frames for exclusive and ASIO output, or 0 for the device's or driver's default.</summary>
		public static int OutputBufferFrames { get; set; }
		public static string[] AsioDrivers() => NativePcmSession.AsioDrivers();
		/// <summary>Whether this build ships the optional ASIO library (Windows x64).</summary>
		public static bool AsioAvailable => NativePcmSession.AsioAvailable;
		/// <summary>Opens the ASIO driver's settings window, also when no table is playing.</summary>
		public static bool ShowAsioControlPanel(string driver) => !string.IsNullOrEmpty(driver) && NativePcmSession.ShowAsioControlPanel(driver);
		/// <summary>Unloads a driver loaded only for its settings window. Playing streams are not affected.</summary>
		public static void CloseAsioControlPanel() => NativePcmSession.CloseAsioControlPanel();
		public bool DeviceOutput => _session?.HasDeviceOutput == true;
		public bool AsioOutput => _session?.HasAsioOutput == true;
		/// <summary>The mode actually playing: the requested one or what it fell back to.</summary>
		public PcmOutputMode ActiveOutputMode { get; private set; }
		/// <summary>Audio queued in the device stream after the last write, or 0 on Unity's callback.</summary>
		public float DeviceOutputQueuedMs => DeviceOutput ? _outputMetrics[4] : 0;
		public float DeviceOutputUnderruns => DeviceOutput ? _outputMetrics[6] : 0;
		public float EqResponseDb(int route, float hz) => _session?.EqResponse(route, hz) ?? 0;
		public float CompressorReductionDb(int route) => route >= 0 && route < 8 ? _processingMeters[route] : 0;
		public static PcmAudioRenderer Current { get { foreach (var renderer in Active) if (renderer && renderer.IsReady) return renderer; return null; } }
		public static void SetGlobalProcessing(AudioProcessingSettings settings)
		{
			GlobalProcessing = settings.Copy(); GlobalProcessing.Normalize();
			foreach (var renderer in Active) renderer._pendingProcessing = GlobalProcessing.ToNative();
		}
		public void PreviewProcessing(int bus, bool enabled) { if (bus >= 0 && bus < 7) _session?.Preview(bus, enabled); }
		public bool PreviewProcessing(AudioClip clip, int bus)
		{
			if (_pendingProcessing != null && ProcessingReady && _session.SetProcessing(_pendingProcessing)) _pendingProcessing = null;
			return ProcessingReady && bus >= 0 && bus < 7 && TryClip(clip, out int id) && _session.PreviewClip(id, bus);
		}
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
				_prepareProcessing = _session.ProcessingSupported && !_session.PrepareProcessing(_rate);
				_pendingProcessing = GlobalProcessing.ToNative();
				if (!_prepareProcessing && _session.SetProcessing(_pendingProcessing)) _pendingProcessing = null;
				Array.Copy(GlobalLevels, _busLevels, GlobalLevels.Length); Active.Add(this);
				_playfield = GetComponentInChildren<PlayfieldComponent>(true);
				for (int i = 0; i < _busLevels.Length; ++i) NativePcmSession.VpePcmSetBusGain(_session.Context, i, _busLevels[i]);
				if (!StartDeviceOutput(AudioListener.pause || _applicationPaused)) CreateCarrier();
				AudioSettings.OnAudioConfigurationChanged += ConfigurationChanged;
				UpdateStatus();
			} catch (Exception error) {
				Status = "Reference fallback: " + error.Message; Diagnose(Status); Shutdown();
			}
		}
		/// <summary>Opens ASIO when configured, else the WASAPI stream. False leaves the graph to Unity's carrier.</summary>
		private bool StartDeviceOutput(bool paused)
		{
			ActiveOutputMode = PcmOutputMode.Shared;
			if (!PreferDeviceOutput) return false;
			if (OutputMode == PcmOutputMode.Asio) {
				if (string.IsNullOrEmpty(AsioDriver)) Diagnose("ASIO output has no driver selected; using shared output.");
				else if (_session.StartAsioOutput(AsioDriver, _rate, OutputBufferFrames, paused, out var error)) { ActiveOutputMode = PcmOutputMode.Asio; return true; }
				else Diagnose("ASIO driver '" + AsioDriver + "' did not start (" + error + "); using shared output.");
			}
			if (OutputMode == PcmOutputMode.Exclusive) {
				if (_session.StartDeviceOutput(_rate, paused, true, OutputBufferFrames)) { ActiveOutputMode = PcmOutputMode.Exclusive; return true; }
				Diagnose("Exclusive output is not available (the device is in use or the format is unsupported, 0x" + NativePcmSession.LastDeviceOutputError.ToString("X8") + "); using shared output.");
			}
			return _session.StartDeviceOutput(_rate, paused);
		}

		/// <summary>
		/// Reopens the output with the current <see cref="OutputMode"/>, <see cref="AsioDriver"/> and
		/// <see cref="OutputBufferFrames"/>, without reloading the table. Main thread only.
		/// </summary>
		public void ReopenOutput()
		{
			if (_session == null || _session.Context == IntPtr.Zero) return;
			_session.StopDeviceOutput();
			DestroyCarrier();
			if (!StartDeviceOutput(Paused)) CreateCarrier();
			UpdateStatus();
		}

		private void UpdateStatus()
		{
			var output = !_session.HasDeviceOutput ? "Unity output"
				: ActiveOutputMode == PcmOutputMode.Asio ? "ASIO output (" + AsioDriver + ")"
				: ActiveOutputMode == PcmOutputMode.Exclusive ? "exclusive output" : "low-latency output";
			// Exclusive can't take a device that Unity's own output is using during play; it usually can when a table loads.
			var requested = OutputMode != PcmOutputMode.Shared && ActiveOutputMode != OutputMode
				? " (" + (OutputMode == PcmOutputMode.Asio ? "ASIO" : "exclusive") + " output unavailable, retried when the next table loads)" : "";
			Status = "Central stereo PCM, " + output + requested + (_session.ProcessingSupported ? "; stereo DSP available" : " (dry); update native plugin to enable effects");
		}

		/// <summary>Removes Unity's carrier and waits until its callback left the graph, so a device stream can render alone.</summary>
		private void DestroyCarrier()
		{
			if (_callback) _callback.Session = null;
			_session?.CloseCallbackRendering();
			if (_output) { _output.Stop(); Destroy(_output.gameObject); }
			_output = null; _callback = null;
			if (_silence) { Destroy(_silence); _silence = null; }
		}
		/// <summary>Unity audio callback that renders the graph when no device stream is open.</summary>
		private void CreateCarrier()
		{
			var outputObject = new GameObject("VPE central PCM output"); outputObject.transform.SetParent(transform, false);
			_session.OpenCallbackRendering();
			_callback = outputObject.AddComponent<PcmAudioOutput>(); _callback.Session = _session; _callback.SampleRate = _rate;
			_callback.Paused = Paused || AudioListener.pause || _applicationPaused ? 1 : 0; // before Play: the carrier ignores the listener's pause
			_output = outputObject.AddComponent<AudioSource>();
			_silence = AudioClip.Create("VPE PCM carrier", 32, 2, _rate, false);
			_output.clip = _silence; _output.loop = true; _output.playOnAwake = false; _output.spatialBlend = 0;
			_output.spatialize = false; _output.dopplerLevel = 0; _output.priority = 0; _output.ignoreListenerPause = true;
			_output.bypassReverbZones = true; _output.Play();
		}
		private void LateUpdate()
		{
			if (!IsReady) return;
			if (_prepareProcessing && _session.PrepareProcessing(_rate)) _prepareProcessing = false;
			if (!_prepareProcessing && _pendingProcessing != null && _session.SetProcessing(_pendingProcessing)) _pendingProcessing = null;
			bool paused = AudioListener.pause || _applicationPaused;
			Volatile.Write(ref _paused, paused ? 1 : 0); _rom?.SetPaused(paused);
			_session.SetDeviceOutputPaused(paused);
			if (_callback) Volatile.Write(ref _callback.Paused, paused ? 1 : 0);
			if (_output && !_output.isPlaying && !paused) _output.Play(); // AudioSettings.Reset can stop the carrier.
			_session.DeviceOutputMetrics(_outputMetrics);
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
			_session.ProcessingMeters(_processingMeters);
		}
		internal bool TryClip(AudioSource source, out int clip)
		{
			clip = -1;
			if (!IsReady || !source.clip) return false;
			if (source.outputAudioMixerGroup) { BypassedVoices++; Diagnose("An authored mixer group remains on Reference playback and bypasses central buses."); return false; }
			return TryClip(source.clip, out clip);
		}
		private bool TryClip(AudioClip asset, out int clip)
		{
			clip = -1;
			if (!IsReady || !asset) return false;
			if (_clips.TryGetValue(asset, out clip)) return clip >= 0;
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
			_session?.Preview(0, false);
			var previousRate = _rate;
			Volatile.Write(ref _rate, AudioSettings.outputSampleRate);
			if (_callback) Volatile.Write(ref _callback.SampleRate, _rate);
			// The device stream follows device changes itself; it only reopens to render at Unity's new rate.
			if (_session?.HasDeviceOutput == true && _rate != previousRate) {
				if (!StartDeviceOutput(Paused)) CreateCarrier();
				UpdateStatus();
			}
			if (_session?.ProcessingSupported == true) _prepareProcessing = true;
			if (IsReady) NativePcmSession.VpePcmFlushStream(_session.Context);
			if (AudioSettings.GetConfiguration().speakerMode != AudioSpeakerMode.Stereo) {
				Status = "Reference fallback after output layout changed"; Diagnose(Status);
				SoundPlayback.ResumeReference(this); Shutdown();
			}
		}
		private void OnApplicationPause(bool paused)
		{
			if (paused) _session?.Preview(0, false);
			_applicationPaused = paused;
			bool outputPaused = paused || AudioListener.pause;
			Volatile.Write(ref _paused, outputPaused ? 1 : 0);
			_session?.SetDeviceOutputPaused(outputPaused);
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
