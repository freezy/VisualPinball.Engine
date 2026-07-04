// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using NLog;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Simulation
{
	internal interface IGamelogicInputDispatcher : IDisposable
	{
		void DispatchSwitch(string switchId, bool isClosed);
		void FlushMainThread();
	}

	internal static class GamelogicInputDispatcherFactory
	{
		public static IGamelogicInputDispatcher Create(IGamelogicEngine gamelogicEngine)
		{
			if (gamelogicEngine == null) {
				return NoopInputDispatcher.Instance;
			}

			if (gamelogicEngine is IGamelogicInputThreading { SwitchDispatchMode: GamelogicInputDispatchMode.SimulationThread }) {
				return new DirectInputDispatcher(gamelogicEngine);
			}

			return new MainThreadQueuedInputDispatcher(gamelogicEngine);
		}
	}

	internal sealed class NoopInputDispatcher : IGamelogicInputDispatcher
	{
		public static readonly NoopInputDispatcher Instance = new NoopInputDispatcher();

		private NoopInputDispatcher()
		{
		}

		public void DispatchSwitch(string switchId, bool isClosed)
		{
		}

		public void FlushMainThread()
		{
		}

		public void Dispose()
		{
		}
	}

	internal sealed class DirectInputDispatcher : IGamelogicInputDispatcher
	{
		private readonly IGamelogicEngine _gamelogicEngine;

		public DirectInputDispatcher(IGamelogicEngine gamelogicEngine)
		{
			_gamelogicEngine = gamelogicEngine;
		}

		public void DispatchSwitch(string switchId, bool isClosed)
		{
			_gamelogicEngine.Switch(switchId, isClosed);
		}

		public void FlushMainThread()
		{
		}

		public void Dispose()
		{
		}
	}

	internal sealed class MainThreadQueuedInputDispatcher : IGamelogicInputDispatcher
	{
		private readonly struct QueuedSwitchEvent
		{
			public readonly string SwitchId;
			public readonly bool IsClosed;
			public readonly Action AfterDispatch;

			public QueuedSwitchEvent(string switchId, bool isClosed, Action afterDispatch = null)
			{
				SwitchId = switchId;
				IsClosed = isClosed;
				AfterDispatch = afterDispatch;
			}
		}

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const int MaxQueuedEvents = 8192;

		private readonly IGamelogicEngine _gamelogicEngine;
		private readonly object _queueLock = new object();
		private readonly Queue<QueuedSwitchEvent> _queue = new Queue<QueuedSwitchEvent>(256);
		private int _droppedEvents;

		public MainThreadQueuedInputDispatcher(IGamelogicEngine gamelogicEngine)
		{
			_gamelogicEngine = gamelogicEngine;
		}

		public void DispatchSwitch(string switchId, bool isClosed)
		{
			DispatchSwitch(switchId, isClosed, null);
		}

		/// <summary>
		/// Queues a switch event and runs <paramref name="afterDispatch"/> on the main thread
		/// right after the gamelogic engine processed it — i.e. after any synchronous reaction
		/// (such as a scripted nudge) has taken place.
		/// </summary>
		public void DispatchSwitch(string switchId, bool isClosed, Action afterDispatch)
		{
			lock (_queueLock) {
				if (_queue.Count >= MaxQueuedEvents) {
					_droppedEvents++;
					return;
				}
				_queue.Enqueue(new QueuedSwitchEvent(switchId, isClosed, afterDispatch));
			}
		}

		public void FlushMainThread()
		{
			while (true) {
				QueuedSwitchEvent item;
				int dropped = 0;
				lock (_queueLock) {
					if (_queue.Count == 0) {
						dropped = _droppedEvents;
						_droppedEvents = 0;
						item = default;
					} else {
						item = _queue.Dequeue();
					}
				}

				if (dropped > 0) {
					Logger.Warn($"[SimulationThread] Dropped {dropped} queued switch events for {_gamelogicEngine.Name}");
				}

				if (item.SwitchId == null) {
					break;
				}

				_gamelogicEngine.Switch(item.SwitchId, item.IsClosed);
				item.AfterDispatch?.Invoke();
			}
		}

		public void Dispose()
		{
			lock (_queueLock) {
				_queue.Clear();
				_droppedEvents = 0;
			}
		}
	}
}