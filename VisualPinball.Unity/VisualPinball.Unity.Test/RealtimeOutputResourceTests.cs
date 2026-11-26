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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Plugins;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace VisualPinball.Unity.Test
{
	public class RealtimeOutputResourceTests
	{
		private GameObject _table;
		private RealtimeEngine _engine;
		private PluginResourceRegistry _registry;
		private GamelogicPluginResources _resources;
		private readonly List<Exception> _faults = new List<Exception>();

		[SetUp]
		public void SetUp()
		{
			_faults.Clear();
			_table = new GameObject("Table");
			_engine = new RealtimeEngine();
			_registry = new PluginResourceRegistry(_faults.Add);
			_resources = new GamelogicPluginResources(_table, _engine, new GameIdentity("pinmame", "t2_l8"), 1, new OwnerScheduler(), _registry, _faults.Add);
		}

		[TearDown]
		public void TearDown()
		{
			_resources.Dispose();
			UnityEngine.Object.DestroyImmediate(_table);
		}

		[Test]
		public void TheBackglassAndDofBindTheGameAtTheSameTime()
		{
			using var b2s = _registry.Bind<IPluginControllerSource>(new PluginResourceSelection(ResourceKind.Outputs) { Session = 1 });
			using var dof = _registry.Bind<IPluginRealtimeOutputSource>(new PluginResourceSelection(ResourceKind.RealtimeOutputs) { Session = 1 });
			Assert.That(b2s.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(b2s.Descriptor.Id, Is.EqualTo(_resources.ControllerId + "/outputs"));
			Assert.That(dof.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(dof.Descriptor.Id, Is.EqualTo(_resources.ControllerId + "/outputs/realtime"));
			Assert.That(dof.Descriptor.Role, Is.EqualTo("realtime-outputs"));
			Assert.That(dof.Source.Numbering, Is.EqualTo(RealtimeOutputNumbering.PinMame));

			var backglass = new List<int>();
			var cabinet = new List<(RealtimeOutputKind, int, int)>();
			using var b2sSubscription = b2s.Connect(source => source.Subscribe(change => { if (change.Kind == ControllerEventKind.Lamp) backglass.Add(change.Number); }));
			using var dofSubscription = dof.Connect(source => source.Subscribe((kind, number, value) => cabinet.Add((kind, number, value))));

			_engine.Lamp(81, 1);
			_engine.NativeLamp(81, 1);
			RealtimeOutputPoll.For(_engine).PollFromFrame();

			Assert.That(backglass, Is.EqualTo(new[] { 81 }));
			// the snapshot has every lamp, lamp 81 still off, then the change
			Assert.That(cabinet, Has.Count.EqualTo(_engine.SnapshotSize + 2));
			Assert.That(cabinet.Where(value => value.Item2 == 81 || value.Item1 == RealtimeOutputKind.SnapshotBoundary), Is.EqualTo(new[] {
				(RealtimeOutputKind.Lamp, 81, 0), (RealtimeOutputKind.SnapshotBoundary, 0, 0), (RealtimeOutputKind.Lamp, 81, 1),
			}));
			Assert.That(_faults, Is.Empty);
		}

		[Test]
		public void TheEngineOnlyProducesTheFeedWhileSomebodyReceivesIt()
		{
			var poll = RealtimeOutputPoll.For(_engine);
			Assert.That(_engine.Listener, Is.Null);
			poll.PollFromFrame();
			Assert.That(_engine.Polls, Is.EqualTo(0));

			var dof = Realtime();
			var first = dof.Subscribe((_, _, _) => { });
			var second = dof.Subscribe((_, _, _) => { });
			Assert.That(_engine.Listener, Is.Not.Null);
			poll.PollFromFrame();
			Assert.That(_engine.Polls, Is.EqualTo(1));

			first.Dispose();
			Assert.That(_engine.Listener, Is.Not.Null);
			second.Dispose();
			Assert.That(_engine.Listener, Is.Null);
			poll.PollFromFrame();
			Assert.That(_engine.Polls, Is.EqualTo(1));

			dof.Subscribe((_, _, _) => { });
			Assert.That(_engine.Listener, Is.Not.Null);
			_resources.Dispose();
			Assert.That(_engine.Listener, Is.Null);
			Assert.That(_engine.Native, Is.Null);
		}

		[Test]
		public void TheSimulationThreadTakesOverThePollFromTheFrame()
		{
			var poll = RealtimeOutputPoll.For(_engine);
			using var subscription = Realtime().Subscribe((_, _, _) => { });

			poll.SimulationThreadStarted();
			poll.PollFromFrame();
			Assert.That(_engine.Polls, Is.EqualTo(0));
			poll.PollFromSimulationThread();
			Assert.That(_engine.Polls, Is.EqualTo(1));

			poll.SimulationThreadStopped();
			poll.PollFromFrame();
			Assert.That(_engine.Polls, Is.EqualTo(2));
		}

		[Test]
		public void APollThatThrowsDoesNotStopTheNextOne()
		{
			var poll = RealtimeOutputPoll.For(_engine);
			var values = new List<int>();
			using var subscription = Realtime().Subscribe((kind, _, value) => { if (kind == RealtimeOutputKind.Solenoid) values.Add(value); });
			// the failure is logged as an error
			UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
			_engine.Throw = true;
			poll.PollFromSimulationThread();
			_engine.Throw = false;
			_engine.Solenoid(28, 1);
			poll.PollFromSimulationThread();
			Assert.That(values, Is.EqualTo(new[] { 1 }));
		}

		[Test]
		public void PollingDoesNotAllocate()
		{
			var poll = RealtimeOutputPoll.For(_engine);
			var counter = new Counter();
			using var subscription = Realtime().Subscribe(counter.Receive);
			poll.PollFromSimulationThread();
			_engine.Solenoid(28, 1);
			poll.PollFromSimulationThread();

			_engine.Solenoid(28, 0);
			Assert.That(() => poll.PollFromSimulationThread(), Is.Not.AllocatingGCMemory());
			Assert.That(counter.Count, Is.EqualTo(_engine.SnapshotSize + 1 + 2));
		}

		private IPluginRealtimeOutputSource Realtime()
		{
			using var binding = _registry.Bind<IPluginRealtimeOutputSource>(new PluginResourceSelection(ResourceKind.RealtimeOutputs) { Session = 1 });
			return binding.Source;
		}

		private sealed class Counter
		{
			internal int Count;
			internal void Receive(RealtimeOutputKind kind, int number, int value) => Count++;
		}

		/// <summary>An engine without a ROM that reports native and real-time outputs when told to.</summary>
		private sealed class RealtimeEngine : IGamelogicEngine, IGamelogicNativeOutputs, IGamelogicRealtimeOutputs
		{
			internal INativeOutputListener Native;
			internal IRealtimeOutputListener Listener;
			internal int Polls;
			internal bool Throw;
			internal int SnapshotSize => _lamps.Length;
			private readonly int[] _lamps = new int[128];
			private int _pendingKind = -1, _pendingNumber, _pendingValue;

			internal void NativeLamp(int number, int value)
			{
				Native?.NativeOutputChanged(ControllerEventKind.Lamp, number, value, ControllerValueSemantics.Binary, 1);
				Native?.NativeOutputPollEnd();
			}
			internal void Lamp(int number, int value) => Queue(RealtimeOutputKind.Lamp, number, value);
			internal void Solenoid(int number, int value) => Queue(RealtimeOutputKind.Solenoid, number, value);
			private void Queue(RealtimeOutputKind kind, int number, int value) { _pendingKind = (int)kind; _pendingNumber = number; _pendingValue = value; }

			public string RealtimeNumbering => RealtimeOutputNumbering.PinMame;
			public void SetNativeOutputListener(INativeOutputListener listener) => Native = listener;
			public void SetRealtimeOutputListener(IRealtimeOutputListener listener) => Listener = listener;
			public void PollRealtimeOutputs()
			{
				var listener = Listener;
				if (listener == null) return;
				Polls++;
				if (Throw) throw new InvalidOperationException("broken poll");
				if (listener.BeginSnapshot()) {
					for (var i = 0; i < _lamps.Length; i++) listener.SnapshotValue(RealtimeOutputKind.Lamp, i, _lamps[i]);
					listener.EndSnapshot();
				}
				if (_pendingKind >= 0) {
					if (_pendingKind == (int)RealtimeOutputKind.Lamp) _lamps[_pendingNumber] = _pendingValue;
					listener.OutputChanged((RealtimeOutputKind)_pendingKind, _pendingNumber, _pendingValue);
					_pendingKind = -1;
				}
			}

			public string Name => "Real-time test engine";
			public Task OnInit(Player player, TableApi tableApi, BallManager ballManager, CancellationToken ct) => Task.CompletedTask;
			public event EventHandler<RequestedDisplays> OnDisplaysRequested;
			public event EventHandler<string> OnDisplayClear;
			public event EventHandler<DisplayFrameData> OnDisplayUpdateFrame;
			public void DisplayChanged(DisplayFrameData displayFrameData) { }
			public GamelogicEngineSwitch[] RequestedSwitches => Array.Empty<GamelogicEngineSwitch>();
			public void Switch(string id, bool isClosed) { }
			public GamelogicEngineLamp[] RequestedLamps => Array.Empty<GamelogicEngineLamp>();
			public event EventHandler<LampEventArgs> OnLampChanged;
			public event EventHandler<LampsEventArgs> OnLampsChanged;
			public GamelogicEngineCoil[] RequestedCoils => Array.Empty<GamelogicEngineCoil>();
			public event EventHandler<CoilEventArgs> OnCoilChanged;
			public GamelogicEngineWire[] AvailableWires => Array.Empty<GamelogicEngineWire>();
			public event EventHandler<EventArgs> OnStarted;
			public void SetCoil(string id, bool isEnabled) { }
			public void SetLamp(string id, float value, bool isCoil = false, LampSource source = LampSource.Lamp) { }
			public bool GetSwitch(string id) => false;
			public bool GetCoil(string id) => false;
			public LampState GetLamp(string id) => default;
			public event EventHandler<SwitchEventArgs2> OnSwitchChanged;
		}
	}
}
