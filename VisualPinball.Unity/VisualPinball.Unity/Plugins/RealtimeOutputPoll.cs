// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NLog;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Decides who calls an engine's <see cref="IGamelogicRealtimeOutputs.PollRealtimeOutputs"/>: the simulation thread
	/// on every tick while it runs, the player's frame update otherwise, and nobody while no listener is set.
	/// </summary>
	/// <remarks>
	/// There is one instance per engine, shared by <see cref="GamelogicPluginResources"/> (which sets the listener),
	/// <see cref="Simulation.SimulationThread"/> and <see cref="Player"/>. The polls themselves do not allocate; a poll
	/// that throws is logged, throttled, and the next one runs as usual.
	/// </remarks>
	internal sealed class RealtimeOutputPoll
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly LogThrottle PollFailureThrottle = new(3, TimeSpan.FromSeconds(30));
		private static readonly ConditionalWeakTable<IGamelogicRealtimeOutputs, RealtimeOutputPoll> Polls = new();

		private readonly IGamelogicRealtimeOutputs _engine;
		private int _listening;
		private int _simulationThreads;

		private RealtimeOutputPoll(IGamelogicRealtimeOutputs engine)
		{
			_engine = engine;
		}

		/// <summary>The poll of an engine, or null when the engine has no real-time outputs.</summary>
		internal static RealtimeOutputPoll For(IGamelogicEngine engine)
			=> engine is IGamelogicRealtimeOutputs realtime ? Polls.GetValue(realtime, e => new RealtimeOutputPoll(e)) : null;

		/// <summary>Whether a listener is set, so polls report something.</summary>
		internal bool IsListening => Volatile.Read(ref _listening) != 0;

		/// <summary>Whether a simulation thread polls, so the frame update does not.</summary>
		internal bool IsPolledBySimulationThread => Volatile.Read(ref _simulationThreads) > 0;

		/// <summary>
		/// Sets the engine's listener, or null, on the main thread. Polls start with the next tick or frame.
		/// </summary>
		internal void SetListener(IRealtimeOutputListener listener)
		{
			if (listener == null) {
				Volatile.Write(ref _listening, 0);
			}
			_engine.SetRealtimeOutputListener(listener);
			if (listener != null) {
				Volatile.Write(ref _listening, 1);
			}
		}

		/// <summary>
		/// A simulation thread's loop starts ticking for this engine's game. Called on that thread, so a thread that never
		/// gets there (physics did not initialize) leaves the polls to the frame update.
		/// </summary>
		internal void SimulationThreadStarted() => Interlocked.Increment(ref _simulationThreads);

		/// <summary>The simulation thread's loop ended, normally or not (called from its <c>finally</c>); the frame update polls again.</summary>
		internal void SimulationThreadStopped() => Interlocked.Decrement(ref _simulationThreads);

		/// <summary>Called by the simulation thread on every tick, between the time fence and the shared state.</summary>
		internal void PollFromSimulationThread()
		{
			if (Volatile.Read(ref _listening) != 0) {
				Poll();
			}
		}

		/// <summary>Called once per frame on the main thread. Polls only when no simulation thread does.</summary>
		internal void PollFromFrame()
		{
			if (Volatile.Read(ref _listening) != 0 && Volatile.Read(ref _simulationThreads) <= 0) {
				Poll();
			}
		}

		private void Poll()
		{
			try {
				_engine.PollRealtimeOutputs();
			} catch (Exception e) {
				if (PollFailureThrottle.ShouldLog(out var suppressed)) {
					Logger.Error(e, "Polling the real-time outputs failed ({Suppressed} similar suppressed).", suppressed);
				}
			}
		}
	}
}
