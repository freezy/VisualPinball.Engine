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
	/// <summary>An explicit owner-thread queue. Awaiting Switch returns to this owner without relying on Unity's synchronization context.</summary>
	public sealed class OwnerScheduler
	{
		private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
		private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();
		public bool IsOwner => Thread.CurrentThread.ManagedThreadId == _thread;
		public void VerifyOwner() { if (!IsOwner) throw new InvalidOperationException("This plugin operation belongs on the owner thread."); }
		public void Post(Action action) => _pending.Enqueue(action);
		public void Pump(int budget = 4096)
		{
			VerifyOwner();
			for (int i = 0; i < budget && _pending.TryDequeue(out var action); i++) action();
		}
		public OwnerAwaitable Switch() => new OwnerAwaitable(this);
		public readonly struct OwnerAwaitable
		{
			private readonly OwnerScheduler _scheduler;
			internal OwnerAwaitable(OwnerScheduler scheduler) { _scheduler = scheduler; }
			public Awaiter GetAwaiter() => new Awaiter(_scheduler);
			public readonly struct Awaiter : INotifyCompletion
			{
				private readonly OwnerScheduler _scheduler;
				internal Awaiter(OwnerScheduler scheduler) { _scheduler = scheduler; }
				public bool IsCompleted => _scheduler.IsOwner;
				public void OnCompleted(Action continuation) => _scheduler.Post(continuation);
				public void GetResult() => _scheduler.VerifyOwner();
			}
		}
	}
}
