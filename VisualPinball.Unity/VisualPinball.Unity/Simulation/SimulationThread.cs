// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using NLog;
using Logger = NLog.Logger;
using VisualPinball.Engine.Common;
using VisualPinball.Unity;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// High-performance simulation thread that runs physics and PinMAME
	/// at 1000 Hz (1ms per tick) independent of rendering frame rate.
	///
	/// Goals:
	/// - Sub-millisecond input latency
	/// - Decoupled from rendering
	/// - Allocation-free hot path
	/// - Lock-free communication with main thread
	/// </summary>
	public class SimulationThread : IDisposable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const string LogPrefix = "[PinMAME-debug]";

		#region Constants

		private const long TickIntervalUsec = 1000; // 1ms = 1000 microseconds
		private const long BusyWaitThresholdUsec = 100; // Last 100us busy-wait for precision
		private const int MaxCoilOutputsPerTick = 128;
		private const long TimeFenceUpdateIntervalUsec = 5_000;

		#endregion

		#region Fields

		private readonly PhysicsEngine _physicsEngine;
		private readonly IGamelogicEngine _gamelogicEngine;
		private readonly IGamelogicTimeFence _timeFence;
		private readonly IGamelogicCoilOutputFeed _coilOutputFeed;
		private readonly IGamelogicInputDispatcher _inputDispatcher;
		private readonly Action<string, bool> _simulationCoilDispatcher;
		private readonly InputEventBuffer _inputBuffer;
		private readonly SimulationState _sharedState;

		private Thread _thread;
		private volatile bool _running = false;
		private volatile bool _paused = false;

		// Timing (Stopwatch ticks - avoids high-frequency P/Invoke)
		private readonly long _tickIntervalTicks;
		private readonly long _busyWaitThresholdTicks;
		private long _lastTickTicks;
		private long _simulationTimeUsec;
		private long _lastTimeFenceUsec = long.MinValue;

		// Input state tracking (allocation-free indexed arrays)
		private readonly bool[] _actionStates;
		private readonly string[] _actionToSwitchId;
		private volatile bool _inputMappingsBuilt;

		// Statistics
		private long _tickCount = 0;
		private long _inputEventsProcessed = 0;
		private long _inputEventsDropped = 0;

		private volatile bool _gamelogicStarted;
		private volatile bool _needsInitialSwitchSync;

		private readonly object _externalSwitchQueueLock = new object();
		private readonly Queue<PendingSwitchEvent> _externalSwitchQueue = new Queue<PendingSwitchEvent>(128);
		private const int MaxExternalSwitchQueueSize = 8192;

		private readonly struct PendingSwitchEvent
		{
			public readonly string SwitchId;
			public readonly bool IsClosed;

			public PendingSwitchEvent(string switchId, bool isClosed)
			{
				SwitchId = switchId;
				IsClosed = isClosed;
			}
		}

		#endregion

		#region Constructor

		public SimulationThread(PhysicsEngine physicsEngine, IGamelogicEngine gamelogicEngine,
			Action<string, bool> simulationCoilDispatcher)
		{
			_physicsEngine = physicsEngine ?? throw new ArgumentNullException(nameof(physicsEngine));
			_gamelogicEngine = gamelogicEngine;
			_timeFence = gamelogicEngine as IGamelogicTimeFence;
			_coilOutputFeed = gamelogicEngine as IGamelogicCoilOutputFeed;
			_inputDispatcher = GamelogicInputDispatcherFactory.Create(gamelogicEngine);
			_simulationCoilDispatcher = simulationCoilDispatcher;

			_inputBuffer = new InputEventBuffer(1024);
			_sharedState = new SimulationState();

			// Precompute timing constants
			_tickIntervalTicks = (Stopwatch.Frequency * TickIntervalUsec) / 1_000_000;
			_busyWaitThresholdTicks = (Stopwatch.Frequency * BusyWaitThresholdUsec) / 1_000_000;
			if (_tickIntervalTicks <= 0) {
				_tickIntervalTicks = 1;
			}

			// Initialize input state + mapping arrays
			var actionCount = Enum.GetValues(typeof(NativeInputApi.InputAction)).Length;
			_actionStates = new bool[actionCount];
			_actionToSwitchId = new string[actionCount];
			_needsInitialSwitchSync = true;

			if (_gamelogicEngine != null) {
				_gamelogicEngine.OnStarted += OnGamelogicStarted;
			}
		}

		#endregion

		#region Public API

		/// <summary>
		/// Start the simulation thread
		/// </summary>
		public void Start()
		{
			if (_running) return;

			_running = true;
			_paused = false;
			_gamelogicStarted = _gamelogicEngine != null;
			_lastTickTicks = Stopwatch.GetTimestamp();
			_simulationTimeUsec = 0;
			_lastTimeFenceUsec = long.MinValue;
			_tickCount = 0;
			_inputEventsProcessed = 0;
			_inputEventsDropped = 0;
			_needsInitialSwitchSync = true;

			_thread = new Thread(SimulationThreadFunc)
			{
				Name = "VPE Simulation Thread",
				IsBackground = true,
				#if UNITY_EDITOR
				Priority = ThreadPriority.AboveNormal
				#else
				Priority = ThreadPriority.Highest
				#endif
			};
			_thread.Start();

			Logger.Info($"{LogPrefix} [SimulationThread] Started at 1000 Hz");
		}

		/// <summary>
		/// Stop the simulation thread
		/// </summary>
		public void Stop()
		{
			if (!_running) return;

			_running = false;

			if (_thread != null && _thread.IsAlive)
			{
				_thread.Join(5000); // Wait up to 5 seconds
			}

			Logger.Info($"{LogPrefix} [SimulationThread] Stopped after {_tickCount} ticks, {_inputEventsProcessed} input events, {_inputEventsDropped} dropped");
		}

		/// <summary>
		/// Pause the simulation (for debugging)
		/// </summary>
		public void Pause()
		{
			_paused = true;
		}

		/// <summary>
		/// Resume the simulation
		/// </summary>
		public void Resume()
		{
			_paused = false;
		}

		/// <summary>
		/// Enqueue an input event from the input polling thread
		/// </summary>
		public void EnqueueInputEvent(NativeInputApi.InputEvent evt)
		{
			if (!_inputBuffer.TryEnqueue(evt)) {
				Interlocked.Increment(ref _inputEventsDropped);
			}
		}

		/// <summary>
		/// Get the current shared state (for main thread to read)
		/// </summary>
		public ref readonly SimulationState.Snapshot GetSharedState()
		{
			return ref _sharedState.GetFrontBuffer();
		}

		public void FlushMainThreadInputDispatch()
		{
			_inputDispatcher.FlushMainThread();
		}

		public bool EnqueueExternalSwitch(string switchId, bool isClosed)
		{
			if (string.IsNullOrEmpty(switchId)) {
				return false;
			}

			InputLatencyTracker.RecordSwitchInputDispatched(switchId, isClosed);

			lock (_externalSwitchQueueLock) {
				if (_externalSwitchQueue.Count >= MaxExternalSwitchQueueSize) {
					return false;
				}
				_externalSwitchQueue.Enqueue(new PendingSwitchEvent(switchId, isClosed));
				return true;
			}
		}

		#endregion

		#region Simulation Thread

		private void SimulationThreadFunc()
		{
			// Editor playmode is a hostile environment for time-critical threads (domain/scene reload,
			// asset imports, editor windows). Keep time-critical only for player builds.
			#if !UNITY_EDITOR
			NativeInputApi.VpeSetThreadPriority();
			#endif

			// Wait for physics engine to be fully initialized
			// This prevents accessing physics state before it's ready
			Logger.Info($"{LogPrefix} [SimulationThread] Waiting for physics initialization...");
			int waitCount = 0;
			while (_running && _physicsEngine != null && !_physicsEngine.IsInitialized && waitCount < 100)
			{
				Thread.Sleep(50);
				waitCount++;
			}

			if (waitCount >= 100)
			{
				Logger.Error($"{LogPrefix} [SimulationThread] Timeout waiting for physics initialization");
				_running = false;
				return;
			}

			Logger.Info($"{LogPrefix} [SimulationThread] Physics initialized, starting simulation");

			// Try to enable no-GC region for hot path
			bool noGcRegion = false;
			try
			{
				// Allocate 10MB for no-GC region
				if (GC.TryStartNoGCRegion(10 * 1024 * 1024, true))
				{
					noGcRegion = true;
						Logger.Info($"{LogPrefix} [SimulationThread] No-GC region enabled");
				}
			}
			catch (Exception ex)
			{
				Logger.Warn($"{LogPrefix} [SimulationThread] Failed to start no-GC region: {ex.Message}");
			}

			try
			{
				// Build input mappings once (not on hot path)
				BuildInputMappingsIfNeeded();

				// Main simulation loop
				while (_running)
				{
					if (_paused)
					{
						Thread.Sleep(10);
						continue;
					}

					// Timing: Sleep until next tick, then busy-wait for precision.
					long targetTicks = _lastTickTicks + _tickIntervalTicks;
					long nowTicks = Stopwatch.GetTimestamp();
					long sleepTicks = targetTicks - nowTicks;

					if (sleepTicks > _busyWaitThresholdTicks)
					{
						var sleepMs = (int)(((sleepTicks - _busyWaitThresholdTicks) * 1000) / Stopwatch.Frequency);
						if (sleepMs > 0) {
							Thread.Sleep(sleepMs);
						} else {
							Thread.Yield();
						}
					}

					#if !UNITY_EDITOR
					// Busy-wait for precision (last 100us)
					var spinner = new SpinWait();
					while (Stopwatch.GetTimestamp() < targetTicks)
					{
						spinner.SpinOnce();
					}
					#endif

					// Execute simulation tick (hot path - must be allocation-free!)
					SimulationTick();

					_lastTickTicks = targetTicks;
					_tickCount++;
				}
			}
			finally
			{
				// Exit no-GC region
				if (noGcRegion && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
				{
					try
					{
						GC.EndNoGCRegion();
						Logger.Info($"{LogPrefix} [SimulationThread] No-GC region ended");
					}
					catch { }
				}
			}
		}

		/// <summary>
		/// Single simulation tick - MUST BE ALLOCATION-FREE!
		/// </summary>
		private void SimulationTick()
		{
			// 0. Process switch events that originated on Unity/main thread.
			ProcessExternalSwitchEvents();

			// 1. Process input events from ring buffer
			ProcessInputEvents();

			// 2. Apply low-latency coil outputs from gamelogic to simulation-side handlers.
			ProcessGamelogicOutputs();

			// 3. Update physics simulation
			UpdatePhysics();

			// 4. Move the emulation fence after inputs+outputs+physics.
			// Throttle updates to reduce fence wake/sleep churn in PinMAME.
			if (_timeFence != null && (_lastTimeFenceUsec == long.MinValue || _simulationTimeUsec - _lastTimeFenceUsec >= TimeFenceUpdateIntervalUsec)) {
				_timeFence.SetTimeFence(_simulationTimeUsec / 1_000_000.0);
				_lastTimeFenceUsec = _simulationTimeUsec;
			}

			// 5. Write to shared state and swap buffers
			WriteSharedState();

			// Increment simulation time
			_simulationTimeUsec += TickIntervalUsec;
		}

		/// <summary>
		/// Process all pending input events from the ring buffer
		/// </summary>
		private void ProcessInputEvents()
		{
			BuildInputMappingsIfNeeded();

			while (_inputBuffer.TryDequeue(out var evt))
			{
				var actionIndex = evt.Action;
				if ((uint)actionIndex >= (uint)_actionStates.Length) {
					continue;
				}

				bool isPressed = evt.Value > 0.5f;
				bool previousState = _actionStates[actionIndex];
				_actionStates[actionIndex] = isPressed;

				if (previousState == isPressed) {
					continue;
				}

				InputLatencyTracker.RecordInputPolled((NativeInputApi.InputAction)actionIndex, isPressed, evt.TimestampUsec);

				// Only forward to GLE once it's ready (or at least has started)
				if (_gamelogicEngine != null && _gamelogicStarted) {
					SendMappedSwitch(actionIndex, isPressed);
				}
				_inputEventsProcessed++;
			}

			// If the GLE just started, ensure it sees the current input state.
			if (_gamelogicEngine != null && _gamelogicStarted && _needsInitialSwitchSync) {
				SyncAllMappedSwitches();
				_needsInitialSwitchSync = false;
			}
		}

		private void ProcessGamelogicOutputs()
		{
			if (_coilOutputFeed == null || _simulationCoilDispatcher == null) {
				return;
			}

			var processed = 0;
			while (processed < MaxCoilOutputsPerTick && _coilOutputFeed.TryDequeueCoilEvent(out var coilEvent)) {
				_simulationCoilDispatcher(coilEvent.Id, coilEvent.IsEnabled);
				processed++;
			}
		}

		private void ProcessExternalSwitchEvents()
		{
			while (true) {
				PendingSwitchEvent evt;
				lock (_externalSwitchQueueLock) {
					if (_externalSwitchQueue.Count == 0) {
						break;
					}
					evt = _externalSwitchQueue.Dequeue();
				}

				if (_gamelogicEngine != null && _gamelogicStarted) {
					_inputDispatcher.DispatchSwitch(evt.SwitchId, evt.IsClosed);
				}
			}
		}

		private void SendMappedSwitch(int actionIndex, bool isPressed)
		{
			if ((uint)actionIndex >= (uint)_actionToSwitchId.Length) {
				return;
			}
			var switchId = _actionToSwitchId[actionIndex];
			if (switchId == null) {
				return;
			}
			if (actionIndex == (int)NativeInputApi.InputAction.Start && Logger.IsInfoEnabled) {
				Logger.Info($"{LogPrefix} [SimulationThread] Input Start -> Switch({switchId}, {isPressed})");
			}
			if (Logger.IsInfoEnabled && isPressed) {
				if (actionIndex == (int)NativeInputApi.InputAction.LeftFlipper) {
					Logger.Info($"{LogPrefix} [SimulationThread] Input LeftFlipper -> Switch({switchId}, True)");
				}
				else if (actionIndex == (int)NativeInputApi.InputAction.RightFlipper) {
					Logger.Info($"{LogPrefix} [SimulationThread] Input RightFlipper -> Switch({switchId}, True)");
				}
			}
			_inputDispatcher.DispatchSwitch(switchId, isPressed);
		}

		private void SyncAllMappedSwitches()
		{
			for (var i = 0; i < _actionToSwitchId.Length; i++)
			{
				var switchId = _actionToSwitchId[i];
				if (switchId == null) {
					continue;
				}
				_inputDispatcher.DispatchSwitch(switchId, _actionStates[i]);
			}
		}

		private void BuildInputMappingsIfNeeded()
		{
			if (_inputMappingsBuilt) {
				return;
			}
			BuildInputMappings();
			_inputMappingsBuilt = true;
			_needsInitialSwitchSync = true;
		}

		private void BuildInputMappings()
		{
			Array.Clear(_actionToSwitchId, 0, _actionToSwitchId.Length);

			if (_gamelogicEngine == null) {
				return;
			}

			var requestedSwitches = _gamelogicEngine.RequestedSwitches;
			for (var i = 0; i < requestedSwitches.Length; i++)
			{
				var sw = requestedSwitches[i];
				if (sw == null || string.IsNullOrEmpty(sw.InputActionHint)) {
					continue;
				}

				if (!TryMapInputActionHint(sw.InputActionHint, out var action)) {
					continue;
				}

				var actionIndex = (int)action;
				if ((uint)actionIndex >= (uint)_actionToSwitchId.Length) {
					continue;
				}

				// Prefer the first mapping we see.
				_actionToSwitchId[actionIndex] ??= sw.Id;
			}

			if (Logger.IsDebugEnabled)
			{
				Logger.Debug($"{LogPrefix} [SimulationThread] Built input action -> switch mappings");
			}

			if (Logger.IsInfoEnabled) {
				LogMapping(NativeInputApi.InputAction.Start, "Start");
				LogMapping(NativeInputApi.InputAction.CoinInsert1, "CoinInsert1");
				LogMapping(NativeInputApi.InputAction.LeftFlipper, "LeftFlipper");
				LogMapping(NativeInputApi.InputAction.RightFlipper, "RightFlipper");
			}
		}

		private void LogMapping(NativeInputApi.InputAction action, string name)
		{
			var idx = (int)action;
			var mapped = (uint)idx < (uint)_actionToSwitchId.Length ? _actionToSwitchId[idx] : null;
			Logger.Info($"{LogPrefix} [SimulationThread] Mapping: {name}={mapped}");
		}

		private static bool TryMapInputActionHint(string inputActionHint, out NativeInputApi.InputAction action)
		{
			// Keep this allocation-free and fast: match against known InputConstants strings.
			if (inputActionHint == InputConstants.ActionLeftFlipper) {
				action = NativeInputApi.InputAction.LeftFlipper;
				return true;
			}
			if (inputActionHint == InputConstants.ActionRightFlipper) {
				action = NativeInputApi.InputAction.RightFlipper;
				return true;
			}
			if (inputActionHint == InputConstants.ActionUpperLeftFlipper) {
				action = NativeInputApi.InputAction.UpperLeftFlipper;
				return true;
			}
			if (inputActionHint == InputConstants.ActionUpperRightFlipper) {
				action = NativeInputApi.InputAction.UpperRightFlipper;
				return true;
			}
			if (inputActionHint == InputConstants.ActionLeftMagnasave) {
				action = NativeInputApi.InputAction.LeftMagnasave;
				return true;
			}
			if (inputActionHint == InputConstants.ActionRightMagnasave) {
				action = NativeInputApi.InputAction.RightMagnasave;
				return true;
			}
			if (inputActionHint == InputConstants.ActionStartGame) {
				action = NativeInputApi.InputAction.Start;
				return true;
			}
			if (inputActionHint == InputConstants.ActionPlunger) {
				action = NativeInputApi.InputAction.Plunge;
				return true;
			}
			if (inputActionHint == InputConstants.ActionPlungerAnalog) {
				action = NativeInputApi.InputAction.PlungerAnalog;
				return true;
			}
			if (inputActionHint == InputConstants.ActionInsertCoin1) {
				action = NativeInputApi.InputAction.CoinInsert1;
				return true;
			}
			if (inputActionHint == InputConstants.ActionInsertCoin2) {
				action = NativeInputApi.InputAction.CoinInsert2;
				return true;
			}
			if (inputActionHint == InputConstants.ActionInsertCoin3) {
				action = NativeInputApi.InputAction.CoinInsert3;
				return true;
			}
			if (inputActionHint == InputConstants.ActionInsertCoin4) {
				action = NativeInputApi.InputAction.CoinInsert4;
				return true;
			}
			if (inputActionHint == InputConstants.ActionSlamTilt) {
				action = NativeInputApi.InputAction.SlamTilt;
				return true;
			}

			action = default;
			return false;
		}

		/// <summary>
		/// Update physics simulation (1ms step)
		/// </summary>
		private void UpdatePhysics()
		{
			if (_physicsEngine != null)
			{
				// Execute physics tick directly on simulation thread
				// This works now because we changed Allocator.Temp to Allocator.TempJob
				// in the physics hot path, allowing custom threads to execute physics.
				_physicsEngine.ExecuteTick((ulong)_simulationTimeUsec);
			}
		}

		/// <summary>
		/// Write simulation state to shared memory and swap buffers
		/// </summary>
		private void WriteSharedState()
		{
			ref var backBuffer = ref _sharedState.GetBackBuffer();

			// Update timing
			backBuffer.SimulationTimeUsec = _simulationTimeUsec;
			backBuffer.RealTimeUsec = GetTimestampUsec();


			// Copy PinMAME state (coils, lamps, GI)
			// This is where we'd copy the changed outputs from PinMAME
			// For now, this is a placeholder
			// TODO: Implement state copying

			// Increment physics state version (main thread will detect changes)
			backBuffer.PhysicsStateVersion++;

			// Atomically swap buffers (lock-free)
			_sharedState.SwapBuffers();
		}

		private static long GetTimestampUsec()
		{
			long ticks = Stopwatch.GetTimestamp();
			return (ticks * 1_000_000) / Stopwatch.Frequency;
		}

		#endregion

		private void OnGamelogicStarted(object sender, EventArgs e)
		{
			_gamelogicStarted = true;
			_inputMappingsBuilt = false; // switches can be populated after init
			_needsInitialSwitchSync = true;
		}

		#region Dispose

		public void Dispose()
		{
			Stop();
			if (_gamelogicEngine != null) {
				_gamelogicEngine.OnStarted -= OnGamelogicStarted;
			}
			_inputDispatcher?.Dispose();
			lock (_externalSwitchQueueLock) {
				_externalSwitchQueue.Clear();
			}
			_inputBuffer?.Dispose();
			_sharedState?.Dispose();
		}

		#endregion
	}
}
