// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// The plugin host's owner thread: a queue of work that runs on the thread that created the scheduler, which plugins use
	/// to get back to that thread after background work.
	/// </summary>
	/// <remarks>
	/// The Player creates one scheduler on Unity's main thread and hands it to every plugin through
	/// <see cref="PluginApplicationContext.Scheduler"/>. Plugin hooks, resource events and Unity calls all belong on this
	/// thread. Queued work runs when the host pumps the queue, which <see cref="PluginHost.Tick"/> does once per frame, so
	/// <c>await Switch()</c> resumes on the next frame at the latest. The scheduler does not depend on Unity's
	/// synchronization context, so it also works after <c>ConfigureAwait(false)</c> and in tests without Unity.
	/// <example>
	/// The B2S plugin reads the backglass file on a worker and returns to the owner thread before touching Unity:
	/// <code>
	/// var backglass = await Task.Run(() =&gt; DirectB2SReader.Read(path), cancellation);
	/// await _application.Scheduler.Switch();
	/// var view = new B2SBackglassView(backglass, options); // owner thread again
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class OwnerScheduler
	{
		private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
		private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();
		/// <summary>
		/// True when the calling thread is the owner thread, the thread that constructed this scheduler. Safe to read from any thread.
		/// </summary>
		public bool IsOwner => Thread.CurrentThread.ManagedThreadId == _thread;
		/// <summary>
		/// Throws unless called on the owner thread. Plugins call it at the top of callbacks that must not run on a worker, for
		/// example the DMD plugin's presentation factory.
		/// </summary>
		/// <exception cref="InvalidOperationException">The calling thread is not the owner thread.</exception>
		public void VerifyOwner() { if (!IsOwner) throw new InvalidOperationException("This plugin operation belongs on the owner thread."); }
		/// <summary>
		/// Queues an action to run on the owner thread during the next <see cref="Pump"/>. Thread-safe; actions run in the order
		/// they were posted. Unlike <see cref="Switch"/>, the action is always queued, even when called on the owner thread.
		/// </summary>
		/// <remarks>
		/// An exception thrown by the action escapes <see cref="Pump"/> (and so <see cref="PluginHost.Tick"/>), so catch inside
		/// the action. <c>GamelogicPluginResources</c> posts controller events raised off the owner thread this way; the DMD plugin
		/// posts the release of its native resources once its worker has stopped.
		/// </remarks>
		/// <param name="action">The work to run on the owner thread.</param>
		public void Post(Action action) => _pending.Enqueue(action);
		/// <summary>
		/// Runs queued actions on the owner thread, at most <paramref name="budget"/> of them, so a flood of posts cannot stall a
		/// frame. Actions left over run on the next call. The host calls this from <see cref="PluginHost.Tick"/>; plugins do not
		/// need to call it.
		/// </summary>
		/// <param name="budget">The maximum number of actions to run in this call.</param>
		/// <exception cref="InvalidOperationException">The calling thread is not the owner thread.</exception>
		public void Pump(int budget = 4096)
		{
			VerifyOwner();
			for (int i = 0; i < budget && _pending.TryDequeue(out var action); i++) action();
		}
		/// <summary>
		/// Returns an awaitable that continues on the owner thread. On the owner thread it completes immediately; elsewhere the
		/// continuation is posted and runs during the next <see cref="Pump"/>.
		/// </summary>
		/// <remarks>
		/// Use it after every <c>await</c> that may resume on a worker (<c>Task.Run</c>, <c>ConfigureAwait(false)</c>) before
		/// touching plugin state, resources or Unity. If nothing pumps the scheduler, the continuation never runs.
		/// </remarks>
		public OwnerAwaitable Switch() => new OwnerAwaitable(this);
		/// <summary>The awaitable returned by <see cref="Switch"/>. Use it only with <c>await</c>.</summary>
		public readonly struct OwnerAwaitable
		{
			private readonly OwnerScheduler _scheduler;
			internal OwnerAwaitable(OwnerScheduler scheduler) { _scheduler = scheduler; }
			/// <summary>Returns the awaiter the compiler uses for <c>await</c>.</summary>
			public Awaiter GetAwaiter() => new Awaiter(_scheduler);
			/// <summary>Awaiter that resumes on the scheduler's owner thread. Called by compiler-generated code, not directly.</summary>
			public readonly struct Awaiter : INotifyCompletion
			{
				private readonly OwnerScheduler _scheduler;
				internal Awaiter(OwnerScheduler scheduler) { _scheduler = scheduler; }
				/// <summary>True on the owner thread, in which case the <c>await</c> continues synchronously without queueing.</summary>
				public bool IsCompleted => _scheduler.IsOwner;
				/// <summary>Posts the continuation to the owner queue; it runs during the next <see cref="OwnerScheduler.Pump"/>.</summary>
				/// <param name="continuation">The rest of the awaiting method.</param>
				public void OnCompleted(Action continuation) => _scheduler.Post(continuation);
				/// <summary>Ends the <c>await</c>, checking that execution really is on the owner thread.</summary>
				/// <exception cref="InvalidOperationException">The continuation was resumed on another thread.</exception>
				public void GetResult() => _scheduler.VerifyOwner();
			}
		}
	}
}
