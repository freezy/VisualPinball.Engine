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
using Unity.Mathematics;
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
		private const string LogPrefix = "[VPE]";

		#region Constants

		private const long TickIntervalUsec = 1000; // 1ms = 1000 microseconds
		private const long BusyWaitThresholdUsec = 25; // Keep active spin short to avoid starving render/main thread

		/// <summary>
		/// Maximal tick backlog the loop replays after a stall. Missed ticks beyond
		/// this are dropped from the timetable instead of being run back to back,
		/// which would otherwise fast-forward the game at several times real time
		/// until the timetable is caught up (a 1.5 s stall used to replay as ~1.5 s
		/// of physics at 4-5x). Dropping only shortens the timetable; the simulation
		/// clock itself is not advanced, so dropping never pushes it ahead of Unity's
		/// scaled clock. If Unity time got ahead during the stall, the regular main-thread
		/// clock sync in <see cref="SimulationTick"/> brings the simulation clock up
		/// and physics steps to it, bounded by <c>PhysicsConstants.MaxSubSteps</c>.
		/// The bound is generous enough to absorb frame hitches and sleep jitter
		/// unnoticed, and short enough that a skip reads as a brief hitch.
		/// </summary>
		private const long MaxBacklogUsec = 100_000;
		private const int MaxCoilOutputsPerTick = 128;

		#endregion

		#region Fields

		private readonly PhysicsEngine _physicsEngine;
		private readonly IGamelogicEngine _gamelogicEngine;
		private readonly IGamelogicTimeFence _timeFence;
		private readonly IGamelogicCoilOutputFeed _coilOutputFeed;
		private readonly IGamelogicSharedStateWriter _sharedStateWriter;
		private readonly IGamelogicPerformanceStats _gamelogicPerformanceStats;
		private readonly IGamelogicLatencyStats _gamelogicLatencyStats;
		private readonly IGamelogicInputDispatcher _inputDispatcher;
		private readonly Action<string, float> _simulationCoilDispatcher;
		private readonly InputEventBuffer _inputBuffer;
		private readonly SimulationState _sharedState;
		private readonly System.Random _nudgeRandom = new System.Random();
		private readonly List<bool> _pendingPlumbTiltEvents = new List<bool>(8);

		private Thread _thread;
		private volatile bool _running = false;
		private volatile bool _paused = false;
		private volatile Exception _fault;

		// Timing (Stopwatch ticks - avoids high-frequency P/Invoke)
		private readonly long _tickIntervalTicks;
		private readonly long _busyWaitThresholdTicks;
		private readonly long _maxBacklogTicks;
		private long _lastTickTicks;
		private long _simulationTimeUsec;
		private long _droppedBacklogUsec;
		private bool _rebaseTimetable;
		private long _lastTimeFenceUsec = long.MinValue;
		private long _lastTimeFenceIntervalUsec;
		private long _lastSimulationTickDurationUsec;
		private long _lastSnapshotCopyUsec;
		private long _lastSwitchDispatchUsec;
		private long _lastFlipperInputUsec;
		private long _lastCoilDispatchUsec;
		private double _simulationClockScale = 1.0;
		private long _latestMainThreadClockUsec;
		private volatile bool _hasMainThreadClockSync;

		// Input state tracking (allocation-free indexed arrays)
		private readonly bool[] _actionStates;
		// Switches driven by each input action. An action can drive several switches (e.g. a lower
		// and an upper flipper switch on the same button), each with its own polarity and state.
		private readonly InputSwitchBinding[][] _actionBindings;

		// Hardware-rule wires that connect an input action to a simulation-thread-capable coil (e.g. a
		// P-ROC flipper: "Left Flipper" -> flipper main coil). The native input poller dispatches these
		// on the simulation thread so the coil energizes in the same tick as the key, instead of relying
		// on the managed Input System callback (which the native path bypasses). Guarded by its own lock:
		// registration happens on the main thread when rules are (de)installed, dispatch on the sim thread
		// only on an actual input transition, so contention is negligible.
		private readonly Dictionary<int, List<ISimulationThreadCoil>> _actionWireCoils = new();
		private readonly object _actionWireCoilLock = new object();
		private volatile bool _inputMappingsBuilt;
		private volatile InputSwitchMapping[] _inputSwitchMappings;

		// Statistics
		private long _tickCount = 0;
		private long _inputEventsProcessed = 0;
		private long _inputEventsDropped = 0;

		private volatile bool _gamelogicStarted;
		private volatile bool _needsInitialSwitchSync;
		private bool _externalSwitchQueueWarningIssued;
		private bool _externalSwitchQueueDropWarningIssued;

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
			Action<string, float> simulationCoilDispatcher)
		{
			_physicsEngine = physicsEngine ?? throw new ArgumentNullException(nameof(physicsEngine));
			_gamelogicEngine = gamelogicEngine;
			_timeFence = gamelogicEngine as IGamelogicTimeFence;
			_coilOutputFeed = gamelogicEngine as IGamelogicCoilOutputFeed;
			_sharedStateWriter = gamelogicEngine as IGamelogicSharedStateWriter;
			_gamelogicPerformanceStats = gamelogicEngine as IGamelogicPerformanceStats;
			_gamelogicLatencyStats = gamelogicEngine as IGamelogicLatencyStats;
			_inputDispatcher = GamelogicInputDispatcherFactory.Create(gamelogicEngine);
			_simulationCoilDispatcher = simulationCoilDispatcher;

			_inputBuffer = new InputEventBuffer(1024);
			_sharedState = new SimulationState();

			// Precompute timing constants
			_tickIntervalTicks = (Stopwatch.Frequency * TickIntervalUsec) / 1_000_000;
			_busyWaitThresholdTicks = (Stopwatch.Frequency * BusyWaitThresholdUsec) / 1_000_000;
			_maxBacklogTicks = (Stopwatch.Frequency * MaxBacklogUsec) / 1_000_000;
			if (_tickIntervalTicks <= 0) {
				_tickIntervalTicks = 1;
			}

			// Initialize input state + mapping arrays
			var actionCount = Enum.GetValues(typeof(NativeInputApi.InputAction)).Length;
			_actionStates = new bool[actionCount];
			_actionBindings = new InputSwitchBinding[actionCount][];
			_needsInitialSwitchSync = true;

			if (_gamelogicEngine != null) {
				_gamelogicEngine.OnStarted += OnGamelogicStarted;
			}
		}

		#endregion

		#region Public API

		/// <summary>
		/// Start the simulation thread.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		public void Start()
		{
			if (_running) return;

			_running = true;
			_paused = false;
			_fault = null;
			NativeInputManager.GamePaused = false;
			_gamelogicStarted = _gamelogicEngine != null;
			_lastTickTicks = Stopwatch.GetTimestamp();
			_simulationTimeUsec = _hasMainThreadClockSync ? Interlocked.Read(ref _latestMainThreadClockUsec) : 0;
			_lastTimeFenceUsec = long.MinValue;
			_tickCount = 0;
			_inputEventsProcessed = 0;
			_inputEventsDropped = 0;
			_needsInitialSwitchSync = true;

			if (SimulationTrace.Enabled) {
				SimulationTrace.Begin();
			}

			_thread = new Thread(SimulationThreadFunc)
			{
				Name = "VPE Simulation Thread",
				IsBackground = true,
				Priority = ThreadPriority.AboveNormal
			};
			_thread.Start();

			Logger.Info($"{LogPrefix} [SimulationThread] Started at 1000 Hz");
		}

		/// <summary>
		/// The exception that stopped the simulation thread, or null while it runs fine.
		/// </summary>
		/// <remarks><b>Thread:</b> Any thread.</remarks>
		public Exception Fault => _fault;

		/// <summary>
		/// Stop the simulation thread.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		public void Stop()
		{
			// a session left from the pause menu must not keep the next one's input gated
			NativeInputManager.GamePaused = false;
			if (!_running) return;

			_running = false;

			var exited = true;
			if (_thread != null && _thread.IsAlive)
			{
				exited = _thread.Join(5000); // Wait up to 5 seconds
			}

			if (exited) {
				SimulationTrace.End();
			} else {
				// the thread may still be writing into the trace buffers; never free them under it
				Logger.Warn($"{LogPrefix} [SimulationThread] Thread did not exit within 5 s, abandoning the simulation trace.");
				SimulationTrace.Abandon();
			}

			Logger.Info($"{LogPrefix} [SimulationThread] Stopped after {_tickCount} ticks, {_inputEventsProcessed} input events, {_inputEventsDropped} dropped");
		}

		/// <summary>
		/// Pause the simulation (for debugging).
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only (sets volatile flag).</remarks>
		public void Pause()
		{
			_paused = true;
			NativeInputManager.GamePaused = true; // input stops at the source while paused
		}

		/// <summary>
		/// Resume the simulation.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only (sets volatile flag).</remarks>
		public void Resume()
		{
			_paused = false;
			NativeInputManager.GamePaused = false;
		}

		/// <summary>
		/// Enqueue an input event from the input polling thread.
		/// </summary>
		/// <remarks><b>Thread:</b> Native input polling thread (thread-safe via lock-free ring buffer).</remarks>
		public void EnqueueInputEvent(NativeInputApi.InputEvent evt)
		{
			// While paused (e.g. the in-game menu is open) input is menu-exclusive: drop it so held/pressed
			// keys don't accumulate in the ring buffer and flush into the game on resume.
			if (_paused) {
				return;
			}
			if (!_inputBuffer.TryEnqueue(evt)) {
				Interlocked.Increment(ref _inputEventsDropped);
			}
		}

		/// <summary>
		/// Get the current shared state (for main thread to read).
		/// Returns a peek at the current read buffer without acquiring a new
		/// one. The actual acquire happens in
		/// <see cref="PhysicsEngine.ApplyMovements"/> to avoid
		/// double-swapping per frame.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		public ref readonly SimulationState.Snapshot GetSharedState()
		{
			return ref _sharedState.PeekReadBuffer();
		}

		/// <summary>
		/// The triple-buffered SimulationState, so the caller can pass it to
		/// <see cref="PhysicsEngine.SetSimulationState"/>.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only (used during initialization).</remarks>
		public SimulationState SharedState => _sharedState;

		/// <summary>
		/// Dispatches plumb tilt state changes on the main thread through the
		/// table's plumb tilt component. Set by <see cref="SimulationThreadComponent"/>
		/// only when that component exists and is enabled by player settings.
		/// </summary>
		public Action<bool> MainThreadTiltDispatcher { get; set; }

		/// <summary>
		/// When true, native cabinet Tilt input is routed to
		/// <see cref="MainThreadTiltDispatcher"/> instead of directly to any
		/// input-action switch mapping. This is used for real cabinet plumb bobs.
		/// </summary>
		public bool DispatchPhysicalTiltInputToMainThread { get; set; }

		private readonly Queue<bool> _mainThreadTiltStates = new();
		private readonly object _mainThreadTiltLock = new();

		/// <summary>
		/// Flush any queued main-thread input dispatches.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		public void FlushMainThreadInputDispatch()
		{
			_inputDispatcher.FlushMainThread();

			while (true) {
				bool tiltState;
				lock (_mainThreadTiltLock) {
					if (_mainThreadTiltStates.Count == 0) {
						break;
					}
					tiltState = _mainThreadTiltStates.Dequeue();
				}
				MainThreadTiltDispatcher?.Invoke(tiltState);
			}
		}

		private void QueueMainThreadTiltState(bool tiltState)
		{
			lock (_mainThreadTiltLock) {
				_mainThreadTiltStates.Enqueue(tiltState);
			}
		}

		/// <summary>
		/// Publish the latest Unity-scaled simulation clock sample from the
		/// main thread so the simulation thread can stay aligned with
		/// single-threaded timing semantics, including slow-motion and
		/// time-lapse modes.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		public void SyncClockFromMainThread(ulong currentClockUsec, float timeScale)
		{
			Interlocked.Exchange(ref _latestMainThreadClockUsec, (long)currentClockUsec);
			Volatile.Write(ref _simulationClockScale, math.max(0.0, timeScale));
			_hasMainThreadClockSync = true;
		}

		/// <summary>
		/// Enqueue a switch event that originated from the main thread (or any
		/// non-polling thread). The event is picked up by the simulation
		/// thread on the next tick.
		/// </summary>
		/// <remarks><b>Thread:</b> Any thread (protected by lock).</remarks>
		public bool EnqueueExternalSwitch(string switchId, bool isClosed)
		{
			if (string.IsNullOrEmpty(switchId)) {
				return false;
			}

			InputLatencyTracker.RecordSwitchInputDispatched(switchId, isClosed);

			lock (_externalSwitchQueueLock) {
				if (_externalSwitchQueue.Count >= MaxExternalSwitchQueueSize) {
					if (!_externalSwitchQueueDropWarningIssued) {
						_externalSwitchQueueDropWarningIssued = true;
						Logger.Warn($"{LogPrefix} [SimulationThread] External switch queue is full ({MaxExternalSwitchQueueSize}). Dropping switch events.");
					}
					return false;
				}
				_externalSwitchQueue.Enqueue(new PendingSwitchEvent(switchId, isClosed));
				if (!_externalSwitchQueueWarningIssued && _externalSwitchQueue.Count >= MaxExternalSwitchQueueSize / 2) {
					_externalSwitchQueueWarningIssued = true;
					Logger.Warn($"{LogPrefix} [SimulationThread] External switch queue backlog reached {_externalSwitchQueue.Count} items.");
				}
				return true;
			}
		}

		#endregion

		#region Simulation Thread

		private void SimulationThreadFunc()
		{
			// Editor playmode is a hostile environment for time-critical threads (domain/scene reload,
			// asset imports, editor windows). Keep time-critical only for player builds.
			// Wait for physics engine to be fully initialized
			// This prevents accessing physics state before it's ready
			Logger.Debug("{Prefix:l} [SimulationThread] Waiting for physics initialization...", LogPrefix);
			int waitCount = 0;
			while (_running && _physicsEngine != null && !_physicsEngine.IsInitialized && waitCount < 100)
			{
				Thread.Sleep(50);
				waitCount++;
			}

			if (waitCount >= 100)
			{
				// _running stays set, so Stop() still joins the thread and ends the trace
				Logger.Error("{Prefix:l} [SimulationThread] Timeout waiting for physics initialization", LogPrefix);
				return;
			}

			Logger.Debug("{Prefix:l} [SimulationThread] Physics initialized, starting simulation", LogPrefix);

			// Try to enable no-GC region for hot path
			bool noGcRegion = false;
			try
			{
				// Allocate 10MB for no-GC region
				if (GC.TryStartNoGCRegion(10 * 1024 * 1024, true))
				{
					noGcRegion = true;
					Logger.Debug("{Prefix:l} [SimulationThread] No-GC region enabled", LogPrefix);
				}
			}
			catch (Exception ex)
			{
				// Mono doesn't implement it; the simulation runs fine without
				Logger.Debug(ex, "{Prefix:l} [SimulationThread] Cannot start a no-GC region", LogPrefix);
			}

			try
			{
				// Build input mappings once (not on hot path)
				BuildInputMappingsIfNeeded();

				// The initialization wait above may have taken a while; start the
				// timetable now so it does not begin with a backlog.
				_lastTickTicks = Stopwatch.GetTimestamp();
				var iterationStartTicks = _lastTickTicks;

				// Main simulation loop
				while (_running)
				{
					if (_paused)
					{
						Thread.Sleep(10);
						// Unity time stands still while paused (the menu sets timeScale to
						// zero); hold the timetable so no backlog accumulates to replay.
						_lastTickTicks = Stopwatch.GetTimestamp();
						iterationStartTicks = _lastTickTicks;
						continue;
					}

					// Timing: Sleep until next tick, then busy-wait for precision.
					long targetTicks = _lastTickTicks + _tickIntervalTicks;
					long nowTicks = Stopwatch.GetTimestamp();
					long sleepTicks = targetTicks - nowTicks;
					var tracing = SimulationTrace.IsRecording;
					if (tracing) {
						SimulationTrace.Tick.WaitRequestedUsec = (int)SimulationTrace.TicksToUsec(sleepTicks);
						SimulationTrace.Tick.DroppedUsec = 0;
						SimulationTrace.Tick.WaitMode = 0;
					}

					// Bound the catch-up after a stall (GC, editor hitch, overloaded ticks):
					// keep at most MaxBacklogUsec of missed ticks to replay and drop the rest
					// from the timetable, instead of replaying every missed tick at multiple
					// real-time speed. See MaxBacklogUsec for why the clock is left alone.
					if (-sleepTicks > _maxBacklogTicks)
					{
						var skippedTicks = -sleepTicks - _maxBacklogTicks;
						_lastTickTicks += skippedTicks;
						targetTicks += skippedTicks;
						sleepTicks += skippedTicks;
						_droppedBacklogUsec += (skippedTicks * 1_000_000L) / Stopwatch.Frequency;
						if (tracing) {
							SimulationTrace.Tick.DroppedUsec = (int)SimulationTrace.TicksToUsec(skippedTicks);
						}
					}

					if (sleepTicks > _busyWaitThresholdTicks)
					{
						var sleepMs = (int)(((sleepTicks - _busyWaitThresholdTicks) * 1000) / Stopwatch.Frequency);
						if (sleepMs > 0) {
							Thread.Sleep(sleepMs);
						} else {
							Thread.Yield();
						}
						if (tracing) {
							SimulationTrace.Tick.WaitMode = sleepMs > 0 ? 2 : 1;
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

					if (tracing) {
						var startTicks = Stopwatch.GetTimestamp();
						SimulationTrace.Tick.StartUsec = SimulationTrace.TicksToUsec(startTicks);
						SimulationTrace.Tick.TargetUsec = SimulationTrace.TicksToUsec(targetTicks);
						SimulationTrace.Tick.WaitUsec = SimulationTrace.ElapsedUsec(iterationStartTicks, startTicks);
					}

					// Execute simulation tick (hot path - must be allocation-free!)
					SimulationTick();
					iterationStartTicks = Stopwatch.GetTimestamp();

					if (_rebaseTimetable)
					{
						// The tick synchronized the simulation clock to Unity time, which
						// already accounts for whatever stalled us; replaying the timetable
						// debt on top would count that interval twice.
						_rebaseTimetable = false;
						_lastTickTicks = Stopwatch.GetTimestamp();
					}
					else
					{
						_lastTickTicks = targetTicks;
					}
					_tickCount++;
				}
			}
			catch (Exception ex)
			{
				// Logged below, once the no-GC region is closed: formatting the exception
				// allocates, and the region may already be exhausted. _running stays set, so
				// Stop() still joins the thread and ends the trace.
				_fault = ex;
			}
			finally
			{
				// Exit no-GC region
				if (noGcRegion && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
				{
					try
					{
						GC.EndNoGCRegion();
						Logger.Debug("{Prefix:l} [SimulationThread] No-GC region ended", LogPrefix);
					}
					catch (InvalidOperationException)
					{
						// the region was already left because its budget ran out
					}
				}
			}

			if (_fault != null)
			{
				Logger.Fatal(_fault, "{Prefix:l} [SimulationThread] Simulation thread died after {Ticks} ticks", LogPrefix, _tickCount);
				VpeLog.Flush(TimeSpan.FromSeconds(2));
			}
		}

		/// <summary>
		/// Single simulation tick - MUST BE ALLOCATION-FREE!
		/// </summary>
		/// <remarks><b>Thread:</b> Simulation thread only.</remarks>
		private void SimulationTick()
		{
			var tickStartTicks = Stopwatch.GetTimestamp();
			var tracing = SimulationTrace.IsRecording;
			if (tracing) {
				ref var trace = ref SimulationTrace.Tick;
				trace.Index = _tickCount;
				trace.SimTimeUsec = _simulationTimeUsec;
				trace.SyncedClockUsec = _hasMainThreadClockSync ? Interlocked.Read(ref _latestMainThreadClockUsec) : 0;
				trace.ClockJumpUsec = 0;
				trace.Gc0 = GC.CollectionCount(0);
			}

			if (_hasMainThreadClockSync) {
				var syncedClockUsec = Interlocked.Read(ref _latestMainThreadClockUsec);
				if (syncedClockUsec > _simulationTimeUsec) {
					if (tracing) {
						SimulationTrace.Tick.ClockJumpUsec = (int)(syncedClockUsec - _simulationTimeUsec);
					}
					_simulationTimeUsec = syncedClockUsec;
					_rebaseTimetable = true;
				}
			}

			// 0. Process switch events that originated on Unity/main thread.
			ProcessExternalSwitchEvents();
			var switchesDoneTicks = Stopwatch.GetTimestamp();

			// 1. Process input events from ring buffer
			ProcessInputEvents();
			var inputDoneTicks = Stopwatch.GetTimestamp();

			// 2. Apply low-latency coil outputs from gamelogic to simulation-side handlers.
			ProcessGamelogicOutputs();
			var outputsDoneTicks = Stopwatch.GetTimestamp();

			// 3. Update physics simulation
			UpdatePhysics();
			var physicsDoneTicks = Stopwatch.GetTimestamp();

			// 4. Move the emulation fence after inputs+outputs+physics.
			// Throttle updates to reduce fence wake/sleep churn in PinMAME.
			if (_timeFence != null && _simulationTimeUsec != _lastTimeFenceUsec) {
				_lastTimeFenceIntervalUsec = _lastTimeFenceUsec == long.MinValue ? 0 : _simulationTimeUsec - _lastTimeFenceUsec;
				_timeFence.SetTimeFence(_simulationTimeUsec / 1_000_000.0);
				_lastTimeFenceUsec = _simulationTimeUsec;
			}
			var fenceDoneTicks = Stopwatch.GetTimestamp();

			// 5. Write to shared state and swap buffers
			WriteSharedState();

			// Increment simulation time
			_simulationTimeUsec += ScaledTickIntervalUsec();
			var tickEndTicks = Stopwatch.GetTimestamp();
			_lastSimulationTickDurationUsec = (tickEndTicks - tickStartTicks) * 1_000_000L / Stopwatch.Frequency;

			if (tracing) {
				ref var trace = ref SimulationTrace.Tick;
				trace.SwitchesUsec = SimulationTrace.ElapsedUsec(tickStartTicks, switchesDoneTicks);
				trace.InputUsec = SimulationTrace.ElapsedUsec(switchesDoneTicks, inputDoneTicks);
				trace.OutputsUsec = SimulationTrace.ElapsedUsec(inputDoneTicks, outputsDoneTicks);
				trace.FenceUsec = SimulationTrace.ElapsedUsec(physicsDoneTicks, fenceDoneTicks);
				trace.TotalUsec = SimulationTrace.ElapsedUsec(tickStartTicks, tickEndTicks);
				SimulationTrace.CommitTick();
			}
		}

		/// <summary>
		/// Process all pending input events from the ring buffer.
		/// </summary>
		/// <remarks><b>Thread:</b> Simulation thread only.</remarks>
		private void ProcessInputEvents()
		{
			BuildInputMappingsIfNeeded();

			while (_inputBuffer.TryDequeue(out var evt))
			{
				if (evt.EventType != (int)NativeInputApi.InputEventType.Action) {
					continue;
				}

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

				// Fire any hardware-rule wire coils bound to this action on the simulation thread, so a
				// P-ROC flipper energizes within the same tick as the key press. This is the wire path the
				// managed Input System would otherwise own, which native polling bypasses.
				DispatchInputActionWires(actionIndex, isPressed);

				InputLatencyTracker.RecordInputPolled((NativeInputApi.InputAction)actionIndex, isPressed, evt.TimestampUsec);

				if (actionIndex == (int)NativeInputApi.InputAction.Tilt
				    && DispatchPhysicalTiltInputToMainThread
				    && MainThreadTiltDispatcher != null) {
					QueueMainThreadTiltState(isPressed);
					_inputEventsProcessed++;
					continue;
				}

				// Only forward to GLE once it's ready (or at least has started)
				if (_gamelogicEngine != null && _gamelogicStarted) {
					if (isPressed && IsNudgeAction(actionIndex)) {
						// The default nudge is suppressed when the gamelogic reacted to the
						// key by nudging itself. For main-thread-dispatched GLEs the switch
						// is only queued here, so the decision must run after the main
						// thread actually delivered it; hence the after-dispatch callback.
						var nudgeIndexBeforeSwitchDispatch = _physicsEngine.KeyboardNudgeIndex;
						var nudgeActionIndex = actionIndex;
						SendMappedSwitch(actionIndex, isPressed, () => {
							if (nudgeIndexBeforeSwitchDispatch == _physicsEngine.KeyboardNudgeIndex) {
								ApplyDefaultKeyboardNudge(nudgeActionIndex);
							}
						});
					} else {
						SendMappedSwitch(actionIndex, isPressed);
					}
				} else if (isPressed && IsNudgeAction(actionIndex)) {
					ApplyDefaultKeyboardNudge(actionIndex);
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
				_lastCoilDispatchUsec = GetTimestampUsec();
				_simulationCoilDispatcher(coilEvent.Id, coilEvent.Value);
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

		private void SendMappedSwitch(int actionIndex, bool isPressed, Action afterDispatch = null)
		{
			var bindings = (uint)actionIndex < (uint)_actionBindings.Length ? _actionBindings[actionIndex] : null;
			if (bindings == null) {
				// No switch mapped: the GLE never sees the action, run the follow-up now.
				afterDispatch?.Invoke();
				return;
			}

			// VP-style coin door behavior toggles only on key-down, so a release may dispatch nothing.
			// The follow-up runs once, with the last switch this action dispatches.
			var lastDispatched = -1;
			for (var i = 0; i < bindings.Length; i++) {
				if (!bindings[i].ToggleOnPress || isPressed) {
					lastDispatched = i;
				}
			}
			if (lastDispatched < 0) {
				afterDispatch?.Invoke();
				return;
			}

			_lastSwitchDispatchUsec = GetTimestampUsec();
			if (isPressed && IsFlipperAction(actionIndex)) {
				_lastFlipperInputUsec = _lastSwitchDispatchUsec;
			}
			for (var i = 0; i <= lastDispatched; i++) {
				var binding = bindings[i];
				bool isClosed;
				if (binding.ToggleOnPress) {
					if (!isPressed) {
						continue;
					}
					isClosed = !binding.State;
				} else {
					isClosed = binding.InvertsPressed ? !isPressed : isPressed;
				}
				binding.State = isClosed;

				if (Logger.IsTraceEnabled && (isPressed || actionIndex == (int)NativeInputApi.InputAction.Start)) {
					Logger.Trace("{Prefix:l} [SimulationThread] Input {Action} -> Switch({SwitchId}, {IsClosed})", LogPrefix, (NativeInputApi.InputAction)actionIndex, binding.SwitchId, isClosed);
				}
				var followUp = i == lastDispatched ? afterDispatch : null;
				if (followUp != null && _inputDispatcher is MainThreadQueuedInputDispatcher queuedDispatcher) {
					queuedDispatcher.DispatchSwitch(binding.SwitchId, isClosed, followUp);
				} else {
					_inputDispatcher.DispatchSwitch(binding.SwitchId, isClosed);
					followUp?.Invoke();
				}
			}
		}

		private static bool IsFlipperAction(int actionIndex)
		{
			return actionIndex == (int)NativeInputApi.InputAction.LeftFlipper
				|| actionIndex == (int)NativeInputApi.InputAction.RightFlipper
				|| actionIndex == (int)NativeInputApi.InputAction.LeftStagedFlipper
				|| actionIndex == (int)NativeInputApi.InputAction.RightStagedFlipper
				|| actionIndex == (int)NativeInputApi.InputAction.UpperLeftFlipper
				|| actionIndex == (int)NativeInputApi.InputAction.UpperRightFlipper;
		}

		private static bool IsNudgeAction(int actionIndex)
		{
			return actionIndex == (int)NativeInputApi.InputAction.LeftNudge
				|| actionIndex == (int)NativeInputApi.InputAction.RightNudge
				|| actionIndex == (int)NativeInputApi.InputAction.CenterNudge;
		}

		/// <summary>
		/// Applies the built-in keyboard nudge for tables whose gamelogic did not
		/// handle the nudge key itself.
		/// </summary>
		/// <remarks>
		/// VP tables can script their own nudge response. The simulation thread
		/// therefore applies this fallback only after switch dispatch has had a
		/// chance to call into gamelogic and increment the physics engine's keyboard
		/// nudge counter.
		/// </remarks>
		private void ApplyDefaultKeyboardNudge(int actionIndex)
		{
			const float baseForce = 2f;
			var angle = ((float)_nudgeRandom.NextDouble() - 0.5f) * 15f * baseForce;
			var force = (0.6f + (float)_nudgeRandom.NextDouble() * 0.8f) * baseForce;
			if (actionIndex == (int)NativeInputApi.InputAction.LeftNudge) {
				_physicsEngine.Nudge(75f + angle, force);
			} else if (actionIndex == (int)NativeInputApi.InputAction.RightNudge) {
				_physicsEngine.Nudge(285f + angle, force);
			} else if (actionIndex == (int)NativeInputApi.InputAction.CenterNudge) {
				_physicsEngine.Nudge(angle, force);
			}
		}

		private void SyncAllMappedSwitches()
		{
			for (var i = 0; i < _actionBindings.Length; i++)
			{
				var bindings = _actionBindings[i];
				if (bindings == null) {
					continue;
				}
				for (var j = 0; j < bindings.Length; j++) {
					_inputDispatcher.DispatchSwitch(bindings[j].SwitchId, bindings[j].State);
				}
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
			Array.Clear(_actionBindings, 0, _actionBindings.Length);

			if (_gamelogicEngine == null) {
				return;
			}

			// The table's switch mappings are authoritative: native polling bypasses the Input System
			// path in SwitchPlayer, so a switch the author bound to a key in the switch manager only
			// works if it lands here. The GLE's hints are the fallback for when no table is attached.
			var inputSwitchMappings = _inputSwitchMappings;
			if (inputSwitchMappings != null) {
				for (var i = 0; i < inputSwitchMappings.Length; i++) {
					var mapping = inputSwitchMappings[i];
					AssignInputSwitch(mapping.InputAction, mapping.SwitchId, mapping.NormallyClosed);
				}

			} else {
				var requestedSwitches = _gamelogicEngine.RequestedSwitches;
				for (var i = 0; i < requestedSwitches.Length; i++) {
					var sw = requestedSwitches[i];
					if (sw == null || string.IsNullOrEmpty(sw.InputActionHint)) {
						continue;
					}
					AssignInputSwitch(sw.InputActionHint, sw.Id, sw.NormallyClosed);
				}
			}

			if (Logger.IsDebugEnabled)
			{
				Logger.Debug($"{LogPrefix} [SimulationThread] Built input action -> switch mappings");
			}

			if (Logger.IsDebugEnabled) {
				LogMapping(NativeInputApi.InputAction.Start, "Start");
				LogMapping(NativeInputApi.InputAction.CoinInsert1, "CoinInsert1");
				LogMapping(NativeInputApi.InputAction.LeftFlipper, "LeftFlipper");
				LogMapping(NativeInputApi.InputAction.RightFlipper, "RightFlipper");
			}
		}

		private void AssignInputSwitch(string inputAction, string switchId, bool normallyClosed)
		{
			if (string.IsNullOrEmpty(inputAction) || string.IsNullOrEmpty(switchId)) {
				return;
			}

			// Aliases such as "Fire 1" share a native action with their canonical name.
			if (!TryMapInputActionHint(inputAction, out var action)
				&& !TryMapInputActionHint(InputManager.GetCanonicalActionName(inputAction), out action)) {
				Logger.Debug("{Prefix:l} [SimulationThread] Input action {InputAction} of switch {SwitchId} has no native input equivalent, ignoring.", LogPrefix, inputAction, switchId);
				return;
			}

			var actionIndex = (int)action;
			if ((uint)actionIndex >= (uint)_actionBindings.Length) {
				return;
			}

			var bindings = _actionBindings[actionIndex];
			if (bindings != null) {
				for (var i = 0; i < bindings.Length; i++) {
					if (bindings[i].SwitchId == switchId) {
						return;
					}
				}
			}

			var toggleOnPress = inputAction == InputConstants.ActionCoinDoorOpenClose;
			var binding = new InputSwitchBinding {
				SwitchId = switchId,
				InvertsPressed = normallyClosed,
				ToggleOnPress = toggleOnPress,
				State = toggleOnPress
					? normallyClosed
					: (normallyClosed ? !_actionStates[actionIndex] : _actionStates[actionIndex]),
			};
			if (bindings == null) {
				_actionBindings[actionIndex] = new[] { binding };
			} else {
				Array.Resize(ref bindings, bindings.Length + 1);
				bindings[^1] = binding;
				_actionBindings[actionIndex] = bindings;
			}
		}

		private void LogMapping(NativeInputApi.InputAction action, string name)
		{
			var idx = (int)action;
			var bindings = (uint)idx < (uint)_actionBindings.Length ? _actionBindings[idx] : null;
			var mapped = bindings == null ? null : string.Join(", ", Array.ConvertAll(bindings, b => b.SwitchId));
			Logger.Debug("{Prefix:l} [SimulationThread] Mapping: {Action}={Switches:l}", LogPrefix, name, mapped);
		}

		/// <summary>
		/// Sets the table's input-action-to-switch mappings (switches whose source is the Input
		/// System). These replace the gamelogic engine's input action hints when routing native
		/// input to switches. Called on the main thread.
		/// </summary>
		internal void SetInputSwitchMappings(InputSwitchMapping[] mappings)
		{
			_inputSwitchMappings = mappings;
			_inputMappingsBuilt = false;
			_needsInitialSwitchSync = true;
		}

		/// <summary>
		/// Registers a hardware-rule wire coil so the simulation-thread input loop energizes it when the
		/// given input action fires. Only coils that expose the simulation-thread dispatch path are
		/// eligible; others must keep to the main-thread wire path. Called on the main thread.
		/// </summary>
		internal void RegisterInputActionWireCoil(string inputActionHint, ISimulationThreadCoil coil)
		{
			if (coil == null || !TryMapInputActionHint(inputActionHint, out var action)) {
				return;
			}
			var actionIndex = (int)action;
			lock (_actionWireCoilLock) {
				if (!_actionWireCoils.TryGetValue(actionIndex, out var list)) {
					list = new List<ISimulationThreadCoil>(2);
					_actionWireCoils[actionIndex] = list;
				}
				if (!list.Contains(coil)) {
					list.Add(coil);
				}
			}
		}

		/// <summary>Removes a previously registered hardware-rule wire coil. Called on the main thread.</summary>
		internal void UnregisterInputActionWireCoil(string inputActionHint, ISimulationThreadCoil coil)
		{
			if (coil == null || !TryMapInputActionHint(inputActionHint, out var action)) {
				return;
			}
			lock (_actionWireCoilLock) {
				if (_actionWireCoils.TryGetValue((int)action, out var list)) {
					list.Remove(coil);
				}
			}
		}

		/// <summary>Drops all registered hardware-rule wire coils, e.g. when the gamelogic engine restarts.</summary>
		internal void ClearInputActionWireCoils()
		{
			lock (_actionWireCoilLock) {
				_actionWireCoils.Clear();
			}
		}

		private void DispatchInputActionWires(int actionIndex, bool enabled)
		{
			lock (_actionWireCoilLock) {
				if (!_actionWireCoils.TryGetValue(actionIndex, out var list) || list.Count == 0) {
					return;
				}
				var value = enabled ? 1f : 0f;
				for (var i = 0; i < list.Count; i++) {
					list[i].OnCoilSimulationThread(value);
				}
			}
		}

		internal static bool TryMapInputActionHint(string inputActionHint, out NativeInputApi.InputAction action)
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
			if (inputActionHint == InputConstants.ActionCoinDoorOpenClose) {
				action = NativeInputApi.InputAction.CoinDoor;
				return true;
			}
			if (inputActionHint == InputConstants.ActionCoinDoorCancel || inputActionHint == InputConstants.ActionCoinDoorBack || inputActionHint == InputConstants.ActionCoinDoorUpDown) {
				action = NativeInputApi.InputAction.Service1;
				return true;
			}
			if (inputActionHint == InputConstants.ActionCoinDoorDown || inputActionHint == InputConstants.ActionCoinDoorMinus || inputActionHint == InputConstants.ActionCoinDoorAdvance) {
				action = NativeInputApi.InputAction.Service2;
				return true;
			}
			if (inputActionHint == InputConstants.ActionCoinDoorUp || inputActionHint == InputConstants.ActionCoinDoorPlus) {
				action = NativeInputApi.InputAction.Service3;
				return true;
			}
			if (inputActionHint == InputConstants.ActionCoinDoorEnter || inputActionHint == InputConstants.ActionCoinDoorSelect) {
				action = NativeInputApi.InputAction.Service4;
				return true;
			}
			if (inputActionHint == InputConstants.ActionSelfTest) {
				action = NativeInputApi.InputAction.Service1;
				return true;
			}
			if (inputActionHint == InputConstants.ActionLeftAdvance) {
				action = NativeInputApi.InputAction.UpperLeftFlipper;
				return true;
			}
			if (inputActionHint == InputConstants.ActionRightAdvance) {
				action = NativeInputApi.InputAction.UpperRightFlipper;
				return true;
			}
			if (inputActionHint == InputConstants.ActionSlamTilt) {
				action = NativeInputApi.InputAction.SlamTilt;
				return true;
			}
			if (inputActionHint == InputConstants.ActionLeftNudge) {
				action = NativeInputApi.InputAction.LeftNudge;
				return true;
			}
			if (inputActionHint == InputConstants.ActionRightNudge) {
				action = NativeInputApi.InputAction.RightNudge;
				return true;
			}
			if (inputActionHint == InputConstants.ActionCenterNudge) {
				action = NativeInputApi.InputAction.CenterNudge;
				return true;
			}
			if (inputActionHint == InputConstants.ActionTilt) {
				action = NativeInputApi.InputAction.Tilt;
				return true;
			}

			action = default;
			return false;
		}

		/// <summary>
		/// Update physics simulation (1ms step).
		/// </summary>
		/// <remarks><b>Thread:</b> Simulation thread only.</remarks>
		private void UpdatePhysics()
		{
			if (_physicsEngine != null)
			{
				// Execute physics tick directly on simulation thread
				// This works now because we changed Allocator.Temp to Allocator.TempJob
				// in the physics hot path, allowing custom threads to execute physics.
				var startTicks = Stopwatch.GetTimestamp();
				_physicsEngine.ExecuteTick((ulong)_simulationTimeUsec);
				var executeDoneTicks = Stopwatch.GetTimestamp();
				ProcessPlumbTiltEvents();
				if (SimulationTrace.IsRecording) {
					SimulationTrace.Tick.PhysicsUsec = SimulationTrace.ElapsedUsec(startTicks, executeDoneTicks);
					SimulationTrace.Tick.PlumbUsec = SimulationTrace.ElapsedUsec(executeDoneTicks, Stopwatch.GetTimestamp());
				}
			}
		}

		/// <summary>
		/// Queues physics-produced plumb-bob tilt edges for the authored table
		/// component that owns switch dispatch.
		/// </summary>
		/// <remarks>
		/// Main-thread gamelogic engines receive these through the same external
		/// switch queue as regular main-thread input so switch state, wires, and ROM
		/// input stay in sync.
		/// </remarks>
		private void ProcessPlumbTiltEvents()
		{
			if (MainThreadTiltDispatcher == null || DispatchPhysicalTiltInputToMainThread) {
				return;
			}

			_pendingPlumbTiltEvents.Clear();
			_physicsEngine.DrainPlumbTiltEvents(_pendingPlumbTiltEvents);
			if (_pendingPlumbTiltEvents.Count == 0) {
				return;
			}

			for (var i = 0; i < _pendingPlumbTiltEvents.Count; i++) {
				QueueMainThreadTiltState(_pendingPlumbTiltEvents[i]);
			}
		}

		/// <summary>
		/// Write simulation state to shared memory and publish the buffer.
		/// Called on the sim thread after physics has executed, so reading
		/// physics state maps is safe (single writer, sequential).
		/// </summary>
		private void WriteSharedState()
		{
			ref var writeBuffer = ref _sharedState.GetWriteBuffer();

			// Update timing
			writeBuffer.SimulationTimeUsec = _simulationTimeUsec;
			writeBuffer.RealTimeUsec = GetTimestampUsec();
			writeBuffer.SimulationTickDurationUsec = _lastSimulationTickDurationUsec;
			writeBuffer.FenceUpdateIntervalUsec = _lastTimeFenceIntervalUsec;
			writeBuffer.DroppedBacklogUsec = _droppedBacklogUsec;
			writeBuffer.LastSwitchDispatchUsec = _lastSwitchDispatchUsec;
			writeBuffer.LastFlipperInputUsec = _lastFlipperInputUsec;
			writeBuffer.LastCoilDispatchUsec = _lastCoilDispatchUsec;
			lock (_externalSwitchQueueLock) {
				writeBuffer.ExternalSwitchQueueDepth = _externalSwitchQueue.Count;
			}
			var diagStartTicks = Stopwatch.GetTimestamp();
			_physicsEngine.FillDiagnostics(ref writeBuffer);
			if (_gamelogicPerformanceStats != null && _gamelogicPerformanceStats.TryGetPerformanceStats(out var performanceStats)) {
				writeBuffer.GamelogicCallbackRateHz = performanceStats.CallbackRateHz;
			}
			if (_gamelogicLatencyStats != null && _gamelogicLatencyStats.TryGetLatencyStats(out var latencyStats)) {
				writeBuffer.LastSwitchObservationUsec = latencyStats.LastSwitchObservationUsec;
				writeBuffer.LastCoilOutputUsec = latencyStats.LastCoilOutputUsec;
			}
			var diagDoneTicks = Stopwatch.GetTimestamp();

			// Copy PinMAME state (coils, lamps, GI)
			writeBuffer.CoilCount = 0;
			writeBuffer.LampCount = 0;
			writeBuffer.GICount = 0;
			_sharedStateWriter?.WriteSharedState(ref writeBuffer);
			if (SimulationTrace.IsRecording) {
				SimulationTrace.Tick.DiagUsec = SimulationTrace.ElapsedUsec(diagStartTicks, diagDoneTicks);
				SimulationTrace.Tick.WriterUsec = SimulationTrace.ElapsedUsec(diagDoneTicks, Stopwatch.GetTimestamp());
			}

			// Increment physics state version (main thread will detect changes)
			writeBuffer.PhysicsStateVersion++;

			// Snapshot animation data from physics state maps into the buffer.
			// Acquire the physics lock for the snapshot copy so teardown cannot
			// dispose the native maps between ExecuteTick() and publication.
			var snapshotStartUsec = GetTimestampUsec();
			if (!_physicsEngine.TrySnapshotAnimations(ref writeBuffer)) {
				return;
			}
			_lastSnapshotCopyUsec = GetTimestampUsec() - snapshotStartUsec;
			writeBuffer.SnapshotCopyUsec = _lastSnapshotCopyUsec;
			if (SimulationTrace.IsRecording) {
				SimulationTrace.Tick.SnapshotUsec = (int)_lastSnapshotCopyUsec;
			}
			writeBuffer.PublishRealTimeUsec = GetTimestampUsec();

			// Atomically publish this buffer (lock-free triple-buffer swap)
			_sharedState.PublishWriteBuffer();
		}

		private static long GetTimestampUsec()
		{
			long ticks = Stopwatch.GetTimestamp();
			return (ticks * 1_000_000) / Stopwatch.Frequency;
		}

		private long ScaledTickIntervalUsec()
		{
			var scaledTickUsec = (long)math.round(TickIntervalUsec * Volatile.Read(ref _simulationClockScale));
			return scaledTickUsec < 0 ? 0 : scaledTickUsec;
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

	/// <summary>
	/// One switch driven by an input action on the simulation thread.
	/// </summary>
	internal sealed class InputSwitchBinding
	{
		public string SwitchId;
		public bool InvertsPressed;
		public bool ToggleOnPress;
		public bool State;
	}

	/// <summary>
	/// A switch the table maps to an input action, as handed to the simulation thread.
	/// </summary>
	internal readonly struct InputSwitchMapping
	{
		public readonly string InputAction;
		public readonly string SwitchId;
		public readonly bool NormallyClosed;

		public InputSwitchMapping(string inputAction, string switchId, bool normallyClosed)
		{
			InputAction = inputAction;
			SwitchId = switchId;
			NormallyClosed = normallyClosed;
		}
	}
}
