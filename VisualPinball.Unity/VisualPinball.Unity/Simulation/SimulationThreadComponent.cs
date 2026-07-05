// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Common;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Serialized nudge sensor settings stored on <see cref="SimulationThreadComponent"/>.
	/// </summary>
	/// <remarks>
	/// The component stores mappings as strings so they can be packed with the
	/// table and survive native device ids that contain punctuation. At runtime
	/// <see cref="ToEngineConfig"/> converts these strings into managed
	/// <see cref="NudgeSensorConfig"/> objects used by the physics engine.
	/// </remarks>
	[Serializable]
	public sealed class SimulationThreadNudgeSensorConfig
	{
		public NudgeSensorType Type = NudgeSensorType.CabinetDirect;

		[Range(0f, 2f)]
		public float Strength = 1f;

		[Range(0f, 200f)]
		public float CabinetMassKg = 113f;

		[Tooltip("Rotates the sensor board X/Y axes into cabinet coordinates.")]
		public NudgeSensorMountRotation MountRotation = NudgeSensorMountRotation.Rotation0;

		[Tooltip("Mirrors the sensor board X axis before applying mount rotation.")]
		public bool MountMirror;

		public string X = string.Empty;
		public string Y = string.Empty;
		public string AccelerationX = string.Empty;
		public string AccelerationY = string.Empty;
		public string VelocityX = string.Empty;
		public string VelocityY = string.Empty;

		/// <summary>
		/// Clamps numeric values and normalizes null mapping strings.
		/// </summary>
		public void Normalize()
		{
			Strength = Mathf.Clamp(Strength, 0f, 2f);
			CabinetMassKg = Mathf.Clamp(CabinetMassKg <= 0f ? 113f : CabinetMassKg, 0f, 200f);
			MountRotation = NudgeSensorMountTransform.NormalizeRotation(MountRotation);
			X ??= string.Empty;
			Y ??= string.Empty;
			AccelerationX ??= string.Empty;
			AccelerationY ??= string.Empty;
			VelocityX ??= string.Empty;
			VelocityY ??= string.Empty;
		}

		/// <summary>
		/// Converts serialized component settings to the runtime nudge config.
		/// </summary>
		public NudgeSensorConfig ToEngineConfig()
		{
			return ToCabinetSettings().ToEngineConfig();
		}

		/// <summary>
		/// Converts this component-specific sensor shape into the shared cabinet
		/// input settings object.
		/// </summary>
		public CabinetNudgeSensorSettings ToCabinetSettings()
		{
			return CabinetNudgeSensorSettings.From(this);
		}

		/// <summary>
		/// Captures the current raw value of every mapped axis as its neutral
		/// center.
		/// </summary>
		/// <returns>The number of mappings whose raw center was updated.</returns>
		public int CalibrateRawCenters(IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			var count = 0;
			count += CalibrateRawCenter(ref X, devices);
			count += CalibrateRawCenter(ref Y, devices);
			count += CalibrateRawCenter(ref AccelerationX, devices);
			count += CalibrateRawCenter(ref AccelerationY, devices);
			count += CalibrateRawCenter(ref VelocityX, devices);
			count += CalibrateRawCenter(ref VelocityY, devices);
			return count;
		}

		/// <summary>
		/// Clears saved neutral centers from every mapping.
		/// </summary>
		public void ResetRawCenters()
		{
			ResetRawCenter(ref X);
			ResetRawCenter(ref Y);
			ResetRawCenter(ref AccelerationX);
			ResetRawCenter(ref AccelerationY);
			ResetRawCenter(ref VelocityX);
			ResetRawCenter(ref VelocityY);
		}

		/// <summary>
		/// Builds a serialized mapping from a native device/axis pair.
		/// </summary>
		internal static string BuildMapping(NativeInputDeviceInfo device, NativeInputAxisInfo axis,
			SensorMappingKind kind, float scale, float rawCenter)
		{
			return new SensorMapping {
				DeviceId = device.Id,
				AxisId = axis.AxisId,
				Kind = kind,
				DeadZone = 0.02f,
				Scale = scale,
				Limit = 1f,
				RawCenter = rawCenter
			}.ToString();
		}

		/// <summary>
		/// Updates one serialized mapping with the current raw axis center.
		/// </summary>
		private static int CalibrateRawCenter(ref string value, IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			if (!SensorMapping.TryParse(value, out var mapping)) {
				return 0;
			}
			if (!TryFindAxis(devices, mapping.DeviceId, mapping.AxisId, out var axis)) {
				return 0;
			}
			mapping.RawCenter = axis.RawValue;
			value = mapping.ToString();
			return 1;
		}

		/// <summary>
		/// Clears one serialized mapping's raw center without changing device/axis
		/// identity.
		/// </summary>
		private static void ResetRawCenter(ref string value)
		{
			if (!SensorMapping.TryParse(value, out var mapping)) {
				return;
			}
			mapping.RawCenter = 0f;
			value = mapping.ToString();
		}

		/// <summary>
		/// Finds the current axis snapshot for a serialized mapping.
		/// </summary>
		private static bool TryFindAxis(IReadOnlyList<NativeInputDeviceInfo> devices, string deviceId, int axisId,
			out NativeInputAxisInfo axis)
		{
			if (devices != null) {
				for (var i = 0; i < devices.Count; i++) {
					var device = devices[i];
					if (device.Id != deviceId || device.Axes == null) {
						continue;
					}
					for (var j = 0; j < device.Axes.Count; j++) {
						if (device.Axes[j].AxisId == axisId) {
							axis = device.Axes[j];
							return true;
						}
					}
				}
			}
			axis = default;
			return false;
		}
	}

	/// <summary>
	/// Unity component that manages the high-performance simulation thread.
	/// Add this to your table GameObject to enable sub-millisecond input latency.
	///
	/// Architecture:
	/// - Simulation thread runs at 1000 Hz (1ms per tick)
	/// - Input polling thread runs at 500-1000 Hz
	/// - Unity main thread runs at display refresh rate (60-144 Hz)
	/// - Lock-free communication between threads using ring buffers and triple-buffering
	/// </summary>
	[AddComponentMenu("Visual Pinball/Simulation Thread")]
	[RequireComponent(typeof(PhysicsEngine))]
	[PackAs("SimulationThread")]
	public class SimulationThreadComponent : MonoBehaviour, IPackable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const string LogPrefix = "[VPE]";

		public static SimulationThreadComponent EnsureFor(PhysicsEngine physicsEngine)
		{
			if (!physicsEngine) {
				return null;
			}

			return physicsEngine.GetComponent<SimulationThreadComponent>()
				?? physicsEngine.gameObject.AddComponent<SimulationThreadComponent>();
		}

		public static SimulationThreadComponent EnsureForTable(GameObject tableRoot)
		{
			if (!tableRoot) {
				return null;
			}

			return EnsureFor(tableRoot.GetComponentInChildren<PhysicsEngine>(true));
		}

		#region Inspector Fields

		[Header("Simulation Settings")]
		[Tooltip("Enable the high-performance simulation thread (1000 Hz)")]
		public bool EnableSimulationThread = true;

		[Tooltip("Enable native input polling (requires the VisualPinball.NativeInput native plugin for the current platform)")]
		public bool EnableNativeInput = true;

		[Tooltip("Input polling interval in microseconds (default 500 us = 2000 Hz)")]
		[Range(100, 2000)]
		public int InputPollingIntervalUs = 500;

		[Header("Nudge Sensors")]
		[Tooltip("Default analog nudge sensor mappings used by editor/direct play mode. The player app can still override these from its user config.")]
		public List<SimulationThreadNudgeSensorConfig> NudgeSensors = new();

		[Header("Debug")]
		[Tooltip("Show simulation statistics in console")]
		public bool ShowStatistics = false;

		[Tooltip("Statistics update interval in seconds")]
		[Range(1f, 10f)]
		public float StatisticsInterval = 5f;

		#endregion

		#region Fields

		private PhysicsEngine _physicsEngine;
		private IGamelogicEngine _gamelogicEngine;
		private SimulationThread _simulationThread;
		private NativeInputManager _inputManager;

		private bool _started = false;
		private float _lastStatisticsTime;
		private float _simulationThreadSpeedX;
		private float _simulationThreadHz;
		private long _lastSampleSimulationUsec;
		private float _lastSampleUnscaledTime;

		public float SimulationThreadSpeedX => _simulationThreadSpeedX;
		public float SimulationThreadHz => _simulationThreadHz;
		public float InputThreadTargetHz => _inputManager?.TargetPollingHz ?? 0f;
		public float InputThreadActualHz => _inputManager?.ActualEventRateHz ?? 0f;
		public bool IsRunning => _started;

		#endregion

		#region Packaging

		public byte[] Pack() => SimulationThreadComponentPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => SimulationThreadComponentPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		#region Unity Lifecycle

		private void Awake()
		{
			_physicsEngine = GetComponent<PhysicsEngine>();
			// Note: Player.GamelogicEngine is assigned in Player.Awake().
			// Script execution order can cause this Awake() to run first, so we resolve
			// the engine again in StartSimulation().
		}

		private void Start()
		{
			if (!EnableSimulationThread)
			{
				Logger.Info($"{LogPrefix} [SimulationThreadComponent] Simulation thread disabled");
				return;
			}

			StartSimulation();
		}

		private void Update()
		{
			if (!_started || _simulationThread == null) return;

			_simulationThread.SyncClockFromMainThread(_physicsEngine.CurrentSimulationClockUsec, _physicsEngine.CurrentSimulationClockScale);

			// Engines that are not thread-safe for switch updates receive queued
			// events here on Unity's main thread.
			_simulationThread.FlushMainThreadInputDispatch();

			// Read shared state from simulation thread
			ref readonly var state = ref _simulationThread.GetSharedState();

			// Apply state to Unity GameObjects
			ApplySimulationState(in state);

			UpdateSimulationSpeed(in state);

			// Show statistics
			if (ShowStatistics && Time.time - _lastStatisticsTime >= StatisticsInterval)
			{
				LogStatistics(in state);
				_lastStatisticsTime = Time.time;
			}
		}

		private void OnDestroy()
		{
			StopSimulation();
		}

		private void OnDisable()
		{
			StopSimulation();
		}

		private void OnApplicationQuit()
		{
			StopSimulation();
		}

		#endregion

		#region Public API

		/// <summary>
		/// Starts the simulation thread, native input polling, and external physics
		/// timing.
		/// </summary>
		public void StartSimulation()
		{
			if (_started) return;

			try
			{
				var player = GetComponent<Player>() ?? GetComponentInParent<Player>() ?? GetComponentInChildren<Player>();

				// Resolve dependencies (safe even if Awake order differs)
				_physicsEngine ??= GetComponent<PhysicsEngine>();
				if (_gamelogicEngine == null) {
					_gamelogicEngine = player != null
						? player.GamelogicEngine
						: (GetComponent<IGamelogicEngine>() ?? GetComponentInParent<IGamelogicEngine>() ?? GetComponentInChildren<IGamelogicEngine>());
				}

				if (_gamelogicEngine == null) {
					Logger.Warn($"{LogPrefix} [SimulationThreadComponent] No IGamelogicEngine found (input will not reach PinMAME)");
				}

				// Enable external timing on PhysicsEngine
				// This disables Unity's Update() loop and gives control to the simulation thread
				_physicsEngine.SetExternalTiming(true);
				ApplyNudgeSensorSettings();

				// Create simulation thread
				_simulationThread = new SimulationThread(_physicsEngine, _gamelogicEngine,
					player != null
						? new Action<string, bool>((coilId, isEnabled) => player.DispatchCoilSimulationThread(coilId, isEnabled))
						: null);
				ConfigureTiltBobRouting();
				_simulationThread.SyncClockFromMainThread(_physicsEngine.CurrentSimulationClockUsec, _physicsEngine.CurrentSimulationClockScale);

				// Provide the triple-buffered SimulationState to PhysicsEngine so
				// that ApplyMovements() can read lock-free snapshots.
				_physicsEngine.SetSimulationState(_simulationThread.SharedState);

				// Initialize and start native input if enabled
				if (EnableNativeInput)
				{
					_inputManager = NativeInputManager.Instance;
					if (_inputManager.Initialize())
					{
						_inputManager.SetSimulationThread(_simulationThread);
						_physicsEngine.AttachNativeInputManager(_inputManager);
						_inputManager.StartPolling(InputPollingIntervalUs);
					}
					else
					{
						Logger.Warn($"{LogPrefix} [SimulationThreadComponent] Native input not available, falling back to Unity Input System");
					}
				}

				// Start simulation thread
				_simulationThread.Start();

				_started = true;
				_lastStatisticsTime = Time.time;

				Logger.Info($"{LogPrefix} [SimulationThreadComponent] Simulation started with external physics timing");
			}
			catch (Exception ex)
			{
				Logger.Error($"{LogPrefix} [SimulationThreadComponent] Failed to start simulation: {ex}");
			}
		}

		/// <summary>
		/// Stops the simulation and input threads and returns the physics engine to
		/// Unity-driven timing.
		/// </summary>
		public void StopSimulation()
		{
			if (!_started) return;

			_physicsEngine?.DetachNativeInputManager(_inputManager);
			_inputManager?.StopPolling();
			_inputManager?.Dispose();
			_inputManager = null;
			_simulationThread?.Stop();
			_simulationThread?.Dispose();
			_simulationThread = null;

			// Restore normal Unity Update() loop timing
			_physicsEngine.SetExternalTiming(false);
			_physicsEngine.SetSimulationState(null);

			_started = false;
			_simulationThreadSpeedX = 0f;
			_simulationThreadHz = 0f;
			_lastSampleSimulationUsec = 0;
			_lastSampleUnscaledTime = 0f;

			Logger.Info($"{LogPrefix} [SimulationThreadComponent] Simulation stopped");
		}

		/// <summary>
		/// Pause the simulation (for debugging)
		/// </summary>
		public void PauseSimulation()
		{
			_simulationThread?.Pause();
		}

		/// <summary>
		/// Resume the simulation
		/// </summary>
		public void ResumeSimulation()
		{
			_simulationThread?.Resume();
		}

		/// <summary>
		/// Returns a copy of the latest simulation-state snapshot (timing/latency/diagnostics) for a
		/// performance overlay. Empty <c>default</c> when not running.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		public SimulationState.Snapshot GetCurrentSnapshot()
		{
			return _started && _simulationThread != null ? _simulationThread.GetSharedState() : default;
		}

		/// <summary>
		/// The measured input-to-on-screen latency in milliseconds (input poll to flipper visual movement),
		/// averaged over the calls since the last sample. Returns 0 until the first flipper press is observed.
		/// Works for any table with flippers (independent of the gamelogic engine's coil-latency stats).
		/// Main-thread only.
		/// </summary>
		public float SampleInputLatencyMs() => InputLatencyTracker.SampleFlipperLatencyMs(false);

		/// <summary>
		/// Lists native input devices that can be mapped to nudge sensors.
		/// </summary>
		public IReadOnlyList<NativeInputDeviceInfo> ListNudgeInputDevices()
		{
			var inputManager = _inputManager ?? NativeInputManager.TryGetExistingInstance();
			return inputManager == null ? Array.Empty<NativeInputDeviceInfo>() : inputManager.ListDevices();
		}

		/// <summary>
		/// Captures the current native-input settings from this component and the
		/// current nudge settings from the sibling physics engine.
		/// </summary>
		public CabinetInputSettings GetCabinetInputSettings()
		{
			_physicsEngine ??= GetComponent<PhysicsEngine>();
			var settings = new CabinetInputSettings {
				enableNativeInput = EnableNativeInput,
				inputPollingIntervalUs = InputPollingIntervalUs,
				nudge = _physicsEngine != null ? _physicsEngine.GetNudgeSettings() : new CabinetNudgeSettings()
			};
			settings.nudge.sensors = CabinetNudgeSensorSettings.FromSimulationThreadSensors(NudgeSensors);
			settings.Normalize();
			return settings;
		}

		/// <summary>
		/// Applies shared cabinet-input settings to this component and, optionally,
		/// to the sibling physics engine.
		/// </summary>
		public void ApplyCabinetInputSettings(CabinetInputSettings settings, bool applyNudgeToPhysics = true)
		{
			settings ??= new CabinetInputSettings();
			settings.Normalize();

			EnableNativeInput = settings.enableNativeInput;
			InputPollingIntervalUs = settings.inputPollingIntervalUs;
			ApplyNudgeSettings(settings.nudge, applyNudgeToPhysics);
			ApplyNativeInputSettingsIfRunning();
		}

		/// <summary>
		/// Applies only the shared nudge settings, keeping native input polling
		/// options unchanged.
		/// </summary>
		public void ApplyNudgeSettings(CabinetNudgeSettings settings, bool applyNudgeToPhysics = true)
		{
			settings ??= new CabinetNudgeSettings();
			settings.Normalize();

			NudgeSensors = settings.ToSimulationThreadSensorConfigs();
			ApplyNudgeSensorSettings();

			if (applyNudgeToPhysics) {
				_physicsEngine ??= GetComponent<PhysicsEngine>();
				settings.ApplyTo(_physicsEngine);
			} else {
				ConfigureTiltBobRouting();
			}
		}

		/// <summary>
		/// Routes plumb tilt edges between the simulation thread and the table's tilt-bob
		/// component that owns the authored switch mapping.
		/// </summary>
		internal void ConfigureTiltBobRouting(TiltBobComponent tiltBob = null)
		{
			if (_simulationThread == null) {
				return;
			}

			tiltBob ??= FindTiltBobComponent();
			if (tiltBob != null && (tiltBob.UsesSimulatedPlumb || tiltBob.UsesPhysicalTiltInput)) {
				_simulationThread.MainThreadTiltDispatcher = tiltBob.QueueTiltStateFromSimulationThread;
				_simulationThread.DispatchPhysicalTiltInputToMainThread = tiltBob.UsesPhysicalTiltInput;
				return;
			}

			_simulationThread.MainThreadTiltDispatcher = null;
			_simulationThread.DispatchPhysicalTiltInputToMainThread = false;
		}

		private TiltBobComponent FindTiltBobComponent()
		{
			_physicsEngine ??= GetComponent<PhysicsEngine>();
			if (_physicsEngine != null) {
				return TiltBobComponent.FindFor(_physicsEngine);
			}

			return GetComponentInParent<TiltBobComponent>(true)
			       ?? GetComponentInChildren<TiltBobComponent>(true);
		}

		/// <summary>
		/// Reconciles native input polling with the current component fields when
		/// settings are changed while Play Mode is already running.
		/// </summary>
		private void ApplyNativeInputSettingsIfRunning()
		{
			if (!_started || _simulationThread == null) {
				return;
			}

			if (!EnableNativeInput) {
				_physicsEngine?.DetachNativeInputManager(_inputManager);
				_inputManager?.StopPolling();
				return;
			}

			_inputManager ??= NativeInputManager.Instance;
			if (!_inputManager.Initialize()) {
				Logger.Warn($"{LogPrefix} [SimulationThreadComponent] Native input not available, falling back to Unity Input System");
				return;
			}

			_inputManager.SetSimulationThread(_simulationThread);
			_physicsEngine?.AttachNativeInputManager(_inputManager);
			var pollingIntervalUs = InputPollingIntervalUs;
#if UNITY_EDITOR
			if (pollingIntervalUs < 1000) {
				pollingIntervalUs = 1000;
			}
#endif
			if (_inputManager.IsPolling) {
				var targetPollingHz = pollingIntervalUs > 0 ? 1000000f / pollingIntervalUs : 0f;
				if (Mathf.Abs(_inputManager.TargetPollingHz - targetPollingHz) < 0.1f) {
					return;
				}
				_inputManager.StopPolling();
			}
			_inputManager.StartPolling(InputPollingIntervalUs);
		}

		/// <summary>
		/// Pushes serialized nudge sensor settings into the physics engine.
		/// </summary>
		public void ApplyNudgeSensorSettings()
		{
			if (_physicsEngine == null) {
				_physicsEngine = GetComponent<PhysicsEngine>();
			}
			if (_physicsEngine == null) {
				return;
			}

			NudgeSensors ??= new List<SimulationThreadNudgeSensorConfig>();
			if (NudgeSensors.Count > NudgeState.MaxSensors) {
				NudgeSensors.RemoveRange(NudgeState.MaxSensors, NudgeSensors.Count - NudgeState.MaxSensors);
			}

			var configs = new List<NudgeSensorConfig>(NudgeSensors.Count);
			for (var i = 0; i < NudgeSensors.Count; i++) {
				NudgeSensors[i] ??= new SimulationThreadNudgeSensorConfig();
				configs.Add(NudgeSensors[i].ToEngineConfig());
			}
			_physicsEngine.ConfigureNudgeSensors(configs);
		}

		/// <summary>
		/// Calibrates all mapped nudge channels around their current raw input
		/// values.
		/// </summary>
		/// <remarks>
		/// This is intentionally a center capture, not a gain calibration pass. It
		/// solves the common KL25Z/Pinscape case where a resting accelerometer does
		/// not report exactly zero, while the physics-side gain calibrator continues
		/// to learn velocity/acceleration scale from motion.
		/// </remarks>
		public int CalibrateNudgeSensorCenters()
		{
			var devices = ListNudgeInputDevices();
			var calibrated = 0;
			if (NudgeSensors != null) {
				foreach (var sensor in NudgeSensors) {
					calibrated += sensor?.CalibrateRawCenters(devices) ?? 0;
				}
			}
			if (calibrated > 0) {
				ApplyNudgeSensorSettings();
			}
			return calibrated;
		}

		/// <summary>
		/// Clears saved raw centers for all nudge mappings.
		/// </summary>
		public int ResetNudgeSensorCenters()
		{
			var reset = 0;
			if (NudgeSensors != null) {
				foreach (var sensor in NudgeSensors) {
					if (sensor == null) {
						continue;
					}
					sensor.ResetRawCenters();
					reset++;
				}
			}
			if (reset > 0) {
				ApplyNudgeSensorSettings();
			}
			return reset;
		}

		/// <summary>
		/// Attempts to map the first connected cabinet-style device to acceleration
		/// X/Y channels.
		/// </summary>
		/// <remarks>
		/// The heuristic prefers HID X/Y accelerometer usages, then falls back to
		/// named X/Y axes, and finally any two non-Z acceleration axes. This keeps
		/// one-click setup useful for KL25Z/Pinscape boards without hard-coding a
		/// vendor-specific device name.
		/// </remarks>
		public bool TryAutoConfigureFirstCabinetSensor(out string message)
		{
			var devices = ListNudgeInputDevices();
			for (var i = 0; i < devices.Count; i++) {
				var device = devices[i];
				if (!device.IsConnected || device.Axes == null) {
					continue;
				}
				if (TryPickAxisPair(device, out var xAxis, out var yAxis)) {
					NudgeSensors ??= new List<SimulationThreadNudgeSensorConfig>();
					var existingSensor = NudgeSensors.Count == 0 ? null : NudgeSensors[0];
					var sensor = new SimulationThreadNudgeSensorConfig {
						Type = NudgeSensorType.CabinetDirect,
						Strength = 1f,
						CabinetMassKg = 113f,
						MountRotation = existingSensor?.MountRotation ?? NudgeSensorMountRotation.Rotation0,
						MountMirror = existingSensor?.MountMirror ?? false,
						AccelerationX = SimulationThreadNudgeSensorConfig.BuildMapping(device, xAxis,
							SensorMappingKind.Acceleration, 9.81f, xAxis.RawValue),
						AccelerationY = SimulationThreadNudgeSensorConfig.BuildMapping(device, yAxis,
							SensorMappingKind.Acceleration, 9.81f, yAxis.RawValue)
					};
					if (NudgeSensors.Count == 0) {
						NudgeSensors.Add(sensor);
					} else {
						NudgeSensors[0] = sensor;
					}
					ApplyNudgeSensorSettings();
					var deviceName = string.IsNullOrEmpty(device.Name) ? device.Id : device.Name;
					message = $"Mapped {deviceName} axes {AxisName(xAxis)}/{AxisName(yAxis)}.";
					return true;
				}
			}

			message = "No connected input device with two usable axes was found.";
			return false;
		}

		internal bool EnqueueSwitchFromMainThread(string switchId, bool isClosed)
		{
			if (!_started || _simulationThread == null) {
				return false;
			}

			return _simulationThread.EnqueueExternalSwitch(switchId, isClosed);
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Apply simulation state to Unity GameObjects
		/// </summary>
		private void ApplySimulationState(in SimulationState.Snapshot state)
		{
			// Animation data (ball positions, flipper angles, etc.) is now
			// applied lock-free by PhysicsEngine.ApplyMovements() via the
			// triple-buffered snapshot. This method handles any remaining
			// state that isn't covered by the snapshot (PinMAME coils, lamps,
			// GI).
			if (_gamelogicEngine is IGamelogicSharedStateApplier sharedStateApplier) {
				sharedStateApplier.ApplySharedState(in state);
			}
		}

		/// <summary>
		/// Log statistics about simulation performance
		/// </summary>
		private void LogStatistics(in SimulationState.Snapshot state)
		{
			long simTimeMs = state.SimulationTimeUsec / 1000;
			long realTimeMs = state.RealTimeUsec / 1000;
			double ratio = (double)simTimeMs / realTimeMs;
			var snapshotAgeUsec = state.PublishRealTimeUsec > 0 ? TimestampUsec - state.PublishRealTimeUsec : 0;
			var switchToObservationUsec = state.LastSwitchDispatchUsec > 0 && state.LastSwitchObservationUsec >= state.LastSwitchDispatchUsec
				? state.LastSwitchObservationUsec - state.LastSwitchDispatchUsec
				: -1;
			var flipperToCoilOutputUsec = state.LastFlipperInputUsec > 0 && state.LastCoilOutputUsec >= state.LastFlipperInputUsec
				? state.LastCoilOutputUsec - state.LastFlipperInputUsec
				: -1;
			var coilDispatchToPublishUsec = state.LastCoilDispatchUsec > 0 && state.PublishRealTimeUsec >= state.LastCoilDispatchUsec
				? state.PublishRealTimeUsec - state.LastCoilDispatchUsec
				: -1;

			Logger.Info($"{LogPrefix} [SimulationThread] Stats: SimTime={simTimeMs}ms, RealTime={realTimeMs}ms, Ratio={ratio:F3}x, PhysicsVer={state.PhysicsStateVersion}, Tick={state.SimulationTickDurationUsec}us, Snapshot={state.SnapshotCopyUsec}us, Kinematic={state.KinematicScanUsec}us, EventDrain={state.EventDrainUsec}us, InputQ={state.PendingInputActionCount}, ScheduledQ={state.PendingScheduledActionCount}, SwitchQ={state.ExternalSwitchQueueDepth}, GLE={state.GamelogicCallbackRateHz:F1}Hz, Fence={state.FenceUpdateIntervalUsec}us, SnapshotAge={snapshotAgeUsec}us, Switch->PinMAME={switchToObservationUsec}us, Flipper->Coil={flipperToCoilOutputUsec}us, Coil->Publish={coilDispatchToPublishUsec}us, Balls={state.BallCount}/{state.BallSourceCount}, Floats={state.FloatAnimationCount}/{state.FloatAnimationSourceCount}, Float2={state.Float2AnimationCount}/{state.Float2AnimationSourceCount}");

			if (state.BallSnapshotsTruncated != 0 || state.FloatAnimationsTruncated != 0 || state.Float2AnimationsTruncated != 0) {
				Logger.Warn($"{LogPrefix} [SimulationThread] Snapshot truncation detected: Balls={state.BallSnapshotsTruncated != 0}, Floats={state.FloatAnimationsTruncated != 0}, Float2={state.Float2AnimationsTruncated != 0}");
			}
		}

		private static long TimestampUsec => (Stopwatch.GetTimestamp() * 1_000_000L) / Stopwatch.Frequency;

		/// <summary>
		/// Picks a likely X/Y axis pair from one native device.
		/// </summary>
		private static bool TryPickAxisPair(NativeInputDeviceInfo device, out NativeInputAxisInfo xAxis,
			out NativeInputAxisInfo yAxis)
		{
			xAxis = default;
			yAxis = default;
			if (TryFindAxis(device, "X", 0x30, NativeInputApi.AxisKind.Acceleration, out xAxis) &&
			    TryFindAxis(device, "Y", 0x31, NativeInputApi.AxisKind.Acceleration, out yAxis) &&
			    xAxis.AxisId != yAxis.AxisId) {
				return true;
			}

			var xSet = TryFindAxis(device, "X", 0x30, null, out xAxis);
			var ySet = TryFindAxis(device, "Y", 0x31, null, out yAxis);
			if (xSet && ySet && xAxis.AxisId != yAxis.AxisId) {
				return true;
			}

			for (var i = 0; i < device.Axes.Count; i++) {
				var axis = device.Axes[i];
				if (axis.Kind != NativeInputApi.AxisKind.Acceleration || IsNamedAxis(axis, "Z", 0x32)) {
					continue;
				}
				if ((xSet && axis.AxisId == xAxis.AxisId) || (ySet && axis.AxisId == yAxis.AxisId)) {
					continue;
				}
				if (!xSet) {
					xAxis = axis;
					xSet = true;
				} else {
					yAxis = axis;
					ySet = true;
					break;
				}
			}

			return xSet && ySet && xAxis.AxisId != yAxis.AxisId;
		}

		/// <summary>
		/// Finds an axis by HID usage or display name, optionally constrained by
		/// reported axis kind.
		/// </summary>
		private static bool TryFindAxis(NativeInputDeviceInfo device, string axisName, int usage,
			NativeInputApi.AxisKind? kind, out NativeInputAxisInfo axis)
		{
			if (device.Axes != null) {
				for (var i = 0; i < device.Axes.Count; i++) {
					var candidate = device.Axes[i];
					if (kind.HasValue && candidate.Kind != kind.Value) {
						continue;
					}
					if (IsNamedAxis(candidate, axisName, usage)) {
						axis = candidate;
						return true;
					}
				}
			}
			axis = default;
			return false;
		}

		/// <summary>
		/// Checks whether a native axis looks like the requested X/Y/Z axis.
		/// </summary>
		private static bool IsNamedAxis(NativeInputAxisInfo axis, string axisName, int usage)
		{
			if (axis.Usage == usage) {
				return true;
			}

			var name = axis.Name;
			if (string.IsNullOrEmpty(name)) {
				return false;
			}
			if (string.Equals(name, axisName, StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			return name.EndsWith(" " + axisName, StringComparison.OrdinalIgnoreCase)
			       || name.EndsWith("-" + axisName, StringComparison.OrdinalIgnoreCase)
			       || name.EndsWith("_" + axisName, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Returns a human-readable axis label for inspector messages.
		/// </summary>
		private static string AxisName(NativeInputAxisInfo axis)
		{
			return string.IsNullOrEmpty(axis.Name) ? $"Axis {axis.AxisId}" : axis.Name;
		}

		/// <summary>
		/// Updates smoothed simulation speed diagnostics for the inspector.
		/// </summary>
		private void UpdateSimulationSpeed(in SimulationState.Snapshot state)
		{
			var now = Time.unscaledTime;

			if (_lastSampleUnscaledTime <= 0f) {
				_lastSampleUnscaledTime = now;
				_lastSampleSimulationUsec = state.SimulationTimeUsec;
				_simulationThreadSpeedX = 0f;
				_simulationThreadHz = 0f;
				return;
			}

			var deltaTime = now - _lastSampleUnscaledTime;
			if (deltaTime < 0.05f) {
				return;
			}

			var deltaSimulationUsec = state.SimulationTimeUsec - _lastSampleSimulationUsec;
			if (deltaSimulationUsec < 0) {
				deltaSimulationUsec = 0;
			}

			var instantSpeedX = deltaTime > 0f ? (float)deltaSimulationUsec / (deltaTime * 1_000_000f) : 0f;
			_simulationThreadSpeedX = Mathf.Lerp(_simulationThreadSpeedX, instantSpeedX, 0.3f);
			_simulationThreadHz = _simulationThreadSpeedX * 1000f;

			_lastSampleUnscaledTime = now;
			_lastSampleSimulationUsec = state.SimulationTimeUsec;
		}

		#endregion
	}
}
