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

		#endregion

		#region Unity Lifecycle

		private void Awake()
		{
			_physicsEngine = GetComponent<PhysicsEngine>();

			// Get gamelogic engine from Player if available
			var player = GetComponent<Player>();
			if (player != null)
			{
				_gamelogicEngine = player.GamelogicEngine;
			}
		}

		private void Start()
		{
			if (!EnableSimulationThread)
			{
				Logger.Info("[SimulationThreadComponent] Simulation thread disabled");
				return;
			}

			StartSimulation();
		}

		private void Update()
		{
			if (!_started || _simulationThread == null) return;

			// Read shared state from simulation thread
			ref readonly var state = ref _simulationThread.GetSharedState();

			// Apply state to Unity GameObjects
			ApplySimulationState(in state);

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
				// Create simulation thread
				_simulationThread = new SimulationThread(_physicsEngine, _gamelogicEngine);

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
						Logger.Warn("[SimulationThreadComponent] Native input not available, falling back to Unity Input System");
					}
				}

				// Start simulation thread
				_simulationThread.Start();

				_started = true;
				_lastStatisticsTime = Time.time;

				Logger.Info("[SimulationThreadComponent] Simulation started");
			}
			catch (Exception ex)
			{
				Logger.Error($"[SimulationThreadComponent] Failed to start simulation: {ex}");
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

			_started = false;

			Logger.Info("[SimulationThreadComponent] Simulation stopped");
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

			Logger.Info($"[SimulationThread] Stats: SimTime={simTimeMs}ms, RealTime={realTimeMs}ms, Ratio={ratio:F3}x, PhysicsVer={state.PhysicsStateVersion}");
		}

		#endregion
	}
}