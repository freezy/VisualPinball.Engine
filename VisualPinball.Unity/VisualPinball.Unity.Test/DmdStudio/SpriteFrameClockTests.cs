// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class SpriteFrameClockTests
	{
		private DmdSpriteAsset _sprite;

		[SetUp]
		public void SetUp()
		{
			_sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			for (var index = 0; index < 3; index++) {
				_sprite.Frames.Add(new DmdBitmapData { Width = 1, Height = 1, Pixels = new[] { (byte)index } });
			}
			_sprite.FrameDurations.AddRange(new[] { 1, 2, 1 });
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(_sprite);

		[Test]
		public void OnceBecomesInvisibleAfterTheLastFrame()
		{
			AssertFrame(DmdLoopMode.Once, 0, 0);
			AssertFrame(DmdLoopMode.Once, 1, 1);
			AssertFrame(DmdLoopMode.Once, 2, 1);
			AssertFrame(DmdLoopMode.Once, 3, 2);
			Assert.That(SpriteFrameClock.TryGetFrame(_sprite, 4, DmdLoopMode.Once, 0, out _), Is.False);
		}

		[Test]
		public void HoldLastStaysOnTheLastFrame()
		{
			AssertFrame(DmdLoopMode.HoldLast, 100, 2);
		}

		[Test]
		public void LoopRepeatsDurations()
		{
			AssertFrame(DmdLoopMode.Loop, 4, 0);
			AssertFrame(DmdLoopMode.Loop, 6, 1);
		}

		[Test]
		public void PingPongDoesNotDuplicateEndpoints()
		{
			var expected = new[] { 0, 1, 1, 2, 1, 1 };
			for (var tick = 0; tick < expected.Length; tick++) {
				AssertFrame(DmdLoopMode.PingPong, tick, expected[tick]);
			}
		}

		[Test]
		public void MismatchedDurationsUseAuthoredEntriesThenOneFrameDefaults()
		{
			_sprite.FrameDurations = new System.Collections.Generic.List<int> { 2 };

			AssertFrame(DmdLoopMode.Once, 0, 0);
			AssertFrame(DmdLoopMode.Once, 1, 0);
			AssertFrame(DmdLoopMode.Once, 2, 1);
			AssertFrame(DmdLoopMode.Once, 3, 2);
			Assert.That(SpriteFrameClock.TryGetFrame(_sprite, 4, DmdLoopMode.Once, 0, out _), Is.False);
		}

		private void AssertFrame(DmdLoopMode mode, int tick, int expected)
		{
			Assert.That(SpriteFrameClock.TryGetFrame(_sprite, tick, mode, 0, out var actual), Is.True);
			Assert.That(actual, Is.EqualTo(expected));
		}
	}
}
