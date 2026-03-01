// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Unity component that manages the high-performance simulation thread.
	/// Add this to your table GameObject to enable sub-millisecond input latency.
	///
	/// Architecture:
	/// - Simulation thread runs at 1000 Hz (1ms per tick)
	/// - Input polling thread runs at 500-1000 Hz
	/// - Unity main thread runs at display refresh rate (60-144 Hz)
	/// - Lock-free communication between threads using ring buffers and double-buffering
	/// </summary>
	[AddComponentMenu("Visual Pinball/Simulation Thread")]
	[RequireComponent(typeof(PhysicsEngine))]
	public class SimulationThreadComponent : MonoBehaviour
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const string LogPrefix = "[PinMAME-debug]";

		#region Inspector Fields

		[Header("Simulation Settings")]
		[Tooltip("Enable the high-performance simulation thread (1000 Hz)")]
		public bool EnableSimulationThread = true;

		[Tooltip("Enable native input polling (Windows only, requires VpeNativeInput.dll)")]
		public bool EnableNativeInput = true;

		[Tooltip("Input polling interval in microseconds (default 500μs = 2000 Hz)")]
		[Range(100, 2000)]
		public int InputPollingIntervalUs = 500;

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
		/// Start the simulation thread
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

				// Create simulation thread
				_simulationThread = new SimulationThread(_physicsEngine, _gamelogicEngine,
					player != null
						? new Action<string, bool>((coilId, isEnabled) => player.DispatchCoilSimulationThread(coilId, isEnabled))
						: null);

				// Initialize and start native input if enabled
				if (EnableNativeInput)
				{
					_inputManager = NativeInputManager.Instance;
					if (_inputManager.Initialize())
					{
						_inputManager.SetSimulationThread(_simulationThread);
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
		/// Stop the simulation thread
		/// </summary>
		public void StopSimulation()
		{
			if (!_started) return;

			_inputManager?.StopPolling();
			_simulationThread?.Stop();
			_simulationThread?.Dispose();
			_simulationThread = null;

			// Restore normal Unity Update() loop timing
			_physicsEngine.SetExternalTiming(false);

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
			// This is where we'd update GameObjects based on the simulation state
			// For now, the physics engine will continue to update GameObjects directly
			// In a full implementation, we'd read the physics state here and apply it

			// TODO: Apply ball positions, flipper rotations, etc. from shared state
		}

		/// <summary>
		/// Log statistics about simulation performance
		/// </summary>
		private void LogStatistics(in SimulationState.Snapshot state)
		{
			long simTimeMs = state.SimulationTimeUsec / 1000;
			long realTimeMs = state.RealTimeUsec / 1000;
			double ratio = (double)simTimeMs / realTimeMs;

			Logger.Info($"{LogPrefix} [SimulationThread] Stats: SimTime={simTimeMs}ms, RealTime={realTimeMs}ms, Ratio={ratio:F3}x, PhysicsVer={state.PhysicsStateVersion}");
		}

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
