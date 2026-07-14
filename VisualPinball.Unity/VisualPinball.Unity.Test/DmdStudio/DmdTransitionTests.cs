// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class DmdTransitionTests
	{
		[TestCase(DmdTransitionType.Cut)]
		[TestCase(DmdTransitionType.Push)]
		[TestCase(DmdTransitionType.Cover)]
		[TestCase(DmdTransitionType.Uncover)]
		[TestCase(DmdTransitionType.WipeOn)]
		[TestCase(DmdTransitionType.SplitIn)]
		[TestCase(DmdTransitionType.SplitOut)]
		[TestCase(DmdTransitionType.Dissolve)]
		[TestCase(DmdTransitionType.FadeThroughBlack)]
		[TestCase(DmdTransitionType.ScrollOff)]
		public void EveryTransitionHasExactEndpoints(DmdTransitionType type)
		{
			var from = Pattern(8, 4, 17, 3);
			var to = Pattern(8, 4, 29, 11);
			var destination = new DmdSurface(8, 4, DmdPixelFormat.I8);

			DmdTransitions.Compose(destination, from, to, type, DmdDirection.Left, 0f);
			if (type == DmdTransitionType.Cut) {
				Assert.That(destination.Data, Is.EqualTo(to.Data));
			} else {
				Assert.That(destination.Data, Is.EqualTo(from.Data));
			}
			DmdTransitions.Compose(destination, from, to, type, DmdDirection.Left, 1f);
			Assert.That(destination.Data, Is.EqualTo(to.Data));
		}

		[TestCase(DmdTransitionType.Push, new byte[] { 3, 4, 5, 6 })]
		[TestCase(DmdTransitionType.Cover, new byte[] { 1, 2, 5, 6 })]
		[TestCase(DmdTransitionType.Uncover, new byte[] { 3, 4, 7, 8 })]
		[TestCase(DmdTransitionType.WipeOn, new byte[] { 1, 2, 7, 8 })]
		[TestCase(DmdTransitionType.ScrollOff, new byte[] { 3, 4, 7, 8 })]
		public void HorizontalMotionUsesRoundedIntegerOffsets(DmdTransitionType type, byte[] expected)
		{
			var from = Surface(4, 1, 1, 2, 3, 4);
			var to = Surface(4, 1, 5, 6, 7, 8);
			var destination = new DmdSurface(4, 1, DmdPixelFormat.I8);

			DmdTransitions.Compose(destination, from, to, type, DmdDirection.Left, 0.5f);

			Assert.That(destination.Data, Is.EqualTo(expected));
		}

		[Test]
		public void DissolveUsesTheBayerFourByFourThreshold()
		{
			var from = new DmdSurface(4, 4, DmdPixelFormat.I8);
			var to = new DmdSurface(4, 4, DmdPixelFormat.I8);
			var destination = new DmdSurface(4, 4, DmdPixelFormat.I8);
			to.Clear(255);

			DmdTransitions.Compose(destination, from, to, DmdTransitionType.Dissolve,
				DmdDirection.Left, 0.5f);

			Assert.That(destination.Data, Is.EqualTo(new byte[] {
				255, 0, 255, 0,
				0, 255, 0, 255,
				255, 0, 255, 0,
				0, 255, 0, 255,
			}));
		}

		[Test]
		public void FadeThroughBlackIsBlackAtItsMidpoint()
		{
			var from = Surface(2, 1, 100, 200);
			var to = Surface(2, 1, 50, 250);
			var destination = new DmdSurface(2, 1, DmdPixelFormat.I8);

			DmdTransitions.Compose(destination, from, to, DmdTransitionType.FadeThroughBlack,
				DmdDirection.Left, 0.5f);

			Assert.That(destination.Data, Is.EqualTo(new byte[] { 0, 0 }));
		}

		[TestCase(DmdTransitionType.Cut, 646444229u)]
		[TestCase(DmdTransitionType.Push, 686640837u)]
		[TestCase(DmdTransitionType.Cover, 1602420165u)]
		[TestCase(DmdTransitionType.Uncover, 872900293u)]
		[TestCase(DmdTransitionType.WipeOn, 1788679621u)]
		[TestCase(DmdTransitionType.SplitIn, 2720066757u)]
		[TestCase(DmdTransitionType.SplitOut, 1911098821u)]
		[TestCase(DmdTransitionType.Dissolve, 661240773u)]
		[TestCase(DmdTransitionType.FadeThroughBlack, 1924397413u)]
		[TestCase(DmdTransitionType.ScrollOff, 872900293u)]
		public void FullSizeTransitionHasStableGoldenHash(DmdTransitionType type, uint expected)
		{
			var from = Pattern(128, 32, 17, 3);
			var to = Pattern(128, 32, 29, 11);
			var destination = new DmdSurface(128, 32, DmdPixelFormat.I8);

			DmdTransitions.Compose(destination, from, to, type, DmdDirection.Up, 0.37f);

			var hash = DmdSurfaceAssert.Hash(destination.Data);
			if (DmdSurfaceAssert.RegenerateGoldenHashes) {
				Assert.Fail($"Replace the {type} transition golden hash with {hash}u.");
			}
			Assert.That(hash, Is.EqualTo(expected), type.ToString());
		}

		[Test]
		public void SameTimelineProducesIdenticalHashes()
		{
			var first = RenderTimeline();
			var second = RenderTimeline();

			Assert.That(second, Is.EqualTo(first));
		}

		private static uint[] RenderTimeline()
		{
			var from = Pattern(16, 8, 11, 5);
			var to = Pattern(16, 8, 23, 7);
			var destination = new DmdSurface(16, 8, DmdPixelFormat.I8);
			var hashes = new uint[21];
			for (var frame = 0; frame < hashes.Length; frame++) {
				DmdTransitions.Compose(destination, from, to, DmdTransitionType.Dissolve,
					DmdDirection.Down, frame / 20f);
				hashes[frame] = DmdSurfaceAssert.Hash(destination.Data);
			}
			return hashes;
		}

		private static DmdSurface Pattern(int width, int height, int multiplier, int addend)
		{
			var surface = new DmdSurface(width, height, DmdPixelFormat.I8);
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x++) {
					surface.Data[y * width + x] = (byte)(x * multiplier + y * (multiplier + 14) +
					                                      x * y * 7 + addend);
				}
			}
			return surface;
		}

		private static DmdSurface Surface(int width, int height, params byte[] values)
		{
			var surface = new DmdSurface(width, height, DmdPixelFormat.I8);
			if (values.Length != surface.Data.Length) {
				throw new ArgumentException();
			}
			values.CopyTo(surface.Data, 0);
			return surface;
		}
	}
}
