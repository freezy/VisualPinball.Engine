// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using AOT;
using NLog;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Manages native input polling and forwards events to the simulation thread.
	/// Runs input polling on a separate thread at high frequency (500-1000 Hz).
	/// </summary>
	public class NativeInputManager : IDisposable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const string LogPrefix = "[VPE]";
		private static int _loggedFirstEvent;

		#region Fields

		private static volatile NativeInputManager _instance;
		private static readonly object _instanceLock = new object();

		private volatile SimulationThread _simulationThread;
		private bool _initialized = false;
		private bool _polling = false;
		private int _pollIntervalUs = 0;
		private const double PerfSampleWindowSeconds = 0.25;
		private long _inputPerfWindowStartTicks = Stopwatch.GetTimestamp();
		private int _inputEventsInWindow;
		private float _actualEventRateHz;

		// Immutable snapshot mapping native device indices to stable device ids, swapped as a whole
		// on the main thread so the input polling thread can read it lock-free. Rebuilt after
		// StartPolling, because that's when the native side switches to its polling enumeration,
		// which is the only index space consistent with axis events.
		private volatile Dictionary<int, string> _deviceIdsByIndex = new();

		// Input configuration
		private readonly List<NativeInputApi.InputBinding> _bindings = new();

		// Optional override supplied by the host app (e.g. the player's custom key bindings). When set,
		// StartPolling sends these to the native layer instead of the built-in defaults. Static so it
		// survives the per-table singleton churn (Dispose nulls _instance).
		private static volatile List<NativeInputApi.InputBinding> _configuredBindings;

		// Set from the main thread (Application.isFocused). When the app window isn't focused, native input
		// is dropped so background key presses (e.g. typing in another app) don't reach the game.
		private static volatile bool _appFocused = true;

		// Callback delegate (must be kept alive to prevent GC)
		private NativeInputApi.InputEventCallback _callbackDelegate;

		#endregion

		#region Singleton

		public static NativeInputManager Instance
		{
			get
			{
				if (_instance == null)
				{
					lock (_instanceLock)
					{
						if (_instance == null)
						{
							_instance = new NativeInputManager();
						}
					}
				}
				return _instance;
			}
		}

		public static NativeInputManager TryGetExistingInstance()
		{
			return Volatile.Read(ref _instance);
		}

		public bool IsPolling => _polling;

		public float TargetPollingHz => _polling && _pollIntervalUs > 0 ? 1000000f / _pollIntervalUs : 0f;
		public float ActualEventRateHz => _polling ? Volatile.Read(ref _actualEventRateHz) : 0f;
		public event Action<NativeInputApi.InputEvent> AxisInputReceived;

		private NativeInputManager()
		{
			// Private constructor for singleton
		}

		#endregion

		#region Public API

		/// <summary>
		/// Initialize native input system
		/// </summary>
		public bool Initialize()
		{
			if (_initialized) return true;

			int result = NativeInputApi.VpeInputInit();
			if (result == 0)
			{
				Logger.Error($"{LogPrefix} [NativeInputManager] Failed to initialize native input system");
				return false;
			}

			try {
				var protocolVersion = NativeInputApi.VpeInputGetProtocolVersion();
				if (protocolVersion != NativeInputApi.ProtocolVersion) {
					Logger.Error($"{LogPrefix} [NativeInputManager] Native input protocol mismatch: managed={NativeInputApi.ProtocolVersion}, native={protocolVersion}");
					NativeInputApi.VpeInputShutdown();
					return false;
				}
			} catch (EntryPointNotFoundException) {
				Logger.Error($"{LogPrefix} [NativeInputManager] Native input plugin is too old for protocol {NativeInputApi.ProtocolVersion}");
				NativeInputApi.VpeInputShutdown();
				return false;
			}

			_initialized = true;
			Logger.Info($"{LogPrefix} [NativeInputManager] Initialized");

			// Setup default bindings
			SetupDefaultBindings();

			return true;
		}

		/// <summary>
		/// Set the simulation thread to forward input events to
		/// </summary>
		public void SetSimulationThread(SimulationThread simulationThread)
		{
			_simulationThread = simulationThread;
		}

		/// <summary>
		/// Add an input binding
		/// </summary>
		public void AddBinding(NativeInputApi.InputAction action, NativeInputApi.KeyCode keyCode)
		{
			_bindings.Add(new NativeInputApi.InputBinding
			{
				Action = (int)action,
				BindingType = (int)NativeInputApi.BindingType.Keyboard,
				KeyCode = (int)keyCode
			});
		}

		/// <summary>
		/// Clear all bindings
		/// </summary>
		public void ClearBindings()
		{
			_bindings.Clear();
		}

		/// <summary>
		/// Custom bindings supplied by the host application. When non-null and non-empty, these are sent
		/// to the native layer on the next <see cref="StartPolling"/> instead of the built-in defaults.
		/// Set this from your config before launching a table (and again whenever the user edits bindings).
		/// Static so it outlives the per-table singleton lifecycle.
		/// </summary>
		public static List<NativeInputApi.InputBinding> ConfiguredBindings
		{
			get => _configuredBindings;
			set => _configuredBindings = value;
		}

		/// <summary>
		/// Whether the app window currently has focus. The host sets this each frame from
		/// <c>Application.isFocused</c>; native input events are dropped while it is false.
		/// </summary>
		public static bool AppFocused
		{
			get => _appFocused;
			set => _appFocused = value;
		}

		public IReadOnlyList<NativeInputDeviceInfo> ListDevices()
		{
			var result = new List<NativeInputDeviceInfo>();
			if (!_initialized) {
				return result;
			}

			var count = NativeInputApi.VpeInputListDevices(null, 0);
			if (count <= 0) {
				RebuildDeviceIdCache(result);
				return result;
			}

			var devices = new NativeInputApi.InputDeviceInfo[count];
			var copied = NativeInputApi.VpeInputListDevices(devices, devices.Length);
			for (var i = 0; i < global::System.Math.Min(count, copied); i++) {
				var axes = ListDeviceAxes(devices[i].DeviceIndex);
				result.Add(new NativeInputDeviceInfo(
					devices[i].DeviceIndex,
					devices[i].StableId ?? string.Empty,
					devices[i].DisplayName ?? string.Empty,
					devices[i].IsConnected != 0,
					axes
				));
			}
			RebuildDeviceIdCache(result);
			return result;
		}

		/// <summary>
		/// Resolves a native device index (as carried by axis events) to the device's stable id.
		/// Safe to call from the input polling thread.
		/// </summary>
		public bool TryGetDeviceId(int deviceIndex, out string deviceId)
		{
			return _deviceIdsByIndex.TryGetValue(deviceIndex, out deviceId);
		}

		private void RebuildDeviceIdCache(IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			var cache = new Dictionary<int, string>(devices.Count);
			for (var i = 0; i < devices.Count; i++) {
				cache[devices[i].DeviceIndex] = devices[i].Id;
			}
			_deviceIdsByIndex = cache;
		}

		public IReadOnlyList<NativeInputAxisInfo> ListDeviceAxes(int deviceIndex)
		{
			if (!_initialized) {
				return Array.Empty<NativeInputAxisInfo>();
			}

			var count = NativeInputApi.VpeInputListDeviceAxes(deviceIndex, null, 0);
			if (count <= 0) {
				return Array.Empty<NativeInputAxisInfo>();
			}

			var axes = new NativeInputApi.InputAxisInfo[count];
			var copied = NativeInputApi.VpeInputListDeviceAxes(deviceIndex, axes, axes.Length);
			var result = new NativeInputAxisInfo[global::System.Math.Min(count, copied)];
			for (var i = 0; i < result.Length; i++) {
				result[i] = new NativeInputAxisInfo(
					axes[i].AxisId,
					axes[i].Name ?? string.Empty,
					axes[i].UsagePage,
					axes[i].Usage,
					(NativeInputApi.AxisKind)axes[i].Kind,
					axes[i].RawValue,
					axes[i].TimestampUsec
				);
			}
			return result;
		}

		/// <summary>
		/// Start input polling
		/// </summary>
		/// <param name="pollIntervalUs">Polling interval in microseconds (default 500)</param>
		public bool StartPolling(int pollIntervalUs = 500)
		{
			#if UNITY_EDITOR
			// Avoid extremely aggressive polling in the editor; it can delay/derail PinMAME stop/start.
			if (pollIntervalUs < 1000) {
				pollIntervalUs = 1000;
			}
			#endif

			if (!_initialized)
			{
				Logger.Error($"{LogPrefix} [NativeInputManager] Not initialized");
				return false;
			}

			if (_polling)
			{
				Logger.Warn($"{LogPrefix} [NativeInputManager] Already polling");
				return true;
			}

			// Send bindings to native layer: host-supplied custom bindings if present, else the defaults
			var configured = _configuredBindings;
			var toSend = configured != null && configured.Count > 0 ? configured.ToArray() : _bindings.ToArray();
			NativeInputApi.VpeInputSetBindings(toSend, toSend.Length);
			Logger.Info($"{LogPrefix} [NativeInputManager] Sent {toSend.Length} bindings ({(configured != null && configured.Count > 0 ? "custom" : "default")})");

			// Create callback delegate (keep reference to prevent GC)
			_callbackDelegate = OnInputEvent;

			// Start polling thread
			int result = NativeInputApi.VpeInputStartPolling(_callbackDelegate, IntPtr.Zero, pollIntervalUs);
			if (result == 0)
			{
				Logger.Error($"{LogPrefix} [NativeInputManager] Failed to start polling");
				return false;
			}

			_polling = true;
			_pollIntervalUs = pollIntervalUs;
			Logger.Info($"{LogPrefix} [NativeInputManager] Started polling at {pollIntervalUs}us interval ({1000000 / pollIntervalUs} Hz)");

			// The native side switched to its polling device enumeration; refresh the
			// index → device-id cache so axis events resolve against the right snapshot.
			ListDevices();

			return true;
		}

		/// <summary>
		/// Stop input polling
		/// </summary>
		public void StopPolling()
		{
			if (!_polling) return;

			NativeInputApi.VpeInputStopPolling();
			_polling = false;
			_pollIntervalUs = 0;
			Volatile.Write(ref _actualEventRateHz, 0f);

			Logger.Info($"{LogPrefix} [NativeInputManager] Stopped polling");
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Setup default input bindings
		/// </summary>
		private void SetupDefaultBindings()
		{
			ClearBindings();
			_bindings.AddRange(BuildDefaultBindings());
			Logger.Info($"{LogPrefix} [NativeInputManager] Configured {_bindings.Count} default bindings");
		}

		/// <summary>
		/// Builds the built-in default keyboard bindings. Public and static so a host app (e.g. a
		/// key-rebinding UI) can read the defaults to seed its own configuration and merge overrides.
		/// </summary>
		public static List<NativeInputApi.InputBinding> BuildDefaultBindings()
		{
			var list = new List<NativeInputApi.InputBinding>();
			void Add(NativeInputApi.InputAction action, NativeInputApi.KeyCode keyCode)
			{
				list.Add(new NativeInputApi.InputBinding {
					Action = (int)action,
					BindingType = (int)NativeInputApi.BindingType.Keyboard,
					KeyCode = (int)keyCode
				});
			}

			// Flippers
			Add(NativeInputApi.InputAction.LeftFlipper, NativeInputApi.KeyCode.LShift);
			Add(NativeInputApi.InputAction.RightFlipper, NativeInputApi.KeyCode.RShift);
			Add(NativeInputApi.InputAction.LeftStagedFlipper, NativeInputApi.KeyCode.LShift);
			Add(NativeInputApi.InputAction.RightStagedFlipper, NativeInputApi.KeyCode.RShift);
			Add(NativeInputApi.InputAction.UpperLeftFlipper, NativeInputApi.KeyCode.A);
			Add(NativeInputApi.InputAction.UpperRightFlipper, NativeInputApi.KeyCode.Quote);

			// Magna saves
			Add(NativeInputApi.InputAction.LeftMagnasave, NativeInputApi.KeyCode.LControl);
			Add(NativeInputApi.InputAction.RightMagnasave, NativeInputApi.KeyCode.RControl);

			// Start
			Add(NativeInputApi.InputAction.Start, NativeInputApi.KeyCode.D1);

			// Coin chutes
			Add(NativeInputApi.InputAction.CoinInsert1, NativeInputApi.KeyCode.D5);
			Add(NativeInputApi.InputAction.CoinInsert2, NativeInputApi.KeyCode.D4);
			Add(NativeInputApi.InputAction.CoinInsert3, NativeInputApi.KeyCode.D3);
			Add(NativeInputApi.InputAction.CoinInsert4, NativeInputApi.KeyCode.D6);

			// Cabinet/service controls
			Add(NativeInputApi.InputAction.ExtraBall, NativeInputApi.KeyCode.B);
			Add(NativeInputApi.InputAction.Lockbar, NativeInputApi.KeyCode.LAlt);
			Add(NativeInputApi.InputAction.PauseGame, NativeInputApi.KeyCode.P);
			Add(NativeInputApi.InputAction.ExitGame, NativeInputApi.KeyCode.Escape);
			Add(NativeInputApi.InputAction.SlamTilt, NativeInputApi.KeyCode.Home);
			Add(NativeInputApi.InputAction.CoinDoor, NativeInputApi.KeyCode.End);
			Add(NativeInputApi.InputAction.Reset, NativeInputApi.KeyCode.F3);
			Add(NativeInputApi.InputAction.Service1, NativeInputApi.KeyCode.D7);
			Add(NativeInputApi.InputAction.Service2, NativeInputApi.KeyCode.D8);
			Add(NativeInputApi.InputAction.Service3, NativeInputApi.KeyCode.D9);
			Add(NativeInputApi.InputAction.Service4, NativeInputApi.KeyCode.D0);
			Add(NativeInputApi.InputAction.Service6, NativeInputApi.KeyCode.PageUp);
			Add(NativeInputApi.InputAction.Service7, NativeInputApi.KeyCode.PageDown);

			// Nudging
			Add(NativeInputApi.InputAction.LeftNudge, NativeInputApi.KeyCode.Z);
			Add(NativeInputApi.InputAction.RightNudge, NativeInputApi.KeyCode.Slash);
			Add(NativeInputApi.InputAction.CenterNudge, NativeInputApi.KeyCode.Space);
			Add(NativeInputApi.InputAction.Tilt, NativeInputApi.KeyCode.T);

			// Plunger
			Add(NativeInputApi.InputAction.Plunge, NativeInputApi.KeyCode.Return);

			return list;
		}

		/// <summary>
		/// Input event callback from native layer (called on input polling thread)
		/// </summary>
		[MonoPInvokeCallback(typeof(NativeInputApi.InputEventCallback))]
		private static void OnInputEvent(ref NativeInputApi.InputEvent evt, IntPtr userData)
		{
			if (Interlocked.Exchange(ref _loggedFirstEvent, 1) == 0) {
				Logger.Info($"{LogPrefix} [NativeInputManager] First event: Type={evt.EventType}, Action={evt.Action}, Device={evt.DeviceIndex}, Axis={evt.AxisId}, Value={evt.Value}, Timestamp={evt.TimestampUsec}");
			}
			if (Logger.IsTraceEnabled) {
				Logger.Trace($"{LogPrefix} [NativeInputManager] Received from native: Type={evt.EventType}, Action={evt.Action}, Device={evt.DeviceIndex}, Axis={evt.AxisId}, Value={evt.Value}, Timestamp={evt.TimestampUsec}");
			}

			// Drop input while the app window isn't focused, so background key presses don't reach the game.
			if (!_appFocused) {
				return;
			}

			var instance = Volatile.Read(ref _instance);
			instance?.MarkInputEventActivity();
			if (evt.EventType == (int)NativeInputApi.InputEventType.Axis) {
				instance?.AxisInputReceived?.Invoke(evt);
				return;
			}

			// Forward action events to simulation thread via ring buffer.
			instance?._simulationThread?.EnqueueInputEvent(evt);
		}

		private void MarkInputEventActivity()
		{
			Interlocked.Increment(ref _inputEventsInWindow);

			var nowTicks = Stopwatch.GetTimestamp();
			var startTicks = Volatile.Read(ref _inputPerfWindowStartTicks);
			var elapsedSeconds = (nowTicks - startTicks) / (double)Stopwatch.Frequency;
			if (elapsedSeconds < PerfSampleWindowSeconds) {
				return;
			}

			if (Interlocked.CompareExchange(ref _inputPerfWindowStartTicks, nowTicks, startTicks) != startTicks) {
				return;
			}

			var eventsInWindow = Interlocked.Exchange(ref _inputEventsInWindow, 0);
			var rate = elapsedSeconds > 0.0 ? eventsInWindow / elapsedSeconds : 0.0;
			Volatile.Write(ref _actualEventRateHz, (float)rate);
		}

		#endregion

		#region Dispose

		public void Dispose()
		{
			StopPolling();

			if (_initialized)
			{
				NativeInputApi.VpeInputShutdown();
				_initialized = false;
			}

			_instance = null;
		}

		#endregion
	}

	public sealed class NativeInputDeviceInfo
	{
		public NativeInputDeviceInfo(int deviceIndex, string id, string name, bool isConnected, IReadOnlyList<NativeInputAxisInfo> axes)
		{
			DeviceIndex = deviceIndex;
			Id = id;
			Name = name;
			IsConnected = isConnected;
			Axes = axes;
		}

		public int DeviceIndex { get; }
		public string Id { get; }
		public string Name { get; }
		public bool IsConnected { get; }
		public IReadOnlyList<NativeInputAxisInfo> Axes { get; }
	}

	public readonly struct NativeInputAxisInfo
	{
		public NativeInputAxisInfo(int axisId, string name, int usagePage, int usage, NativeInputApi.AxisKind kind, float rawValue, long timestampUsec)
		{
			AxisId = axisId;
			Name = name;
			UsagePage = usagePage;
			Usage = usage;
			Kind = kind;
			RawValue = rawValue;
			TimestampUsec = timestampUsec;
		}

		public int AxisId { get; }
		public string Name { get; }
		public int UsagePage { get; }
		public int Usage { get; }
		public NativeInputApi.AxisKind Kind { get; }
		public float RawValue { get; }
		public long TimestampUsec { get; }
	}
}
