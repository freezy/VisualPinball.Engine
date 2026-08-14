// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using ColorChannel = VisualPinball.Engine.Math.ColorChannel;
using EngineColor = VisualPinball.Engine.Math.Color;

namespace VisualPinball.Unity.Test.Game
{
	[TestFixture]
	public class LampPlayerTests
	{
		private GameObject _root;
		private LampPlayer _lampPlayer;

		[TearDown]
		public void TearDown()
		{
			_lampPlayer?.OnDestroy();
			if (_root) UnityEngine.Object.DestroyImmediate(_root);
		}

		[Test]
		public void UpdatesEveryLampMappedToTheSameOutput()
		{
			_root = new GameObject("Table");
			var table = _root.AddComponent<TableComponent>();
			var player = _root.AddComponent<Player>();
			var gamelogicEngine = _root.AddComponent<DefaultGamelogicEngine>();
			var firstLamp = CreateLamp("First Lamp");
			var secondLamp = CreateLamp("Second Lamp");
			var firstApi = new RecordingLampApi();
			var secondApi = new RecordingLampApi();
			table.MappingConfig.Lamps.Add(new LampMapping { Id = "42", Device = firstLamp });
			table.MappingConfig.Lamps.Add(new LampMapping { Id = "42", Device = secondLamp });

			_lampPlayer = new LampPlayer();
			_lampPlayer.Awake(player, table, gamelogicEngine);
			_lampPlayer.RegisterLamp(firstLamp, firstApi);
			_lampPlayer.RegisterLamp(secondLamp, secondApi);
			_lampPlayer.OnStart();
			_lampPlayer.HandleLampEvent("42", LampStatus.On);

			Assert.That(firstApi.LastStatus, Is.EqualTo(LampStatus.On));
			Assert.That(secondApi.LastStatus, Is.EqualTo(LampStatus.On));
		}

		[Test]
		public void CombinesSeparateRgbAddressesOnTheSameLamp()
		{
			_root = new GameObject("Table");
			var table = _root.AddComponent<TableComponent>();
			var player = _root.AddComponent<Player>();
			var gamelogicEngine = _root.AddComponent<DefaultGamelogicEngine>();
			var lamp = CreateLamp("RGB Lamp");
			var api = new RecordingLampApi();
			table.MappingConfig.Lamps.Add(RgbMapping("101", ColorChannel.Red, lamp));
			table.MappingConfig.Lamps.Add(RgbMapping("102", ColorChannel.Green, lamp));
			table.MappingConfig.Lamps.Add(RgbMapping("103", ColorChannel.Blue, lamp));

			_lampPlayer = new LampPlayer();
			_lampPlayer.Awake(player, table, gamelogicEngine);
			_lampPlayer.RegisterLamp(lamp, api);
			_lampPlayer.OnStart();
			_lampPlayer.HandleLampEvent("101", 255f);
			_lampPlayer.HandleLampEvent("102", 128f);
			_lampPlayer.HandleLampEvent("103", 0f);

			Assert.That(api.LastColor.r, Is.EqualTo(1f).Within(0.001f));
			Assert.That(api.LastColor.g, Is.EqualTo(128f / 255f).Within(0.001f));
			Assert.That(api.LastColor.b, Is.Zero.Within(0.001f));
		}

		[Test]
		public void KeepsSharedRgbStateInSyncAfterADirectColorUpdate()
		{
			_root = new GameObject("Table");
			var table = _root.AddComponent<TableComponent>();
			var player = _root.AddComponent<Player>();
			var gamelogicEngine = _root.AddComponent<DefaultGamelogicEngine>();
			var lamp = CreateLamp("RGB Lamp");
			var api = new RecordingLampApi();
			table.MappingConfig.Lamps.Add(RgbMapping("101", ColorChannel.Red, lamp));
			table.MappingConfig.Lamps.Add(RgbMapping("102", ColorChannel.Green, lamp));

			_lampPlayer = new LampPlayer();
			_lampPlayer.Awake(player, table, gamelogicEngine);
			_lampPlayer.RegisterLamp(lamp, api);
			_lampPlayer.OnStart();
			_lampPlayer.HandleLampEvent("101", new EngineColor(64, 128, 192, 255));
			_lampPlayer.HandleLampEvent("102", 255f);

			Assert.That(api.LastColor.r, Is.EqualTo(64f / 255f).Within(0.001f));
			Assert.That(api.LastColor.g, Is.EqualTo(1f).Within(0.001f));
			Assert.That(api.LastColor.b, Is.EqualTo(192f / 255f).Within(0.001f));
		}

		private static LampMapping RgbMapping(string id, ColorChannel channel, ILampDeviceComponent device)
		{
			return new LampMapping {
				Id = id,
				Type = LampType.RgbMulti,
				Channel = channel,
				Device = device,
			};
		}

		private LightComponent CreateLamp(string name)
		{
			var gameObject = new GameObject(name);
			gameObject.transform.SetParent(_root.transform);
			return gameObject.AddComponent<LightComponent>();
		}

		private sealed class RecordingLampApi : IApiLamp
		{
			public event EventHandler Init;

			internal LampStatus LastStatus { get; private set; }
			internal Color LastColor { get; private set; }

			public void OnLamp(LampStatus newStatus) => LastStatus = newStatus;
			public void OnLamp(float intensity) { }
			public void OnLamp(Color color) => LastColor = color;
			public void OnChange(bool enabled) => LastStatus = enabled ? LampStatus.On : LampStatus.Off;
			public void OnInit(BallManager ballManager) => Init?.Invoke(this, EventArgs.Empty);
			public void OnDestroy() { }
		}
	}
}
