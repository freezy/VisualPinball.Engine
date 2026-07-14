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
	public class DmdBlitterTests
	{
		[Test]
		public void BlendModesUseTheSpecifiedIntegerEquations()
		{
			var source = Bitmap(1, 1, 200, 128);

			Assert.That(Blend(100, source, DmdBlendMode.Opaque, 255), Is.EqualTo(200));
			Assert.That(Blend(100, source, DmdBlendMode.Opaque, 128), Is.EqualTo(150));
			Assert.That(Blend(100, source, DmdBlendMode.Alpha, 255), Is.EqualTo(150));
			Assert.That(Blend(100, source, DmdBlendMode.Add, 255), Is.EqualTo(200));
			Assert.That(Blend(100, Bitmap(1, 1, 0, 255), DmdBlendMode.Invert, 128), Is.EqualTo(155));
			Assert.That(Blend(100, Bitmap(1, 1, 255, 127), DmdBlendMode.Invert, 255), Is.EqualTo(100));
		}

		[Test]
		public void ClipsNegativeCoordinatesAndTintsI8IntoRgb()
		{
			var surface = new DmdSurface(3, 1, DmdPixelFormat.Rgb24);
			var source = new DmdBitmapData {
				Width = 2,
				Height = 1,
				Pixels = new byte[] { 255, 128 }
			};

			DmdBlitter.Blit(surface, source, -1, 0, DmdBlendMode.Opaque, byte.MaxValue,
				new Color32(255, 128, 0, 255));

			Assert.That(surface.Data, Is.EqualTo(new byte[] { 128, 64, 0, 0, 0, 0, 0, 0, 0 }));
		}

		[Test]
		public void AlphaBlendsRgbPerChannelAndHandlesExtremeClipping()
		{
			var surface = new DmdSurface(1, 1, DmdPixelFormat.Rgb24);
			surface.Data[0] = 110;
			surface.Data[1] = 120;
			surface.Data[2] = 130;
			var source = new DmdBitmapData {
				Width = 1,
				Height = 1,
				Format = DmdPixelFormat.Rgb24,
				Pixels = new byte[] { 10, 20, 30 },
				Alpha = new byte[] { 128 }
			};

			DmdBlitter.Blit(surface, source, 0, 0, DmdBlendMode.Alpha, byte.MaxValue,
				new Color32(1, 2, 3, 4));
			Assert.That(surface.Data, Is.EqualTo(new byte[] { 60, 70, 80 }));
			Assert.DoesNotThrow(() => DmdBlitter.Blit(surface, source, int.MinValue, int.MaxValue,
				DmdBlendMode.Alpha, byte.MaxValue, new Color32()));
			Assert.DoesNotThrow(() => DmdBlitter.FillRect(surface, int.MaxValue, int.MinValue,
				int.MaxValue, int.MaxValue, DmdShade.White, byte.MaxValue));
		}

		[Test]
		public void FillAndMaskClipAtSurfaceBounds()
		{
			var surface = new DmdSurface(4, 2, DmdPixelFormat.I8);
			var shade = new DmdShade { Intensity = 200 };
			DmdBlitter.FillRect(surface, -1, 0, 4, 2, shade, 128);
			var mask = new DmdBitmapData {
				Width = 2,
				Height = 1,
				Pixels = new byte[] { 255, 128 }
			};

			DmdBlitter.ApplyAlphaMask(surface, mask, 1, 1);

			Assert.That(surface.Data, Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 100, 50, 0 }));
		}

		[Test]
		public void ScriptedBitmapCompositionHasStableGoldenHash()
		{
			var surface = new DmdSurface(8, 4, DmdPixelFormat.I8);
			surface.Clear(17);
			var source = new DmdBitmapData {
				Width = 5,
				Height = 3,
				Pixels = new byte[] {
					0, 64, 128, 192, 255,
					255, 192, 128, 64, 0,
					32, 96, 160, 224, 128,
				},
				Alpha = new byte[] {
					255, 192, 128, 64, 0,
					0, 64, 128, 192, 255,
					255, 255, 255, 255, 255,
				}
			};
			DmdBlitter.Blit(surface, source, 2, 1, DmdBlendMode.Alpha, 192, (Color32)Color.white);

			var hash = DmdSurfaceAssert.Hash(surface.Data);
			if (DmdSurfaceAssert.RegenerateGoldenHashes) {
				Assert.Fail($"Replace the bitmap golden hash with {hash}u.");
			}
			Assert.That(hash, Is.EqualTo(2409974179u),
				DmdSurfaceAssert.ToAscii(surface));
		}

		private static byte Blend(byte initial, DmdBitmapData source, DmdBlendMode mode, byte opacity)
		{
			var surface = new DmdSurface(1, 1, DmdPixelFormat.I8);
			surface.Data[0] = initial;
			DmdBlitter.Blit(surface, source, 0, 0, mode, opacity, (Color32)Color.white);
			return surface.Data[0];
		}

		private static DmdBitmapData Bitmap(int width, int height, byte value, byte alpha)
		{
			return new DmdBitmapData {
				Width = width,
				Height = height,
				Pixels = new[] { value },
				Alpha = new[] { alpha }
			};
		}
	}
}
