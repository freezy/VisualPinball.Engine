// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Runtime;
using System.Threading;
using NLog;
using Logger = NLog.Logger;

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

		#region Constants

		private const long TickIntervalUsec = 1000; // 1ms = 1000 microseconds
		private const long BusyWaitThresholdUsec = 100; // Last 100μs busy-wait for precision
		private const double TickIntervalSeconds = 0.001; // 1ms in seconds

		#endregion

		#region Fields

		private readonly PhysicsEngine _physicsEngine;
		private readonly IGamelogicEngine _gamelogicEngine;
		private readonly InputEventBuffer _inputBuffer;
		private readonly SimulationState _sharedState;

		private Thread _thread;
		private volatile bool _running = false;
		private volatile bool _paused = false;

		// Timing
		private long _lastTickUsec;
		private long _simulationTimeUsec;
		private double _pinmameSimulationTimeSeconds;

		// Input state tracking
		private readonly Dictionary<NativeInputApi.InputAction, bool> _inputStates = new();

		// Statistics
		private long _tickCount = 0;
		private long _inputEventsProcessed = 0;

		#endregion

		#region Constructor

		public SimulationThread(PhysicsEngine physicsEngine, IGamelogicEngine gamelogicEngine)
		{
			_physicsEngine = physicsEngine ?? throw new ArgumentNullException(nameof(physicsEngine));
			_gamelogicEngine = gamelogicEngine;

			_inputBuffer = new InputEventBuffer(1024);
			_sharedState = new SimulationState();

			// Initialize input states
			foreach (NativeInputApi.InputAction action in Enum.GetValues(typeof(NativeInputApi.InputAction)))
			{
				_inputStates[action] = false;
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
			_lastTickUsec = NativeInputApi.VpeGetTimestampUsec();
			_simulationTimeUsec = 0;
			_pinmameSimulationTimeSeconds = 0.0;
			_tickCount = 0;

			_thread = new Thread(SimulationThreadFunc)
			{
				Name = "VPE Simulation Thread",
				IsBackground = false,
				Priority = ThreadPriority.Highest
			};
			_thread.Start();

			Logger.Info("[SimulationThread] Started at 1000 Hz");
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

			Logger.Info($"[SimulationThread] Stopped after {_tickCount} ticks, {_inputEventsProcessed} input events");
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
			_inputBuffer.TryEnqueue(evt);
		}

		/// <summary>
		/// Get the current shared state (for main thread to read)
		/// </summary>
		public ref readonly SimulationState.Snapshot GetSharedState()
		{
			return ref _sharedState.GetFrontBuffer();
		}

		#endregion

		#region Simulation Thread

		private void SimulationThreadFunc()
		{
			// Set thread priority to time-critical
			NativeInputApi.VpeSetThreadPriority();

			// Try to enable no-GC region for hot path
			bool noGcRegion = false;
			try
			{
				// Allocate 10MB for no-GC region
				if (GC.TryStartNoGCRegion(10 * 1024 * 1024, true))
				{
					noGcRegion = true;
					Logger.Info("[SimulationThread] No-GC region enabled");
				}
			}
			catch (Exception ex)
			{
				Logger.Warn($"[SimulationThread] Failed to start no-GC region: {ex.Message}");
			}

			try
			{
				// Main simulation loop
				while (_running)
				{
					if (_paused)
					{
						Thread.Sleep(10);
						continue;
					}

					// Timing: Sleep until next tick, then busy-wait for precision
					long targetTimeUsec = _lastTickUsec + TickIntervalUsec;
					long nowUsec = NativeInputApi.VpeGetTimestampUsec();
					long sleepUsec = targetTimeUsec - nowUsec;

					if (sleepUsec > BusyWaitThresholdUsec)
					{
						// Sleep most of the time to avoid CPU waste
						Thread.Sleep((int)((sleepUsec - BusyWaitThresholdUsec) / 1000));
					}

					// Busy-wait for precision (last 100μs)
					while (NativeInputApi.VpeGetTimestampUsec() < targetTimeUsec)
					{
						Thread.SpinWait(10); // ~40ns per iteration
					}

					// Execute simulation tick (hot path - must be allocation-free!)
					SimulationTick();

					_lastTickUsec = targetTimeUsec;
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
						Logger.Info("[SimulationThread] No-GC region ended");
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
			// 1. Process input events from ring buffer
			ProcessInputEvents();

			// 2. Advance PinMAME simulation using SetTimeFence
			if (_gamelogicEngine != null)
			{
				AdvancePinMAME();
			}

			// 3. Poll PinMAME outputs (coils, lamps, GI)
			if (_gamelogicEngine != null)
			{
				PollPinMAMEOutputs();
			}

			// 4. Update physics simulation
			UpdatePhysics();

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
			while (_inputBuffer.TryDequeue(out var evt))
			{
				var action = (NativeInputApi.InputAction)evt.Action;
				bool isPressed = evt.Value > 0.5f;

				// Track state change
				bool previousState = _inputStates[action];
				_inputStates[action] = isPressed;

				// Only process if state changed
				if (previousState != isPressed)
				{
					HandleInputAction(action, isPressed);
					_inputEventsProcessed++;
				}
			}
		}

		/// <summary>
		/// Handle a single input action (map to switch/coil)
		/// </summary>
		private void HandleInputAction(NativeInputApi.InputAction action, bool isPressed)
		{
			// Map input actions to switch IDs
			// This is a simplified example - actual mapping would come from configuration
			switch (action)
			{
				case NativeInputApi.InputAction.LeftFlipper:
					_gamelogicEngine?.Switch("s_flipper_left", isPressed);
					break;

				case NativeInputApi.InputAction.RightFlipper:
					_gamelogicEngine?.Switch("s_flipper_right", isPressed);
					break;

				case NativeInputApi.InputAction.Start:
					_gamelogicEngine?.Switch("s_start", isPressed);
					break;

				case NativeInputApi.InputAction.Plunge:
					_gamelogicEngine?.Switch("s_plunger", isPressed);
					break;

				// Add more mappings as needed
			}
		}

		/// <summary>
		/// Advance PinMAME simulation to current time using SetTimeFence
		/// </summary>
		private void AdvancePinMAME()
		{
			// Increment PinMAME time
			_pinmameSimulationTimeSeconds += TickIntervalSeconds;

			// Tell PinMAME to run until this time
			// Note: This requires the PinMAME API to expose SetTimeFence in C#
			// For now, we'll assume it's running asynchronously
			// TODO: Add PinmameSetTimeFence to pinmame-dotnet API
		}

		/// <summary>
		/// Poll PinMAME for output changes (coils, lamps, GI)
		/// </summary>
		private void PollPinMAMEOutputs()
		{
			// This would use the PinMAME API to get changed outputs
			// For now, this is a placeholder
			// TODO: Implement PinMAME output polling
		}

		/// <summary>
		/// Update physics simulation (1ms step)
		/// </summary>
		private void UpdatePhysics()
		{
			// TODO: Refactor PhysicsEngine to expose ExecuteTick(long deltaUsec) method
			//
			// Required changes to PhysicsEngine.cs:
			// 1. Add public method: ExecuteTick(long deltaUsec)
			// 2. Expose PhysicsState for external access (or use internal state)
			// 3. Update timing from external source instead of Time.deltaTime
			// 4. Ensure thread-safety for concurrent access
			//
			// Proposed implementation:
			// public void ExecuteTick(long deltaUsec)
			// {
			//     _physicsEnv.CurPhysicsFrameTime = deltaUsec;
			//     _physicsEnv.NextPhysicsFrameTime = deltaUsec + PhysicsConstants.PhysicsStepTime;
			//     PhysicsUpdate.Execute(ref _physicsState, ref _physicsEnv, ...);
			// }
			//
			// For now, physics continues to run in Unity's Update() loop.
			// This integration is tracked in SIMULATION_THREAD_TODO.md Phase 1.
		}

		/// <summary>
		/// Write simulation state to shared memory and swap buffers
		/// </summary>
		private void WriteSharedState()
		{
			ref var backBuffer = ref _sharedState.GetBackBuffer();

			// Update timing
			backBuffer.SimulationTimeUsec = _simulationTimeUsec;
			backBuffer.RealTimeUsec = NativeInputApi.VpeGetTimestampUsec();

			// Copy PinMAME state (coils, lamps, GI)
			// This is where we'd copy the changed outputs from PinMAME
			// For now, this is a placeholder
			// TODO: Implement state copying

			// Increment physics state version (main thread will detect changes)
			backBuffer.PhysicsStateVersion++;

			// Atomically swap buffers (lock-free)
			_sharedState.SwapBuffers();
		}

		#endregion

		#region Dispose

		public void Dispose()
		{
			Stop();
			_inputBuffer?.Dispose();
			_sharedState?.Dispose();
		}

		#endregion
	}
}