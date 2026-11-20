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
		private static float GlobalTransitionGain = 1;
		private static float GlobalMasterGain = 1;
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
		private readonly Dictionary<AudioClip, int> _clips = new(), _monoClips = new();
		private readonly HashSet<string> _diagnostics = new();
		private readonly float[] _busLevels = { 1, 1, 1, 1, 1, 1, 1 };
		private readonly float[] _meters = new float[12];
		private readonly float[] _processingMeters = new float[16];
		private readonly float[] _outputMetrics = new float[NativePcmSession.OutputMetricCount];
		private readonly System.Collections.Generic.List<PcmAudioStream> _streams = new();
		private AudioListener _listener;
		private float _listenerSearch;
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
		private int _extraDevices; // devices playing besides the main output
		private OutputPlan _plan; // what the outputs opened with; null on Unity's carrier
		private readonly bool[] _split = new bool[2]; // the mechanical buses that play on two pairs right now
		private readonly List<string> _startProblems = new();
		private readonly bool[] _tapDown = new bool[PcmOutputRouting.MaxExtraOutputs];
		private readonly float[] _tapMetrics = new float[NativePcmSession.OutputMetricCount];
		private float _tapCheck;
		private string _loggedStatus;
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
		/// <summary>How the device stream reaches the hardware (Windows). ASIO falls back to Exclusive, that to Shared, and that to Unity.</summary>
		public static PcmOutputMode OutputMode { get; set; } = PcmOutputMode.Shared;
		/// <summary>
		/// ASIO driver for <see cref="PcmOutputMode.Asio"/> (needs the optional VpeAsio library). ASIO skips the
		/// Windows audio engine and the device's Windows driver buffers entirely.
		/// </summary>
		public static string AsioDriver { get; set; } = string.Empty;
		/// <summary>Buffer size in frames for exclusive and ASIO output, or 0 for the device's or driver's default.</summary>
		public static int OutputBufferFrames { get; set; }
		/// <summary>
		/// Which device and channels each sound bus plays on. Applies when an output opens: call
		/// <see cref="ReopenOutput"/> on the playing mixer after changing it.
		/// </summary>
		public static PcmOutputRouting Routing { get; set; } = new();
		/// <summary>The system's playback devices, empty where the mixer plays through Unity only.</summary>
		public static IReadOnlyList<PcmOutputDevice> OutputDevices() => NativePcmSession.Devices();
		/// <summary>What became of <see cref="Routing"/> when the outputs opened; empty when every bus plays where it was sent.</summary>
		public string RoutingStatus { get; private set; } = string.Empty;
		public static string[] AsioDrivers() => NativePcmSession.AsioDrivers();
		private static readonly Dictionary<string, int> AsioOutputCounts = new();
		/// <summary>
		/// The outputs an ASIO driver has, or 0 when unknown. The first question for a driver that isn't playing loads
		/// it for a moment; the answer is kept, so ask for the driver the user chose, not for every installed one.
		/// </summary>
		public static int AsioOutputs(string driver)
		{
			if (string.IsNullOrEmpty(driver)) return 0;
			if (AsioOutputCounts.TryGetValue(driver, out int known)) return known;
			int outputs = NativePcmSession.AsioDriverOutputs(driver);
			if (outputs > 0) AsioOutputCounts[driver] = outputs; // unknown is asked again: the driver may have been busy
			return outputs;
		}
		/// <summary>Whether this build ships the optional ASIO library (Windows x64).</summary>
		public static bool AsioAvailable => NativePcmSession.AsioAvailable;
		/// <summary>Opens the ASIO driver's settings window, also when no table is playing.</summary>
		public static bool ShowAsioControlPanel(string driver)
		{
			if (string.IsNullOrEmpty(driver)) return false;
			AsioOutputCounts.Remove(driver); // its panel may change which outputs it has: asked again next time
			return NativePcmSession.ShowAsioControlPanel(driver);
		}
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
		/// <summary>Temporary master envelope for Player transitions. Does not edit the user's processing or volume settings.</summary>
		public static void SetGlobalTransitionGain(float gain, int milliseconds)
		{
			if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0 || gain > 1) throw new ArgumentOutOfRangeException(nameof(gain));
			if (milliseconds < 0 || milliseconds > 1000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
			GlobalTransitionGain = gain;
			foreach (var renderer in Active) renderer.ApplyOutputGain(milliseconds);
		}
		/// <summary>
		/// The user's master volume, 0..1, for audio that reaches the speakers neither through Unity's listener nor
		/// through this mixer (a plugin's own device output, for example): such an output multiplies its level by it.
		/// </summary>
		public static float MasterGain => GlobalMasterGain;
		/// <summary>Raised on the main thread after <see cref="MasterGain"/> changed.</summary>
		public static event Action MasterGainChanged;
		/// <summary>
		/// The user's master volume, 0..1 (0 mutes). It scales the mixer's own device output; while the mixer plays
		/// through Unity, the listener's volume is the master and this one stays out of the way.
		/// </summary>
		public static void SetGlobalMasterGain(float gain)
		{
			if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0 || gain > 1) throw new ArgumentOutOfRangeException(nameof(gain));
			if (gain == GlobalMasterGain) return;
			GlobalMasterGain = gain;
			MasterGainChanged?.Invoke();
			foreach (var renderer in Active) renderer.ApplyMasterGain();
		}
		private void ApplyMasterGain()
		{
			if (_session == null || _session.Context == IntPtr.Zero) return;
			// its own stage leaves a running pause or resume fade alone; a plugin from before it takes the master on the envelope
			if (!_session.SetMasterGain(DeviceOutput ? GlobalMasterGain : 1)) ApplyOutputGain(30);
		}
		/// <summary>
		/// Sets the master and the transition envelope. <paramref name="device"/> overrides whether the mixer plays through
		/// its own output, for the levels a device needs before it starts; output switches apply the master at once.
		/// </summary>
		private void ApplyOutputGain(int milliseconds, bool? device = null)
		{
			if (_session == null || _session.Context == IntPtr.Zero) return;
			// The master has its own stage in the native graph, which also holds for a preview while the game is paused. A
			// plugin from before that stage takes it on the transition envelope.
			var master = (device ?? DeviceOutput) ? GlobalMasterGain : 1;
			var gain = _session.SetMasterGain(master, milliseconds == 0) ? GlobalTransitionGain : GlobalTransitionGain * master;
			if (!_session.SetTransitionGain(gain, milliseconds)) Diagnose("Update the native audio plugin to enable smooth master transitions.");
		}
		public static void SetGlobalProcessing(AudioProcessingSettings settings)
		{
			GlobalProcessing = settings.Copy(); GlobalProcessing.Normalize();
			foreach (var renderer in Active) renderer._pendingProcessing = GlobalProcessing.ToNative();
		}
		public void PreviewProcessing(int bus, bool enabled) { if (bus >= 0 && bus < 7) _session?.Preview(bus, enabled); }
		/// <summary>
		/// Three seconds of quiet noise bursts, left then right, on the pair near the backbox of a mechanical bus (0 or
		/// 1) that plays on four speakers. While it isn't split, its one pair plays them.
		/// </summary>
		public void PreviewBackSpeakers(int bus) { if (bus >= 0 && bus < _split.Length) _session?.Preview(_split[bus] ? 7 + bus : bus, true); }
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
				ApplyOutputGain(0);
				AudioSettings.OnAudioConfigurationChanged += ConfigurationChanged;
				UpdateStatus();
			} catch (Exception error) {
				Status = "Reference fallback: " + error.Message; Diagnose(Status); Shutdown();
			}
		}
		/// <summary>
		/// Opens a live PCM input on a sound bus (see <see cref="SoundBuses"/>), for audio a plugin produces itself.
		/// Null when the mixer isn't ready or all inputs are in use; the caller then keeps its own playback.
		/// </summary>
		public PcmAudioStream OpenStream(string bus)
		{
			var index = SoundBuses.Index(bus);
			if (!IsReady || index < 0) return null;
			var id = _session.OpenStream(index);
			if (id < 0) { Diagnose("All live audio inputs of the mixer are in use; a plugin keeps its own playback."); return null; }
			var stream = new PcmAudioStream(_session, id);
			_streams.RemoveAll(existing => !existing.IsOpen);
			_streams.Add(stream);
			return stream;
		}

		/// <summary>Volume of a voice: its source's, and for 3D plugin voices (<paramref name="listener"/>) attenuated by distance.</summary>
		internal float Gain(AudioSource source, int bus, bool listener = false)
		{
			if (source.mute) return 0;
			if (!listener || bus == 0 || bus == 1 || source.spatialBlend <= 0) return source.volume; // mechanical sounds pan across the playfield instead
			var ear = Listener();
			if (!ear) return source.volume;
			var distance = Vector3.Distance(ear.transform.position, source.transform.position);
			float min = Mathf.Max(source.minDistance, .0001f), max = Mathf.Max(source.maxDistance, min);
			float attenuation = source.rolloffMode switch {
				AudioRolloffMode.Linear => Mathf.Clamp01(1 - (distance - min) / Mathf.Max(max - min, .0001f)),
				AudioRolloffMode.Custom => Mathf.Clamp01(source.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(Mathf.Clamp01(distance / max))),
				_ => distance <= min ? 1 : min / Mathf.Min(distance, max),
			};
			return source.volume * Mathf.Lerp(1, attenuation, source.spatialBlend);
		}

		private AudioListener Listener()
		{
			if (!_listener && Time.unscaledTime >= _listenerSearch) {
				_listener = FindAnyObjectByType<AudioListener>();
				_listenerSearch = Time.unscaledTime + 1;
			}
			return _listener;
		}

		/// <summary>
		/// Opens the output that renders the graph (ASIO, exclusive or shared) and the devices besides it. False leaves
		/// the graph to Unity's carrier.
		/// </summary>
		private bool StartDeviceOutput(bool paused)
		{
			ActiveOutputMode = PcmOutputMode.Shared;
			_session.StopTapOutputs();
			RoutingStatus = string.Empty; _extraDevices = 0; _plan = null; _startProblems.Clear();
			Array.Clear(_tapDown, 0, _tapDown.Length);
			var everythingOnMain = new OutputPlan();
			if (!PreferDeviceOutput) { Publish(everythingOnMain); return false; }
			// a device renders as soon as it starts: give it the master the listener applied so far, and the caller sets
			// the levels for the output that actually opened
			ApplyOutputGain(0, true);
			var routing = Routing ?? new PcmOutputRouting();
			var devices = _session.RoutingSupported ? NativePcmSession.Devices() : new List<PcmOutputDevice>();
			if (!OpenMainOutput(paused, routing, devices, out var plan)) { Publish(everythingOnMain); return false; }
			for (int tap = 0; tap < plan.TapDevices.Count; ++tap) {
				if (_session.StartTapOutput(tap, plan.TapDevices[tap], _rate, paused)) { ++_extraDevices; continue; }
				plan.FoldTap(tap); // the main output has the channels for it: every zone of the plan is reserved
				_startProblems.Add(DeviceName(devices, plan.TapDevices[tap], plan.TapNames[tap]) + " is not available; its sounds play on the main output.");
			}
			Publish(plan);
			_plan = plan;
			RoutingStatus = string.Join(" ", _startProblems);
			foreach (var problem in _startProblems) Diagnose(problem);
			return true;
		}

		private void Publish(OutputPlan plan)
		{
			plan.Publish(_session);
			for (int bus = 0; bus < _split.Length; ++bus) _split[bus] = plan.Split(bus);
		}

		private static string DeviceName(List<PcmOutputDevice> devices, string id, string saved)
		{
			foreach (var device in devices) if (device.Id == id) return device.Name;
			return string.IsNullOrEmpty(saved) ? "An output device" : saved;
		}

		/// <summary>
		/// Opens the output that renders the graph. Each candidate gets its own plan published before it opens, since an
		/// output reads the channels it needs from the routing: an ASIO driver shares its other outputs with the extra
		/// outputs, a Windows device its own channels, and the default device may have fewer than the chosen one.
		/// </summary>
		private bool OpenMainOutput(bool paused, PcmOutputRouting routing, List<PcmOutputDevice> devices, out OutputPlan plan)
		{
			if (OutputMode == PcmOutputMode.Asio) {
				if (string.IsNullOrEmpty(AsioDriver)) Diagnose("ASIO output has no driver selected; using exclusive output.");
				else {
					plan = OutputPlan.From(routing, devices, OutputPlan.Main.Asio);
					Publish(plan);
					if (_session.StartAsioOutput(AsioDriver, _rate, OutputBufferFrames, paused, out var error)) { ActiveOutputMode = PcmOutputMode.Asio; return true; }
					Diagnose("ASIO driver '" + AsioDriver + "' did not start (" + error + "); using exclusive output.");
				}
			}
			plan = OutputPlan.From(routing, devices, OutputPlan.Main.Chosen);
			Publish(plan);
			var device = _session.RoutingSupported ? routing.MainDevice : null;
			if (OpenDeviceOutput(paused, device)) return true;
			if (string.IsNullOrEmpty(device)) return false;
			Diagnose(DeviceName(devices, device, routing.MainDeviceName) + " is not available; playing on the default device.");
			plan = OutputPlan.From(routing, devices, OutputPlan.Main.Default);
			Publish(plan);
			return OpenDeviceOutput(paused, null);
		}

		/// <summary>Opens the Windows device stream: exclusive when asked for (or when ASIO was), else shared.</summary>
		private bool OpenDeviceOutput(bool paused, string device)
		{
			// Who asks for ASIO wants the shortest way to the device: without the driver, exclusive output is the next best.
			if (OutputMode != PcmOutputMode.Shared) {
				if (_session.StartDeviceOutput(_rate, paused, true, OutputBufferFrames, device)) { ActiveOutputMode = PcmOutputMode.Exclusive; return true; }
				Diagnose("Exclusive output is not available (the device is in use or the format is unsupported, 0x" + NativePcmSession.LastDeviceOutputError.ToString("X8") + "); using shared output.");
			}
			ActiveOutputMode = PcmOutputMode.Shared;
			return _session.StartDeviceOutput(_rate, paused, false, 0, device);
		}

		/// <summary>
		/// Follows the devices besides the main one: while one is disconnected its sounds play on the main output, and
		/// they go back when it returns.
		/// </summary>
		private void WatchTapOutputs()
		{
			if (_plan == null || _plan.TapDevices.Count == 0 || Time.unscaledTime < _tapCheck) return;
			_tapCheck = Time.unscaledTime + .5f;
			bool changed = false;
			for (int tap = 0; tap < _plan.TapDevices.Count; ++tap) {
				// state 2: the stream lost its device and waits for it
				bool down = _session.TapOutputMetrics(tap, _tapMetrics) && _tapMetrics[0] == 2;
				changed |= down != _tapDown[tap];
				_tapDown[tap] = down;
			}
			if (!changed) return;
			var live = _plan.Copy();
			var problems = new List<string>(_startProblems);
			for (int tap = 0; tap < _plan.TapDevices.Count; ++tap) {
				if (!_tapDown[tap]) continue;
				live.FoldTap(tap);
				problems.Add((string.IsNullOrEmpty(_plan.TapNames[tap]) ? "An output device" : _plan.TapNames[tap]) + " is not connected; its sounds play on the main output.");
			}
			Publish(live);
			RoutingStatus = string.Join(" ", problems);
		}

		/// <summary>
		/// A <see cref="PcmOutputRouting"/> turned into what the native graph takes: a zone per output in use, and per
		/// zone its stereo pair and the tap (device) it plays on.
		/// </summary>
		private sealed class OutputPlan
		{
			/// <summary>What renders the graph: an ASIO driver, the chosen Windows device, or the default one in its place.</summary>
			internal enum Main { Asio, Chosen, Default }

			internal int[] BusZone = new int[7], ZonePair = new int[5], ZoneTap = new int[5];
			/// <summary>The zones of the plan: their channels stay open on their output while their buses play elsewhere.</summary>
			internal int[] Reserved = new int[5];
			/// <summary>Per mechanical bus: whether it plays on four speakers, and the zone of the pair near the backbox.</summary>
			internal bool[] Surround = new bool[2];
			internal int[] BackZone = new int[2];
			/// <summary>The devices besides the main one, in the order of their taps.</summary>
			internal readonly List<string> TapDevices = new(), TapNames = new();

			internal OutputPlan Copy()
			{
				var copy = new OutputPlan {
					BusZone = (int[])BusZone.Clone(), ZonePair = (int[])ZonePair.Clone(), ZoneTap = (int[])ZoneTap.Clone(), Reserved = (int[])Reserved.Clone(),
					Surround = (bool[])Surround.Clone(), BackZone = (int[])BackZone.Clone()
				};
				copy.TapDevices.AddRange(TapDevices); copy.TapNames.AddRange(TapNames);
				return copy;
			}

			/// <summary>Whether a mechanical bus really plays on two pairs: two outputs on the same channels are one pair.</summary>
			internal bool Split(int bus) => Surround[bus] && BackZone[bus] != BusZone[bus];

			internal static OutputPlan From(PcmOutputRouting routing, List<PcmOutputDevice> devices, Main mainOutput)
			{
				var plan = new OutputPlan();
				bool asio = mainOutput == Main.Asio;
				string defaultDevice = string.Empty;
				foreach (var device in devices) if (device.IsDefault) defaultDevice = device.Id;
				// The stereo pairs a device has; a pair it lacks would play on its first one, on top of what is there.
				int Pairs(string id) { foreach (var device in devices) if (device.Id == id) return Mathf.Max(1, device.Channels / 2); return 1; }
				// an ASIO driver is no Windows device: only an output without a device shares it, and its outputs aren't known here
				string main = asio ? null : mainOutput == Main.Default || string.IsNullOrEmpty(routing.MainDevice) ? defaultDevice : routing.MainDevice;
				// a driver whose outputs aren't known yet may have any pair; once it has played or was asked, its real count holds
				int asioOutputs = asio && AsioOutputCounts.TryGetValue(AsioDriver ?? string.Empty, out int counted) ? counted : 0;
				int mainPairs = !asio ? Pairs(main) : asioOutputs > 0 ? Mathf.Clamp(asioOutputs / 2, 1, 8) : 8;
				int Pair(int pair, int pairs) => pair >= 0 && pair < pairs ? pair : 0;
				plan.ZonePair[0] = Pair(routing.MainPair, mainPairs);
				plan.Reserved[0] = 1;
				// the outputs in use: where a bus plays, and the back pair of a mechanical bus on four speakers
				bool Used(int output)
				{
					if (Array.IndexOf(routing.BusOutput, output) >= 0) return true;
					for (int bus = 0; bus < routing.BusSurround.Length; ++bus) if (routing.BusSurround[bus] && routing.BusBackOutput[bus] == output) return true;
					return false;
				}
				var zoneOfOutput = new int[PcmOutputRouting.MaxExtraOutputs + 1];
				int zones = 1;
				for (int output = 1; output <= PcmOutputRouting.MaxExtraOutputs && output <= routing.Extra.Count; ++output) {
					if (!Used(output)) continue;
					var extra = routing.Extra[output - 1];
					int tap = 0, pair;
					if (!string.IsNullOrEmpty(extra.Device) && extra.Device != main) {
						tap = plan.TapDevices.IndexOf(extra.Device) + 1;
						if (tap == 0) { plan.TapDevices.Add(extra.Device); plan.TapNames.Add(extra.Name); tap = plan.TapDevices.Count; }
						pair = Pair(extra.Pair, Pairs(extra.Device));
					} else pair = Pair(extra.Pair, mainPairs);
					// two outputs on the same channels are one zone, so they share one limiter
					int zone = -1;
					for (int existing = 0; existing < zones; ++existing) if (plan.ZoneTap[existing] == tap && plan.ZonePair[existing] == pair) zone = existing;
					if (zone < 0) { zone = zones++; plan.ZonePair[zone] = pair; plan.ZoneTap[zone] = tap; plan.Reserved[zone] = 1; }
					zoneOfOutput[output] = zone;
				}
				int Zone(int output) => output > 0 && output < zoneOfOutput.Length ? zoneOfOutput[output] : 0;
				for (int bus = 0; bus < plan.BusZone.Length; ++bus) plan.BusZone[bus] = Zone(routing.BusOutput[bus]);
				for (int bus = 0; bus < plan.Surround.Length; ++bus) {
					plan.Surround[bus] = routing.BusSurround[bus];
					plan.BackZone[bus] = plan.Surround[bus] ? Zone(routing.BusBackOutput[bus]) : 0;
				}
				return plan;
			}

			/// <summary>Moves the buses of a device that isn't there to the main output.</summary>
			internal void FoldTap(int tap)
			{
				for (int bus = 0; bus < BusZone.Length; ++bus) if (ZoneTap[BusZone[bus]] == tap + 1) BusZone[bus] = 0;
				for (int bus = 0; bus < BackZone.Length; ++bus) if (ZoneTap[BackZone[bus]] == tap + 1) BackZone[bus] = 0;
			}
			internal void Publish(NativePcmSession session)
			{
				session.SetRouting(BusZone, ZonePair, ZoneTap, Reserved);
				for (int bus = 0; bus < Surround.Length; ++bus) session.SetSurround(bus, Surround[bus], BackZone[bus]);
			}
		}

		/// <summary>
		/// Reopens the output with the current <see cref="OutputMode"/>, <see cref="AsioDriver"/> and
		/// <see cref="OutputBufferFrames"/>, without reloading the table. Main thread only.
		/// </summary>
		public void ReopenOutput()
		{
			if (_session == null || _session.Context == IntPtr.Zero) return;
			_session.StopTapOutputs();
			_session.StopDeviceOutput();
			DestroyCarrier();
			if (!StartDeviceOutput(Paused)) CreateCarrier();
			ApplyOutputGain(0);
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
			if (_extraDevices > 0) output += " and " + _extraDevices + (_extraDevices == 1 ? " more device" : " more devices");
			Status = "Central stereo PCM, " + output + requested + (_session.ProcessingSupported ? "; stereo DSP available" : " (dry); update native plugin to enable effects");
			if (Status != _loggedStatus) Logger.Info("Audio mixer: {Status:l}", _loggedStatus = Status);
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
			if (_output && !_output.isPlaying) _output.Play(); // AudioSettings.Reset can stop the carrier; it ignores the listener's pause, the graph pauses game voices itself
			_session.DeviceOutputMetrics(_outputMetrics);
			WatchTapOutputs();
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
		internal bool TryClip(AudioSource source, out int clip, bool listener = false)
		{
			clip = -1;
			if (!IsReady || !source.clip) return false;
			if (source.outputAudioMixerGroup) { BypassedVoices++; Diagnose("An authored mixer group remains on Reference playback and bypasses central buses."); return false; }
			// a 3D plugin voice pans like Unity's, which mixes the clip down to mono first
			return TryClip(source.clip, out clip, listener && source.spatialBlend >= .5f && source.clip.channels == 2);
		}
		private bool TryClip(AudioClip asset, out int clip, bool mono = false)
		{
			clip = -1;
			if (!IsReady || !asset) return false;
			var clips = mono ? _monoClips : _clips;
			if (clips.TryGetValue(asset, out clip)) return clip >= 0;
			if (asset.loadState != AudioDataLoadState.Loaded) {
				BypassedVoices++; Diagnose("A clip still loading uses Reference playback; later plays will retry the central route.");
				return false;
			}
			long count = (long)asset.samples * asset.channels;
			if ((asset.channels == 1 || asset.channels == 2) && asset.loadState == AudioDataLoadState.Loaded && count <= 8 * 1024 * 1024 && _clipSamples + count <= 64 * 1024 * 1024) {
				try {
					var samples = new float[(int)count];
					if (asset.GetData(samples, 0)) {
						if (mono) {
							var down = new float[asset.samples];
							for (int i = 0; i < down.Length; ++i) down[i] = (samples[2 * i] + samples[2 * i + 1]) * .5f;
							clip = NativePcmSession.VpePcmRegisterClip(_session.Context, down, asset.samples, 1, asset.frequency);
							count = down.Length;
						} else {
							clip = NativePcmSession.VpePcmRegisterClip(_session.Context, samples, asset.samples, asset.channels, asset.frequency);
						}
					}
					if (clip >= 0) _clipSamples += count;
				} catch (Exception error) { Diagnose("Clip preparation failed: " + error.Message); }
			}
			clips.Add(asset, clip);
			if (clip < 0) { BypassedVoices++; Diagnose("An unreadable, oversized or unsupported clip remains on Reference playback."); }
			return clip >= 0;
		}
		/// <summary>Where a mechanical voice is along the playfield: 0 at the player's end, 1 at the backbox; the middle for everything else.</summary>
		internal float Depth(AudioSource source, int bus)
		{
			if ((bus != 0 && bus != 1) || !_playfield || source.spatialBlend <= 0) return .5f;
			var position = source.transform.position.TranslateToVpx(_playfield.transform);
			// the playfield's top edge is the backbox end
			float depth = 1 - Mathf.Clamp01((position.y - _playfield.Top) / Mathf.Max(1, _playfield.Bottom - _playfield.Top));
			return Mathf.Lerp(.5f, depth, source.spatialBlend);
		}
		internal float Pan(AudioSource source, int bus, bool listener = false)
		{
			if ((bus == 0 || bus == 1) && _playfield && source.spatialBlend > 0) {
				var position = source.transform.position.TranslateToVpx(_playfield.transform);
				return Mathf.Lerp(source.panStereo, Mathf.Clamp((position.x-_playfield.Left) / Mathf.Max(1, _playfield.Right-_playfield.Left) * 2-1, -1, 1), source.spatialBlend);
			}
			if (listener && source.spatialBlend > 0 && Listener() is { } ear && ear) {
				// 3D plugin voices pan by their direction from the listener, as Unity's own stereo panning does.
				var local = ear.transform.InverseTransformPoint(source.transform.position);
				var side = local.sqrMagnitude > 1e-8f ? local.normalized.x : 0;
				return Mathf.Clamp(Mathf.Lerp(source.panStereo, side, source.spatialBlend), -1, 1);
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
				ApplyOutputGain(0);
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
			foreach (var stream in _streams) stream.Detach();
			_streams.Clear();
			SoundPlayback.StopOwner(this);
			if (_output) { _output.Stop(); Destroy(_output.gameObject); _output = null; _callback = null; }
			_session?.Dispose(); _session = null; _clips.Clear(); _monoClips.Clear(); _clipSamples = 0;
			if (_silence) { Destroy(_silence); _silence = null; }
		}
	}
}
