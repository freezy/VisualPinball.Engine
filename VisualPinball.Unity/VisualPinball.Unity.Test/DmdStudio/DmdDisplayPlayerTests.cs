// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class DmdDisplayPlayerTests
	{
		[Test]
		public void CuePlayerEventsReachSceneDisplayAndTeardownUnsubscribes()
		{
			var displayObject = new GameObject("Display");
			var gleObject = new GameObject("GLE");
			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			var displayPlayer = new DisplayPlayer();
			try {
				var display = displayObject.AddComponent<CountingDisplayComponent>();
				display.Id = "studio";
				var gle = gleObject.AddComponent<DefaultGamelogicEngine>();
				displayPlayer.Awake(gle);
				project.DisplayId = display.Id;
				project.Width = 4;
				project.Height = 2;
				project.FrameRate = 10;
				project.ColorMode = DmdColorMode.Mono16;
				var emitter = new GleDisplayEmitter(
					displays => Raise(gle, "OnDisplaysRequested", displays),
					frame => Raise(gle, "OnDisplayUpdateFrame", frame),
					id => Raise(gle, "OnDisplayClear", id));

				using (var cuePlayer = new DmdCuePlayer(project, emitter)) {
					cuePlayer.Start();
					cuePlayer.Tick(0d);
					cuePlayer.Tick(DmdCuePlayer.ReannounceDelaySeconds);

					Assert.That(display.ResizeCount, Is.EqualTo(1));
					Assert.That(display.ClearCount, Is.EqualTo(1));
					Assert.That(display.FrameCount, Is.EqualTo(2));
				}

				Assert.That(display.ClearCount, Is.EqualTo(2));
				displayPlayer.OnDestroy();
				Raise(gle, "OnDisplayUpdateFrame",
					new DisplayFrameData(display.Id, DisplayFrameFormat.Dmd8, new byte[8]));
				Assert.That(display.FrameCount, Is.EqualTo(2));
			} finally {
				Object.DestroyImmediate(project);
				Object.DestroyImmediate(gleObject);
				Object.DestroyImmediate(displayObject);
			}
		}

		[Test]
		public void ReannouncingAnIdenticalConfigDoesNotResizeOrClear()
		{
			var gameObject = new GameObject("Display");
			try {
				var display = gameObject.AddComponent<CountingDisplayComponent>();
				display.Id = "dmd0";
				var player = new DisplayPlayer();
				var displaysField = typeof(DisplayPlayer).GetField("_displayGameObjects",
					BindingFlags.Instance | BindingFlags.NonPublic);
				var handler = typeof(DisplayPlayer).GetMethod("HandleDisplaysRequested",
					BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.That(displaysField, Is.Not.Null);
				Assert.That(handler, Is.Not.Null);
				var displays = (Dictionary<string, DisplayComponent>)displaysField.GetValue(player);
				displays.Add(display.Id, display);
				var config = new DisplayConfig("dmd0", 140, 36, true, Color.red, Color.black);

				handler.Invoke(player, new object[] { null, new RequestedDisplays(config) });
				handler.Invoke(player, new object[] { null, new RequestedDisplays(
					new DisplayConfig("dmd0", 140, 36, true, Color.red, Color.black)) });

				Assert.That(display.ResizeCount, Is.EqualTo(1));
				Assert.That(display.ClearCount, Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		private static void Raise<T>(DefaultGamelogicEngine gle, string eventName, T args)
		{
			var field = typeof(DefaultGamelogicEngine).GetField(eventName,
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, $"Could not find backing field for {eventName}.");
			((System.EventHandler<T>)field.GetValue(gle))?.Invoke(gle, args);
		}
	}

	public class CountingDisplayComponent : DisplayComponent
	{
		public int ResizeCount { get; private set; }
		public int ClearCount { get; private set; }
		public int FrameCount { get; private set; }

		public override string Id { get; set; }
		public override Color LitColor { get; set; }
		public override Color UnlitColor { get; set; }
		public override float MeshHeight => 1f;
		public override float AspectRatio { get; set; }
		protected override float MeshWidth => 1f;
		protected override float MeshDepth => 0.01f;

		public override void UpdateDimensions(int width, int height, bool flipX = false)
		{
			ResizeCount++;
		}

		public override void Clear()
		{
			ClearCount++;
		}

		public override void UpdateFrame(DisplayFrameFormat format, byte[] data)
		{
			FrameCount++;
		}

		protected override Material CreateMaterial() => null;
	}
}
