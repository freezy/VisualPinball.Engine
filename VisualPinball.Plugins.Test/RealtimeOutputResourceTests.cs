// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class RealtimeOutputResourceTests
	{
		[Test]
		public void BackglassAndDofBindTheirOwnOutputsAtTheSameTime()
		{
			var registry = new PluginResourceRegistry();
			using var mapped = new ControllerResourceSource();
			using var native = new ControllerResourceSource(ResourceKind.Outputs);
			using var realtime = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			using var mappedPublication = registry.Publish(new PluginResourceDescriptor("pinmame", "pinmame", "pinmame", "PinMAME", "controller", ResourceKind.Controller, 1, 1), mapped);
			using var nativePublication = registry.Publish(new PluginResourceDescriptor("pinmame/outputs", "pinmame", "pinmame", "PinMAME outputs", "outputs", ResourceKind.Outputs, 1, 2), native);
			using var realtimePublication = registry.Publish(new PluginResourceDescriptor("pinmame/outputs/realtime", "pinmame", "pinmame", "PinMAME real-time outputs", "realtime-outputs", ResourceKind.RealtimeOutputs, 1, 3), realtime);

			// the B2S backglass binds the outputs without a role, DOF the real-time outputs
			using var b2s = registry.Bind<IPluginControllerSource>(new PluginResourceSelection(ResourceKind.Outputs) { Controller = "pinmame", Session = 1 });
			using var dof = registry.Bind<IPluginRealtimeOutputSource>(new PluginResourceSelection(ResourceKind.RealtimeOutputs) { Controller = "pinmame", Session = 1 });
			using var pinup = registry.Bind<IPluginControllerSource>(new PluginResourceSelection(ResourceKind.Controller) { Controller = "pinmame", Session = 1 });
			Assert.That(b2s.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(b2s.Source, Is.SameAs(native));
			Assert.That(dof.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(dof.Source, Is.SameAs(realtime));
			Assert.That(dof.Source!.Numbering, Is.EqualTo("pinmame"));
			Assert.That(pinup.Source, Is.SameAs(mapped));

			int backglassLamp = 0;
			var dofValues = new List<(RealtimeOutputKind, int, int)>();
			using var b2sSubscription = b2s.Connect(source => source.Subscribe(change => backglassLamp = change.Number));
			using var dofSubscription = dof.Connect(source => source.Subscribe((kind, number, value) => dofValues.Add((kind, number, value))));
			native.Publish(new PluginControllerEvent(ControllerEventKind.Lamp, "81", 1, 1, 0, 81, ControllerValueSemantics.Binary, 1));
			if (realtime.BeginSnapshot()) realtime.EndSnapshot();
			realtime.Publish(RealtimeOutputKind.Lamp, 81, 1);
			Assert.That(backglassLamp, Is.EqualTo(81));
			Assert.That(dofValues, Is.EqualTo(new[] { (RealtimeOutputKind.SnapshotBoundary, 0, 0), (RealtimeOutputKind.Lamp, 81, 1) }));
		}

		[Test]
		public void ARealtimeResourceMustImplementTheRealtimeContract()
		{
			var registry = new PluginResourceRegistry();
			Assert.Throws<ArgumentException>(() => registry.Publish(new PluginResourceDescriptor("pinmame/outputs/realtime", "pinmame", "pinmame", "fake", "realtime-outputs", ResourceKind.RealtimeOutputs, 1, 1), new FakeRealtimeSource()));
			using var outputs = new ControllerResourceSource(ResourceKind.Outputs);
			Assert.Throws<ArgumentException>(() => registry.Publish(new PluginResourceDescriptor("pinmame/outputs/realtime", "pinmame", "pinmame", "fake", "realtime-outputs", ResourceKind.RealtimeOutputs, 1, 1), outputs));
		}

		[Test]
		public void ANewReceiverGetsItsSnapshotThenTheBoundaryThenChangesOnly()
		{
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			var values = new List<(RealtimeOutputKind, int, int)>();
			using var subscription = source.Subscribe((kind, number, value) => values.Add((kind, number, value)));
			Assert.That(source.SnapshotRequested, Is.True);

			// a change the snapshot already contains is not delivered before it
			source.Publish(RealtimeOutputKind.Solenoid, 28, 1);
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Solenoid, 28, 1);
			source.PublishSnapshot(RealtimeOutputKind.Switch, 15, 0);
			source.EndSnapshot();
			source.Publish(RealtimeOutputKind.Switch, 15, 1);
			Assert.That(source.BeginSnapshot(), Is.False);

			Assert.That(values, Is.EqualTo(new[] {
				(RealtimeOutputKind.Solenoid, 28, 1), (RealtimeOutputKind.Switch, 15, 0), (RealtimeOutputKind.SnapshotBoundary, 0, 0),
				(RealtimeOutputKind.Switch, 15, 1),
			}));
		}

		[Test]
		public void AReceiverThatJoinsLaterGetsItsOwnSnapshotWhileTheOthersGetChanges()
		{
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			var first = new List<(RealtimeOutputKind, int, int)>();
			var second = new List<(RealtimeOutputKind, int, int)>();
			using var firstSubscription = source.Subscribe((kind, number, value) => first.Add((kind, number, value)));
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 0);
			source.EndSnapshot();

			using var secondSubscription = source.Subscribe((kind, number, value) => second.Add((kind, number, value)));
			source.Publish(RealtimeOutputKind.Lamp, 11, 1);
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 1);
			source.EndSnapshot();
			source.Publish(RealtimeOutputKind.Lamp, 11, 0);

			Assert.That(first, Is.EqualTo(new[] {
				(RealtimeOutputKind.Lamp, 11, 0), (RealtimeOutputKind.SnapshotBoundary, 0, 0), (RealtimeOutputKind.Lamp, 11, 1), (RealtimeOutputKind.Lamp, 11, 0),
			}));
			Assert.That(second, Is.EqualTo(new[] {
				(RealtimeOutputKind.Lamp, 11, 1), (RealtimeOutputKind.SnapshotBoundary, 0, 0), (RealtimeOutputKind.Lamp, 11, 0),
			}));
		}

		[Test]
		public void ModulatedValuesAreDeliveredInOrderWithoutCoalescing()
		{
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			var values = new List<int>();
			using var subscription = source.Subscribe((kind, number, value) => { if (kind == RealtimeOutputKind.Solenoid) values.Add(value); });
			if (source.BeginSnapshot()) source.EndSnapshot();
			foreach (int value in new[] { 0, 255, 128, 0 }) source.Publish(RealtimeOutputKind.Solenoid, 9, value);
			Assert.That(values, Is.EqualTo(new[] { 0, 255, 128, 0 }));
		}

		[Test]
		public void DeliveryDoesNotAllocate()
		{
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			var counter = new Counter();
			using var subscription = source.Subscribe(counter.Receive);
			using var other = source.Subscribe(counter.Receive);
			for (int warmup = 0; warmup < 2; warmup++) {
				if (source.BeginSnapshot()) { source.PublishSnapshot(RealtimeOutputKind.Lamp, 1, 0); source.EndSnapshot(); }
				source.Publish(RealtimeOutputKind.Lamp, 1, 1);
			}
			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 10000; i++) {
				source.BeginSnapshot();
				source.Publish(RealtimeOutputKind.Solenoid, i & 63, i & 255);
			}
			long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.That(allocated, Is.EqualTo(0));
			// both receivers: one snapshot value, the boundary and two warm-up changes, then every change
			Assert.That(counter.Count, Is.EqualTo(2 * (1 + 1 + 2) + 2 * 10000));
		}

		[Test]
		public void TheFirstAndTheLastReceiverAreReported()
		{
			var changes = new List<bool>();
			var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame, receiversChanged: changes.Add);
			var first = source.Subscribe((_, _, _) => { });
			var second = source.Subscribe((_, _, _) => { });
			first.Dispose(); first.Dispose();
			second.Dispose();
			using (source.Subscribe((_, _, _) => { })) { }
			source.Subscribe((_, _, _) => { });
			source.Dispose();
			Assert.That(changes, Is.EqualTo(new[] { true, false, true, false, true, false }));
			Assert.Throws<ObjectDisposedException>(() => source.Subscribe((_, _, _) => { }));
		}

		[Test]
		public void AFailingReceiverDoesNotStopTheOthers()
		{
			var faults = new List<Exception>();
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame, faults.Add);
			int received = 0;
			using var failing = source.Subscribe((_, _, _) => throw new InvalidOperationException("broken"));
			using var working = source.Subscribe((_, _, _) => received++);
			if (source.BeginSnapshot()) source.EndSnapshot();
			source.Publish(RealtimeOutputKind.Switch, 21, 1);
			source.Publish(RealtimeOutputKind.Switch, 21, 0);
			Assert.That(received, Is.EqualTo(3));
			// one report per receiver, however often it throws
			Assert.That(faults, Has.Count.EqualTo(1));
		}

		[Test]
		public void AnAbortedSnapshotIsSentAgainAndOnlyACompleteOneEndsWithTheBoundary()
		{
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			var values = new List<(RealtimeOutputKind, int, int)>();
			using var subscription = source.Subscribe((kind, number, value) => values.Add((kind, number, value)));
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Solenoid, 1, 0);
			// the producer threw half-way
			source.AbortSnapshot();
			source.Publish(RealtimeOutputKind.Solenoid, 1, 1);
			Assert.That(source.SnapshotRequested, Is.True);

			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Solenoid, 1, 1);
			source.PublishSnapshot(RealtimeOutputKind.Solenoid, 2, 0);
			source.EndSnapshot();
			source.Publish(RealtimeOutputKind.Solenoid, 2, 1);

			Assert.That(values, Is.EqualTo(new[] {
				(RealtimeOutputKind.Solenoid, 1, 0), (RealtimeOutputKind.SnapshotAborted, 0, 0),
				(RealtimeOutputKind.Solenoid, 1, 1), (RealtimeOutputKind.Solenoid, 2, 0), (RealtimeOutputKind.SnapshotBoundary, 0, 0),
				(RealtimeOutputKind.Solenoid, 2, 1),
			}));
		}

		[Test]
		public void AReceiverLearnsWhenItsAttemptIsAbandoned()
		{
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			var throwing = 1;
			var flaky = new List<(RealtimeOutputKind, int, int)>();
			var steady = new List<(RealtimeOutputKind, int, int)>();
			using var flakySubscription = source.Subscribe((kind, number, value) => {
				flaky.Add((kind, number, value));
				if (kind == RealtimeOutputKind.Lamp && throwing-- > 0) throw new InvalidOperationException("flaky");
			});
			using var steadySubscription = source.Subscribe((kind, number, value) => steady.Add((kind, number, value)));

			// the receiver throws: its attempt is called off, the other one goes live
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 1);
			source.EndSnapshot();
			// the producer gives up: nobody is in a snapshot but the flaky receiver
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 0);
			source.AbortSnapshot();
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 0);
			source.EndSnapshot();

			Assert.That(flaky, Is.EqualTo(new[] {
				(RealtimeOutputKind.Lamp, 11, 1), (RealtimeOutputKind.SnapshotAborted, 0, 0),
				(RealtimeOutputKind.Lamp, 11, 0), (RealtimeOutputKind.SnapshotAborted, 0, 0),
				(RealtimeOutputKind.Lamp, 11, 0), (RealtimeOutputKind.SnapshotBoundary, 0, 0),
			}));
			Assert.That(steady, Is.EqualTo(new[] { (RealtimeOutputKind.Lamp, 11, 1), (RealtimeOutputKind.SnapshotBoundary, 0, 0) }));
		}

		[Test]
		public void BeginningASnapshotAgainAbandonsTheUnfinishedOne()
		{
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame);
			var boundaries = 0;
			using var subscription = source.Subscribe((kind, _, _) => { if (kind == RealtimeOutputKind.SnapshotBoundary) boundaries++; });
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 0);
			// no end: the producer's poll threw before it
			Assert.That(source.BeginSnapshot(), Is.True);
			source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 0);
			source.EndSnapshot();
			source.EndSnapshot();
			Assert.That(boundaries, Is.EqualTo(1));
			Assert.That(source.BeginSnapshot(), Is.False);
		}

		[Test]
		public void AReceiverThatThrowsDuringItsSnapshotGetsAnotherAndIsDroppedAfterTooManyFailures()
		{
			var faults = new List<Exception>();
			using var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame, faults.Add);
			var failures = 2;
			var flaky = new List<(RealtimeOutputKind, int, int)>();
			var brokenCalls = 0;
			using var flakySubscription = source.Subscribe((kind, number, value) => {
				if (kind == RealtimeOutputKind.Lamp && number == 12 && failures > 0) {
					failures--;
					throw new InvalidOperationException("flaky");
				}
				flaky.Add((kind, number, value));
			});
			using var brokenSubscription = source.Subscribe((kind, _, _) => {
				brokenCalls++;
				if (kind == RealtimeOutputKind.Lamp) throw new InvalidOperationException("broken");
			});

			for (var attempt = 0; attempt < RealtimeOutputResourceSource.MaxSnapshotAttempts; attempt++) {
				Assert.That(source.BeginSnapshot(), Is.True, $"attempt {attempt + 1}");
				source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 1);
				source.PublishSnapshot(RealtimeOutputKind.Lamp, 12, 0);
				source.EndSnapshot();
				source.Publish(RealtimeOutputKind.Lamp, 11, 0);
				source.Publish(RealtimeOutputKind.Lamp, 11, 1);
			}
			Assert.That(source.BeginSnapshot(), Is.False, "the broken receiver is gone");

			// the flaky receiver went live on its third, complete snapshot only
			Assert.That(flaky.SkipWhile(v => v.Item1 != RealtimeOutputKind.SnapshotBoundary), Is.EqualTo(new[] {
				(RealtimeOutputKind.SnapshotBoundary, 0, 0), (RealtimeOutputKind.Lamp, 11, 0), (RealtimeOutputKind.Lamp, 11, 1),
			}));
			Assert.That(flaky.Count(v => v.Item1 == RealtimeOutputKind.SnapshotBoundary), Is.EqualTo(1));
			var calls = brokenCalls;
			source.Publish(RealtimeOutputKind.Lamp, 11, 0);
			Assert.That(brokenCalls, Is.EqualTo(calls));
			// each receiver's first exception, and the drop
			Assert.That(faults.Select(f => f.Message), Is.EqualTo(new[] { "broken", "flaky", "A real-time output receiver failed 3 snapshots in a row and receives nothing anymore." }));
		}

		[Test]
		public void AReceiverDroppedForFailingIsGoneLikeOneThatLeft()
		{
			var changes = new List<bool>();
			var faults = new List<Exception>();
			var source = new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame, faults.Add, changes.Add);
			var calls = 0;
			var broken = source.Subscribe((_, _, _) => { calls++; throw new InvalidOperationException("broken"); });
			for (var attempt = 0; attempt < RealtimeOutputResourceSource.MaxSnapshotAttempts; attempt++) {
				Assert.That(source.BeginSnapshot(), Is.True);
				source.PublishSnapshot(RealtimeOutputKind.Lamp, 11, 1);
				source.PublishSnapshot(RealtimeOutputKind.Lamp, 12, 0);
				source.PublishSnapshot(RealtimeOutputKind.Lamp, 13, 0);
				source.EndSnapshot();
			}
			// the rest of each failed attempt is skipped, each retried one is called off, and the last receiver leaving stops
			// the producer
			Assert.That(calls, Is.EqualTo(RealtimeOutputResourceSource.MaxSnapshotAttempts * 2 - 1));
			Assert.That(source.HasReceivers, Is.False);
			Assert.That(changes, Is.EqualTo(new[] { true, false }));
			Assert.That(faults, Has.Count.EqualTo(2));
			broken.Dispose();
			source.Dispose();
			Assert.That(changes, Is.EqualTo(new[] { true, false }));
		}

		private sealed class Counter
		{
			internal int Count;
			internal void Receive(RealtimeOutputKind kind, int number, int value) => Count++;
		}

		private sealed class FakeRealtimeSource : IPluginResourceSource
		{
			public ResourceKind Kind => ResourceKind.RealtimeOutputs;
		}
	}
}
