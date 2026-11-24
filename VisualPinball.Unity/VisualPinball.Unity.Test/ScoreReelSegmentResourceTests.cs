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

namespace VisualPinball.Unity.Test
{
	public class ScoreReelSegmentResourceTests
	{
		private GameObject _table;
		private ScoreReelDisplayComponent _reels;
		private DisplayEngine _engine;
		private PluginResourceRegistry _registry;
		private GamelogicPluginResources _resources;
		private readonly List<Exception> _faults = new List<Exception>();

		[SetUp]
		public void SetUp()
		{
			_table = new GameObject("Table");
			var reels = new GameObject("Reels");
			reels.transform.SetParent(_table.transform);
			_reels = reels.AddComponent<ScoreReelDisplayComponent>();
			_reels.Id = "display0";
			_engine = new DisplayEngine();
			_registry = new PluginResourceRegistry(_faults.Add);
			_resources = new GamelogicPluginResources(_table, _engine, new GameIdentity("test", "reels"), 1, new OwnerScheduler(), _registry, _faults.Add);
		}

		[TearDown]
		public void TearDown()
		{
			_resources.Dispose();
			UnityEngine.Object.DestroyImmediate(_table);
		}

		[Test]
		public void AScoreReelsSegmentDisplayIsAnnouncedInTheLayoutOfItsFrames()
		{
			_engine.Request(new DisplayConfig("display0", 5, 1));
			var source = Segments("display0");
			var frames = new List<PluginSegmentFrame>();
			float[] lit = null;
			// the frame's brightness is only lent for the call
			using var subscription = source.Subscribe(frame => { frames.Add(frame); lit = frame.Brightness.ToArray(); });

			_reels.OnDisplayChanged?.Invoke(_reels, new DisplayFrameData("display0", DisplayFrameFormat.Numeric, BitConverter.GetBytes(1230f)));

			Assert.That(source.Layout, Is.EqualTo("seg7"));
			Assert.That(frames, Has.Count.EqualTo(1));
			Assert.That(frames[0].Layout, Is.EqualTo(source.Layout));
			Assert.That(frames[0].Elements, Is.EqualTo(5));
			// 01230: the last two digits are a 3 (segments a, b, c, d, g) and a 0 (a to f)
			Assert.That(Mask(lit, 3), Is.EqualTo(0x4F));
			Assert.That(Mask(lit, 4), Is.EqualTo(0x3F));
			Assert.That(_faults, Is.Empty);
		}

		[Test]
		public void OtherSegmentDisplaysKeepTheirLayout()
		{
			_engine.Request(new DisplayConfig("display1", 5, 1));
			Assert.That(Segments("display1").Layout, Is.EqualTo("seg16"));
		}

		private IPluginSegmentSource Segments(string id)
		{
			var descriptor = _registry.Snapshot().Single(d => d.Id.EndsWith("/segments/" + id, StringComparison.Ordinal));
			Assert.That(_registry.TryGetSource<IPluginSegmentSource>(descriptor, out var source), Is.True);
			return source;
		}

		private static int Mask(float[] brightness, int element)
		{
			var mask = 0;
			for (var bit = 0; bit < 16; bit++) {
				if (brightness[element * 16 + bit] > 0) mask |= 1 << bit;
			}
			return mask;
		}

		/// <summary>An engine without a ROM that only announces displays.</summary>
		private class DisplayEngine : IGamelogicEngine
		{
			public void Request(params DisplayConfig[] displays) => OnDisplaysRequested?.Invoke(this, new RequestedDisplays(displays));

			public string Name => "Display test engine";
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
