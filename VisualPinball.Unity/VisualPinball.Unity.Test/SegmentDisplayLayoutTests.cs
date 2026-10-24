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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Test
{
	public class SegmentDisplayLayoutTests
	{
		private GameObject _go;
		private SegmentDisplayComponent _display;

		[SetUp]
		public void SetUp()
		{
			_go = new GameObject("Segment display");
			_display = _go.AddComponent<SegmentDisplayComponent>();
			_display.Id = "display0";
			_display.NumSegments = 14;
		}

		[TearDown]
		public void TearDown()
		{
			UnityEngine.Object.DestroyImmediate(_go);
		}

		[Test]
		public void ShouldTakeTheSegmentCountFromTheReportedLayout()
		{
			var engine = new LayoutEngine("seg16");
			var player = new DisplayPlayer();
			player.Awake(engine);
			engine.Request(new DisplayConfig("display0", 7, 1));
			player.OnDestroy();

			Assert.That(_display.NumSegments, Is.EqualTo(16));
			Assert.That(_go.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("__NumSegments"), Is.EqualTo(16));
		}

		[Test]
		public void ShouldTakeTheLayoutWithAnEmptyMaterialSlot()
		{
			_go.AddComponent<MeshRenderer>();
			var engine = new LayoutEngine("seg16");
			var player = new DisplayPlayer();
			player.Awake(engine);
			engine.Request(new DisplayConfig("display0", 7, 1));
			player.OnDestroy();

			Assert.That(_display.NumSegments, Is.EqualTo(16));
			Assert.That(_go.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("__NumSegments"), Is.EqualTo(16));
		}

		[Test]
		public void ShouldKeepTheConfiguredCountWithoutALayout()
		{
			var engine = new LayoutEngine(null);
			var player = new DisplayPlayer();
			player.Awake(engine);
			engine.Request(new DisplayConfig("display0", 7, 1));
			player.OnDestroy();

			Assert.That(_display.NumSegments, Is.EqualTo(14));
		}

		[Test]
		public void ShouldKeepTheConfiguredCountForAnUnknownLayout()
		{
			var engine = new LayoutEngine("seg10");
			var player = new DisplayPlayer();
			player.Awake(engine);
			engine.Request(new DisplayConfig("display0", 7, 1));
			player.OnDestroy();

			Assert.That(_display.NumSegments, Is.EqualTo(14));
		}

		[TestCase("seg7", 7)]
		[TestCase("seg9", 9)]
		[TestCase("seg14", 14)]
		[TestCase("seg16", 16)]
		[TestCase("gottlieb", 0)]
		public void ShouldMapLayouts(string layout, int segments)
		{
			Assert.That(SegmentDisplayComponent.SegmentsOf(layout), Is.EqualTo(segments));
		}

		/// <summary>An engine that only announces displays, reporting one layout for all (or none).</summary>
		private class LayoutEngine : IGamelogicEngine, ISegmentDisplayMetadata
		{
			private readonly string _layout;

			public LayoutEngine(string layout) => _layout = layout;

			public void Request(params DisplayConfig[] displays) => OnDisplaysRequested?.Invoke(this, new RequestedDisplays(displays));

			public bool TryGetSegmentLayout(string displayId, out string layout, out string hardware)
			{
				layout = _layout ?? string.Empty;
				hardware = string.Empty;
				return _layout != null;
			}

			public string Name => "Layout test engine";
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
