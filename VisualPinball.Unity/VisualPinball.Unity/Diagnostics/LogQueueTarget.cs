// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NLog;
using NLog.Common;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The queue between the threads that log and the targets that write.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// The logging thread only enqueues the event together with its own name. Everything else,
	/// including rendering the message and the exception, happens on the writer thread.
	/// NLog's own <c>AsyncTargetWrapper</c> renders events that carry an exception or object
	/// arguments on the logging thread, and so does merely touching
	/// <see cref="LogEventInfo.Properties"/>, which is why the thread name isn't stored there
	/// until the event is written.
	/// </para>
	/// <para>
	/// The writer drains the queue every 50 ms, or right away for errors. NLog calls
	/// <see cref="FlushAsync"/> while holding this target's lock, so a flush only queues a
	/// request that the writer completes after its next drain. <see cref="Drain"/> writes
	/// everything queued so far on the calling thread; the session boundary uses it so nothing
	/// from the previous session ends up in the next one.
	/// </para>
	/// <para>
	/// Targets behind this queue must not log through NLog while they write: they run on the
	/// writer thread, and a log call from there needs this target's lock, which a closing
	/// configuration may hold. <see cref="CloseTarget"/> only waits with a timeout for that
	/// reason, and gives up on what's queued rather than deadlock.
	/// </para>
	/// </remarks>
	internal sealed class LogQueueTarget : WrapperTargetBase
	{
		private const int BatchSize = 200;
		private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

		private readonly ConcurrentQueue<(AsyncLogEventInfo logEvent, string thread)> _queue = new();
		private readonly ConcurrentQueue<AsyncContinuation> _flushRequests = new();
		private readonly int _limit;
		private readonly bool _startWriter;
		private readonly object _drainLock = new();
		private readonly AutoResetEvent _wake = new(false);
		private readonly List<AsyncLogEventInfo> _batch = new(BatchSize);
		private int _count;
		private volatile bool _abandoned;
		private Thread _drainingThread;
		private volatile bool _closing;
		private Thread _writer;

		/// <summary>
		/// Raised on the logging thread when an event is dropped because the queue is full.
		/// </summary>
		public event Action Dropped;

		/// <summary>
		/// Number of events waiting to be written.
		/// </summary>
		public int Count => Volatile.Read(ref _count);

		/// <param name="startWriter">False only in tests, which drain by hand.</param>
		public LogQueueTarget(string name, Target wrappedTarget, int limit, bool startWriter = true)
		{
			Name = name;
			WrappedTarget = wrappedTarget;
			_limit = limit;
			_startWriter = startWriter;
		}

		protected override void InitializeTarget()
		{
			base.InitializeTarget();
			_closing = false;
			if (!_startWriter) {
				return;
			}
			_writer = new Thread(WriterLoop) {
				Name = "VPE Log Writer",
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
			};
			_writer.Start();
		}

		protected override void Write(AsyncLogEventInfo logEvent)
		{
			if (Interlocked.Increment(ref _count) > _limit) {
				Interlocked.Decrement(ref _count);
				Dropped?.Invoke();
				logEvent.Continuation(null);
				return;
			}
			_queue.Enqueue((logEvent, VpeLog.CurrentThreadName()));
			if (logEvent.LogEvent.Level >= LogLevel.Error) {
				_wake.Set();
			}
		}

		/// <summary>
		/// Writes everything queued so far, on the calling thread. Does nothing when called
		/// from inside a write (a target or continuation that logs or flushes); the drain
		/// that is already running picks those events up.
		/// </summary>
		public void Drain()
		{
			var current = Thread.CurrentThread;
			if (Volatile.Read(ref _drainingThread) == current) {
				return;
			}
			lock (_drainLock) {
				// nested drains return above, so the batch list is never shared
				_drainingThread = current;
				try {
					while (!_abandoned) {
						_batch.Clear();
						while (_batch.Count < BatchSize && _queue.TryDequeue(out var item)) {
							item.logEvent.LogEvent.Properties[VpeLog.ThreadProperty] = item.thread;
							_batch.Add(item.logEvent);
						}
						if (_batch.Count == 0) {
							return;
						}
						var written = _batch.Count;
						try {
							WrappedTarget.WriteAsyncLogEvents(_batch.ToArray());
						} catch (Exception e) {
							InternalLogger.Error(e, "VPE log queue: writing a batch failed.");
						} finally {
							Interlocked.Add(ref _count, -written);
							_batch.Clear();
						}
					}
				} finally {
					_drainingThread = null;
				}
			}
		}

		protected override void FlushAsync(AsyncContinuation asyncContinuation)
		{
			// Called under this target's lock: never wait here, and never touch the wrapped
			// targets here either (a drain on another thread may hold their locks). The writer,
			// or a pool thread when there is none, drains and flushes them.
			_flushRequests.Enqueue(asyncContinuation);
			if (_writer != null) {
				_wake.Set();
			} else {
				ThreadPool.QueueUserWorkItem(_ => DrainAndCompleteFlushes());
			}
		}

		/// <summary>
		/// Stops the writer thread and writes what's left on the calling thread. Call it before
		/// the configuration holding this target is replaced: NLog closes targets under their
		/// lock, which is no place to wait for a thread.
		/// </summary>
		/// <returns>False if the writer didn't stop in time.</returns>
		public bool Stop(TimeSpan timeout)
		{
			_closing = true;
			_wake.Set();
			var writer = _writer;
			if (writer != null && writer != Thread.CurrentThread && !writer.Join(timeout)) {
				return false;
			}
			_writer = null;
			// also completes flushes requested while the writer was on its way out
			DrainAndCompleteFlushes();
			return true;
		}

		protected override void CloseTarget()
		{
			// Called under NLog's lock: wait for nothing that might need it. VpeLog stops the
			// writer before it replaces the configuration, so this normally finds it gone.
			_closing = true;
			_wake.Set();
			var writer = _writer;
			var stopped = writer == null || writer == Thread.CurrentThread || writer.Join(TimeSpan.FromSeconds(2));
			if (stopped) {
				_writer = null;
				if (Monitor.TryEnter(_drainLock, TimeSpan.FromMilliseconds(500))) {
					try {
						Drain();
					} finally {
						Monitor.Exit(_drainLock);
					}
				} else {
					stopped = false;
				}
			}
			if (!stopped) {
				// Something is stuck in a target. The wrapped targets are about to be closed, so
				// nothing may write to them anymore; what's still queued is lost.
				_abandoned = true;
				InternalLogger.Error("VPE log queue: the writer did not stop, {0} log events are lost.", Count);
				UnityEngine.Debug.unityLogger.LogWarning("VisualPinball", $"The log writer did not stop in time, {Count} log lines are lost.");
			}
			while (_flushRequests.TryDequeue(out var continuation)) {
				continuation(null);
			}
			base.CloseTarget();
		}

		private void WriterLoop()
		{
			while (!_closing) {
				_wake.WaitOne(Interval);
				DrainAndCompleteFlushes();
			}
			DrainAndCompleteFlushes();
		}

		private void DrainAndCompleteFlushes()
		{
			// Take the requests first: every event logged before a request was queued before
			// it, so the drain below writes it (or waits for the drain writing it) before the
			// request completes. Requests that come in later belong to the next caller.
			List<AsyncContinuation> requests = null;
			while (_flushRequests.TryDequeue(out var request)) {
				(requests ??= new List<AsyncContinuation>()).Add(request);
			}
			Drain();
			if (requests == null) {
				return;
			}
			foreach (var continuation in requests) {
				try {
					WrappedTarget.Flush(continuation);
				} catch (Exception e) {
					continuation(e);
				}
			}
		}
	}
}
