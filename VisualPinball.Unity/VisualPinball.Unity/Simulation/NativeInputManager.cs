// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Threading;
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
		private const string LogPrefix = "[PinMAME-debug]";
		private static int _loggedFirstEvent;

		#region Fields

		private static volatile NativeInputManager _instance;
		private static readonly object _instanceLock = new object();

		private volatile SimulationThread _simulationThread;
		private bool _initialized = false;
		private bool _polling = false;

		// Input configuration
		private readonly List<NativeInputApi.InputBinding> _bindings = new();

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
		/// Start input polling
		/// </summary>
		/// <param name="pollIntervalUs">Polling interval in microseconds (default 500)</param>
		public bool StartPolling(int pollIntervalUs = 500)
		{
			#if UNITY_EDITOR
			// Avoid extremely aggressive polling in the editor; it can starve other threads
			// (notably PinMAME's emulation thread) and lead to flaky stop/start.
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

			// Send bindings to native layer
			NativeInputApi.VpeInputSetBindings(_bindings.ToArray(), _bindings.Count);

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
			Logger.Info($"{LogPrefix} [NativeInputManager] Started polling at {pollIntervalUs}us interval ({1000000 / pollIntervalUs} Hz)");

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

			// Flippers
			AddBinding(NativeInputApi.InputAction.LeftFlipper, NativeInputApi.KeyCode.LShift);
			AddBinding(NativeInputApi.InputAction.RightFlipper, NativeInputApi.KeyCode.RShift);
			// Fallback keys (useful when modifier VKs are unreliable in some contexts)
			AddBinding(NativeInputApi.InputAction.LeftFlipper, NativeInputApi.KeyCode.A);
			AddBinding(NativeInputApi.InputAction.RightFlipper, NativeInputApi.KeyCode.D);

			// Start
			AddBinding(NativeInputApi.InputAction.Start, NativeInputApi.KeyCode.D1);

			// Coin
			AddBinding(NativeInputApi.InputAction.CoinInsert1, NativeInputApi.KeyCode.D5);

			// Plunger (align with Unity InputManager defaults: Enter)
			AddBinding(NativeInputApi.InputAction.Plunge, NativeInputApi.KeyCode.Return);
			AddBinding(NativeInputApi.InputAction.Plunge, NativeInputApi.KeyCode.Space);

			Logger.Info($"{LogPrefix} [NativeInputManager] Configured {_bindings.Count} default bindings");
		}

		/// <summary>
		/// Input event callback from native layer (called on input polling thread)
		/// </summary>
		[MonoPInvokeCallback(typeof(NativeInputApi.InputEventCallback))]
		private static void OnInputEvent(ref NativeInputApi.InputEvent evt, IntPtr userData)
		{
			if (Interlocked.Exchange(ref _loggedFirstEvent, 1) == 0) {
				Logger.Info($"{LogPrefix} [NativeInputManager] First event: Action={evt.Action}, Value={evt.Value}, Timestamp={evt.TimestampUsec}");
			}
			if (Logger.IsTraceEnabled) {
				Logger.Trace($"{LogPrefix} [NativeInputManager] Received from native: Action={evt.Action}, Value={evt.Value}, Timestamp={evt.TimestampUsec}");
			}

			// Forward to simulation thread via ring buffer
			var instance = Volatile.Read(ref _instance);
			instance?._simulationThread?.EnqueueInputEvent(evt);
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
}
